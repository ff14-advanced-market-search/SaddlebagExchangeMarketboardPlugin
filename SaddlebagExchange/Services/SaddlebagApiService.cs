using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SaddlebagExchange.Models;

namespace SaddlebagExchange.Services
{
    /// <summary>
    /// Client for Saddlebag Exchange API (https://api.saddlebagexchange.com).
    /// Wiki notes HTTP may be required due to Cloudflare: http://api.saddlebagexchange.com
    /// </summary>
    public sealed class SaddlebagApiService
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        // Wiki: HTTP may be required (Cloudflare). Try HTTPS first.
        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("https://api.saddlebagexchange.com"),
            DefaultRequestHeaders = { { "Accept", "application/json" } },
            Timeout = TimeSpan.FromSeconds(30)
        };

        /// <summary>
        /// POST /api/scan — Reselling search. Returns items to buy on other servers/vendors and sell on home server.
        /// </summary>
        public async Task<List<ResellingResultItem>> ScanAsync(ResellingParams request, CancellationToken cancel = default)
        {
            using var content = JsonContent.Create(request);
            using var response = await _http.PostAsync("/api/scan", content, cancel).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
            var wrapper = JsonSerializer.Deserialize<ResellingScanResponse>(json, JsonOptions);
            return wrapper?.Data ?? new List<ResellingResultItem>();
        }
    }
}
