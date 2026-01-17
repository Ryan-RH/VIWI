using System.Linq;
using VIWI.Core;

namespace VIWI.Helpers
{
    internal interface IPCHelper
    {
        public static bool IsPluginLoaded(string internalName)
        {
            try
            {
                return VIWIContext.PluginInterface?.InstalledPlugins
                    ?.Any(p => p.InternalName == internalName && p.IsLoaded) ?? false;
            }
            catch
            {
                return false;
            }
        }
    }
}
