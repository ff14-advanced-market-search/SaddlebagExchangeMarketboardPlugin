using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin;
using Dalamud.Utility;

namespace SaddlebagExchange.UI
{
    public sealed class HomeTab
    {
        private const float ServerComboWidth = 200f * 1.2f; // match Reselling Search / Market Overview
        private const string GuidesUrl = "https://github.com/ff14-advanced-market-search/saddlebag-with-pockets/wiki";
        private const string PatreonUrl = "https://www.patreon.com/indopan";
        private const string DiscordUrl = "https://discord.gg/9dHx2rEq9F";
        private const string WebsiteUrl = "https://saddlebagexchange.com/wow";

        private string _defaultDc = string.Empty;

        public void Draw(IDalamudPluginInterface? pluginInterface, Func<string>? getDefaultHomeServer = null, Action<string>? setDefaultHomeServer = null, Action<int>? onSelectTool = null)
        {
            ImGui.Spacing();

            ImGui.Text("Saddlebag Exchange");
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextWrapped("Marketboard analytics and cross-world arbitrage for FFXIV. Use the links below for guides, community, and the full website.");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("Guides"))
                Util.OpenLink(GuidesUrl);
            ImGui.SameLine();
            if (ImGui.Button("Patreon"))
                Util.OpenLink(PatreonUrl);
            ImGui.SameLine();
            if (ImGui.Button("Discord"))
                Util.OpenLink(DiscordUrl);
            ImGui.SameLine();
            if (ImGui.Button("Website"))
                Util.OpenLink(WebsiteUrl);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (getDefaultHomeServer != null && setDefaultHomeServer != null)
                DrawDefaultHomeServerSection(getDefaultHomeServer, setDefaultHomeServer);

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (onSelectTool != null)
                DrawToolsSection(onSelectTool);

            ImGui.Spacing();
        }

        private void DrawToolsSection(Action<int> onSelectTool)
        {
            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.5f, 0.3f, 0.7f, 1f));
            ImGui.Text("TOOLS & FEATURES");
            ImGui.PopStyleColor();
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + 4f);
            ImGui.Text("Everything You Need for Gil Making.");
            ImGui.Spacing();

            float avail = ImGui.GetContentRegionAvail().X;
            float cardMaxWidth = 300f * ImGuiHelpers.GlobalScale;
            float cardWidth = Math.Min(avail, cardMaxWidth);
            float cardHeight = 88f * ImGuiHelpers.GlobalScale;
            float wrapX = cardWidth - ImGui.GetStyle().WindowPadding.X * 2f;

            // Reselling Trade Searches
            using (var child = ImRaii.Child("##tool_reselling", new System.Numerics.Vector2(cardWidth, cardHeight), true, ImGuiWindowFlags.None))
            {
                if (child.Success)
                {
                    var resellingMin = ImGui.GetCursorScreenPos();
                    var resellingHitSize = ImGui.GetContentRegionAvail();
                    if (ImGui.InvisibleButton("##btn_reselling", resellingHitSize))
                        onSelectTool(1);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Click to open Reselling Search");
                    ImGui.SetCursorScreenPos(resellingMin);
                    ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.45f, 0.28f, 0.65f, 1f));
                    ImGui.Text("Reselling Trade Searches");
                    ImGui.PopStyleColor();
                    ImGui.SetCursorPosX(ImGui.GetStyle().WindowPadding.X);
                    ImGui.PushTextWrapPos(ImGui.GetCursorPos().X + wrapX);
                    ImGui.TextWrapped("Find items you can buy on other servers and resell on your own for a profit!");
                    ImGui.PopTextWrapPos();
                }
            }

            ImGui.Spacing();

            // Marketshare Overview
            using (var child = ImRaii.Child("##tool_marketshare", new System.Numerics.Vector2(cardWidth, cardHeight), true, ImGuiWindowFlags.None))
            {
                if (child.Success)
                {
                    var msMin = ImGui.GetCursorScreenPos();
                    var msHitSize = ImGui.GetContentRegionAvail();
                    if (ImGui.InvisibleButton("##btn_marketshare", msHitSize))
                        onSelectTool(2);
                    if (ImGui.IsItemHovered())
                        ImGui.SetTooltip("Click to open Market Overview");
                    ImGui.SetCursorScreenPos(msMin);
                    ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.45f, 0.28f, 0.65f, 1f));
                    ImGui.Text("Marketshare Overview");
                    ImGui.PopStyleColor();
                    ImGui.SetCursorPosX(ImGui.GetStyle().WindowPadding.X);
                    ImGui.PushTextWrapPos(ImGui.GetCursorPos().X + wrapX);
                    ImGui.TextWrapped("Finds the best items to sell! Shows the top 200 best selling items on your home server.");
                    ImGui.PopTextWrapPos();
                }
            }
        }

        private void DrawDefaultHomeServerSection(Func<string> getDefaultHomeServer, Action<string> setDefaultHomeServer)
        {
            ImGui.Text("Default home server");
            ImGui.SameLine();
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Default home server for all searches. Pre-filled whenever you run a search.");
            ImGui.Spacing();

            string currentWorld = getDefaultHomeServer();
            if (!string.IsNullOrEmpty(currentWorld))
            {
                var dc = WorldList.GetDataCenterForWorld(currentWorld);
                if (!string.IsNullOrEmpty(dc))
                    _defaultDc = dc;
            }

            string dcPreview = string.IsNullOrEmpty(_defaultDc) ? "Select data center..." : _defaultDc;
            ImGui.SetNextItemWidth(ServerComboWidth * ImGuiHelpers.GlobalScale);
            if (ImGui.BeginCombo("Data Center", dcPreview))
            {
                foreach (string dc in WorldList.GetDataCenters())
                {
                    bool selected = _defaultDc == dc;
                    if (ImGui.Selectable(dc, selected))
                    {
                        _defaultDc = dc;
                        var dcWorlds = WorldList.GetWorlds(dc);
                        if (dcWorlds.Length > 0 && Array.IndexOf(dcWorlds, currentWorld) < 0)
                            setDefaultHomeServer(string.Empty);
                    }
                    if (selected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.SameLine();

            string[] worlds = string.IsNullOrEmpty(_defaultDc) ? Array.Empty<string>() : WorldList.GetWorlds(_defaultDc);
            string worldPreview = string.IsNullOrEmpty(currentWorld) ? "Select world..." : currentWorld;
            ImGui.SetNextItemWidth(ServerComboWidth * ImGuiHelpers.GlobalScale);
            if (ImGui.BeginCombo("World", worldPreview))
            {
                foreach (string world in worlds)
                {
                    bool selected = currentWorld == world;
                    if (ImGui.Selectable(world, selected))
                        setDefaultHomeServer(world);
                    if (selected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
        }
    }
}
