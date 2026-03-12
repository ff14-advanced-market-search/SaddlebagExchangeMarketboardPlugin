using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private int _sortColumnIndex = -1;
        private bool _sortAscending = true;

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

        private static void OpenUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return;
            try
            {
                using var _ = Process.Start(new ProcessStartInfo { FileName = url, UseShellExecute = true });
            }
            catch { /* ignore */ }
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

        private enum ResultColumn
        {
            ItemName = 0,
            ProfitAmount,
            AvgPpu,
            HomePrice,
            LowestPpu,
            ProfitPercent,
            Roi,
            SalesPerHour,
            Server,
            StackSize,
            Universalis,
            Vendor,
            Saddlebag,
            RegionMedianNQ,
            RegionSalesNQ,
            _Count
        }

        private void DrawResultsTable(List<ResellingResultItem> results)
        {
            ImGui.Text($"Results: {results.Count} items (click column header to sort)");
            ImGui.Spacing();

            const int colCount = (int)ResultColumn._Count;
            if (!ImGui.BeginTable("ResellingResults", colCount, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY, new System.Numerics.Vector2(-1, 320)))
                return;

            ImGui.TableSetupColumn("Item Name", ImGuiTableColumnFlags.WidthStretch, 0, (int)ResultColumn.ItemName);
            ImGui.TableSetupColumn("Profit", ImGuiTableColumnFlags.WidthFixed, 72, (int)ResultColumn.ProfitAmount);
            ImGui.TableSetupColumn("Avg PPU", ImGuiTableColumnFlags.WidthFixed, 72, (int)ResultColumn.AvgPpu);
            ImGui.TableSetupColumn("Home", ImGuiTableColumnFlags.WidthFixed, 64, (int)ResultColumn.HomePrice);
            ImGui.TableSetupColumn("Low PPU", ImGuiTableColumnFlags.WidthFixed, 64, (int)ResultColumn.LowestPpu);
            ImGui.TableSetupColumn("Profit %", ImGuiTableColumnFlags.WidthFixed, 56, (int)ResultColumn.ProfitPercent);
            ImGui.TableSetupColumn("ROI %", ImGuiTableColumnFlags.WidthFixed, 48, (int)ResultColumn.Roi);
            ImGui.TableSetupColumn("Sales/hr", ImGuiTableColumnFlags.WidthFixed, 56, (int)ResultColumn.SalesPerHour);
            ImGui.TableSetupColumn("Server", ImGuiTableColumnFlags.WidthFixed, 90, (int)ResultColumn.Server);
            ImGui.TableSetupColumn("Stack", ImGuiTableColumnFlags.WidthFixed, 40, (int)ResultColumn.StackSize);
            ImGui.TableSetupColumn("Universalis", ImGuiTableColumnFlags.WidthFixed, 72, (int)ResultColumn.Universalis);
            ImGui.TableSetupColumn("Vendor", ImGuiTableColumnFlags.WidthFixed, 48, (int)ResultColumn.Vendor);
            ImGui.TableSetupColumn("Item Data", ImGuiTableColumnFlags.WidthFixed, 56, (int)ResultColumn.Saddlebag);
            ImGui.TableSetupColumn("Reg Med NQ", ImGuiTableColumnFlags.WidthFixed, 72, (int)ResultColumn.RegionMedianNQ);
            ImGui.TableSetupColumn("Reg Sales NQ", ImGuiTableColumnFlags.WidthFixed, 72, (int)ResultColumn.RegionSalesNQ);

            var sorted = SortResults(results);

            // Draw sortable header row (click to sort)
            ImGui.TableNextRow();
            for (int c = 0; c < colCount; c++)
            {
                ImGui.TableNextColumn();
                bool active = _sortColumnIndex == c;
                string label = GetColumnHeader(c) + (active ? (_sortAscending ? " ▲" : " ▼") : "");
                if (ImGui.Selectable(label, active, ImGuiSelectableFlags.None, System.Numerics.Vector2.Zero))
                {
                    if (_sortColumnIndex == c)
                        _sortAscending = !_sortAscending;
                    else
                    {
                        _sortColumnIndex = c;
                        _sortAscending = true;
                    }
                }
            }

            int rowIndex = 0;
            foreach (var row in sorted)
            {
                ImGui.PushID(rowIndex);
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.Text(row.ItemName ?? row.ItemId.ToString());
                ImGui.TableNextColumn();
                ImGui.Text(row.Profit >= 999_999_999 ? "∞" : row.Profit.ToString("N0"));
                ImGui.TableNextColumn();
                ImGui.Text(row.SellPrice.ToString("N0"));
                ImGui.TableNextColumn();
                ImGui.Text(row.HomeServerPrice == 0 ? "-" : row.HomeServerPrice.ToString("N0"));
                ImGui.TableNextColumn();
                ImGui.Text(row.BuyPrice.ToString("N0"));
                ImGui.TableNextColumn();
                ImGui.Text(row.ProfitPercent >= 999_999_999 ? "∞" : row.ProfitPercent.ToString("F1"));
                ImGui.TableNextColumn();
                ImGui.Text(row.Roi.ToString("F1"));
                ImGui.TableNextColumn();
                ImGui.Text(row.SaleRates.ToString("F4"));
                ImGui.TableNextColumn();
                ImGui.Text(row.BuyServer ?? "-");
                ImGui.TableNextColumn();
                ImGui.Text(row.StackSize.ToString());
                ImGui.TableNextColumn();
                if (ImGui.SmallButton("Univ")) OpenUrl(row.UniversalisUrl);
                ImGui.TableNextColumn();
                if (!string.IsNullOrEmpty(row.NpcVendorInfo))
                { if (ImGui.SmallButton("Vend")) OpenUrl(row.NpcVendorInfo); }
                else ImGui.Text("-");
                ImGui.TableNextColumn();
                if (ImGui.SmallButton("Data")) OpenUrl(row.SaddlebagUrl);
                ImGui.TableNextColumn();
                ImGui.Text(row.RegionWeeklyMedianNQ.ToString("N0"));
                ImGui.TableNextColumn();
                ImGui.Text((row.SalesPerWeek ?? 0).ToString());
                ImGui.PopID();
                rowIndex++;
            }

            ImGui.EndTable();
        }

        private static string GetColumnHeader(int column)
        {
            return column switch
            {
                (int)ResultColumn.ItemName => "Item Name",
                (int)ResultColumn.ProfitAmount => "Profit",
                (int)ResultColumn.AvgPpu => "Avg PPU",
                (int)ResultColumn.HomePrice => "Home",
                (int)ResultColumn.LowestPpu => "Low PPU",
                (int)ResultColumn.ProfitPercent => "Profit %",
                (int)ResultColumn.Roi => "ROI %",
                (int)ResultColumn.SalesPerHour => "Sales/hr",
                (int)ResultColumn.Server => "Server",
                (int)ResultColumn.StackSize => "Stack",
                (int)ResultColumn.Universalis => "Univ.",
                (int)ResultColumn.Vendor => "Vendor",
                (int)ResultColumn.Saddlebag => "Data",
                (int)ResultColumn.RegionMedianNQ => "Reg Med NQ",
                (int)ResultColumn.RegionSalesNQ => "Reg Sales NQ",
                _ => ""
            };
        }

        private List<ResellingResultItem> SortResults(List<ResellingResultItem> results)
        {
            if (_sortColumnIndex < 0 || _sortColumnIndex >= (int)ResultColumn._Count)
                return results;
            var list = new List<ResellingResultItem>(results);
            int dir = _sortAscending ? 1 : -1;
            list.Sort((a, b) =>
            {
                int c = _sortColumnIndex switch
                {
                    (int)ResultColumn.ItemName => string.Compare(a.ItemName ?? "", b.ItemName ?? "", StringComparison.Ordinal),
                    (int)ResultColumn.ProfitAmount => a.Profit.CompareTo(b.Profit),
                    (int)ResultColumn.AvgPpu => a.SellPrice.CompareTo(b.SellPrice),
                    (int)ResultColumn.HomePrice => a.HomeServerPrice.CompareTo(b.HomeServerPrice),
                    (int)ResultColumn.LowestPpu => a.BuyPrice.CompareTo(b.BuyPrice),
                    (int)ResultColumn.ProfitPercent => a.ProfitPercent.CompareTo(b.ProfitPercent),
                    (int)ResultColumn.Roi => a.Roi.CompareTo(b.Roi),
                    (int)ResultColumn.SalesPerHour => a.SaleRates.CompareTo(b.SaleRates),
                    (int)ResultColumn.Server => string.Compare(a.BuyServer ?? "", b.BuyServer ?? "", StringComparison.Ordinal),
                    (int)ResultColumn.StackSize => a.StackSize.CompareTo(b.StackSize),
                    (int)ResultColumn.RegionMedianNQ => a.RegionWeeklyMedianNQ.CompareTo(b.RegionWeeklyMedianNQ),
                    (int)ResultColumn.RegionSalesNQ => (a.SalesPerWeek ?? 0).CompareTo(b.SalesPerWeek ?? 0),
                    _ => 0
                };
                return c * dir;
            });
            return list;
        }
    }
}
