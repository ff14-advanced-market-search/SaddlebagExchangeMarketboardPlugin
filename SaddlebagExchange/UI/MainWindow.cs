using System;
using System.IO;
using System.Reflection;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin;

namespace SaddlebagExchange.UI
{
    public sealed class MainWindow : Window, IDisposable
    {
        private int _selectedToolIndex;
        private readonly HomeTab _homeTab = new();
        private readonly ResellingSearchTab _resellingSearch = new();
        private readonly MarketshareTab _marketshareTab = new();
        private readonly IDalamudPluginInterface? _pluginInterface;
        private string _defaultHomeServer = string.Empty;

        public MainWindow(IDalamudPluginInterface? pluginInterface)
            : base("Saddlebag Exchange")
        {
            _pluginInterface = pluginInterface;
            LoadDefaultHomeServer();
        }

        public string GetDefaultHomeServer() => _defaultHomeServer;

        public void SetDefaultHomeServer(string? homeServer)
        {
            _defaultHomeServer = (homeServer ?? string.Empty).Trim();
            SaveDefaultHomeServer();
            _resellingSearch.SetDefaultHomeServer(string.IsNullOrEmpty(_defaultHomeServer) ? null : _defaultHomeServer);
            _marketshareTab.SetDefaultHomeServer(string.IsNullOrEmpty(_defaultHomeServer) ? null : _defaultHomeServer);
        }

        private string GetConfigFilePath()
        {
            if (_pluginInterface != null)
            {
                try
                {
                    var configDir = _pluginInterface.GetPluginConfigDirectory();
                    if (!string.IsNullOrEmpty(configDir))
                        return Path.Combine(configDir, "default_home_server.txt");
                }
                catch { /* fallback */ }
            }
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(dir ?? ".", "default_home_server.txt");
        }

        private static string GetLegacyConfigFilePath()
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(dir ?? ".", "default_home_server.txt");
        }

        private void LoadDefaultHomeServer()
        {
            try
            {
                var path = GetConfigFilePath();
                if (!File.Exists(path))
                {
                    var legacyPath = GetLegacyConfigFilePath();
                    if (legacyPath != path && File.Exists(legacyPath))
                    {
                        var s = File.ReadAllText(legacyPath).Trim();
                        if (!string.IsNullOrEmpty(s))
                        {
                            _defaultHomeServer = s;
                            _resellingSearch.SetDefaultHomeServer(_defaultHomeServer);
                            _marketshareTab.SetDefaultHomeServer(_defaultHomeServer);
                            SaveDefaultHomeServer();
                        }
                    }
                    return;
                }
                var content = File.ReadAllText(path).Trim();
                if (string.IsNullOrEmpty(content)) return;
                _defaultHomeServer = content;
                _resellingSearch.SetDefaultHomeServer(_defaultHomeServer);
                _marketshareTab.SetDefaultHomeServer(_defaultHomeServer);
            }
            catch { /* ignore */ }
        }

        private void SaveDefaultHomeServer()
        {
            try
            {
                var path = GetConfigFilePath();
                if (string.IsNullOrEmpty(_defaultHomeServer))
                {
                    if (File.Exists(path))
                        File.Delete(path);
                    return;
                }
                File.WriteAllText(path, _defaultHomeServer);
            }
            catch { /* ignore */ }
        }

        public override void Draw()
        {
            float toolsListWidth = 180f * ImGuiHelpers.GlobalScale;
            using (var child = ImRaii.Child("##tools_list", new Vector2(toolsListWidth, -1), true))
            {
                if (child.Success)
                {
                    if (ImGui.Selectable("Home", _selectedToolIndex == 0))
                        _selectedToolIndex = 0;
                    if (ImGui.Selectable("Reselling Search", _selectedToolIndex == 1))
                        _selectedToolIndex = 1;
                    if (ImGui.Selectable("Market Overview", _selectedToolIndex == 2))
                        _selectedToolIndex = 2;
                }
            }

            ImGui.SameLine();
            using (var child = ImRaii.Child("##tool_content", new Vector2(-1, -1), true))
            {
                if (child.Success)
                {
                    switch (_selectedToolIndex)
                    {
                        case 0:
                            _homeTab.Draw(_pluginInterface, GetDefaultHomeServer, SetDefaultHomeServer, (tabIndex) => _selectedToolIndex = tabIndex);
                            break;
                        case 1:
                            _resellingSearch.Draw();
                            break;
                        case 2:
                            _marketshareTab.Draw();
                            break;
                        default:
                            ImGui.Text("Select a tool from the list.");
                            break;
                    }
                }
            }
        }

        public void Dispose()
        {
            _resellingSearch.Dispose();
            _marketshareTab.Dispose();
        }
    }
}
