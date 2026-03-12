using System;
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
        private IDalamudPluginInterface? _pi;
        private ICommandManager? _cmd;
        private readonly Action _onOpenMainUi;
        private readonly Action _onOpenConfigUi;

        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            _pi = pluginInterface;
            _onOpenMainUi = () => _windowOpen = true;
            _onOpenConfigUi = () => _windowOpen = true; // no separate config; open main window

            var uiBuilder = pluginInterface.UiBuilder;
            uiBuilder.Draw += Draw;
            uiBuilder.OpenMainUi += _onOpenMainUi;
            uiBuilder.OpenConfigUi += _onOpenConfigUi;

            _cmd = pluginInterface.GetService(typeof(ICommandManager)) as ICommandManager;
            _cmd?.AddHandler("/saddlebag", new CommandInfo(OnCommand)
            {
                HelpMessage = "Open Saddlebag Exchange window"
            });

            TrySetDefaultHomeServer(pluginInterface);
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

            // Avoid SetNextWindowSize: plugin was crashing in cimgui.dll (igSetNextWindowSize) when using
            // a different ImGui/cimgui than the host. Window size can be resized by user.
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
            if (_pi == null) return;
            var uiBuilder = _pi.UiBuilder;
            uiBuilder.Draw -= Draw;
            uiBuilder.OpenMainUi -= _onOpenMainUi;
            uiBuilder.OpenConfigUi -= _onOpenConfigUi;
            _cmd?.RemoveHandler("/saddlebag");
            _cmd = null;
            _pi = null;
        }
    }
}
