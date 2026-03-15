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
        private readonly Configuration _config;
        private readonly IPluginLog _log;

        public Plugin(
            IDalamudPluginInterface pluginInterface,
            ICommandManager commandManager,
            IPluginLog log)
        {
            _pi = pluginInterface;
            _cmd = commandManager;
            _log = log;
            _config = pluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            _windowSystem = new WindowSystem(Name);
            _mainWindow = new MainWindow(pluginInterface, _config, () => _pi?.SavePluginConfig(_config));
            _windowSystem.AddWindow(_mainWindow);

            var resellingResultsWindow = new ResellingResultsWindow(_mainWindow.GetResellingTab());
            _mainWindow.GetResellingTab().SetResultsWindow(resellingResultsWindow);
            _windowSystem.AddWindow(resellingResultsWindow);

            var marketshareResultsWindow = new MarketshareResultsWindow(_mainWindow.GetMarketshareTab());
            var marketshareTreemapWindow = new MarketshareTreemapWindow(_mainWindow.GetMarketshareTab());
            _mainWindow.GetMarketshareTab().SetResultsWindow(marketshareResultsWindow);
            _mainWindow.GetMarketshareTab().SetTreemapWindow(marketshareTreemapWindow);
            _windowSystem.AddWindow(marketshareResultsWindow);
            _windowSystem.AddWindow(marketshareTreemapWindow);

            _onDraw = () => _windowSystem.Draw();
            _onOpenUi = () => _mainWindow.IsOpen = true;

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

            _log.Information("Saddlebag Exchange loaded.");
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
