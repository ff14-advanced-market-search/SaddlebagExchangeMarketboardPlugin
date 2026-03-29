using System.Text.Json.Serialization;

namespace SaddlebagExchange.Models
{
    public sealed class ShoppingInputItem
    {
        [JsonPropertyName("itemID")]
        public int ItemId { get; set; }

        [JsonPropertyName("craft_amount")]
        public int CraftAmount { get; set; }

        [JsonPropertyName("hq")]
        public bool Hq { get; set; }

        [JsonPropertyName("job")]
        public int Job { get; set; }
    }

    /// <summary>
    /// Request body for POST /api/v2/shoppinglist.
    /// </summary>
    public sealed class ShoppingListParams
    {
        [JsonPropertyName("home_server")]
        public string HomeServer { get; set; } = string.Empty;

        [JsonPropertyName("shopping_list")]
        public ShoppingInputItem[] ShoppingList { get; set; } = [];

        [JsonPropertyName("region_wide")]
        public bool RegionWide { get; set; }
    }
}
