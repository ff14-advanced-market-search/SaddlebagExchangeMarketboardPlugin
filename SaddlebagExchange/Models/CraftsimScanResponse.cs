using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SaddlebagExchange.Models
{
    /// <summary>
    /// API response for POST /api/v2/craftsim.
    /// </summary>
    public sealed class CraftsimScanResponse
    {
        [JsonPropertyName("data")]
        public List<CraftsimResultItem> Data { get; set; } = new();
    }
}
