using Dalamud.Interface.Windowing;

namespace SaddlebagExchange.UI
{
    public sealed class ShoppingListResultsWindow : Window
    {
        private readonly ShoppingListTab _tab;

        public ShoppingListResultsWindow(ShoppingListTab tab)
            : base("Saddlebag Exchange - Shopping List Results")
        {
            _tab = tab;
        }

        public override void Draw()
        {
            _tab.DrawResultsContent();
        }
    }
}
