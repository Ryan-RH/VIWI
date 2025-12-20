using Dalamud.Configuration;
using Dalamud.Plugin;
using ECommons.Logging;
using System;
using VIWI.Modules.AoEasy;
using VIWI.Modules.AutoLogin;
using VIWI.Modules.Workshoppa;

namespace VIWI.Core.Config
{
    [Serializable]
    public sealed class VIWIConfig : IPluginConfiguration
    {
        public int Version { get; set; } = 1;
        public bool Enabled = true;

        public AutoLoginConfig AutoLogin { get; set; } = new();
        public WorkshoppaConfig Workshoppa { get; set; } = new();
        public AoEasyConfig AoEasy { get; set; } = new();

        [NonSerialized]
        private IDalamudPluginInterface? pluginInterface;

        public void Initialize(IDalamudPluginInterface pi)
        {
            pluginInterface = pi;
        }

        public void Save()
        {
            if (pluginInterface == null)
            {
                PluginLog.Error("[VIWIConfig] Save() called but pluginInterface is null. Did you forget Config.Initialize(pi)?");
                return;
            }

            try
            {
                pluginInterface.SavePluginConfig(this);
                PluginLog.Information("[VIWIConfig] Saved config.");
            }
            catch (Exception ex)
            {
                PluginLog.Error("[VIWIConfig] Failed to save config.");
            }
        }
    }
}
