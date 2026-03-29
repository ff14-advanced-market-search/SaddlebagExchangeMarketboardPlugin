using Dalamud.Interface.Windowing;

namespace SaddlebagExchange.UI
{
    public sealed class CraftsimResultsWindow : Window
    {
        private readonly CraftsimTab _tab;

        public CraftsimResultsWindow(CraftsimTab tab)
            : base("Saddlebag Exchange - Craftsim Results")
        {
            _tab = tab;
        }

        public override void Draw()
        {
            _tab.DrawResultsContent();
        }
    }
}
