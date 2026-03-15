using Dalamud.Interface.Windowing;

namespace SaddlebagExchange.UI
{
    public sealed class ResellingResultsWindow : Window
    {
        private readonly ResellingSearchTab _tab;

        public ResellingResultsWindow(ResellingSearchTab tab)
            : base("Saddlebag Exchange - Results")
        {
            _tab = tab;
        }

        public override void Draw()
        {
            _tab.DrawResultsContent();
        }
    }
}
