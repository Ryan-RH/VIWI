using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using VIWI.Modules.AoEasy;

namespace VIWI.UI.Pages
{
    public sealed class AoEasyPage : IDashboardPage
    {
        public string DisplayName => "AoEasy";
        public string Category => "Modules";
        public bool SupportsEnableToggle => true;
        public string Version => AoEasyModule.ModuleVersion;

        public bool IsEnabled
        {
            get => AoEasyModule.Enabled;
        }

        public void SetEnabled(bool value)
        {
            AoEasyModule.Config.Enabled = value;
            AoEasyModule.SaveConfig();
        }

        public void Draw()
        {
            var config = AoEasyModule.Config;
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.TextUnformatted($"AoEasy – Stop Running Away From Me! - V{Version}");
            ImGui.TextUnformatted($"Enabled: {config.Enabled}");
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.Separator();

            ImGuiHelpers.ScaledDummy(8f);
            ImGui.TextUnformatted("Description:");
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.TextWrapped(
                "STILL IN DEVELOPMENT!!!"
            );
            ImGui.Separator();

            ImGuiHelpers.ScaledDummy(8f);

        }
    }
}
