using System.Text.Json.Serialization;

namespace SaddlebagExchange.Models
{
    /// <summary>
    /// One row from POST /api/scan response. Property names match common API response conventions.
    /// </summary>
    public sealed class ResellingResultItem
    {
        [JsonPropertyName("item_id")]
        public int ItemId { get; set; }

        [JsonPropertyName("item_name")]
        public string? ItemName { get; set; }

        [JsonPropertyName("buy_server")]
        public string? BuyServer { get; set; }

        [JsonPropertyName("buy_price")]
        public long BuyPrice { get; set; }

        [JsonPropertyName("sell_price")]
        public long SellPrice { get; set; }

        [JsonPropertyName("profit")]
        public long Profit { get; set; }

        [JsonPropertyName("roi")]
        public double Roi { get; set; }

        [JsonPropertyName("stack_size")]
        public int StackSize { get; set; }

        [JsonPropertyName("sales_per_week")]
        public int? SalesPerWeek { get; set; }

        [JsonPropertyName("home_server")]
        public string? HomeServer { get; set; }
    }
}
