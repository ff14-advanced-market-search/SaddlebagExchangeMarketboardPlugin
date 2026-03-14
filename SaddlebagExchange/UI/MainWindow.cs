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

        public MainWindow(IDalamudPluginInterface? pluginInterface = null)
        {
            _pluginInterface = pluginInterface;
        }

        public void SetDefaultHomeServer(string? homeServer)
        {
            _resellingSearch.SetDefaultHomeServer(homeServer);
            _marketshareTab.SetDefaultHomeServer(homeServer);
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
                    _homeTab.Draw(_pluginInterface);
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
