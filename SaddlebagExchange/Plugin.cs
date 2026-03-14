using System;
using Dalamud.Plugin;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Dalamud.Bindings.ImGui;
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
        private bool _commandsRegistered;
        private readonly Action _onOpenMainUi;
        private readonly Action _onOpenConfigUi;

        public Plugin(IDalamudPluginInterface pluginInterface)
        {
            _pi = pluginInterface;
            // Only Settings button opens the UI; Open button in /xlplugins intentionally does nothing.
            _onOpenMainUi = () => { };
            _onOpenConfigUi = () => _windowOpen = true;

            var uiBuilder = pluginInterface.UiBuilder;
            uiBuilder.Draw += Draw;
            uiBuilder.OpenMainUi += _onOpenMainUi;
            uiBuilder.OpenConfigUi += _onOpenConfigUi;

            TryRegisterCommands();
            TrySetDefaultHomeServer(pluginInterface);
        }

        private void TryRegisterCommands()
        {
            if (_pi == null || _commandsRegistered) return;
            _cmd = _pi.GetService(typeof(ICommandManager)) as ICommandManager;
            if (_cmd == null) return;
            var help = "Open Saddlebag Exchange window";
            try
            {
                _cmd.AddHandler("/saddlebag", new CommandInfo(OnCommand) { HelpMessage = help });
                _cmd.AddHandler("/saddlebagexchange", new CommandInfo(OnCommand) { HelpMessage = help });
                _commandsRegistered = true;
            }
            catch { /* ignore if already registered or failed */ }
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
            if (!_commandsRegistered)
                TryRegisterCommands();

            if (!_windowOpen)
                return;

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
            _cmd?.RemoveHandler("/saddlebagexchange");
            _cmd = null;
            _pi = null;
        }
    }
}
