using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Component.GUI;

namespace VIWI.Helpers;

internal static unsafe class AddonHelpers
{
    public static bool TryGetAddonByName<T>(IGameGui gameGui, string name, out T* addon)
        where T : unmanaged
    {
        var ptr = gameGui.GetAddonByName(name);
        addon = (T*)(nint)ptr;
        return ptr != nint.Zero;
    }
}
internal static unsafe class AddonState
{
    public static AtkUnitBase* GetAddonById(ushort addonId)
    {
        if (addonId == 0) return null;
        return RaptureAtkUnitManager.Instance()->GetAddonById(addonId);
    }

    public static bool IsAddonReady(AtkUnitBase* addon)
    {
        if (addon == null) return false;

        if (addon->UldManager.NodeList == null) return false;
        if (addon->UldManager.NodeListCount == 0) return false;

        return true;
    }
}