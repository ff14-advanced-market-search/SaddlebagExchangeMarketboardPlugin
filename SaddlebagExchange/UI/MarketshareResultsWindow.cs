using Dalamud.Interface.Windowing;

namespace SaddlebagExchange.UI
{
    public sealed class MarketshareResultsWindow : Window
    {
        private readonly MarketshareTab _tab;

        public MarketshareResultsWindow(MarketshareTab tab)
            : base("Saddlebag Exchange - Market Overview Results")
        {
            _tab = tab;
        }

        public override void Draw()
        {
            _tab.DrawResultsContent();
        }
    }
}
