using System;
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
    ///
    /// API Documentation:
    /// - Swagger docs: https://docs.saddlebagexchange.com/docs
    /// - ReDoc Reference: https://docs.saddlebagexchange.com/redoc
    /// </summary>
    public sealed class SaddlebagApiService : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        private readonly HttpClient _http = new()
        {
            BaseAddress = new Uri("https://api.saddlebagexchange.com"),
            DefaultRequestHeaders =
            {
                { "Accept", "application/json" },
                { "User-Agent", "DalamudPlugin-SaddlebagExchange" }
            },
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

        /// <summary>
        /// POST /api/ffxivmarketshare — Market overview. Returns best-selling items by revenue, sales, etc.
        /// </summary>
        public async Task<List<MarketshareResultItem>> MarketshareAsync(MarketshareParams request, CancellationToken cancel = default)
        {
            using var content = JsonContent.Create(request);
            using var response = await _http.PostAsync("/api/ffxivmarketshare", content, cancel).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
            var wrapper = JsonSerializer.Deserialize<MarketshareScanResponse>(json, JsonOptions);
            return wrapper?.Data ?? new List<MarketshareResultItem>();
        }

        /// <summary>
        /// POST /api/v2/craftsim - Crafting profit calculator.
        /// </summary>
        public async Task<List<CraftsimResultItem>> CraftsimAsync(CraftsimParams request, CancellationToken cancel = default)
        {
            using var content = JsonContent.Create(request);
            using var response = await _http.PostAsync("/api/v2/craftsim", content, cancel).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
            var wrapper = JsonSerializer.Deserialize<CraftsimScanResponse>(json, JsonOptions);
            return wrapper?.Data ?? new List<CraftsimResultItem>();
        }

        /// <summary>
        /// POST /api/v2/shoppinglist - Shopping list generator.
        /// </summary>
        public async Task<ShoppingListResponse> ShoppingListAsync(ShoppingListParams request, CancellationToken cancel = default)
        {
            using var content = JsonContent.Create(request);
            using var response = await _http.PostAsync("/api/v2/shoppinglist", content, cancel).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
            var wrapper = JsonSerializer.Deserialize<ShoppingListResponse>(json, JsonOptions);
            return wrapper ?? new ShoppingListResponse();
        }

        public void Dispose() => _http.Dispose();
    }
}
