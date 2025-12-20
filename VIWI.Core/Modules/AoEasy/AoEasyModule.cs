using ECommons.Logging;
using VIWI.Core;
using VIWI.Core.Config;
using VIWI.Modules.AutoLogin;

namespace VIWI.Modules.AoEasy
{
    public sealed class AoEasyModule : IVIWIModule
    {
        public const string ModuleName = "AoEasy";
        public const string ModuleVersion = "1.0.0";

        public string Name => ModuleName;
        public string Version => ModuleVersion;

        private VIWIConfig vConfig = null!;
        public static AoEasyConfig _configuration = null!;
        public static bool Enabled => _configuration?.Enabled ?? false;

        public void Initialize(VIWIConfig config)
        {
            PluginLog.Information("[VIWI.AoEasy] Initializing...");

            LoadConfig();
            JobData.InitializeJobs();
            JobData.InitializeAbilities();

            PluginLog.Information("[VIWI.AoEasy] Initialized successfully.");
        }

        public void Dispose()
        {
            PluginLog.Information("[VIWI.AoEasy] Disposed.");
        }

        public static void LoadConfig()
        {
            /*_configuration = VIWIContext.PluginInterface.GetPluginConfig() as AoEasyConfig
                     ?? new AoEasyConfig();

            SaveConfig();*/
        }

        public static void SaveConfig()
        {
            //_configuration.Save();
        }
        public void Update()
        {
            var player = VIWIContext.PlayerState;
            if (player != null && JobData.TryGet(player.ClassJob.RowId, out var jobInfo))
            {
                PluginLog.Information($"Current job: {jobInfo.Name} ({jobInfo.Abbreviation}), Role={jobInfo.Role}");
            }
        }
    }
}
