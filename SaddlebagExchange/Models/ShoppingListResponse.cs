using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SaddlebagExchange.Models
{
    public sealed class ShoppingListResultItem
    {
        [JsonPropertyName("hq")]
        public bool Hq { get; set; }

        [JsonPropertyName("itemID")]
        public int ItemId { get; set; }

        [JsonPropertyName("name")]
        public string? Name { get; set; }

        [JsonPropertyName("pricePerUnit")]
        public long PricePerUnit { get; set; }

        [JsonPropertyName("quantity")]
        public int Quantity { get; set; }

        [JsonPropertyName("worldName")]
        public string? WorldName { get; set; }
    }

    /// <summary>
    /// Response for POST /api/v2/shoppinglist.
    /// </summary>
    public sealed class ShoppingListResponse
    {
        [JsonPropertyName("average_cost_per_craft")]
        public long AverageCostPerCraft { get; set; }

        [JsonPropertyName("total_cost")]
        public long TotalCost { get; set; }

        [JsonPropertyName("data")]
        public List<ShoppingListResultItem> Data { get; set; } = new();
    }
}
