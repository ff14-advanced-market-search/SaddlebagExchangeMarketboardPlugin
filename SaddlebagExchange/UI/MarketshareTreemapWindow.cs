using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;

namespace SaddlebagExchange.UI
{
    public sealed class MarketshareTreemapWindow : Window
    {
        private readonly MarketshareTab _tab;

        public MarketshareTreemapWindow(MarketshareTab tab)
            : base("Market Overview - Treemap")
        {
            _tab = tab;
            Size = new Vector2(720, 520);
            SizeCondition = ImGuiCond.FirstUseEver;
        }

        public override void Draw()
        {
            _tab.DrawTreemapContent();
        }
    }
}
