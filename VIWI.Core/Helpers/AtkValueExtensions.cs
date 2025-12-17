using FFXIVClientStructs.FFXIV.Component.GUI;
using AtkValueType = FFXIVClientStructs.FFXIV.Component.GUI.ValueType;

namespace VIWI.Helpers;

internal static unsafe class AtkValueExtensions
{
    public static string ReadAtkString(this AtkValue atkValue)
    {
        // String types are 0x03 (managed string) or 0x08 (Utf8String*)
        if (atkValue.Type != AtkValueType.String && atkValue.Type != AtkValueType.ManagedString)
            return string.Empty;

        var strPtr = atkValue.String;
        if (strPtr == null)
            return string.Empty;

        return atkValue.String.ToString();
    }
}
