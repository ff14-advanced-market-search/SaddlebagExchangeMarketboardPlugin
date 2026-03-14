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
        private const string GuidesUrl = "https://github.com/ff14-advanced-market-search/saddlebag-with-pockets/wiki";
        private const string PatreonUrl = "https://www.patreon.com/indopan";
        private const string DiscordUrl = "https://discord.gg/9dHx2rEq9F";
        private const string WebsiteUrl = "https://saddlebagexchange.com/wow";

        private object? _iconTexture;
        private bool _iconLoadAttempted;

        public void Draw(IDalamudPluginInterface? pluginInterface)
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
