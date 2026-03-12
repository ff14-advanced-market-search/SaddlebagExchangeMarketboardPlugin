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

        [JsonPropertyName("profit_raw_percent")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double ProfitPercent { get; set; }

        [JsonPropertyName("ROI")]
        public double Roi { get; set; }

        [JsonPropertyName("stack_size")]
        public int StackSize { get; set; }

        [JsonPropertyName("regionWeeklySalesAmountNQ")]
        public int? SalesPerWeek { get; set; }

        [JsonPropertyName("home_server_price")]
        public long HomeServerPrice { get; set; }

        [JsonPropertyName("home_update_time")]
        public string? HomeUpdateTime { get; set; }

        [JsonPropertyName("update_time")]
        public string? UpdateTime { get; set; }

        [JsonPropertyName("url")]
        public string? UniversalisUrl { get; set; }

        [JsonPropertyName("npc_vendor_info")]
        public string? NpcVendorInfo { get; set; }

        [JsonPropertyName("sale_rates")]
        [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]
        public double SaleRates { get; set; }

        [JsonPropertyName("regionWeeklyMedianNQ")]
        public long RegionWeeklyMedianNQ { get; set; }

        [JsonPropertyName("regionWeeklyAverageNQ")]
        public long RegionWeeklyAverageNQ { get; set; }

        [JsonPropertyName("regionWeeklyQuantitySoldNQ")]
        public int RegionWeeklyQuantitySoldNQ { get; set; }

        [JsonPropertyName("regionWeeklyMedianHQ")]
        public long RegionWeeklyMedianHQ { get; set; }

        [JsonPropertyName("regionWeeklyAverageHQ")]
        public long RegionWeeklyAverageHQ { get; set; }

        [JsonPropertyName("regionWeeklySalesAmountHQ")]
        public int RegionWeeklySalesAmountHQ { get; set; }

        [JsonPropertyName("regionWeeklyQuantitySoldHQ")]
        public int RegionWeeklyQuantitySoldHQ { get; set; }

        public const string SaddlebagBaseUrl = "https://saddlebagexchange.com/queries/item-data/";
        public string SaddlebagUrl => SaddlebagBaseUrl + ItemId;
    }
}
