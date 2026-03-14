using System;
using System.IO;
using System.Reflection;
using Dalamud.Bindings.ImGui;
using Dalamud.Plugin;

namespace SaddlebagExchange.UI
{
    public sealed class MainWindow
    {
        private int _selectedToolIndex;
        private readonly HomeTab _homeTab = new();
        private readonly ResellingSearchTab _resellingSearch = new();
        private readonly MarketshareTab _marketshareTab = new();
        private readonly IDalamudPluginInterface? _pluginInterface;
        private string _defaultHomeServer = string.Empty;

        public MainWindow(IDalamudPluginInterface? pluginInterface = null)
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

        private static string GetConfigFilePath()
        {
            var dir = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
            return Path.Combine(dir ?? ".", "default_home_server.txt");
        }

        private void LoadDefaultHomeServer()
        {
            try
            {
                var path = GetConfigFilePath();
                if (!File.Exists(path)) return;
                var s = File.ReadAllText(path).Trim();
                if (string.IsNullOrEmpty(s)) return;
                _defaultHomeServer = s;
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

        public void Draw()
        {
            ImGui.BeginChild("##tools_list", new System.Numerics.Vector2(180, -1), true);
            if (ImGui.Selectable("Home", _selectedToolIndex == 0))
                _selectedToolIndex = 0;
            if (ImGui.Selectable("Reselling Search", _selectedToolIndex == 1))
                _selectedToolIndex = 1;
            if (ImGui.Selectable("Market Overview", _selectedToolIndex == 2))
                _selectedToolIndex = 2;
            ImGui.EndChild();

            ImGui.SameLine();
            ImGui.BeginChild("##tool_content", new System.Numerics.Vector2(-1, -1), true);

            switch (_selectedToolIndex)
            {
                case 0:
                    _homeTab.Draw(_pluginInterface, GetDefaultHomeServer, SetDefaultHomeServer);
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

            ImGui.EndChild();
        }
    }
}
