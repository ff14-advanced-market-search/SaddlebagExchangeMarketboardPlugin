using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using SaddlebagExchange.Models;
using SaddlebagExchange.Services;

namespace SaddlebagExchange.UI
{
    public sealed class ResellingSearchTab : IDisposable
    {
        private const float SearchInputWidth = 200f;

        private readonly SaddlebagApiService _api = new();
        private readonly object _scanLock = new();
        private ResellingParams _params = GetDefaultParams();
        private List<ResellingResultItem> _scanResults = new();
        private bool _scanInProgress;
        private string _homeServerBuffer = string.Empty;
        private string _errorMessage = string.Empty;
        private int _sortColumnIndex = -1;
        private bool _sortAscending = true;
        private bool _resultsWindowOpen;
        private bool _showColumnsPopup;
        private bool _showFiltersPopup;
        private string _selectedDataCenter = string.Empty;
        private const int SearchBufferSize = 128;
        private readonly byte[] _searchBuffer = new byte[SearchBufferSize];
        private int _tableIdCounter;
        private string? _copyNotificationText;
        private DateTime _copyNotificationUntil;
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

        // Presets match frontend recommendedQueries (exact titles, descriptions, and params)
        private static readonly (string Label, string Description, ResellingParams Params)[] Presets =
        {
            ("Olivias Furnishing Items Medium Sell", "Finds medium priced furniture to sell.", P(168, 2, 25, 1, 75000, 30000, new[] { 56, 65, 66, 67, 68, 69, 70, 71, 72, 81, 82 }, false, true, true, true)),
            ("Olivias Consumable Collectables Medium Sell", "Medium priced Consumable Collectables to sell.", P(168, 2, 25, 1, 75000, 30000, new[] { 75, 80, 90 }, false, true, true, true)),
            ("Fast Sales Search", "Search for items with high rate of sales.\nMay not return results if your server has slow sales.", P(168, 20, 25, 1, 500, 500, new[] { 0 }, false, false, false, true)),
            ("NPC Vendor Furniture Item Search", "Search for items sold by Housing Vendors which can be resold on the marketboard.", P(168, 2, 50, 1, 5000, 3000, new[] { -4 }, false, true, true, false)),
            ("Commodities Search", "Search for items that sell in larger stack sizes (i.e. larger quantities)", P(168, 2, 25, 2, 1000, 1000, new[] { 0 }, false, false, false, true)),
            ("Mega Value Search", "Searches for the absolute highest value items on the whole marketboard with no regard to sale rates.", P(336, 1, 25, 1, 1000000, 1000000, new[] { 0 }, false, true, true, true)),
            ("NPC Vendor Item Search", "Search for items sold by NPC Vendors which can be resold on the marketboard.", P(48, 5, 50, 1, 1000, 1000, new[] { -1 }, false, true, true, true)),
            ("Beginner Out of Stock Search", "Recommended for Beginners. No level requirement, high profit margins, low risk, low cost, low effort, low competition, but slow sale rates.\nIgnore Average Value, everything this finds can be sold for 70k if there are no other listings on your server.", P(168, 2, 99, 1, 100, 100, new[] { 1, 2, 3, 4, 7 }, true, false, true, true)),
            ("Low Quality Out of Stock Search", "Same rules as the out of stock search, but this one looks for Low Quality items that can sell for like furniture or dyes that can sell for much higher prices than out of stock armor or weapons.", P(168, 2, 99, 1, 100, 100, new[] { 7, 54 }, false, true, true, true)),
            ("Olivias General Flipping Quick Sell", "Low Investment General Flipping Quick Sell.", P(48, 5, 25, 1, 5000, 5000, new[] { 0 }, false, true, true, true)),
            ("Olivias Class Quest Items Quick Sell", "Low Investment Class Quest Items Quick Sell.", P(48, 2, 25, 1, 5000, 5000, new[] { -2, -3 }, false, true, true, true)),
            ("Olivias Furnishing Items Quick Sell", "Low Investment Furnishing Items Quick Sell.", P(48, 5, 25, 1, 5000, 5000, new[] { 56, 65, 66, 67, 68, 69, 70, 71, 72, 81, 82 }, false, true, true, true)),
            ("Olivias Minions, Mounts, and Collectable Items Quick Sell", "Low Investment Minions, Mounts, and Collectable Items Quick Sell.", P(48, 5, 25, 1, 5000, 5000, new[] { 75, 80, 90 }, false, true, true, true)),
            ("Olivias Glamor Medium Sell", "Medium priced glamor items, it will also find class/profession gear ignore these and go for stuff that looks nice.", P(168, 2, 25, 1, 75000, 30000, new[] { 1, 2, -5 }, false, true, true, true)),
            ("Olivias High Investment Furniture Items", "Furnishing items with big profits but slow sales", P(336, 1, 25, 1, 300000, 300000, new[] { 56, 65, 66, 67, 68, 69, 70, 71, 72, 81, 82 }, false, true, true, true)),
            ("Olivias High Investment Collectable Items", "Collectable items with big profits but slow sales", P(336, 1, 25, 1, 300000, 300000, new[] { 75, 80, 90 }, false, true, true, true)),
            ("Olivias High Value Glamor Items", "Finds expensive glamor items, it will also find class/profession gear ignore these and go for stuff that looks nice.", P(336, 1, 25, 1, 300000, 300000, new[] { 1, 2, -5 }, false, true, true, true)),
            ("Olivias High Value Materials", "Finds expensive Materials and Trade goods.", P(336, 1, 25, 1, 300000, 300000, new[] { 6 }, false, true, true, true))
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
            var s = homeServer ?? string.Empty;
            _params.HomeServer = s;
            _homeServerBuffer = s;
        }

        public void Draw()
        {
            string errorMessage;
            lock (_scanLock) { errorMessage = _errorMessage ?? string.Empty; }

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
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Presets[i].Description);
            }
            ImGui.Spacing();

            // --- Search form (order and layout match frontend FullScanForm) ---
            ImGui.Text("Search");
            ImGui.Separator();

            // Primary: Scan Hours, Sale Amount
            int hoursAgo = _params.HoursAgo;
            ImGui.SetNextItemWidth(SearchInputWidth);
            ImGui.InputInt("Hours of data", ref hoursAgo, 24, 168);
            ImGui.SameLine();
            DrawHelpMarker("The time period to search over.\nex: 24 = past 24 hours.\nFor more items to sell, choose a higher number.");
            _params.HoursAgo = Math.Max(1, hoursAgo);

            int minSales = _params.MinSales;
            ImGui.SetNextItemWidth(SearchInputWidth);
            ImGui.InputInt("Min sales", ref minSales, 1, 5);
            ImGui.SameLine();
            DrawHelpMarker("Number of sales in that time.\nex: 5 = 5 sales in the selected period.\nFor more items to sell, choose a lower number.");
            _params.MinSales = Math.Max(0, minSales);

            // ROI, Average Price, Min Profit
            int preferredRoi = _params.PreferredRoi;
            ImGui.SetNextItemWidth(SearchInputWidth);
            ImGui.InputInt("Preferred ROI %", ref preferredRoi, 5, 10);
            ImGui.SameLine();
            DrawHelpMarker("Desired R.O.I (return on investment).\nex: 50 = 50% of sale revenue as profit (after tax).\nFor more profit, choose a higher number (1-100).");
            _params.PreferredRoi = Math.Max(0, preferredRoi);

            int minPpu = _params.MinDesiredAvgPpu;
            ImGui.SetNextItemWidth(SearchInputWidth);
            ImGui.InputInt("Min avg price (gil)", ref minPpu, 1000, 5000);
            ImGui.SameLine();
            DrawHelpMarker("Desired average price per unit.\nex: 10000 = only deals selling for 10000 gil or more.\nFor more items to sell, choose a lower number.");
            _params.MinDesiredAvgPpu = Math.Max(0, minPpu);

            int minProfit = _params.MinProfitAmount;
            ImGui.SetNextItemWidth(SearchInputWidth);
            ImGui.InputInt("Min profit (gil)", ref minProfit, 1000, 5000);
            ImGui.SameLine();
            DrawHelpMarker("Desired min profit amount.\nex: 10000 = only deals with 10000 gil profit or more.\nFor more items to sell, choose a lower number.");
            _params.MinProfitAmount = Math.Max(0, minProfit);

            // Advanced: Min stack size
            int minStack = _params.MinStackSize;
            ImGui.SetNextItemWidth(SearchInputWidth);
            ImGui.InputInt("Min stack size", ref minStack, 1, 10);
            ImGui.SameLine();
            DrawHelpMarker("Desired min stack size.\nex: 10 = only deals in stacks of 10 or more.\nFor more items to sell, choose a lower number.");
            _params.MinStackSize = Math.Max(1, minStack);

            // Checkboxes (same order as frontend)
            bool hq = _params.Hq;
            ImGui.Checkbox("HQ only", ref hq);
            ImGui.SameLine();
            DrawHelpMarker("Only search for hq prices");
            _params.Hq = hq;

            bool regionWide = _params.RegionWide;
            ImGui.Checkbox("Region-wide", ref regionWide);
            ImGui.SameLine();
            DrawHelpMarker("Search all servers in all DataCenters in your region.");
            _params.RegionWide = regionWide;

            bool showOutStock = _params.ShowOutStock;
            ImGui.Checkbox("Show out of stock", ref showOutStock);
            ImGui.SameLine();
            DrawHelpMarker("Include out of stock items in the list.\n(Shown as 100% profit margin, 1 bil gil profit.)");
            _params.ShowOutStock = showOutStock;

            bool includeVendor = _params.IncludeVendor;
            ImGui.Checkbox("Include vendor", ref includeVendor);
            ImGui.SameLine();
            DrawHelpMarker("Compare market prices vs vendor prices\non NQ items purchasable from vendors.");
            _params.IncludeVendor = includeVendor;

            // Item Filter, Home server
            int filterCount = _params.Filters?.Length ?? 0;
            if (ImGui.Button($"Filters ({filterCount})"))
                _showFiltersPopup = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("You can select multiple categories or select all for all types of items.");
            ImGui.SameLine();
            ImGui.Text("Item categories to include in search");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("You can select multiple categories or select all for all types of items.");

            DrawHomeServerCombo();

            ImGui.Spacing();
            bool doSearch = ImGui.Button("Search");
            ImGui.SameLine();
            if (!string.IsNullOrEmpty(errorMessage))
                ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f), errorMessage);

            if (doSearch)
                StartScan();

            DrawFiltersPopup();

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

            if (ImGui.Begin("Saddlebag Exchange - Results", ref _resultsWindowOpen, ImGuiWindowFlags.None))
                DrawResultsTable(results);
            ImGui.End();
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

        private void SetClipboardTextAndNotify(string? text, string? notificationMessage = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                if (OperatingSystem.IsWindows())
                {
                    ClipboardHelper.SetText(text);
                    if (!string.IsNullOrEmpty(notificationMessage))
                    {
                        _copyNotificationText = notificationMessage;
                        _copyNotificationUntil = DateTime.UtcNow + TimeSpan.FromSeconds(2);
                    }
                }
            }
            catch
            {
                /* ignore clipboard errors */
            }
        }

        private static void OpenUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return;
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || !uri.IsAbsoluteUri)
                return;
            try
            {
                using var _ = Process.Start(new ProcessStartInfo { FileName = uri.ToString(), UseShellExecute = true });
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
            {
                _params.HomeServer = p.HomeServer;
                _homeServerBuffer = p.HomeServer;
            }
        }

        private void DrawHomeServerCombo()
        {
            // Sync selected data center from current home server (e.g. after preset or SetDefaultHomeServer)
            if (!string.IsNullOrEmpty(_params.HomeServer))
            {
                var dc = WorldList.GetDataCenterForWorld(_params.HomeServer);
                if (!string.IsNullOrEmpty(dc))
                    _selectedDataCenter = dc;
            }

            // 1) Data Center
            string dcPreview = string.IsNullOrEmpty(_selectedDataCenter) ? "Select data center..." : _selectedDataCenter;
            ImGui.SetNextItemWidth(SearchInputWidth * 1.2f);
            if (ImGui.BeginCombo("Data Center", dcPreview))
            {
                foreach (string dc in WorldList.GetDataCenters())
                {
                    bool selected = _selectedDataCenter == dc;
                    if (ImGui.Selectable(dc, selected))
                    {
                        _selectedDataCenter = dc;
                        // If current world isn't in this DC, clear it
                        var dcWorlds = WorldList.GetWorlds(dc);
                        if (dcWorlds.Length > 0 && Array.IndexOf(dcWorlds, _params.HomeServer) < 0)
                        {
                            _params.HomeServer = string.Empty;
                            _homeServerBuffer = string.Empty;
                        }
                    }
                    if (selected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.SameLine();

            // 2) World (only when a data center is selected)
            string[] worlds = string.IsNullOrEmpty(_selectedDataCenter) ? Array.Empty<string>() : WorldList.GetWorlds(_selectedDataCenter);
            string worldPreview = _params.HomeServer;
            if (string.IsNullOrEmpty(worldPreview))
                worldPreview = "Select world...";
            ImGui.SetNextItemWidth(SearchInputWidth * 1.2f);
            if (ImGui.BeginCombo("World", worldPreview))
            {
                foreach (string world in worlds)
                {
                    bool selected = _params.HomeServer == world;
                    if (ImGui.Selectable(world, selected))
                    {
                        _params.HomeServer = world;
                        _homeServerBuffer = world;
                    }
                    if (selected)
                        ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
        }

        private static void DrawHelpMarker(string tooltip)
        {
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(tooltip);
        }

        private void DrawFiltersPopup()
        {
            if (_showFiltersPopup)
            {
                ImGui.OpenPopup("Item filters");
                _showFiltersPopup = false;
            }
            if (!ImGui.BeginPopup("Item filters"))
                return;

            int count = _params.Filters?.Length ?? 0;
            ImGui.Text($"Filters Selected: {count}");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Number of categories currently selected.");
            ImGui.Separator();
            ImGui.BeginChild("##filter_list", new System.Numerics.Vector2(320, 400), true);
            var filters = _params.Filters ?? Array.Empty<int>();
            var filterSet = new HashSet<int>(filters);
            foreach (var entry in ItemFilterDefs.GetAll())
            {
                if (entry.IsHeader)
                {
                    ImGui.Spacing();
                    ImGui.Text(entry.Label);
                    continue;
                }
                int id = entry.Id!.Value;
                bool isChecked = filterSet.Contains(id);
                bool isMainCategory = id >= 1 && id <= 7;
                string label = isMainCategory ? entry.Label : "-- " + entry.Label;
                if (ImGui.Checkbox(label, ref isChecked))
                {
                    var list = filters.ToList();
                    if (isChecked)
                        list.Add(id);
                    else
                        list.Remove(id);
                    _params.Filters = list.ToArray();
                }
            }
            ImGui.EndChild();
            if (ImGui.Button("Close"))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        private void StartScan()
        {
            _params.HomeServer = _homeServerBuffer.Trim();
            if (string.IsNullOrEmpty(_params.HomeServer))
            {
                lock (_scanLock) { _errorMessage = "Set Home server first."; }
                return;
            }
            lock (_scanLock)
            {
                _errorMessage = string.Empty;
                _scanInProgress = true;
            }
            _ = Task.Run(async () =>
            {
                try
                {
                    var list = await _api.ScanAsync(_params).ConfigureAwait(false);
                    lock (_scanLock)
                    {
                        _scanResults = list ?? new List<ResellingResultItem>();
                        _scanInProgress = false;
                        if (list != null && list.Count > 0)
                            _resultsWindowOpen = true;
                    }
                }
                catch (System.Exception ex)
                {
                    lock (_scanLock)
                    {
                        _scanResults = new List<ResellingResultItem>();
                        _scanInProgress = false;
                        _errorMessage = ex.Message;
                    }
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
            ImGui.InputText("Search", _searchBuffer, ImGuiInputTextFlags.None);
            string countText = string.IsNullOrWhiteSpace(searchFilter)
                ? $"Results: {results.Count} items (click header to sort, scroll horizontally for more columns)"
                : $"Results: {results.Count} items ({filtered.Count} matching)";
            ImGui.Text(countText);
            ImGui.Text("Click an item name to copy it to the clipboard.");
            if (_copyNotificationText != null && DateTime.UtcNow < _copyNotificationUntil)
            {
                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(0.4f, 1f, 0.4f, 1f));
                ImGui.Text(_copyNotificationText);
                ImGui.PopStyleColor();
            }
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
                    DrawCell(row, colId, SetClipboardTextAndNotify);
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

        private static void DrawCell(ResellingResultItem row, int colId, Action<string?, string?>? copyNotify = null)
        {
            switch ((ResultColumn)colId)
            {
                case ResultColumn.ItemName:
                    string name = row.ItemName ?? row.ItemId.ToString();
                    if (ImGui.Selectable(name, false, ImGuiSelectableFlags.None, System.Numerics.Vector2.Zero))
                        copyNotify?.Invoke(name, "Item name copied to clipboard");
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
                    (int)ResultColumn.HomeUpdated => (long.TryParse(a.HomeUpdateTime, out var ha) ? ha : 0L).CompareTo(long.TryParse(b.HomeUpdateTime, out var hb) ? hb : 0L),
                    (int)ResultColumn.LowestPpu => a.BuyPrice.CompareTo(b.BuyPrice),
                    (int)ResultColumn.LowestUpdated => (long.TryParse(a.UpdateTime, out var ua) ? ua : 0L).CompareTo(long.TryParse(b.UpdateTime, out var ub) ? ub : 0L),
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

        public void Dispose() => _api.Dispose();
    }

    internal static class ClipboardHelper
    {
        private const uint CF_UNICODETEXT = 13;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool OpenClipboard(IntPtr hWndNewOwner);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool CloseClipboard();

        [DllImport("user32.dll")]
        private static extern bool EmptyClipboard();

        [DllImport("user32.dll")]
        private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalLock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern bool GlobalUnlock(IntPtr hMem);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GlobalFree(IntPtr hMem);

        private const uint GMEM_MOVEABLE = 0x0002;

        public static void SetText(string text)
        {
            if (text == null) return;
            var bytes = (text.Length + 1) * 2;
            var hMem = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
            if (hMem == IntPtr.Zero) return;
            var ptr = GlobalLock(hMem);
            if (ptr == IntPtr.Zero)
            {
                GlobalFree(hMem);
                return;
            }
            try
            {
                Marshal.Copy(text.ToCharArray(), 0, ptr, text.Length);
                Marshal.WriteInt16(ptr, text.Length * 2, 0);
            }
            finally
            {
                GlobalUnlock(hMem);
            }
            if (!OpenClipboard(IntPtr.Zero))
            {
                GlobalFree(hMem);
                return;
            }
            try
            {
                EmptyClipboard();
                if (SetClipboardData(CF_UNICODETEXT, hMem) == IntPtr.Zero)
                    GlobalFree(hMem);
            }
            finally
            {
                CloseClipboard();
            }
        }
    }
}
