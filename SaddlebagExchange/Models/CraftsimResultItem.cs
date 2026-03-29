using System.Text.Json.Serialization;

namespace SaddlebagExchange.Models
{
    public sealed class CraftsimCostEstimate
    {
        [JsonPropertyName("material_min_listing_cost")]
        public long MaterialMinListingCost { get; set; }

        [JsonPropertyName("material_avg_cost")]
        public long MaterialAvgCost { get; set; }

        [JsonPropertyName("material_median_cost")]
        public long MaterialMedianCost { get; set; }
    }

    public sealed class CraftsimRevenueEstimate
    {
        [JsonPropertyName("revenue_home_min_listing")]
        public long RevenueHomeMinListing { get; set; }

        [JsonPropertyName("revenue_region_min_listing")]
        public long RevenueRegionMinListing { get; set; }

        [JsonPropertyName("revenue_avg")]
        public long RevenueAvg { get; set; }

        [JsonPropertyName("revenue_median")]
        public long RevenueMedian { get; set; }
    }

    /// <summary>
    /// One item from POST /api/v2/craftsim response.
    /// </summary>
    public sealed class CraftsimResultItem
    {
        [JsonPropertyName("itemID")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public int ItemId { get; set; }

        [JsonPropertyName("itemName")]
        public string? ItemName { get; set; }

        [JsonPropertyName("yieldsPerCraft")]
        public int YieldsPerCraft { get; set; }

        [JsonPropertyName("itemData")]
        public string? SaddlebagUrl { get; set; }

        [JsonPropertyName("universalisLink")]
        public string? UniversalisUrl { get; set; }

        [JsonPropertyName("costEst")]
        public CraftsimCostEstimate CostEstimate { get; set; } = new();

        [JsonPropertyName("revenueEst")]
        public CraftsimRevenueEstimate RevenueEstimate { get; set; } = new();

        [JsonPropertyName("hq")]
        public bool Hq { get; set; }

        [JsonPropertyName("soldPerWeek")]
        public int SoldPerWeek { get; set; }

        [JsonPropertyName("profitEst")]
        public long ProfitEstimate { get; set; }
    }
}
