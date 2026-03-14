using System;
using Dalamud.Plugin;
using Dalamud.Game.Command;
using Dalamud.Plugin.Services;
using Dalamud.Interface.Windowing;
using SaddlebagExchange.UI;

namespace SaddlebagExchange
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "Saddlebag Exchange";

        private readonly WindowSystem _windowSystem;
        private readonly MainWindow _mainWindow;
        private readonly Action _onDraw;
        private readonly Action _onOpenUi;
        private IDalamudPluginInterface? _pi;
        private ICommandManager _cmd;

        public Plugin(IDalamudPluginInterface pluginInterface, ICommandManager commandManager)
        {
            _pi = pluginInterface;
            _cmd = commandManager;
            _windowSystem = new WindowSystem(Name);
            _mainWindow = new MainWindow(pluginInterface);
            _windowSystem.AddWindow(_mainWindow);
            _onDraw = () => _windowSystem.Draw();
            _onOpenUi = () => _mainWindow.Toggle();

            var uiBuilder = pluginInterface.UiBuilder;
            uiBuilder.Draw += _onDraw;
            uiBuilder.OpenMainUi += _onOpenUi;
            uiBuilder.OpenConfigUi += _onOpenUi;

            var help = "Open Saddlebag Exchange window";
            try
            {
                _cmd.AddHandler("/saddlebag", new CommandInfo(OnCommand) { HelpMessage = help });
                _cmd.AddHandler("/saddlebagexchange", new CommandInfo(OnCommand) { HelpMessage = help });
                _cmd.AddHandler("/sbex", new CommandInfo(OnCommand) { HelpMessage = help });
            }
            catch { /* ignore if already registered or failed */ }

            TrySetDefaultHomeServer(pluginInterface);
        }

        private void TrySetDefaultHomeServer(IDalamudPluginInterface pi)
        {
            // Optional: set default home server from current world via pi.GetService(IClientState).
            // User can enter home server manually in Reselling Search.
        }

        private void OnCommand(string command, string args)
        {
            _mainWindow.Toggle();
        }

        public void Dispose()
        {
            if (_pi == null) return;
            _mainWindow.Dispose();
            _windowSystem.RemoveAllWindows();
            var uiBuilder = _pi.UiBuilder;
            uiBuilder.Draw -= _onDraw;
            uiBuilder.OpenMainUi -= _onOpenUi;
            uiBuilder.OpenConfigUi -= _onOpenUi;
            _cmd.RemoveHandler("/saddlebag");
            _cmd.RemoveHandler("/saddlebagexchange");
            _cmd.RemoveHandler("/sbex");
            _pi = null;
        }
    }
}
