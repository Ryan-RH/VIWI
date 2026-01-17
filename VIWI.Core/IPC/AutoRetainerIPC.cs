using Dalamud.Plugin.Ipc;
using ECommons.DalamudServices;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace VIWI.IPC;

public sealed class AutoRetainerIPC
{
    public string Name => "AutoRetainer";
    private const string Ipc_Init = "AutoRetainer.Init";
    private const string Ipc_GetRegisteredCids = "AutoRetainer.GetRegisteredCIDs";
    private const string Ipc_GetOfflineCharacterData = "AutoRetainer.GetOfflineCharacterData";
    private const string Ipc_IsBusy = "AutoRetainer.PluginState.IsBusy";
    private const string Ipc_GetMultiModeEnabled = "AutoRetainer.GetMultiModeEnabled";

    public bool IsLoaded =>
        Svc.PluginInterface.InstalledPlugins.Any(p => p.InternalName == Name && p.IsLoaded);

    private readonly ICallGateSubscriber<object> _init;
    private readonly ICallGateSubscriber<List<ulong>> _getCids;
    private readonly ICallGateSubscriber<ulong, object> _getOffline;

    private readonly Lazy<ICallGateSubscriber<bool>> _isBusy;
    private readonly Lazy<ICallGateSubscriber<bool>> _getMultiModeEnabled;

    public AutoRetainerIPC()
    {
        _init = Svc.PluginInterface.GetIpcSubscriber<object>(Ipc_Init);
        _getCids = Svc.PluginInterface.GetIpcSubscriber<List<ulong>>(Ipc_GetRegisteredCids);
        _getOffline = Svc.PluginInterface.GetIpcSubscriber<ulong, object>(Ipc_GetOfflineCharacterData);
        _isBusy = new Lazy<ICallGateSubscriber<bool>>(() => Svc.PluginInterface.GetIpcSubscriber<bool>(Ipc_IsBusy));
        _getMultiModeEnabled = new Lazy<ICallGateSubscriber<bool>>(() => Svc.PluginInterface.GetIpcSubscriber<bool>(Ipc_GetMultiModeEnabled));
    }

    public bool Ready
    {
        get
        {
            if (!IsLoaded) return false;
            try { _init.InvokeAction(); return true; }
            catch { return false; }
        }
    }

    public List<ulong> GetRegisteredCIDs() => _getCids.InvokeFunc() ?? new List<ulong>();
    public OfflineCharacterInfo? GetOfflineCharacterInfo(ulong cid)
    {
        var data = _getOffline.InvokeFunc(cid);
        if (data == null)
            return null;

        return OfflineCharacterInfo.FromUnknownObject(data);
    }
    public bool IsBusy() => Ready && _isBusy.Value.InvokeFunc();
    public bool GetMultiModeEnabled() => Ready && _getMultiModeEnabled.Value.InvokeFunc();

    public sealed record OfflineCharacterInfo(
        ulong CID,
        string Name,
        string World,
        bool ExcludeRetainer,
        bool ExcludeWorkshop)
    {
        public static OfflineCharacterInfo? FromUnknownObject(object data)
        {
            try
            {
                if (data is JObject jo)
                {
                    var cid = jo.Value<ulong?>("CID") ?? 0;
                    var name = (jo.Value<string>("Name") ?? "").Trim();
                    var world = (jo.Value<string>("WorldOverride") ?? jo.Value<string>("World") ?? jo.Value<string>("CurrentWorld") ?? "").Trim();
                    var exRet = jo.Value<bool?>("ExcludeRetainer") ?? false;
                    var exWs = jo.Value<bool?>("ExcludeWorkshop") ?? false;

                    if (cid == 0 || string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(world))
                        return null;

                    return new OfflineCharacterInfo(cid, name, world, exRet, exWs);
                }

                var t = data.GetType();
                var cid2 = Read<ulong>(t, data, "CID");
                var name2 = (Read<string>(t, data, "Name") ?? "").Trim();
                var world2 = ((Read<string>(t, data, "WorldOverride") ?? Read<string>(t, data, "World") ?? Read<string>(t, data, "CurrentWorld")) ?? "").Trim();
                var exRet2 = Read<bool>(t, data, "ExcludeRetainer");
                var exWs2 = Read<bool>(t, data, "ExcludeWorkshop");

                if (cid2 == 0 || string.IsNullOrWhiteSpace(name2) || string.IsNullOrWhiteSpace(world2))
                    return null;

                return new OfflineCharacterInfo(cid2, name2, world2, exRet2, exWs2);
            }
            catch
            {
                return null;
            }
        }

        private static T? Read<T>(Type t, object obj, string propName)
        {
            var p = t.GetProperty(propName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (p == null) return default;
            var v = p.GetValue(obj);
            return v is T tv ? tv : default;
        }
    }
}
