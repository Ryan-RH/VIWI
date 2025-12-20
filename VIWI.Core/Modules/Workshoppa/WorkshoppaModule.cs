using Dalamud.Bindings.ImGui;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using ECommons.Configuration;
using ECommons.Logging;
using ECommons.SimpleGui;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Client.Game.Fate;
using FFXIVClientStructs.FFXIV.Common.Lua;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using VIWI.Core;
using VIWI.Core.Config;
using VIWI.Modules.AutoLogin;
using VIWI.Modules.Workshoppa.External;
using VIWI.Modules.Workshoppa.GameData;
using VIWI.Modules.Workshoppa.Windows;

namespace VIWI.Modules.Workshoppa;

[SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
internal sealed partial class WorkshoppaModule : IVIWIModule
{
    public const string ModuleName = "Workshoppa";
    public const string ModuleVersion = "1.0.0";

    public string Name => ModuleName;
    public string Version => ModuleVersion;

    internal static WorkshoppaModule? Instance { get; private set; }

    // ---- Config ----
    private VIWIConfig vConfig = null!;
    public static WorkshoppaConfig _configuration = null!;
    public static bool Enabled => _configuration?.Enabled ?? false;

    // ---- Services ----
    private readonly IDalamudPluginInterface _pluginInterface;
    private readonly IGameGui _gameGui;
    private readonly IFramework _framework;
    private readonly ICondition _condition;
    private readonly IClientState _clientState;
    private readonly IObjectTable _objectTable;
    private readonly IDataManager dataManager;
    private readonly ICommandManager _commandManager;
    private readonly IAddonLifecycle addonLifecycle;
    private readonly IChatGui _chatGui;
    private readonly ITextureProvider textureProvider;
    private readonly IPluginLog _pluginLog;

    // ---- State / systems ----
    private readonly IReadOnlyList<uint> _fabricationStationIds =
        new uint[] { 2005236, 2005238, 2005240, 2007821, 2011588 }.AsReadOnly();

    internal readonly IReadOnlyList<ushort> WorkshopTerritories =
        new ushort[] { 423, 424, 425, 653, 984 }.AsReadOnly();

    private ExternalPluginHandler _externalPluginHandler = null!;
    private WorkshopCache _workshopCache = null!;
    private GameStrings _gameStrings = null!;

    private WorkshoppaWindow _mainWindow = null!;
    private WorkshoppaRepairKitWindow _repairKitWindow = null!;
    private WorkshoppaCeruleumTankWindow _ceruleumTankWindow = null!;

    private Stage _currentStageInternal = Stage.Stopped;
    private DateTime _continueAt = DateTime.MinValue;
    private DateTime _fallbackAt = DateTime.MaxValue;

    private bool initialized;
    private long _lastUpdateTick;
    private int _updateReentryCount;
    private bool _hooksActive;

    public WorkshoppaModule()
    {
        _pluginInterface = VIWIContext.PluginInterface;
        _gameGui = VIWIContext.GameGui;
        _framework = VIWIContext.Framework;
        _condition = VIWIContext.Condition;
        _clientState = VIWIContext.ClientState;
        _objectTable = VIWIContext.ObjectTable;
        dataManager = VIWIContext.DataManager;
        _commandManager = VIWIContext.CommandManager;
        addonLifecycle = VIWIContext.AddonLifecycle;
        _chatGui = VIWIContext.ChatGui;
        textureProvider = VIWIContext.TextureProvider;
        _pluginLog = VIWIContext.PluginLog;
    }

    public void Initialize(VIWIConfig config)
    {
        if (initialized) return;
        initialized = true;

        Instance = this;
        LoadConfig();
        _externalPluginHandler = new ExternalPluginHandler(_pluginInterface, _pluginLog);
        //_configuration = (WorkshoppaConfig?)_pluginInterface.GetPluginConfig() ?? new WorkshoppaConfig();
        _workshopCache = new WorkshopCache(dataManager, _pluginLog);
        _gameStrings = new(dataManager, _pluginLog);

        _mainWindow = new WorkshoppaWindow(this, _clientState, _configuration, _workshopCache, new IconCache(textureProvider), _chatGui, new RecipeTree(dataManager, _pluginLog), _pluginLog);
        VIWIContext.CorePlugin.WindowSystem.AddWindow(_mainWindow);
        _repairKitWindow = new(_pluginLog, _gameGui, addonLifecycle, _configuration, _externalPluginHandler);
        VIWIContext.CorePlugin.WindowSystem.AddWindow(_repairKitWindow);
        _ceruleumTankWindow = new(_pluginLog, _gameGui, addonLifecycle, _configuration, _externalPluginHandler, _chatGui);
        VIWIContext.CorePlugin.WindowSystem.AddWindow(_ceruleumTankWindow);

        /*if (_configuration.Enabled)
            Enable();*/
        PluginLog.Information("[Workshoppa] Module initialized.");
    }

    public void Dispose()
    {
        try
        {
            if (_ceruleumTankWindow != null) _ceruleumTankWindow.Dispose();
            if (_repairKitWindow != null) _repairKitWindow.Dispose();

            _externalPluginHandler?.RestoreTextAdvance();
            _externalPluginHandler?.Restore();
        }
        catch (Exception)
        {
            PluginLog.Error("[Workshoppa] Dispose failed.");
        }
        finally
        {
            if (Instance == this) Instance = null;
            PluginLog.Information("[Workshoppa] Disposed.");
        }
    }

    public static void SetEnabled(bool value)
    {
        if (_configuration == null) return;

        _configuration.Enabled = value;
        SaveConfig();

        if (value)
            Instance?.Enable();
        else
            Instance?.Disable();
    }

    private void Enable()
    {
        if (_hooksActive)
        {
            _pluginLog.Warning("Enable() ignored: hooks already active.");
            return;
        }

        _hooksActive = true;
        SaveConfig();

        _framework.Update += OnFrameworkUpdate;

        _commandManager.AddHandler("/ws", new CommandInfo(ProcessCommand) { HelpMessage = "Open Workshoppa UI" });
        _commandManager.AddHandler("/workshoppa", new CommandInfo(ProcessCommand) { ShowInHelp = false });
        _commandManager.AddHandler("/buy-tanks", new CommandInfo(ProcessBuyCommand) { HelpMessage = "Buy a given number of ceruleum tank stacks." });
        _commandManager.AddHandler("/fill-tanks", new CommandInfo(ProcessFillCommand) { HelpMessage = "Fill your inventory with a given number of ceruleum tank stacks." });

        _repairKitWindow?.EnableShopListeners();
        _ceruleumTankWindow?.EnableShopListeners();
        addonLifecycle.RegisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesNoPostSetup);
        addonLifecycle.RegisterListener(AddonEvent.PostSetup, "Request", RequestPostSetup);
        addonLifecycle.RegisterListener(AddonEvent.PostRefresh, "Request", RequestPostRefresh);
        addonLifecycle.RegisterListener(AddonEvent.PostUpdate, "ContextIconMenu", ContextIconMenuPostReceiveEvent);
    }

    private void Disable()
    {
        if (!_hooksActive)
        {
            _pluginLog.Warning("Disable() ignored: hooks not active.");
            return;
        }

        _hooksActive = false;
        SaveConfig();

        _repairKitWindow?.DisableShopListeners();
        _ceruleumTankWindow?.DisableShopListeners();
        addonLifecycle.UnregisterListener(AddonEvent.PostUpdate, "ContextIconMenu", ContextIconMenuPostReceiveEvent);
        addonLifecycle.UnregisterListener(AddonEvent.PostRefresh, "Request", RequestPostRefresh);
        addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "Request", RequestPostSetup);
        addonLifecycle.UnregisterListener(AddonEvent.PostSetup, "SelectYesno", SelectYesNoPostSetup);

        _commandManager.RemoveHandler("/fill-tanks");
        _commandManager.RemoveHandler("/buy-tanks");
        _commandManager.RemoveHandler("/workshoppa");
        _commandManager.RemoveHandler("/ws");

        _framework.Update -= OnFrameworkUpdate;

        if (CurrentStage != Stage.Stopped)
        {
            _externalPluginHandler.Restore();
            CurrentStage = Stage.Stopped;
        }

        if (_mainWindow != null && _mainWindow.IsOpen) _mainWindow.IsOpen = false;
        if (_repairKitWindow != null) _repairKitWindow.IsOpen = false;
        if (_ceruleumTankWindow != null) _ceruleumTankWindow.IsOpen = false;
    }

    // ----------------------------
    // Config Handlers
    // ----------------------------
    public static void LoadConfig()
    {
        /*_configuration = VIWIContext.PluginInterface.GetPluginConfig() as WorkshoppaConfig ?? new WorkshoppaConfig();
        SaveConfig();*/
    }

    public static void SaveConfig()
    {
        //_configuration?.Save();
    }
    public void ToggleWorkshoppaUi()
    {
        if (!_configuration.Enabled) return;
        _mainWindow?.Toggle(WorkshoppaWindow.EOpenReason.Command);
    }
    internal Stage CurrentStage
    {
        get => _currentStageInternal;
        private set
        {
            if (_currentStageInternal != value)
            {
                _pluginLog.Debug($"Changing stage from {_currentStageInternal} to {value}");
                _currentStageInternal = value;
            }

            if (value != Stage.Stopped)
                _mainWindow.Flags |= ImGuiWindowFlags.NoCollapse;
            else
                _mainWindow.Flags &= ~ImGuiWindowFlags.NoCollapse;
        }
    }
    private void OnFrameworkUpdate(IFramework framework)
    {
        var tick = Environment.TickCount64; // if you don't have this, use Environment.TickCount64 but framecounter is best
        if (_lastUpdateTick == tick)
        {
            _updateReentryCount++;
            _pluginLog.Error($"FrameworkUpdate re-entered in same frame! count={_updateReentryCount}, stage={CurrentStage}");
            return; // IMPORTANT: do not run logic twice in same frame
        }

        _lastUpdateTick = tick;
        _updateReentryCount = 0;

        if (!_clientState.IsLoggedIn ||
            !WorkshopTerritories.Contains(_clientState.TerritoryType) ||
            _condition[ConditionFlag.BoundByDuty] ||
            _condition[ConditionFlag.BetweenAreas] ||
            _condition[ConditionFlag.BetweenAreas51] ||
            GetDistanceToEventObject(_fabricationStationIds, out var fabricationStation) >= 3f)
        {
            _mainWindow.NearFabricationStation = false;

            if (_mainWindow.IsOpen &&
                _mainWindow.OpenReason == WorkshoppaWindow.EOpenReason.NearFabricationStation &&
                _configuration.CurrentlyCraftedItem == null &&
                _configuration.ItemQueue.Count == 0)
            {
                _mainWindow.IsOpen = false;
            }
        }
        else if (DateTime.Now >= _continueAt)
        {
            _mainWindow.NearFabricationStation = true;

            if (!_mainWindow.IsOpen)
            {
                _mainWindow.IsOpen = true;
                _mainWindow.OpenReason = WorkshoppaWindow.EOpenReason.NearFabricationStation;
            }

            if (_mainWindow.State is WorkshoppaWindow.ButtonState.Pause or WorkshoppaWindow.ButtonState.Stop)
            {
                _mainWindow.State = WorkshoppaWindow.ButtonState.None;
                if (CurrentStage != Stage.Stopped)
                {
                    _externalPluginHandler.Restore();
                    CurrentStage = Stage.Stopped;
                }

                return;
            }
            else if (_mainWindow.State is WorkshoppaWindow.ButtonState.Start or WorkshoppaWindow.ButtonState.Resume &&
                     CurrentStage == Stage.Stopped)
            {
                // TODO Error checking, we should ensure the player has the required job level for *all* crafting parts
                _mainWindow.State = WorkshoppaWindow.ButtonState.None;
                CurrentStage = Stage.TakeItemFromQueue;
            }

            if (CurrentStage != Stage.Stopped && CurrentStage != Stage.RequestStop && !_externalPluginHandler.Saved)
                //_externalPluginHandler.Save();

            switch (CurrentStage)
            {
                case Stage.TakeItemFromQueue:
                    if (CheckContinueWithDelivery())
                        CurrentStage = Stage.ContributeMaterials;
                    else
                        TakeItemFromQueue();
                    break;

                case Stage.TargetFabricationStation:
                    if (_configuration.CurrentlyCraftedItem is { StartedCrafting: true })
                        CurrentStage = Stage.SelectCraftBranch;
                    else
                        CurrentStage = Stage.OpenCraftingLog;

                    InteractWithFabricationStation(fabricationStation!);

                    break;

                case Stage.OpenCraftingLog:
                    OpenCraftingLog();
                    break;

                case Stage.SelectCraftCategory:
                    SelectCraftCategory();
                    break;

                case Stage.WaitCraftLogRefresh:
                    WaitCraftLogRefresh();
                    break;

                case Stage.SelectCraft:
                    SelectCraft();
                    break;

                case Stage.ConfirmCraft:
                    ConfirmCraft();
                    break;

                case Stage.RequestStop:
                    _externalPluginHandler.Restore();
                    CurrentStage = Stage.Stopped;
                    break;

                case Stage.SelectCraftBranch:
                    SelectCraftBranch();
                    break;

                case Stage.ContributeMaterials:
                    ContributeMaterials();
                    break;

                case Stage.OpenRequestItemWindow:
                    // see RequestPostSetup and related
                    if (DateTime.Now > _fallbackAt)
                        goto case Stage.ContributeMaterials;
                    break;

                case Stage.OpenRequestItemSelect:
                case Stage.ConfirmRequestItemWindow:
                    // see RequestPostSetup and related
                    break;


                case Stage.ConfirmMaterialDelivery:
                    // see SelectYesNoPostSetup
                    break;

                case Stage.ConfirmCollectProduct:
                    // see SelectYesNoPostSetup
                    break;

                case Stage.Stopped:
                    break;

                default:
                    _pluginLog.Warning($"Unknown stage {CurrentStage}");
                    break;
            }
        }
    }
    private WorkshopCraft GetCurrentCraft()
    {
        return _workshopCache.Crafts.Single(
            x => x.WorkshopItemId == _configuration.CurrentlyCraftedItem!.WorkshopItemId);
    }
    private void ProcessCommand(string command, string arguments)
    {
        /*if (arguments is "c" or "config")
            _configWindow.Toggle();
        else*/
            _mainWindow.Toggle(WorkshoppaWindow.EOpenReason.Command);
    }

    private void ProcessBuyCommand(string command, string arguments)
    {
        if (_ceruleumTankWindow.TryParseBuyRequest(arguments, out int missingQuantity))
            _ceruleumTankWindow.StartPurchase(missingQuantity);
        else
            _chatGui.PrintError($"Usage: {command} <stacks>");
    }

    private void ProcessFillCommand(string command, string arguments)
    {
        if (_ceruleumTankWindow.TryParseFillRequest(arguments, out int missingQuantity))
            _ceruleumTankWindow.StartPurchase(missingQuantity);
        else
            _chatGui.PrintError($"Usage: {command} <stacks>");
    }

    public void OpenWorkshoppa()
    {
        if (!_configuration.Enabled) return;
        ProcessCommand("/ws", "");
    }

    /*public void OpenRepairKit()
    {
        if (!isEnabled || _repairKitWindow == null) return;
        ProcessCommand("/repairkits", "");
    }

    public void OpenTanks()
    {
        if (!isEnabled || _ceruleumTankWindow == null) return;
        ProcessCommand("/fill-tanks", "");
    }*/
}
