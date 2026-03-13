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
        private bool _resultsWindowOpen;
        private bool _showColumnsPopup;
        private const int SearchBufferSize = 128;
        private readonly byte[] _searchBuffer = new byte[SearchBufferSize];
        private int _tableIdCounter;
        private readonly List<int> _columnOrder = new();
        private readonly bool[] _columnVisible = new bool[(int)ResultColumn._Count];

        private static int[] GetDefaultColumnOrder() => new[]
        {
            (int)ResultColumn.ItemName,
            (int)ResultColumn.Saddlebag,
            (int)ResultColumn.Universalis,
            (int)ResultColumn.Vendor,
            (int)ResultColumn.Server,
            (int)ResultColumn.HomePrice,
            (int)ResultColumn.LowestPpu,
            (int)ResultColumn.ProfitAmount,
            (int)ResultColumn.SalesPerHour,
            (int)ResultColumn.AvgPpu,
            (int)ResultColumn.Roi,
            (int)ResultColumn.ProfitPercent,
            (int)ResultColumn.StackSize,
            (int)ResultColumn.LowestUpdated,
            (int)ResultColumn.HomeUpdated,
            (int)ResultColumn.RegMedNQ,
            (int)ResultColumn.RegAvgNQ,
            (int)ResultColumn.RegSalesNQ,
            (int)ResultColumn.RegQtyNQ,
            (int)ResultColumn.RegMedHQ,
            (int)ResultColumn.RegAvgHQ,
            (int)ResultColumn.RegSalesHQ,
            (int)ResultColumn.RegQtyHQ
        };

        private void EnsureColumnState()
        {
            const int n = (int)ResultColumn._Count;
            if (_columnOrder.Count != n)
            {
                _columnOrder.Clear();
                foreach (int i in GetDefaultColumnOrder())
                {
                    _columnOrder.Add(i);
                    _columnVisible[i] = true;
                }
            }
        }

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

            _resultsWindowOpen = true;
            if (ImGui.Begin("Saddlebag Exchange - Results", ref _resultsWindowOpen, ImGuiWindowFlags.None))
            {
                DrawResultsTable(results);
                ImGui.End();
            }
        }

        private static bool MatchesSearch(ResellingResultItem row, string search)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            var term = search.Trim();
            var comparison = StringComparison.OrdinalIgnoreCase;
            return (row.ItemName != null && row.ItemName.Contains(term, comparison))
                   || (row.BuyServer != null && row.BuyServer.Contains(term, comparison))
                   || (row.NpcVendorInfo != null && row.NpcVendorInfo.Contains(term, comparison))
                   || row.ItemId.ToString().Contains(term, comparison);
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

        private static string FormatTimestamp(string? ms)
        {
            if (string.IsNullOrEmpty(ms) || !long.TryParse(ms, out var t)) return "-";
            try
            {
                var dt = DateTimeOffset.FromUnixTimeMilliseconds(t);
                return dt.LocalDateTime.ToString("M/d HH:mm");
            }
            catch { return ms.Length > 8 ? ms[^8..] : ms; }
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
            HomeUpdated,
            LowestPpu,
            LowestUpdated,
            ProfitPercent,
            Roi,
            SalesPerHour,
            Server,
            StackSize,
            Universalis,
            Vendor,
            Saddlebag,
            RegMedNQ,
            RegAvgNQ,
            RegSalesNQ,
            RegQtyNQ,
            RegMedHQ,
            RegAvgHQ,
            RegSalesHQ,
            RegQtyHQ,
            _Count
        }

        private void DrawResultsTable(List<ResellingResultItem> results)
        {
            EnsureColumnState();
            var visibleCols = _columnOrder.Where(i => _columnVisible[i]).ToList();
            if (visibleCols.Count == 0)
            {
                ImGui.Text("No columns visible. Use Columns to show some.");
                return;
            }

            string searchFilter = System.Text.Encoding.UTF8.GetString(_searchBuffer).TrimEnd('\0');
            var sorted = SortResults(results);
            var filtered = string.IsNullOrWhiteSpace(searchFilter)
                ? sorted
                : sorted.Where(r => MatchesSearch(r, searchFilter)).ToList();
            if (ImGui.InputText("Search", _searchBuffer, ImGuiInputTextFlags.None))
            { }
            ImGui.SameLine();
            string countText = string.IsNullOrWhiteSpace(searchFilter)
                ? $"Results: {results.Count} items (click header to sort, scroll horizontally for more columns)"
                : $"Results: {results.Count} items ({filtered.Count} matching)";
            ImGui.Text(countText);
            if (ImGui.Button("Columns"))
                _showColumnsPopup = true;
            ImGui.SameLine();
            if (ImGui.Button("Reset columns"))
            {
                _columnOrder.Clear();
                foreach (int i in GetDefaultColumnOrder())
                {
                    _columnOrder.Add(i);
                    _columnVisible[i] = true;
                }
            }
            ImGui.SameLine();
            if (ImGui.Button("Reset column widths"))
                _tableIdCounter++;
            ImGui.Spacing();

            var avail = ImGui.GetContentRegionAvail();
            var tableSize = new System.Numerics.Vector2(avail.X, Math.Max(200, avail.Y));
            string tableId = "ResellingResults##" + _tableIdCounter;
            if (!ImGui.BeginTable(tableId, visibleCols.Count, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY | ImGuiTableFlags.ScrollX, tableSize))
                return;

            for (int i = 0; i < visibleCols.Count; i++)
            {
                int colId = visibleCols[i];
                float defaultW = GetDefaultColumnWidth(colId);
                ImGui.TableSetupColumn(GetColumnHeader(colId), ImGuiTableColumnFlags.WidthFixed, defaultW, (uint)colId);
            }
            ImGui.TableSetupScrollFreeze(0, 1);

            ImGui.TableNextRow(ImGuiTableRowFlags.Headers);
            foreach (int colId in visibleCols)
            {
                ImGui.TableNextColumn();
                ImGui.PushID(colId);
                bool active = _sortColumnIndex == colId;
                string headerText = GetColumnHeader(colId) + (active ? (_sortAscending ? " ▲" : " ▼") : "");
                var cellAvail = ImGui.GetContentRegionAvail();
                var p0 = ImGui.GetCursorPos();
                float lineH = ImGui.GetTextLineHeightWithSpacing();
                float buttonH = Math.Min(cellAvail.Y, lineH * 4f);
                if (ImGui.InvisibleButton("sort", new System.Numerics.Vector2(cellAvail.X, buttonH)))
                {
                    if (_sortColumnIndex == colId)
                        _sortAscending = !_sortAscending;
                    else
                    {
                        _sortColumnIndex = colId;
                        _sortAscending = true;
                    }
                }
                ImGui.SetCursorPos(p0);
                ImGui.PushTextWrapPos(p0.X + cellAvail.X);
                ImGui.TextWrapped(headerText);
                ImGui.PopTextWrapPos();
                ImGui.PopID();
            }

            int rowIndex = 0;
            foreach (var row in filtered)
            {
                ImGui.PushID(rowIndex);
                ImGui.TableNextRow();
                foreach (int colId in visibleCols)
                {
                    ImGui.TableNextColumn();
                    DrawCell(row, colId);
                }
                ImGui.PopID();
                rowIndex++;
            }

            ImGui.EndTable();

            if (_showColumnsPopup)
            {
                ImGui.OpenPopup("Column options");
                _showColumnsPopup = false;
            }
            if (ImGui.BeginPopup("Column options"))
            {
                ImGui.Text("Show / hide and reorder columns");
                ImGui.Separator();
                for (int i = 0; i < _columnOrder.Count; i++)
                {
                    int colId = _columnOrder[i];
                    ImGui.PushID(colId);
                    bool vis = _columnVisible[colId];
                    if (ImGui.Checkbox("##vis", ref vis))
                        _columnVisible[colId] = vis;
                    ImGui.SameLine();
                    ImGui.Text(GetColumnHeader(colId));
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Up") && i > 0)
                    {
                        (_columnOrder[i], _columnOrder[i - 1]) = (_columnOrder[i - 1], _columnOrder[i]);
                    }
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Down") && i < _columnOrder.Count - 1)
                    {
                        (_columnOrder[i], _columnOrder[i + 1]) = (_columnOrder[i + 1], _columnOrder[i]);
                    }
                    ImGui.PopID();
                }
                if (ImGui.Button("Close"))
                    ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }

        private static void DrawCell(ResellingResultItem row, int colId)
        {
            switch ((ResultColumn)colId)
            {
                case ResultColumn.ItemName:
                    ImGui.Text(row.ItemName ?? row.ItemId.ToString());
                    break;
                case ResultColumn.ProfitAmount:
                    ImGui.Text(row.Profit >= 999_999_999 ? "∞" : row.Profit.ToString("N0"));
                    break;
                case ResultColumn.AvgPpu:
                    ImGui.Text(row.SellPrice.ToString("N0"));
                    break;
                case ResultColumn.HomePrice:
                    ImGui.Text(row.HomeServerPrice == 0 ? "-" : row.HomeServerPrice.ToString("N0"));
                    break;
                case ResultColumn.HomeUpdated:
                    ImGui.Text(FormatTimestamp(row.HomeUpdateTime));
                    break;
                case ResultColumn.LowestPpu:
                    ImGui.Text(row.BuyPrice.ToString("N0"));
                    break;
                case ResultColumn.LowestUpdated:
                    ImGui.Text(FormatTimestamp(row.UpdateTime));
                    break;
                case ResultColumn.ProfitPercent:
                    ImGui.Text(row.ProfitPercent >= 999_999_999 ? "∞" : row.ProfitPercent.ToString("F1"));
                    break;
                case ResultColumn.Roi:
                    ImGui.Text(row.Roi.ToString("F1"));
                    break;
                case ResultColumn.SalesPerHour:
                    ImGui.Text(row.SaleRates.ToString("F4"));
                    break;
                case ResultColumn.Server:
                    ImGui.Text(row.BuyServer ?? "-");
                    break;
                case ResultColumn.StackSize:
                    ImGui.Text(row.StackSize.ToString());
                    break;
                case ResultColumn.Universalis:
                    if (ImGui.SmallButton("U")) OpenUrl(row.UniversalisUrl);
                    break;
                case ResultColumn.Vendor:
                    if (!string.IsNullOrEmpty(row.NpcVendorInfo))
                    { if (ImGui.SmallButton("V")) OpenUrl(row.NpcVendorInfo); }
                    else ImGui.Text("-");
                    break;
                case ResultColumn.Saddlebag:
                    if (ImGui.SmallButton("S")) OpenUrl(row.SaddlebagUrl);
                    break;
                case ResultColumn.RegMedNQ:
                    ImGui.Text(row.RegionWeeklyMedianNQ.ToString("N0"));
                    break;
                case ResultColumn.RegAvgNQ:
                    ImGui.Text(row.RegionWeeklyAverageNQ.ToString("N0"));
                    break;
                case ResultColumn.RegSalesNQ:
                    ImGui.Text((row.SalesPerWeek ?? 0).ToString());
                    break;
                case ResultColumn.RegQtyNQ:
                    ImGui.Text(row.RegionWeeklyQuantitySoldNQ.ToString());
                    break;
                case ResultColumn.RegMedHQ:
                    ImGui.Text(row.RegionWeeklyMedianHQ.ToString("N0"));
                    break;
                case ResultColumn.RegAvgHQ:
                    ImGui.Text(row.RegionWeeklyAverageHQ.ToString("N0"));
                    break;
                case ResultColumn.RegSalesHQ:
                    ImGui.Text(row.RegionWeeklySalesAmountHQ.ToString());
                    break;
                case ResultColumn.RegQtyHQ:
                    ImGui.Text(row.RegionWeeklyQuantitySoldHQ.ToString());
                    break;
                default:
                    break;
            }
        }

        private static string GetColumnHeader(int column)
        {
            return column switch
            {
                (int)ResultColumn.ItemName => "Item Name",
                (int)ResultColumn.ProfitAmount => "Profit Amount",
                (int)ResultColumn.AvgPpu => "Average Price Per Unit",
                (int)ResultColumn.HomePrice => "Home Server Price",
                (int)ResultColumn.HomeUpdated => "Home Server Info Last Updated At",
                (int)ResultColumn.LowestPpu => "Lowest Price Per Unit",
                (int)ResultColumn.LowestUpdated => "Lowest Price Last Update Time",
                (int)ResultColumn.ProfitPercent => "Profit Percentage",
                (int)ResultColumn.Roi => "Return on Investment",
                (int)ResultColumn.SalesPerHour => "Average Sales Per Hour",
                (int)ResultColumn.Server => "Lowest Price Server",
                (int)ResultColumn.StackSize => "Lowest Price Stack Size",
                (int)ResultColumn.Universalis => "Universalis Link",
                (int)ResultColumn.Vendor => "NPC Vendor Info",
                (int)ResultColumn.Saddlebag => "Item Data",
                (int)ResultColumn.RegMedNQ => "Region Weekly Median NQ",
                (int)ResultColumn.RegAvgNQ => "Region Weekly Average NQ",
                (int)ResultColumn.RegSalesNQ => "Region Weekly Sales Amount NQ",
                (int)ResultColumn.RegQtyNQ => "Region Weekly Quantity Sold NQ",
                (int)ResultColumn.RegMedHQ => "Region Weekly Median HQ",
                (int)ResultColumn.RegAvgHQ => "Region Weekly Average HQ",
                (int)ResultColumn.RegSalesHQ => "Region Weekly Sales Amount HQ",
                (int)ResultColumn.RegQtyHQ => "Region Weekly Quantity Sold HQ",
                _ => ""
            };
        }

        private static float GetDefaultColumnWidth(int column)
        {
            return column switch
            {
                (int)ResultColumn.ItemName => 220f,
                (int)ResultColumn.ProfitAmount => 120f,
                (int)ResultColumn.AvgPpu => 120f,
                (int)ResultColumn.HomePrice => 120f,
                (int)ResultColumn.HomeUpdated => 140f,
                (int)ResultColumn.LowestPpu => 120f,
                (int)ResultColumn.LowestUpdated => 140f,
                (int)ResultColumn.ProfitPercent => 110f,
                (int)ResultColumn.Roi => 120f,
                (int)ResultColumn.SalesPerHour => 130f,
                (int)ResultColumn.Server => 120f,
                (int)ResultColumn.StackSize => 60f,
                (int)ResultColumn.Universalis => 70f,
                (int)ResultColumn.Vendor => 100f,
                (int)ResultColumn.Saddlebag => 70f,
                (int)ResultColumn.RegMedNQ => 120f,
                (int)ResultColumn.RegAvgNQ => 120f,
                (int)ResultColumn.RegSalesNQ => 140f,
                (int)ResultColumn.RegQtyNQ => 120f,
                (int)ResultColumn.RegMedHQ => 120f,
                (int)ResultColumn.RegAvgHQ => 120f,
                (int)ResultColumn.RegSalesHQ => 140f,
                (int)ResultColumn.RegQtyHQ => 120f,
                _ => 100f
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
                    (int)ResultColumn.HomeUpdated => string.Compare(a.HomeUpdateTime ?? "", b.HomeUpdateTime ?? "", StringComparison.Ordinal),
                    (int)ResultColumn.LowestPpu => a.BuyPrice.CompareTo(b.BuyPrice),
                    (int)ResultColumn.LowestUpdated => string.Compare(a.UpdateTime ?? "", b.UpdateTime ?? "", StringComparison.Ordinal),
                    (int)ResultColumn.ProfitPercent => a.ProfitPercent.CompareTo(b.ProfitPercent),
                    (int)ResultColumn.Roi => a.Roi.CompareTo(b.Roi),
                    (int)ResultColumn.SalesPerHour => a.SaleRates.CompareTo(b.SaleRates),
                    (int)ResultColumn.Server => string.Compare(a.BuyServer ?? "", b.BuyServer ?? "", StringComparison.Ordinal),
                    (int)ResultColumn.StackSize => a.StackSize.CompareTo(b.StackSize),
                    (int)ResultColumn.RegMedNQ => a.RegionWeeklyMedianNQ.CompareTo(b.RegionWeeklyMedianNQ),
                    (int)ResultColumn.RegAvgNQ => a.RegionWeeklyAverageNQ.CompareTo(b.RegionWeeklyAverageNQ),
                    (int)ResultColumn.RegSalesNQ => (a.SalesPerWeek ?? 0).CompareTo(b.SalesPerWeek ?? 0),
                    (int)ResultColumn.RegQtyNQ => a.RegionWeeklyQuantitySoldNQ.CompareTo(b.RegionWeeklyQuantitySoldNQ),
                    (int)ResultColumn.RegMedHQ => a.RegionWeeklyMedianHQ.CompareTo(b.RegionWeeklyMedianHQ),
                    (int)ResultColumn.RegAvgHQ => a.RegionWeeklyAverageHQ.CompareTo(b.RegionWeeklyAverageHQ),
                    (int)ResultColumn.RegSalesHQ => a.RegionWeeklySalesAmountHQ.CompareTo(b.RegionWeeklySalesAmountHQ),
                    (int)ResultColumn.RegQtyHQ => a.RegionWeeklyQuantitySoldHQ.CompareTo(b.RegionWeeklyQuantitySoldHQ),
                    _ => 0
                };
                return c * dir;
            });
            return list;
        }
    }
}
