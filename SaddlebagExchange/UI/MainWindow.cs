using System;
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
        private readonly ResellingSearchTab _resellingSearch;
        private readonly MarketshareTab _marketshareTab;
        private readonly CraftsimTab _craftsimTab;
        private readonly IDalamudPluginInterface? _pluginInterface;
        private readonly Configuration _config;
        private readonly Action? _onSaveConfig;

        public MainWindow(IDalamudPluginInterface? pluginInterface, Configuration config, Action? onSaveConfig)
            : base("Saddlebag Exchange")
        {
            _pluginInterface = pluginInterface;
            _config = config;
            _onSaveConfig = onSaveConfig;
            _resellingSearch = new ResellingSearchTab();
            _marketshareTab = new MarketshareTab();
            _craftsimTab = new CraftsimTab();
            if (!string.IsNullOrEmpty(_config.DefaultHomeServer))
            {
                _resellingSearch.SetDefaultHomeServer(_config.DefaultHomeServer);
                _marketshareTab.SetDefaultHomeServer(_config.DefaultHomeServer);
                _craftsimTab.SetDefaultHomeServer(_config.DefaultHomeServer);
            }
        }

        public string GetDefaultHomeServer() => _config.DefaultHomeServer ?? string.Empty;

        public void SetDefaultHomeServer(string? homeServer)
        {
            var value = (homeServer ?? string.Empty).Trim();
            _config.DefaultHomeServer = value;
            _onSaveConfig?.Invoke();
            _resellingSearch.SetDefaultHomeServer(string.IsNullOrEmpty(value) ? null : value);
            _marketshareTab.SetDefaultHomeServer(string.IsNullOrEmpty(value) ? null : value);
            _craftsimTab.SetDefaultHomeServer(string.IsNullOrEmpty(value) ? null : value);
        }

        public ResellingSearchTab GetResellingTab() => _resellingSearch;
        public MarketshareTab GetMarketshareTab() => _marketshareTab;
        public CraftsimTab GetCraftsimTab() => _craftsimTab;

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
                    if (ImGui.Selectable("Craftsim", _selectedToolIndex == 3))
                        _selectedToolIndex = 3;
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
                        case 3:
                            _craftsimTab.Draw();
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
            _craftsimTab.Dispose();
        }
    }
}
