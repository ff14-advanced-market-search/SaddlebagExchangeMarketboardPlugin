using System;
using System.Text.Json.Serialization;

namespace SaddlebagExchange.Models
{
    /// <summary>
    /// Request body for POST /api/ffxivmarketshare (FFXIV Market Overview).
    /// See https://docs.saddlebagexchange.com/openapi.json
    /// </summary>
    public sealed class MarketshareParams
    {
        [JsonPropertyName("server")]
        public string Server { get; set; } = string.Empty;

        [JsonPropertyName("time_period")]
        public int TimePeriod { get; set; }

        [JsonPropertyName("sales_amount")]
        public int SalesAmount { get; set; }

        [JsonPropertyName("average_price")]
        public int AveragePrice { get; set; }

        [JsonPropertyName("filters")]
        public int[] Filters { get; set; } = Array.Empty<int>();

        [JsonPropertyName("sort_by")]
        public string SortBy { get; set; } = "marketValue";
    }
}
