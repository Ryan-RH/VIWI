using Dalamud.Game.ClientState.Objects.Types;
using ECommons.Throttlers;
using FFXIVClientStructs.FFXIV.Component.GUI;
using System;
using System.Configuration;
using System.Linq;
using VIWI.Core;
using VIWI.Helpers;
using VIWI.Modules.Workshoppa.GameData;
using ValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace VIWI.Modules.Workshoppa;

internal sealed partial class WorkshoppaModule
{
    private uint _lastShownCount;
    private uint _stableFrames;

    private void InteractWithFabricationStation(IGameObject fabricationStation)
        => InteractWithTarget(fabricationStation);

    private void TakeItemFromQueue()
    {
        if (_configuration.CurrentlyCraftedItem == null)
        {
            while (_configuration.ItemQueue.Count > 0 && _configuration.CurrentlyCraftedItem == null)
            {
                var firstItem = _configuration.ItemQueue[0];
                if (firstItem.Quantity > 0)
                {
                    _configuration.CurrentlyCraftedItem = new WorkshoppaConfig.CurrentItem
                    {
                        WorkshopItemId = firstItem.WorkshopItemId,
                    };

                    if (firstItem.Quantity > 1)
                        firstItem.Quantity--;
                    else
                        _configuration.ItemQueue.Remove(firstItem);
                }
                else
                    _configuration.ItemQueue.Remove(firstItem);
            }

            //_pluginInterface.SavePluginConfig(_configuration);
            if (_configuration.CurrentlyCraftedItem != null)
                CurrentStage = Stage.TargetFabricationStation;
            else
                CurrentStage = Stage.RequestStop;
        }
        else
            CurrentStage = Stage.TargetFabricationStation;
    }

    private void OpenCraftingLog()
    {
        if (SelectSelectString("craftlog", 0, s => s == _gameStrings.ViewCraftingLog))
            CurrentStage = Stage.SelectCraftCategory;
    }

    private unsafe void SelectCraftCategory()
    {
        var addon = GetCompanyCraftingLogAddon();
        if (addon == null || !addon->IsVisible) return;
        if (addon->AtkValues == null || addon->AtkValuesCount < 20) return;

        var craft = GetCurrentCraft();
        _pluginLog.Information($"Selecting category {craft.Category} and type {craft.Type}");

        var args = stackalloc AtkValue[8];
        args[0] = new() { Type = ValueType.Int, Int = 2 };
        args[1] = new() { Type = ValueType.Int, Int = 0 };
        args[2] = new() { Type = ValueType.UInt, UInt = (uint)craft.Category };
        args[3] = new() { Type = ValueType.UInt, UInt = craft.Type };
        args[4] = new() { Type = ValueType.Int, Int = 0 };
        args[5] = new() { Type = ValueType.Int, Int = 0 };
        args[6] = new() { Type = ValueType.Int, Int = 0 };
        args[7] = new() { Type = ValueType.Int, Int = 0 };

        addon->FireCallback(8, args);
        CurrentStage = Stage.WaitCraftLogRefresh;
        _stableFrames = 0;
        _lastShownCount = 0;
        _continueAt = DateTime.Now.AddSeconds(0.1);
    }

    private unsafe void WaitCraftLogRefresh()
    {
        var addon = GetCompanyCraftingLogAddon();
        if (addon == null || !addon->IsVisible) return;
        if (addon->AtkValues == null || addon->AtkValuesCount < 20) return;

        var atk = addon->AtkValues;
        var shown = atk[13].UInt;

        if (shown == 0)
        {
            _stableFrames = 0;
            return;
        }
        var maxIndex = 17 + 4 * (int)(shown - 1);
        if (maxIndex >= addon->AtkValuesCount)
        {
            _stableFrames = 0;
            return;
        }
        if (shown == _lastShownCount)
            _stableFrames++;
        else
        {
            _lastShownCount = shown;
            _stableFrames = 0;
            return;
        }
        if (_stableFrames >= 2)
        {
            CurrentStage = Stage.SelectCraft;
            _continueAt = DateTime.Now;
        }
    }

    private unsafe void SelectCraft()
    {
        var addon = GetCompanyCraftingLogAddon();
        if (addon == null || !addon->IsVisible) return;
        if (addon->AtkValues == null || addon->AtkValuesCount < 20) return;

        var craft = GetCurrentCraft();
        var atk = addon->AtkValues;

        var shown = atk[13].UInt;
        if (shown == 0) return;

        var maxIndex = 17 + 4 * (int)(shown - 1);
        if (maxIndex >= addon->AtkValuesCount) return;

        bool found = false;
        for (int i = 0; i < (int)shown; i++)
        {
            var id = atk[14 + 4 * i].UInt;
            if (id == craft.WorkshopItemId)
            {
                found = true;
                break;
            }
        }

        if (!found)
        {
            _pluginLog.Error($"Could not find {craft.Name} in current list, is it unlocked?");
            CurrentStage = Stage.RequestStop;
            return;
        }

        _pluginLog.Information($"Selecting craft {craft.WorkshopItemId}");

        var args = stackalloc AtkValue[8];
        args[0] = new() { Type = ValueType.Int, Int = 1 };
        args[1] = new() { Type = ValueType.Int, Int = 0 };
        args[2] = new() { Type = ValueType.Int, Int = 0 };
        args[3] = new() { Type = ValueType.Int, Int = 0 };
        args[4] = new() { Type = ValueType.UInt, UInt = craft.WorkshopItemId };
        args[5] = new() { Type = ValueType.Int, Int = 0 };
        args[6] = new() { Type = ValueType.Int, Int = 0 };
        args[7] = new() { Type = ValueType.Int, Int = 0 };

        addon->FireCallback(8, args);

        CurrentStage = Stage.ConfirmCraft;
        _continueAt = DateTime.Now.AddSeconds(0.1);
    }

    private void ConfirmCraft()
    {
        if (SelectSelectYesno(0, s => s.StartsWith("Craft ", StringComparison.Ordinal)))
        {
            _configuration.CurrentlyCraftedItem!.StartedCrafting = true;
            //_pluginInterface.SavePluginConfig(_configuration);

            CurrentStage = Stage.TargetFabricationStation;
        }
    }
}
