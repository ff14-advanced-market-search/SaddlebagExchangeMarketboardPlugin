using System.Text.Json.Serialization;

namespace SaddlebagExchange.Models
{
    /// <summary>
    /// One item from POST /api/ffxivmarketshare response. Property names match API (itemID, name, etc.).
    /// </summary>
    public sealed class MarketshareResultItem
    {
        [JsonPropertyName("itemID")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int ItemId { get; set; }

        [JsonPropertyName("name")]
        public string? ItemName { get; set; }

        [JsonPropertyName("marketValue")]
        public long MarketValue { get; set; }

        [JsonPropertyName("avg")]
        public long Avg { get; set; }

        [JsonPropertyName("median")]
        public long Median { get; set; }

        [JsonPropertyName("purchaseAmount")]
        public int PurchaseAmount { get; set; }

        [JsonPropertyName("quantitySold")]
        public int QuantitySold { get; set; }

        [JsonPropertyName("percentChange")]
        public double PercentChange { get; set; }

        public const string SaddlebagBaseUrl = "https://saddlebagexchange.com/queries/item-data/";
        public string SaddlebagUrl => SaddlebagBaseUrl + ItemId;
    }
}
