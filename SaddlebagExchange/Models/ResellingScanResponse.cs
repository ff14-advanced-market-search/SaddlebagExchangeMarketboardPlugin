using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SaddlebagExchange.Models
{
    /// <summary>
    /// API response for POST /api/scan — root object with "data" array.
    /// </summary>
    public sealed class ResellingScanResponse
    {
        [JsonPropertyName("data")]
        public List<ResellingResultItem> Data { get; set; } = new();
    }
}
