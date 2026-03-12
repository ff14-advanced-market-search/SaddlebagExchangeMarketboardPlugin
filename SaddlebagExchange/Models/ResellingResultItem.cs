using System.Text.Json.Serialization;

namespace SaddlebagExchange.Models
{
    /// <summary>
    /// One item from POST /api/scan response "data" array. Property names match API exactly.
    /// </summary>
    public sealed class ResellingResultItem
    {
        [JsonPropertyName("item_id")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int ItemId { get; set; }

        [JsonPropertyName("real_name")]
        public string? ItemName { get; set; }

        [JsonPropertyName("server")]
        public string? BuyServer { get; set; }

        [JsonPropertyName("ppu")]
        public long BuyPrice { get; set; }

        [JsonPropertyName("avg_ppu")]
        public long SellPrice { get; set; }

        [JsonPropertyName("profit_amount")]
        public long Profit { get; set; }

        [JsonPropertyName("ROI")]
        public double Roi { get; set; }

        [JsonPropertyName("stack_size")]
        public int StackSize { get; set; }

        [JsonPropertyName("regionWeeklySalesAmountNQ")]
        public int? SalesPerWeek { get; set; }

        [JsonPropertyName("home_server_price")]
        public long HomeServerPrice { get; set; }
    }
}
