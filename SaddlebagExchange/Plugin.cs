using Dalamud.Plugin;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using ImGuiNET;
using SaddlebagExchange.UI;

namespace SaddlebagExchange
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Saddlebag Exchange";

        private bool _windowOpen;
        private readonly MainWindow _mainWindow = new();

        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            TrySetDefaultHomeServer(pluginInterface);
            var cmd = pluginInterface.GetService(typeof(ICommandManager)) as ICommandManager;
            cmd?.AddHandler("/saddlebag", new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Saddlebag Exchange window"
            });
        }

        private void TrySetDefaultHomeServer(IDalamudPluginInterface pi)
        {
            // Optional: set default home server from current world via pi.GetService(IClientState).
            // User can enter home server manually in Reselling Search.
        }

        private void OnCommand(string command, string args)
        {
            _windowOpen = !_windowOpen;
        }

        public void Draw()
        {
            if (!_windowOpen)
                return;

            ImGui.SetNextWindowSize(new System.Numerics.Vector2(700, 500), ImGuiCond.FirstUseEver);
            if (!ImGui.Begin("Saddlebag Exchange", ref _windowOpen))
            {
                ImGui.End();
                return;
            }

            _mainWindow.Draw();
            ImGui.End();
        }

        public void Dispose()
        {
        }
    }
}
