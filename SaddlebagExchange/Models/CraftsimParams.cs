using System.Text.Json.Serialization;

namespace SaddlebagExchange.Models
{
    /// <summary>
    /// Request body for POST /api/v2/craftsim (FFXIV Crafting Profit Calculator).
    /// See https://docs.saddlebagexchange.com/openapi.json
    /// </summary>
    public sealed class CraftsimParams
    {
        [JsonPropertyName("home_server")]
        public string HomeServer { get; set; } = string.Empty;

        [JsonPropertyName("cost_metric")]
        public string CostMetric { get; set; } = "material_median_cost";

        [JsonPropertyName("revenue_metric")]
        public string RevenueMetric { get; set; } = "revenue_home_min_listing";

        [JsonPropertyName("sales_per_week")]
        public int SalesPerWeek { get; set; }

        [JsonPropertyName("median_sale_price")]
        public int MedianSalePrice { get; set; }

        [JsonPropertyName("max_material_cost")]
        public int MaxMaterialCost { get; set; }

        [JsonPropertyName("jobs")]
        public int[] Jobs { get; set; } = new[] { 0 };

        [JsonPropertyName("filters")]
        public int[] Filters { get; set; } = new[] { 0, -5 };

        [JsonPropertyName("stars")]
        public int Stars { get; set; } = -1;

        [JsonPropertyName("lvl_lower_limit")]
        public int LvlLowerLimit { get; set; } = -1;

        [JsonPropertyName("lvl_upper_limit")]
        public int LvlUpperLimit { get; set; } = 1000;

        [JsonPropertyName("yields")]
        public int Yields { get; set; } = -1;

        [JsonPropertyName("hide_expert_recipes")]
        public bool HideExpertRecipes { get; set; } = true;
    }
}
