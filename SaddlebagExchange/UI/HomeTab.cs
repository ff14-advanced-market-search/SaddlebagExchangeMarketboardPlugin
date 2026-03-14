using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;

namespace SaddlebagExchange.UI
{
    public sealed class HomeTab
    {
        private const float ServerComboWidth = 200f * 1.2f; // match Reselling Search / Market Overview
        private const string GuidesUrl = "https://github.com/ff14-advanced-market-search/saddlebag-with-pockets/wiki";
        private const string PatreonUrl = "https://www.patreon.com/indopan";
        private const string DiscordUrl = "https://discord.gg/9dHx2rEq9F";
        private const string WebsiteUrl = "https://saddlebagexchange.com/wow";

        private object? _iconTexture;
        private bool _iconLoadAttempted;
        private string _defaultDc = string.Empty;

        public void Draw(IDalamudPluginInterface? pluginInterface, Func<string>? getDefaultHomeServer = null, Action<string>? setDefaultHomeServer = null, Action<int>? onSelectTool = null)
        {
            ImGui.Spacing();

            TryLoadIcon(pluginInterface);
            if (_iconTexture != null)
            {
                try
                {
                    var handle = GetTextureHandle(_iconTexture);
                    var size = GetTextureSize(_iconTexture);
                    if (handle != IntPtr.Zero && size.X > 0 && size.Y > 0)
                    {
                    float maxSide = 128f;
                    float scale = Math.Min(Math.Min(maxSide / size.X, maxSide / size.Y), 1f);
                    var displaySize = new System.Numerics.Vector2(size.X * scale, size.Y * scale);
                    ImGui.Image(new ImTextureID(handle), displaySize);
                        ImGui.Spacing();
                    }
                }
                catch { /* ignore */ }
            }

            ImGui.Text("Saddlebag Exchange");
            ImGui.Separator();
            ImGui.Spacing();
            ImGui.TextWrapped("Marketboard analytics and cross-world arbitrage for FFXIV. Use the links below for guides, community, and the full website.");
            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (ImGui.Button("Guides"))
                OpenUrl(GuidesUrl);
            ImGui.SameLine();
            if (ImGui.Button("Patreon"))
                OpenUrl(PatreonUrl);
            ImGui.SameLine();
            if (ImGui.Button("Discord"))
                OpenUrl(DiscordUrl);
            ImGui.SameLine();
            if (ImGui.Button("Website"))
                OpenUrl(WebsiteUrl);

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
            const float cardMaxWidth = 300f;
            float cardWidth = Math.Min(avail, cardMaxWidth);
            const float cardHeight = 88f;
            float wrapX = cardWidth - ImGui.GetStyle().WindowPadding.X * 2f;

            // Reselling Trade Searches
            ImGui.BeginChild("##tool_reselling", new System.Numerics.Vector2(cardWidth, cardHeight), true, ImGuiWindowFlags.None);
            var resellingMin = ImGui.GetCursorScreenPos();
            if (ImGui.InvisibleButton("##btn_reselling", new System.Numerics.Vector2(cardWidth, cardHeight)))
                onSelectTool(1);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Click to open Reselling Search");
            ImGui.SetCursorScreenPos(new System.Numerics.Vector2(resellingMin.X + ImGui.GetStyle().WindowPadding.X, resellingMin.Y + ImGui.GetStyle().WindowPadding.Y));
            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.45f, 0.28f, 0.65f, 1f));
            ImGui.Text("Reselling Trade Searches");
            ImGui.PopStyleColor();
            ImGui.SetCursorPosX(ImGui.GetStyle().WindowPadding.X);
            ImGui.PushTextWrapPos(ImGui.GetCursorPos().X + wrapX);
            ImGui.TextWrapped("Find items you can buy on other servers and resell on your own for a profit!");
            ImGui.PopTextWrapPos();
            ImGui.EndChild();

            ImGui.Spacing();

            // Marketshare Overview
            ImGui.BeginChild("##tool_marketshare", new System.Numerics.Vector2(cardWidth, cardHeight), true, ImGuiWindowFlags.None);
            var msMin = ImGui.GetCursorScreenPos();
            if (ImGui.InvisibleButton("##btn_marketshare", new System.Numerics.Vector2(cardWidth, cardHeight)))
                onSelectTool(2);
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Click to open Market Overview");
            ImGui.SetCursorScreenPos(new System.Numerics.Vector2(msMin.X + ImGui.GetStyle().WindowPadding.X, msMin.Y + ImGui.GetStyle().WindowPadding.Y));
            ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.45f, 0.28f, 0.65f, 1f));
            ImGui.Text("Marketshare Overview");
            ImGui.PopStyleColor();
            ImGui.SetCursorPosX(ImGui.GetStyle().WindowPadding.X);
            ImGui.PushTextWrapPos(ImGui.GetCursorPos().X + wrapX);
            ImGui.TextWrapped("Finds the best items to sell! Shows the top 200 best selling items on your home server.");
            ImGui.PopTextWrapPos();
            ImGui.EndChild();
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
            ImGui.SetNextItemWidth(ServerComboWidth);
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
            ImGui.SetNextItemWidth(ServerComboWidth);
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

        private void TryLoadIcon(IDalamudPluginInterface? pluginInterface)
        {
            if (_iconLoadAttempted || pluginInterface == null) return;
            _iconLoadAttempted = true;
            try
            {
                var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
                var path = Path.Combine(dir ?? ".", "icon.png");
                if (!File.Exists(path)) return;
                var textureProvider = pluginInterface.GetService(typeof(Dalamud.Plugin.Services.ITextureProvider));
                if (textureProvider == null) return;
                var loadMethod = textureProvider.GetType().GetMethod("GetFromFileAsync", new[] { typeof(string) });
                if (loadMethod == null) return;
                var task = loadMethod.Invoke(textureProvider, new object[] { path });
                if (task == null) return;
                var getAwaiter = task.GetType().GetMethod("GetAwaiter");
                if (getAwaiter == null) return;
                var awaiter = getAwaiter.Invoke(task, null);
                if (awaiter == null) return;
                var getResult = awaiter.GetType().GetMethod("GetResult");
                if (getResult == null) return;
                _iconTexture = getResult.Invoke(awaiter, null);
            }
            catch { /* ignore */ }
        }

        private static IntPtr GetTextureHandle(object wrap)
        {
            var prop = wrap.GetType().GetProperty("ImGuiHandle");
            if (prop == null) prop = wrap.GetType().GetProperty("Handle");
            return prop?.GetValue(wrap) is IntPtr p ? p : IntPtr.Zero;
        }

        private static System.Numerics.Vector2 GetTextureSize(object wrap)
        {
            var w = wrap.GetType().GetProperty("Width")?.GetValue(wrap) is int wv ? wv : 0;
            var h = wrap.GetType().GetProperty("Height")?.GetValue(wrap) is int hv ? hv : 0;
            return new System.Numerics.Vector2(w, h);
        }

        private static void OpenUrl(string url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.IsAbsoluteUri)
                return;
            try
            {
                using var _ = Process.Start(new ProcessStartInfo { FileName = uri.ToString(), UseShellExecute = true });
            }
            catch
            {
                /* ignore */
            }
        }
    }
}
