using System;
using System.Text.Json.Serialization;

namespace SaddlebagExchange.Models
{
    /// <summary>
    /// Request body for POST /api/scan (FFXIV Reselling Search).
    /// See https://docs.saddlebagexchange.com/openapi.json
    /// </summary>
    public sealed class ResellingParams
    {
        [JsonPropertyName("preferred_roi")]
        public int PreferredRoi { get; set; }

        [JsonPropertyName("min_profit_amount")]
        public int MinProfitAmount { get; set; }

        [JsonPropertyName("min_desired_avg_ppu")]
        public int MinDesiredAvgPpu { get; set; }

        [JsonPropertyName("min_stack_size")]
        public int MinStackSize { get; set; }

        [JsonPropertyName("hours_ago")]
        public int HoursAgo { get; set; }

        [JsonPropertyName("min_sales")]
        public int MinSales { get; set; }

        [JsonPropertyName("hq")]
        public bool Hq { get; set; }

        [JsonPropertyName("home_server")]
        public string HomeServer { get; set; } = string.Empty;

        [JsonPropertyName("filters")]
        public int[] Filters { get; set; } = Array.Empty<int>();

        [JsonPropertyName("region_wide")]
        public bool RegionWide { get; set; }

        [JsonPropertyName("include_vendor")]
        public bool IncludeVendor { get; set; }

        [JsonPropertyName("show_out_stock")]
        public bool ShowOutStock { get; set; }
    }
}
