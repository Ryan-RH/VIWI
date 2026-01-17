using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Ipc.Exceptions;
using ECommons.ImGuiMethods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using VIWI.Core;
using VIWI.Helpers;
using VIWI.Modules.KitchenSink;

namespace VIWI.UI.Pages
{
    public sealed class KitchenSinkPage : IDashboardPage
    {
        public string DisplayName => "KitchenSink";
        public string Category => "Modules";
        public string Version => KitchenSinkModule.ModuleVersion;
        public bool SupportsEnableToggle => true;
        public bool IsEnabled => KitchenSinkModule.Enabled;
        public void SetEnabled(bool value) => KitchenSinkModule.Instance?.SetEnabled(value);

        private bool _buffersInitialized;
        private string _charSearch = string.Empty;
        private readonly Dictionary<ulong, (string Name, string World)> _nameByCid = new();
        private DateTime _nextRefreshAt = DateTime.MinValue;

        public void Draw()
        {
            var module = KitchenSinkModule.Instance;
            var config = module?._configuration;

            if (config == null)
            {
                ImGui.TextDisabled("KitchenSink is not initialized yet.");
                return;
            }

            if (!_buffersInitialized)
            {
                _buffersInitialized = true;
                _charSearch = string.Empty;
                _nameByCid.Clear();
                _nextRefreshAt = DateTime.MinValue;
            }

            ImGuiHelpers.ScaledDummy(4f);

            ImGui.TextUnformatted($"KitchenSink - V{Version}");
            ImGui.SameLine();
            ImGui.TextColored(GradientColor.Get(ImGuiHelper.RainbowColorStart, ImGuiHelper.RainbowColorEnd, 500), "Yes, Everything Is Included!");

            ImGui.TextUnformatted("Enabled:");
            ImGui.SameLine();
            ImGui.TextColored(
                config.Enabled ? new Vector4(0.3f, 1f, 0.3f, 1f) : new Vector4(1f, 0.3f, 0.3f, 1f),
                config.Enabled ? "Yes" : "No - Click the OFF button to Enable KitchenSink!!"
            );

            ImGuiHelpers.ScaledDummy(4f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            ImGui.TextUnformatted("Description:");
            ImGuiHelpers.ScaledDummy(4f);
            ImGui.TextWrapped(
                "KitchenSink is a collection of small utility tools, overlays, and QoL commands originally put together by Liza.\n" +
                "Character Switching helpers, Dropbox helpers, GlamourSet Tracking, OC Carrot Markers, and more!\n" +
                "Some features require specific plugins (e.g. AutoRetainer, Dropbox) to be installed."
            );

            ImGuiHelpers.ScaledDummy(8f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            DrawQuickStatus(module);
            ImGuiHelpers.ScaledDummy(8f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            DrawPerCharacterSection(config, module);

            ImGuiHelpers.ScaledDummy(8f);
            ImGui.Separator();
            ImGuiHelpers.ScaledDummy(8f);

            DrawCommandsCheatsheet();
        }

        private void DrawQuickStatus(KitchenSinkModule module)
        {
            var loggedIn = VIWIContext.ClientState?.IsLoggedIn ?? false;

            ImGui.TextUnformatted("Status:");
            ImGui.SameLine();
            ImGui.TextColored(loggedIn ? new Vector4(0.3f, 1f, 0.3f, 1f) : new Vector4(1f, 0.75f, 0.3f, 1f), loggedIn ? "Logged in" : "Not logged in");

            var ar = module.GetAutoRetainer();
            bool arLoaded = IPCHelper.IsPluginLoaded("AutoRetainer");
            bool arReady = false;

            if (arLoaded)
            {
                try { arReady = ar!.Ready; } catch { arReady = false; }
            }

            ImGui.TextUnformatted("AutoRetainer:");
            ImGui.SameLine();
            ImGui.TextColored(arReady ? new Vector4(0.3f, 1f, 0.3f, 1f) : arLoaded ? new Vector4(1f, 0.75f, 0.3f, 1f) : new Vector4(1f, 0.3f, 0.3f, 1f), arReady ? "Ready" : arLoaded ? "Loaded (not ready)" : "Not loaded");
            ImGuiComponents.HelpMarker("AutoRetainer enables some character-aware features in KitchenSink:\n\n" +
                                        "• Character switching commands (/k+, /k-, /ks)\n" +
                                        "• DTR bar character index display\n" +
                                        "• Character storing for leve count indicators\n");
            bool dbLoaded = IPCHelper.IsPluginLoaded("Dropbox");
            bool dbReady = dbLoaded;

            ImGui.TextUnformatted("Dropbox:");
            ImGui.SameLine();
            ImGui.TextColored(dbReady ? new Vector4(0.3f, 1f, 0.3f, 1f) : dbLoaded ? new Vector4(1f, 0.75f, 0.3f, 1f) : new Vector4(1f, 0.3f, 0.3f, 1f), dbReady ? "Ready" : dbLoaded ? "Loaded (not ready)" : "Not loaded");
            ImGuiComponents.HelpMarker("Dropbox enables inventory and trade helpers via /dbq commands.");
        }

        private void DrawPerCharacterSection(KitchenSinkConfig config, KitchenSinkModule module)
        {
            ImGui.TextUnformatted("Per-character settings");
            ImGuiComponents.HelpMarker(
                "KitchenSink stores some options per character (by LocalContentId).\n" +
                "If you don't see your character here, log in once so KitchenSink can capture it."
            );

            ImGuiHelpers.ScaledDummy(4f);

            ImGui.SetNextItemWidth(-1);
            ImGui.InputTextWithHint("##ks_char_search", "Filter characters (name/world/cid)", ref _charSearch, 128);

            var chars = config.Characters ?? new List<KitchenSinkConfig.CharacterData>();
            if (chars.Count == 0)
            {
                ImGui.TextDisabled("No character entries yet.");
                return;
            }

            RefreshNameCacheIfNeeded(chars, module);

            var filter = _charSearch?.Trim();
            var filtered = string.IsNullOrWhiteSpace(filter) ? chars : chars.Where(x =>
                {
                    var cidStr = x.LocalContentId.ToString();
                    if (_nameByCid.TryGetValue(x.LocalContentId, out var nw))
                    {
                        var blob = $"{nw.Name}@{nw.World} {cidStr}";
                        return blob.Contains(filter!, StringComparison.OrdinalIgnoreCase);
                    }
                    return cidStr.Contains(filter!, StringComparison.OrdinalIgnoreCase);
                }).ToList();

            if (filtered.Count == 0)
            {
                ImGui.TextDisabled("No matches.");
                return;
            }

            foreach (var c in filtered)
            {
                var cid = c.LocalContentId;
                ImGui.PushID((int)(cid % int.MaxValue));

                string header = _nameByCid.TryGetValue(cid, out var nw) ? $"{nw.Name}@{nw.World}  (CID: {cid})" : $"CID: {cid}";
                bool open = ImGui.CollapsingHeader(header, ImGuiTreeNodeFlags.DefaultOpen | ImGuiTreeNodeFlags.AllowItemOverlap);

                ImGui.SetItemAllowOverlap();
                float iconSize = ImGui.GetFrameHeight();
                var rMin = ImGui.GetItemRectMin();
                var rMax = ImGui.GetItemRectMax();

                ImGui.SetCursorScreenPos(new Vector2(rMax.X - iconSize - 6f, rMin.Y + (rMax.Y - rMin.Y - iconSize) * 0.5f));
                ImGui.PushStyleColor(ImGuiCol.Button, 0x66000000);
                ImGui.PushStyleColor(ImGuiCol.ButtonHovered, 0x88FFFFFF);
                ImGui.PushStyleColor(ImGuiCol.ButtonActive, 0xAAFFFFFF);

                if (ImGuiComponents.IconButton(FontAwesomeIcon.Trash))
                {
                    config.Characters.RemoveAll(x => x.LocalContentId == cid);
                    _nameByCid.Remove(cid);
                    module.SaveConfig();

                    ImGui.PopStyleColor(3);
                    ImGui.PopID();
                    break;
                }

                ImGui.PopStyleColor(3);

                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip("Delete this character entry from KitchenSink");

                ImGui.SetCursorScreenPos(new Vector2(rMin.X, rMax.Y + ImGui.GetStyle().ItemSpacing.Y));

                if (open)
                {
                    bool warnLeves = c.WarnAboutLeves;
                    if (ImGui.Checkbox("Warn about Leve allowances on login", ref warnLeves))
                    {
                        c.WarnAboutLeves = warnLeves;
                        module.SaveConfig();
                    }
                }

                ImGui.PopID();

            }
        }

        private void RefreshNameCacheIfNeeded(List<KitchenSinkConfig.CharacterData> chars, KitchenSinkModule module)
        {
            var now = DateTime.UtcNow;
            if (now < _nextRefreshAt)
                return;

            _nextRefreshAt = now.AddSeconds(2);

            var ar = module.GetAutoRetainer();
            if (ar == null || !ar.IsLoaded)
                return;

            bool ready;
            try { ready = ar.Ready; }
            catch { return; }

            if (!ready)
                return;
            foreach (var ch in chars)
            {
                var cid = ch.LocalContentId;
                if (_nameByCid.ContainsKey(cid))
                    continue;

                var info = ar.GetOfflineCharacterInfo(cid);
                if (info != null && !string.IsNullOrWhiteSpace(info.Name) && !string.IsNullOrWhiteSpace(info.World))
                {
                    _nameByCid[cid] = (info.Name.Trim(), info.World.Trim());
                }
            }
        }

        private static void DrawCommandsCheatsheet()
        {
            ImGui.TextUnformatted("Commands:");
            ImGuiHelpers.ScaledDummy(4f);

            if (ImGui.BeginTable("KitchenSinkCommands", 2, ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.RowBg))
            {
                ImGui.TableSetupColumn("Command", ImGuiTableColumnFlags.WidthFixed);
                ImGui.TableSetupColumn("Description", ImGuiTableColumnFlags.WidthStretch);

                void SectionHeader(string text)
                {
                    ImGui.TableNextRow();
                    ImGui.TableSetColumnIndex(0);
                    ImGui.TableSetBgColor(ImGuiTableBgTarget.RowBg0, 0x33000000);
                    ImGui.TextUnformatted(text);
                    ImGui.TableSetColumnIndex(1);
                    ImGui.TextUnformatted("");
                }

                void Row(string cmd, string desc)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(cmd);
                    ImGui.TableNextColumn();
                    ImGui.TextWrapped(desc);
                }

                SectionHeader("Character Switcher");
                Row("/k+", "Switch to the next AR-enabled character.");
                Row("/k-", "Switch to the previous AR-enabled character.");
                Row("/ks [partialCharacterName]", "Switch to the first character with a matching name.");
                Row("/ks [world] [index]", "Switch to the Nth character on the specified world.");

                SectionHeader("Dropbox Queue");
                Row("/dbq item1:qty1 item2:qty2 …", "Queue items for the next trade (* = all).");
                Row("/dbq clear", "Remove all items from the queue.");
                Row("/dbq request …", "Generate a command to fill your inventory.");
                Row("/dbq [shards/crystals/shards+crystals]", "Generate a command to fill shards/crystals to 9999.");

                SectionHeader("Utilities");
                Row("/whatweather [n]", "Toggle weather overlay or set forecast length.");
                Row("/glamoursets", "Show the glamour set tracker.");
                Row("/bunbun", "Toggle OC Bunny overlay.");

                ImGui.EndTable();
            }
        }
    }
}
