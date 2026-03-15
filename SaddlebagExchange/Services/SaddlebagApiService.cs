using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Http;
using SaddlebagExchange.Models;

namespace SaddlebagExchange.Services
{
    /// <summary>
    /// Client for Saddlebag Exchange API (https://api.saddlebagexchange.com).
    /// Uses IHttpClientFactory for DNS refresh and proper lifetime (reviewer requirement).
    /// </summary>
    public sealed class SaddlebagApiService : IDisposable
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
        };

        private const string ClientName = "SaddlebagExchange";
        private readonly IHttpClientFactory _factory;

        public SaddlebagApiService(IHttpClientFactory factory)
        {
            _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        }

        /// <summary>
        /// POST /api/scan — Reselling search. Returns items to buy on other servers/vendors and sell on home server.
        /// </summary>
        public async Task<List<ResellingResultItem>> ScanAsync(ResellingParams request, CancellationToken cancel = default)
        {
            var client = _factory.CreateClient(ClientName);
            using var content = JsonContent.Create(request);
            using var response = await client.PostAsync("/api/scan", content, cancel).ConfigureAwait(false);
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
            var client = _factory.CreateClient(ClientName);
            using var content = JsonContent.Create(request);
            using var response = await client.PostAsync("/api/ffxivmarketshare", content, cancel).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync(cancel).ConfigureAwait(false);
            var wrapper = JsonSerializer.Deserialize<MarketshareScanResponse>(json, JsonOptions);
            return wrapper?.Data ?? new List<MarketshareResultItem>();
        }

        /// <summary>No-op: client is owned by IHttpClientFactory.</summary>
        public void Dispose() { }
    }
}
