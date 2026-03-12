using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using SaddlebagExchange.Models;
using SaddlebagExchange.Services;

namespace SaddlebagExchange.UI
{
    public sealed class ResellingSearchTab
    {
        private const int HomeServerBufferSize = 32;

        private readonly SaddlebagApiService _api = new();
        private readonly object _scanLock = new();
        private ResellingParams _params = GetDefaultParams();
        private List<ResellingResultItem> _scanResults = new();
        private bool _scanInProgress;
        private string _homeServerBuffer = string.Empty;
        private readonly byte[] _homeServerBytes = new byte[HomeServerBufferSize];
        private string _errorMessage = string.Empty;

        private static ResellingParams GetDefaultParams() => new()
        {
            PreferredRoi = 25,
            MinProfitAmount = 10000,
            MinDesiredAvgPpu = 10000,
            MinStackSize = 1,
            HoursAgo = 168,
            MinSales = 2,
            Hq = false,
            HomeServer = string.Empty,
            Filters = new[] { 0 },
            RegionWide = false,
            IncludeVendor = false,
            ShowOutStock = true
        };

        // Presets match frontend recommendedQueries (exact titles and params)
        private static readonly (string Label, ResellingParams Params)[] Presets =
        {
            ("Olivias Furnishing Items Medium Sell", P(168, 2, 25, 1, 75000, 30000, new[] { 56, 65, 66, 67, 68, 69, 70, 71, 72, 81, 82 }, false, true, true, true)),
            ("Olivias Consumable Collectables Medium Sell", P(168, 2, 25, 1, 75000, 30000, new[] { 75, 80, 90 }, false, true, true, true)),
            ("Fast Sales Search", P(168, 20, 25, 1, 500, 500, new[] { 0 }, false, false, false, true)),
            ("NPC Vendor Furniture Item Search", P(168, 2, 50, 1, 5000, 3000, new[] { -4 }, false, true, true, false)),
            ("Commodities Search", P(168, 2, 25, 2, 1000, 1000, new[] { 0 }, false, false, false, true)),
            ("Mega Value Search", P(336, 1, 25, 1, 1000000, 1000000, new[] { 0 }, false, true, true, true)),
            ("NPC Vendor Item Search", P(48, 5, 50, 1, 1000, 1000, new[] { -1 }, false, true, true, true)),
            ("Beginner Out of Stock Search", P(168, 2, 99, 1, 100, 100, new[] { 1, 2, 3, 4, 7 }, true, false, true, true)),
            ("Low Quality Out of Stock Search", P(168, 2, 99, 1, 100, 100, new[] { 7, 54 }, false, true, true, true)),
            ("Olivias General Flipping Quick Sell", P(48, 5, 25, 1, 5000, 5000, new[] { 0 }, false, true, true, true)),
            ("Olivias Class Quest Items Quick Sell", P(48, 2, 25, 1, 5000, 5000, new[] { -2, -3 }, false, true, true, true)),
            ("Olivias Furnishing Items Quick Sell", P(48, 5, 25, 1, 5000, 5000, new[] { 56, 65, 66, 67, 68, 69, 70, 71, 72, 81, 82 }, false, true, true, true)),
            ("Olivias Minions, Mounts, and Collectable Items Quick Sell", P(48, 5, 25, 1, 5000, 5000, new[] { 75, 80, 90 }, false, true, true, true)),
            ("Olivias Glamor Medium Sell", P(168, 2, 25, 1, 75000, 30000, new[] { 1, 2, -5 }, false, true, true, true)),
            ("Olivias High Investment Furniture Items", P(336, 1, 25, 1, 300000, 300000, new[] { 56, 65, 66, 67, 68, 69, 70, 71, 72, 81, 82 }, false, true, true, true)),
            ("Olivias High Investment Collectable Items", P(336, 1, 25, 1, 300000, 300000, new[] { 75, 80, 90 }, false, true, true, true)),
            ("Olivias High Value Glamor Items", P(336, 1, 25, 1, 300000, 300000, new[] { 1, 2, -5 }, false, true, true, true)),
            ("Olivias High Value Materials", P(336, 1, 25, 1, 300000, 300000, new[] { 6 }, false, true, true, true))
        };

        static ResellingParams P(int hours, int minSales, int roi, int minStack, int minProfit, int ppu, int[] filters, bool hq, bool includeVendor, bool showOutStock, bool regionWide) => new()
        {
            HoursAgo = hours,
            MinSales = minSales,
            PreferredRoi = roi,
            MinStackSize = minStack,
            MinProfitAmount = minProfit,
            MinDesiredAvgPpu = ppu,
            Filters = filters,
            Hq = hq,
            IncludeVendor = includeVendor,
            ShowOutStock = showOutStock,
            RegionWide = regionWide,
            HomeServer = string.Empty
        };

        public void SetDefaultHomeServer(string? homeServer)
        {
            if (string.IsNullOrEmpty(homeServer)) return;
            _params.HomeServer = homeServer;
            _homeServerBuffer = homeServer.PadRight(HomeServerBufferSize).Substring(0, HomeServerBufferSize);
        }

        public void Draw()
        {
            ImGui.Spacing();
            ImGui.Text("Reselling Search");
            ImGui.Separator();
            ImGui.Spacing();

            // --- Presets (match frontend recommendedQueries order) ---
            ImGui.Text("Presets");
            const int presetsPerLine = 2;
            for (int i = 0; i < Presets.Length; i++)
            {
                if (i > 0 && i % presetsPerLine != 0) ImGui.SameLine();
                if (ImGui.Button(Presets[i].Label))
                    ApplyPreset(Presets[i].Params);
            }
            ImGui.Spacing();

            // --- Search form ---
            ImGui.Text("Search");
            ImGui.Separator();

            int preferredRoi = _params.PreferredRoi;
            ImGui.InputInt("Preferred ROI %", ref preferredRoi, 5, 10);
            _params.PreferredRoi = preferredRoi;
            int minProfit = _params.MinProfitAmount;
            ImGui.InputInt("Min profit (gil)", ref minProfit, 1000, 5000);
            _params.MinProfitAmount = minProfit;
            int minPpu = _params.MinDesiredAvgPpu;
            ImGui.InputInt("Min avg price (gil)", ref minPpu, 1000, 5000);
            _params.MinDesiredAvgPpu = minPpu;
            int minStack = _params.MinStackSize;
            ImGui.InputInt("Min stack size", ref minStack, 1, 10);
            _params.MinStackSize = minStack;
            int hoursAgo = _params.HoursAgo;
            ImGui.InputInt("Hours of data", ref hoursAgo, 24, 168);
            _params.HoursAgo = hoursAgo;
            int minSales = _params.MinSales;
            ImGui.InputInt("Min sales", ref minSales, 1, 5);
            _params.MinSales = minSales;
            bool hq = _params.Hq;
            ImGui.Checkbox("HQ only", ref hq);
            _params.Hq = hq;
            bool regionWide = _params.RegionWide;
            ImGui.Checkbox("Region-wide", ref regionWide);
            _params.RegionWide = regionWide;
            bool includeVendor = _params.IncludeVendor;
            ImGui.Checkbox("Include vendor", ref includeVendor);
            _params.IncludeVendor = includeVendor;
            bool showOutStock = _params.ShowOutStock;
            ImGui.Checkbox("Show out of stock", ref showOutStock);
            _params.ShowOutStock = showOutStock;

            int len = Encoding.UTF8.GetBytes(_homeServerBuffer, 0, Math.Min(_homeServerBuffer.Length, HomeServerBufferSize - 1), _homeServerBytes, 0);
            _homeServerBytes[len] = 0;
            if (ImGui.InputText("Home server", _homeServerBytes, ImGuiInputTextFlags.None))
            {
                _homeServerBuffer = Encoding.UTF8.GetString(_homeServerBytes).TrimEnd('\0');
                _params.HomeServer = _homeServerBuffer.Trim();
            }

            ImGui.Spacing();
            bool doSearch = ImGui.Button("Search");
            ImGui.SameLine();
            if (!string.IsNullOrEmpty(_errorMessage))
                ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f), _errorMessage);

            if (doSearch)
                StartScan();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            // --- Results ---
            bool loading;
            List<ResellingResultItem> results;
            lock (_scanLock)
            {
                loading = _scanInProgress;
                results = _scanResults;
            }

            if (loading)
            {
                ImGui.Text("Searching...");
                return;
            }

            if (results == null || results.Count == 0)
            {
                ImGui.Text("Run a search to see results.");
                return;
            }

            DrawResultsTable(results);
        }

        private void ApplyPreset(ResellingParams p)
        {
            _params.PreferredRoi = p.PreferredRoi;
            _params.MinProfitAmount = p.MinProfitAmount;
            _params.MinDesiredAvgPpu = p.MinDesiredAvgPpu;
            _params.MinStackSize = p.MinStackSize;
            _params.HoursAgo = p.HoursAgo;
            _params.MinSales = p.MinSales;
            _params.Hq = p.Hq;
            _params.RegionWide = p.RegionWide;
            _params.IncludeVendor = p.IncludeVendor;
            _params.ShowOutStock = p.ShowOutStock;
            _params.Filters = p.Filters.ToArray();
            if (!string.IsNullOrEmpty(p.HomeServer))
                _homeServerBuffer = p.HomeServer.Length <= HomeServerBufferSize ? p.HomeServer : p.HomeServer.Substring(0, HomeServerBufferSize);
        }

        private void StartScan()
        {
            _params.HomeServer = _homeServerBuffer.Trim();
            if (string.IsNullOrEmpty(_params.HomeServer))
            {
                _errorMessage = "Set Home server first.";
                return;
            }
            _errorMessage = string.Empty;
            lock (_scanLock) { _scanInProgress = true; }
            _ = Task.Run(async () =>
            {
                try
                {
                    var list = await _api.ScanAsync(_params).ConfigureAwait(false);
                    lock (_scanLock)
                    {
                        _scanResults = list ?? new List<ResellingResultItem>();
                        _scanInProgress = false;
                    }
                }
                catch (System.Exception ex)
                {
                    lock (_scanLock)
                    {
                        _scanResults = new List<ResellingResultItem>();
                        _scanInProgress = false;
                    }
                    _errorMessage = ex.Message;
                }
            });
        }

        private static void DrawResultsTable(List<ResellingResultItem> results)
        {
            ImGui.Text($"Results: {results.Count} items");
            ImGui.Spacing();

            if (!ImGui.BeginTable("ResellingResults", 8, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY, new System.Numerics.Vector2(-1, 300)))
                return;

            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Buy world", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Buy (gil)", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Sell (gil)", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("Profit", ImGuiTableColumnFlags.WidthFixed, 80);
            ImGui.TableSetupColumn("ROI %", ImGuiTableColumnFlags.WidthFixed, 56);
            ImGui.TableSetupColumn("Stack", ImGuiTableColumnFlags.WidthFixed, 48);
            ImGui.TableSetupColumn("Sales/wk", ImGuiTableColumnFlags.WidthFixed, 56);
            ImGui.TableHeadersRow();

            foreach (var row in results)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(row.ItemName ?? row.ItemId.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(row.BuyServer ?? "-");
                ImGui.TableNextColumn();
                ImGui.Text(row.BuyPrice.ToString("N0"));
                ImGui.TableNextColumn();
                ImGui.Text(row.SellPrice.ToString("N0"));
                ImGui.TableNextColumn();
                ImGui.Text(row.Profit.ToString("N0"));
                ImGui.TableNextColumn();
                ImGui.Text(row.Roi.ToString("F1"));
                ImGui.TableNextColumn();
                ImGui.Text(row.StackSize.ToString());
                ImGui.TableNextColumn();
                ImGui.Text((row.SalesPerWeek ?? 0).ToString());
            }

            ImGui.EndTable();
        }
    }
}
