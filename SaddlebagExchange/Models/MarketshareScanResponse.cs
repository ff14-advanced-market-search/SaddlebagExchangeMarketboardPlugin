using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SaddlebagExchange.Models
{
    /// <summary>
    /// API response for POST /api/ffxivmarketshare.
    /// </summary>
    public sealed class MarketshareScanResponse
    {
        [JsonPropertyName("data")]
        public List<MarketshareResultItem> Data { get; set; } = new();
    }
}
