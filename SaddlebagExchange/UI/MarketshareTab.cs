using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Utility;
using SaddlebagExchange.Models;
using SaddlebagExchange.Services;

namespace SaddlebagExchange.UI
{
    public sealed class MarketshareTab : IDisposable
    {
        private const float InputWidth = 200f;
        private const int SearchBufferSize = 128;

        private readonly SaddlebagApiService _api = new();
        private readonly object _scanLock = new();
        private MarketshareParams _params = new()
        {
            TimePeriod = 168,
            SalesAmount = 3,
            AveragePrice = 10000,
            Filters = new[] { 0 },
            SortBy = "marketValue"
        };
        private List<MarketshareResultItem> _scanResults = new();
        private bool _scanInProgress;
        private string _selectedDataCenter = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _showFiltersPopup;
        private bool _resultsWindowOpen;
        private bool _treemapWindowOpen;
        private int _treemapMetricIndex;
        private bool _showColumnsPopup;
        private readonly byte[] _searchBuffer = new byte[SearchBufferSize];
        private int _sortColumnIndex = 0;
        private bool _sortAscending = false;
        private int _tableIdCounter;
        private string? _copyNotificationText;
        private DateTime _copyNotificationUntil;
        private readonly List<int> _columnOrder = new();
        private readonly bool[] _columnVisible = new bool[(int)MsResultColumn._Count];

        private enum MsResultColumn
        {
            ItemName = 0,
            ItemId,
            MarketValue,
            Avg,
            Median,
            MinPrice,
            PurchaseAmount,
            QuantitySold,
            PercentChange,
            State,
            Universalis,
            Vendor,
            Saddlebag,
            _Count
        }

        private static int[] GetDefaultColumnOrder() => new[]
        {
            (int)MsResultColumn.ItemName,
            (int)MsResultColumn.MarketValue,
            (int)MsResultColumn.PercentChange,
            (int)MsResultColumn.State,
            (int)MsResultColumn.Avg,
            (int)MsResultColumn.Median,
            (int)MsResultColumn.MinPrice,
            (int)MsResultColumn.PurchaseAmount,
            (int)MsResultColumn.QuantitySold,
            (int)MsResultColumn.Saddlebag,
            (int)MsResultColumn.Universalis,
            (int)MsResultColumn.Vendor,
            (int)MsResultColumn.ItemId
        };

        private void EnsureColumnState()
        {
            const int n = (int)MsResultColumn._Count;
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

        private static MarketshareParams P(int timePeriod, int salesAmount, int averagePrice, int[] filters, string sortBy) => new()
        {
            Server = string.Empty,
            TimePeriod = timePeriod,
            SalesAmount = salesAmount,
            AveragePrice = averagePrice,
            Filters = filters,
            SortBy = sortBy
        };

        private static readonly (string Label, string Description, MarketshareParams Params)[] Presets =
        {
            ("Total Market View (Weekly)", "See a general overview of your server's market by revenue earned.", P(168, 3, 10000, new[] { 0 }, "marketValue")),
            ("Highest Price Increases (Weekly)", "Find the best selling items that are out of stock or have had massive price increases!", P(168, 3, 10000, new[] { 0 }, "percentChange")),
            ("Eaving Tidemoores Best Raw Materials to Gather.", "See the best earning and fastest selling raw materials to gather. Made by Eaving Tidemoore.", P(24, 30, 500, new[] { 47, 48, 49, 50 }, "marketValue")),
            ("Mega Value Marketshare (Weekly)", "Find the ultra high value items with the most revenue based on the last 7 days sales.", P(168, 1, 1000000, new[] { 0 }, "marketValue")),
            ("Best Selling Items (Last Hour)", "See the items with the top revenue from the last hour.", P(1, 1, 10, new[] { 0 }, "marketValue")),
            ("Fastest Selling Items (Daily)", "See the fastest selling items from the last 24 hours.", P(24, 35, 10, new[] { 0 }, "purchaseAmount")),
            ("Most Quantity Purchased (Daily).", "See the items that sell in bulk with the most quantity sold in the last 24 hours.", P(24, 35, 10, new[] { 0 }, "quantitySold")),
            ("Best Selling Furniture (Weekly).", "See the best selling furniture items from the last week.", P(168, 3, 10000, new[] { 56, 65, 66, 67, 68, 69, 70, 71, 72, 81, 82 }, "marketValue")),
            ("Best Selling Collectible Items (Weekly).", "See the best selling collectible items from the last week.", P(168, 3, 10000, new[] { 75, 80, 90 }, "marketValue")),
            ("Best Selling Consumable Items (Weekly).", "See the best selling food, seafood, tincture items from the last week.", P(168, 10, 100, new[] { 5 }, "marketValue")),
            ("Best Selling Vendor Items (Weekly).", "See the best selling items you can buy from vendors from the last week.", P(168, 3, 10000, new[] { -1 }, "marketValue")),
            ("Best Selling Gear, Weapons, Armor and Glamors (Weekly).", "See the best selling gear, weapons, armor and glamors from the last week. Excluding crafted raid gear.", P(168, 3, 10000, new[] { 1, 2, 3, 4, -5 }, "marketValue")),
            ("Best Raw Materials to Sell.", "See the best earning and fastest selling raw materials to sell.", P(24, 10, 10, new[] { 6 }, "marketValue"))
        };

        private static readonly (string Value, string Label)[] SortByOptions =
        {
            ("marketValue", "Market Value"),
            ("avg", "Average Price"),
            ("median", "Median Price"),
            ("purchaseAmount", "Purchase Amount"),
            ("quantitySold", "Quantity Sold"),
            ("percentChange", "Percent Change")
        };

        public void SetDefaultHomeServer(string? server)
        {
            var s = server ?? string.Empty;
            _params.Server = s;
            if (!string.IsNullOrEmpty(s))
            {
                var dc = WorldList.GetDataCenterForWorld(s);
                if (!string.IsNullOrEmpty(dc))
                    _selectedDataCenter = dc;
            }
        }

        public void Draw()
        {
            string errorMessage;
            lock (_scanLock) { errorMessage = _errorMessage ?? string.Empty; }

            ImGui.Spacing();
            ImGui.Text("Market Overview");
            ImGui.Separator();
            ImGui.Spacing();

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

            ImGui.Text("Search");
            ImGui.Separator();

            int timePeriod = _params.TimePeriod;
            ImGui.SetNextItemWidth(InputWidth);
            ImGui.InputInt("Time period (hours)", ref timePeriod, 24, 168);
            ImGui.SameLine();
            DrawHelpMarker("Time period to search for sales in hours.\nex: 168 = 7 days, 24 = 1 day.");
            _params.TimePeriod = Math.Max(1, timePeriod);

            int salesAmount = _params.SalesAmount;
            ImGui.SetNextItemWidth(InputWidth);
            ImGui.InputInt("Min sales", ref salesAmount, 1, 5);
            ImGui.SameLine();
            DrawHelpMarker("Ignore items with less than this amount of sales\nin the chosen time period.");
            _params.SalesAmount = Math.Max(0, salesAmount);

            int averagePrice = _params.AveragePrice;
            ImGui.SetNextItemWidth(InputWidth);
            ImGui.InputInt("Min average price (gil)", ref averagePrice, 1000, 10000);
            ImGui.SameLine();
            DrawHelpMarker("Don't show items below this average price.");
            _params.AveragePrice = Math.Max(0, averagePrice);

            int filterCount = _params.Filters?.Length ?? 0;
            if (ImGui.Button($"Filters ({filterCount})"))
                _showFiltersPopup = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("You can select multiple categories or select all for all types of items.");
            ImGui.SameLine();
            ImGui.Text("Item categories");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("You can select multiple categories or select all for all types of items.");

            int sortIndex = Array.FindIndex(SortByOptions, o => o.Value == _params.SortBy);
            if (sortIndex < 0) sortIndex = 0;
            ImGui.SetNextItemWidth(InputWidth * 1.2f);
            if (ImGui.Combo("Sort by", ref sortIndex, SortByOptions.Select(o => o.Label).ToArray(), SortByOptions.Length))
                _params.SortBy = SortByOptions[sortIndex].Value;

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

            bool loading;
            List<MarketshareResultItem> results;
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

            if (ImGui.Begin("Saddlebag Exchange - Market Overview Results", ref _resultsWindowOpen, ImGuiWindowFlags.None))
                DrawResultsTable(results);
            ImGui.End();

            if (_treemapWindowOpen)
                DrawTreemapWindow();
        }

        private void DrawResultsTable(List<MarketshareResultItem> results)
        {
            EnsureColumnState();
            var visibleCols = _columnOrder.Where(i => _columnVisible[i]).ToList();
            if (visibleCols.Count == 0)
            {
                ImGui.Text("No columns visible. Use Columns to show some.");
                return;
            }

            string searchFilter = Encoding.UTF8.GetString(_searchBuffer).TrimEnd('\0').Trim();
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
            ImGui.SameLine();
            if (ImGui.Button("Treemap"))
                _treemapWindowOpen = true;
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Open a heatmap view of the results in a separate window.");
            ImGui.Spacing();

            var avail = ImGui.GetContentRegionAvail();
            var tableSize = new System.Numerics.Vector2(avail.X, Math.Max(200, avail.Y));
            string tableId = "MarketshareResults##" + _tableIdCounter;
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
                float buttonH = Math.Min(cellAvail.Y, ImGui.GetTextLineHeightWithSpacing() * 4f);
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

            if (filtered.Count == 0 && results.Count > 0)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();
                ImGui.PushStyleColor(ImGuiCol.Text, new System.Numerics.Vector4(1f, 0.9f, 0.4f, 1f));
                ImGui.TextWrapped("No rows match your search. Clear the search box above to show all items.");
                ImGui.PopStyleColor();
                for (int i = 1; i < visibleCols.Count; i++)
                    ImGui.TableNextColumn();
            }

            ImGui.EndTable();

            if (_showColumnsPopup)
            {
                ImGui.OpenPopup("Marketshare column options");
                _showColumnsPopup = false;
            }
            if (ImGui.BeginPopup("Marketshare column options"))
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

        private static bool MatchesSearch(MarketshareResultItem row, string search)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            var term = search.Trim();
            var comparison = StringComparison.OrdinalIgnoreCase;
            return (row.ItemName != null && row.ItemName.Contains(term, comparison))
                   || (row.State != null && row.State.Contains(term, comparison))
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
            catch { /* ignore */ }
        }

        private static void OpenUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return;
            Util.OpenLink(url);
        }

        private void DrawCell(MarketshareResultItem row, int colId)
        {
            switch ((MsResultColumn)colId)
            {
                case MsResultColumn.ItemName:
                    string name = !string.IsNullOrEmpty(row.ItemName) ? row.ItemName : row.ItemId.ToString();
                    if (ImGui.Selectable(name, false, ImGuiSelectableFlags.None, System.Numerics.Vector2.Zero))
                        SetClipboardTextAndNotify(name, "Item name copied to clipboard");
                    break;
                case MsResultColumn.ItemId:
                    ImGui.Text(row.ItemId.ToString());
                    break;
                case MsResultColumn.MarketValue:
                    ImGui.Text(row.MarketValue.ToString("N0"));
                    break;
                case MsResultColumn.Avg:
                    ImGui.Text(row.Avg.ToString("N0"));
                    break;
                case MsResultColumn.Median:
                    ImGui.Text(row.Median.ToString("N0"));
                    break;
                case MsResultColumn.MinPrice:
                    ImGui.Text(row.MinPrice.ToString("N0"));
                    break;
                case MsResultColumn.PurchaseAmount:
                    ImGui.Text(row.PurchaseAmount.ToString());
                    break;
                case MsResultColumn.QuantitySold:
                    ImGui.Text(row.QuantitySold.ToString());
                    break;
                case MsResultColumn.PercentChange:
                    ImGui.Text(row.PercentChange.ToString("F2"));
                    break;
                case MsResultColumn.State:
                    ImGui.Text(row.State ?? "-");
                    break;
                case MsResultColumn.Universalis:
                    if (ImGui.SmallButton("U")) OpenUrl(row.UniversalisUrl);
                    break;
                case MsResultColumn.Vendor:
                    if (!string.IsNullOrEmpty(row.NpcVendorInfo))
                    { if (ImGui.SmallButton("V")) OpenUrl(row.NpcVendorInfo); }
                    else ImGui.Text("-");
                    break;
                case MsResultColumn.Saddlebag:
                    if (ImGui.SmallButton("S")) OpenUrl(row.SaddlebagUrl);
                    break;
                default:
                    break;
            }
        }

        private static string GetColumnHeader(int column)
        {
            return column switch
            {
                (int)MsResultColumn.ItemName => "Item Name",
                (int)MsResultColumn.ItemId => "Item ID",
                (int)MsResultColumn.MarketValue => "Weekly Gil Earned",
                (int)MsResultColumn.Avg => "Average Price",
                (int)MsResultColumn.Median => "Median Price",
                (int)MsResultColumn.MinPrice => "Minimum Price",
                (int)MsResultColumn.PurchaseAmount => "Purchase Amount",
                (int)MsResultColumn.QuantitySold => "Quantity Sold",
                (int)MsResultColumn.PercentChange => "Percent Changed",
                (int)MsResultColumn.State => "Market State",
                (int)MsResultColumn.Universalis => "Universalis Link",
                (int)MsResultColumn.Vendor => "NPC Vendor",
                (int)MsResultColumn.Saddlebag => "Item Data",
                _ => "?"
            };
        }

        private static float GetDefaultColumnWidth(int column)
        {
            return column switch
            {
                (int)MsResultColumn.ItemName => 220f,
                (int)MsResultColumn.ItemId => 80f,
                (int)MsResultColumn.MarketValue => 120f,
                (int)MsResultColumn.Avg => 100f,
                (int)MsResultColumn.Median => 100f,
                (int)MsResultColumn.MinPrice => 100f,
                (int)MsResultColumn.PurchaseAmount => 100f,
                (int)MsResultColumn.QuantitySold => 90f,
                (int)MsResultColumn.PercentChange => 100f,
                (int)MsResultColumn.State => 90f,
                (int)MsResultColumn.Universalis => 70f,
                (int)MsResultColumn.Vendor => 70f,
                (int)MsResultColumn.Saddlebag => 70f,
                _ => 100f
            };
        }

        private List<MarketshareResultItem> SortResults(List<MarketshareResultItem> results)
        {
            if (_sortColumnIndex < 0 || _sortColumnIndex >= (int)MsResultColumn._Count)
                return results;
            var list = new List<MarketshareResultItem>(results);
            int dir = _sortAscending ? 1 : -1;
            list.Sort((a, b) =>
            {
                int c = _sortColumnIndex switch
                {
                    (int)MsResultColumn.ItemName => string.Compare(a.ItemName ?? "", b.ItemName ?? "", StringComparison.Ordinal),
                    (int)MsResultColumn.ItemId => a.ItemId.CompareTo(b.ItemId),
                    (int)MsResultColumn.MarketValue => a.MarketValue.CompareTo(b.MarketValue),
                    (int)MsResultColumn.Avg => a.Avg.CompareTo(b.Avg),
                    (int)MsResultColumn.Median => a.Median.CompareTo(b.Median),
                    (int)MsResultColumn.MinPrice => a.MinPrice.CompareTo(b.MinPrice),
                    (int)MsResultColumn.PurchaseAmount => a.PurchaseAmount.CompareTo(b.PurchaseAmount),
                    (int)MsResultColumn.QuantitySold => a.QuantitySold.CompareTo(b.QuantitySold),
                    (int)MsResultColumn.PercentChange => a.PercentChange.CompareTo(b.PercentChange),
                    (int)MsResultColumn.State => string.Compare(a.State ?? "", b.State ?? "", StringComparison.Ordinal),
                    _ => 0
                };
                return c * dir;
            });
            return list;
        }

        private void DrawHomeServerCombo()
        {
            if (!string.IsNullOrEmpty(_params.Server))
            {
                var dc = WorldList.GetDataCenterForWorld(_params.Server);
                if (!string.IsNullOrEmpty(dc))
                    _selectedDataCenter = dc;
            }

            string dcPreview = string.IsNullOrEmpty(_selectedDataCenter) ? "Select data center..." : _selectedDataCenter;
            ImGui.SetNextItemWidth(InputWidth * 1.2f);
            if (ImGui.BeginCombo("Data Center", dcPreview))
            {
                foreach (string dc in WorldList.GetDataCenters())
                {
                    bool selected = _selectedDataCenter == dc;
                    if (ImGui.Selectable(dc, selected))
                    {
                        _selectedDataCenter = dc;
                        var dcWorlds = WorldList.GetWorlds(dc);
                        if (dcWorlds.Length > 0 && Array.IndexOf(dcWorlds, _params.Server) < 0)
                            _params.Server = string.Empty;
                    }
                    if (selected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }

            ImGui.SameLine();

            string[] worlds = string.IsNullOrEmpty(_selectedDataCenter) ? Array.Empty<string>() : WorldList.GetWorlds(_selectedDataCenter);
            string worldPreview = string.IsNullOrEmpty(_params.Server) ? "Select world..." : _params.Server;
            ImGui.SetNextItemWidth(InputWidth * 1.2f);
            if (ImGui.BeginCombo("World", worldPreview))
            {
                foreach (string world in worlds)
                {
                    bool selected = _params.Server == world;
                    if (ImGui.Selectable(world, selected))
                        _params.Server = world;
                    if (selected) ImGui.SetItemDefaultFocus();
                }
                ImGui.EndCombo();
            }
        }

        private static readonly string[] TreemapMetricLabels = { "Weekly Gil Earned", "Price Increase %", "Purchase Amount", "Quantity Sold", "Average Price", "Median Price" };

        private static double GetTreemapMetricValue(MarketshareResultItem item, int metricIndex)
        {
            return metricIndex switch
            {
                0 => item.MarketValue,
                1 => item.PercentChange,
                2 => item.PurchaseAmount,
                3 => item.QuantitySold,
                4 => item.Avg,
                5 => item.Median,
                _ => item.MarketValue
            };
        }

        // State RGB as Vector4(r,g,b,a). API: 'spiking' | 'increasing' | 'stable' | 'decreasing' | 'crashing' | 'out of stock'
        // Out of stock = blue; stable = yellow
        private static System.Numerics.Vector4 TreemapColorForState(string? state)
        {
            if (string.IsNullOrEmpty(state)) return new System.Numerics.Vector4(0.42f, 0.42f, 0.42f, 1f);
            var s = state.Trim();
            if (string.Equals(s, "out of stock", StringComparison.OrdinalIgnoreCase)) return new System.Numerics.Vector4(0.13f, 0.59f, 0.95f, 1f);   // blue
            if (string.Equals(s, "stable", StringComparison.OrdinalIgnoreCase)) return new System.Numerics.Vector4(1f, 0.92f, 0.23f, 1f);       // yellow
            if (string.Equals(s, "increasing", StringComparison.OrdinalIgnoreCase)) return new System.Numerics.Vector4(0.55f, 0.76f, 0.29f, 1f);   // light green
            if (string.Equals(s, "spiking", StringComparison.OrdinalIgnoreCase)) return new System.Numerics.Vector4(0.16f, 0.52f, 0.20f, 1f);     // dark green (spiking)
            // Red family: high R, low B (never high B — that reads purple on ImGui tinting)
            if (string.Equals(s, "decreasing", StringComparison.OrdinalIgnoreCase)) return new System.Numerics.Vector4(1f, 0.45f, 0.38f, 1f);    // light red / coral-red
            if (string.Equals(s, "crashing", StringComparison.OrdinalIgnoreCase)) return new System.Numerics.Vector4(0.86f, 0.18f, 0.14f, 1f);    // dark red
            return new System.Numerics.Vector4(0.42f, 0.42f, 0.42f, 1f);
        }

        private void DrawTreemapWindow()
        {
            List<MarketshareResultItem> results;
            lock (_scanLock) { results = _scanResults ?? new List<MarketshareResultItem>(); }
            if (results.Count == 0)
            {
                _treemapWindowOpen = false;
                return;
            }

            float treemapWinW = 720f * ImGuiHelpers.GlobalScale;
            float treemapWinH = 520f * ImGuiHelpers.GlobalScale;
            ImGui.SetNextWindowSize(new System.Numerics.Vector2(treemapWinW, treemapWinH), ImGuiCond.FirstUseEver);
            if (!ImGui.Begin("Market Overview - Treemap", ref _treemapWindowOpen, ImGuiWindowFlags.None))
            {
                ImGui.End();
                return;
            }

            ImGui.Text("Sort treemap by:");
            ImGui.SameLine();
            ImGui.PushStyleVar(ImGuiStyleVar.ItemSpacing, new System.Numerics.Vector2(4f, 2f));
            for (int i = 0; i < TreemapMetricLabels.Length; i++)
            {
                if (i > 0) ImGui.SameLine();
                bool selected = _treemapMetricIndex == i;
                if (selected)
                {
                    ImGui.PushStyleColor(ImGuiCol.Button, new System.Numerics.Vector4(0.2f, 0.4f, 0.7f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, new System.Numerics.Vector4(0.25f, 0.45f, 0.75f, 1f));
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, new System.Numerics.Vector4(0.15f, 0.35f, 0.65f, 1f));
                }
                if (ImGui.Button(TreemapMetricLabels[i]))
                    _treemapMetricIndex = i;
                if (selected)
                    ImGui.PopStyleColor(3);
            }
            ImGui.PopStyleVar();
            ImGui.Separator();
            ImGui.Spacing();

            var treemapAvail = ImGui.GetContentRegionAvail();
            float treemapMinH = 280f * ImGuiHelpers.GlobalScale;
            using (var child = ImRaii.Child("##treemap_canvas", new System.Numerics.Vector2(treemapAvail.X, Math.Max(treemapMinH, treemapAvail.Y)), true, ImGuiWindowFlags.None))
            {
                if (!child.Success)
                {
                    ImGui.End();
                    return;
                }

                const int maxItems = 48;
                const int cols = 8;
                var items = results
                    .Select(r => (r, v: Math.Max(0, GetTreemapMetricValue(r, _treemapMetricIndex))))
                    .Where(t => t.v > 0)
                    .OrderByDescending(t => t.v)
                    .Take(maxItems)
                    .ToList();
                if (items.Count == 0)
                {
                    ImGui.Text("No data for this metric.");
                    ImGui.End();
                    return;
                }

                double total = items.Sum(t => t.v);
                if (total <= 0) total = 1;
                var avail = ImGui.GetContentRegionAvail();
                float canvasW = avail.X;
                float canvasH = Math.Max(200f * ImGuiHelpers.GlobalScale, avail.Y);
                int numRows = (items.Count + cols - 1) / cols;
                float rowHeightSum = 0f;
                for (int r = 0; r < numRows; r++)
                {
                    int start = r * cols;
                    int count = Math.Min(cols, items.Count - start);
                    double rowSum = 0;
                    for (int i = start; i < start + count; i++)
                        rowSum += items[i].v;
                    rowHeightSum += (float)(rowSum / total) * canvasH;
                }
                if (rowHeightSum <= 0) rowHeightSum = canvasH;
                float y = 0f;
                int idx = 0;
                for (int r = 0; r < numRows; r++)
                {
                    int start = r * cols;
                    int count = Math.Min(cols, items.Count - start);
                    double rowSum = 0;
                    for (int i = start; i < start + count; i++)
                        rowSum += items[i].v;
                    if (rowSum <= 0) rowSum = 1;
                    float rowH = (float)(rowSum / total) * canvasH;
                    float x = 0f;
                    for (int c = 0; c < count; c++)
                    {
                        int i = start + c;
                        var (rowItem, val) = items[i];
                        float cellW = (float)(val / rowSum) * canvasW;
                        if (cellW < 2f) cellW = 2f;
                        ImGui.SetCursorPos(new System.Numerics.Vector2(x, y));
                        var colV4 = TreemapColorForState(rowItem.State);
                        ImGui.PushStyleColor(ImGuiCol.Button, colV4);
                        ImGui.PushStyleColor(ImGuiCol.ButtonHovered, colV4);
                        ImGui.PushStyleColor(ImGuiCol.ButtonActive, colV4);
                        string label = "##tm" + idx;
                        if (ImGui.Button(label, new System.Numerics.Vector2(cellW, rowH)))
                            { }
                        if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                        {
                            string name = !string.IsNullOrEmpty(rowItem.ItemName) ? rowItem.ItemName : rowItem.ItemId.ToString();
                            string valStr = _treemapMetricIndex == 1
                                ? rowItem.PercentChange.ToString("F2") + "%"
                                : (_treemapMetricIndex == 0 || _treemapMetricIndex >= 4 ? ((long)val).ToString("N0") : ((long)val).ToString());
                            string statePart = !string.IsNullOrEmpty(rowItem.State) ? $" ({rowItem.State})" : "";
                            ImGui.SetTooltip($"{name}: {valStr}{statePart}");
                        }
                        ImGui.PopStyleColor(3);
                        x += cellW;
                        idx++;
                    }
                    y += rowH;
                }
            }
            ImGui.End();
        }

        private void ApplyPreset(MarketshareParams p)
        {
            // Preserve current server when preset doesn't specify one (presets use Server = string.Empty)
            if (!string.IsNullOrEmpty(p.Server))
            {
                _params.Server = p.Server;
                var dc = WorldList.GetDataCenterForWorld(p.Server);
                if (!string.IsNullOrEmpty(dc))
                    _selectedDataCenter = dc;
            }
            _params.TimePeriod = p.TimePeriod;
            _params.SalesAmount = p.SalesAmount;
            _params.AveragePrice = p.AveragePrice;
            _params.Filters = p.Filters.ToArray();
            _params.SortBy = p.SortBy;
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
                ImGui.OpenPopup("Marketshare filters");
                _showFiltersPopup = false;
            }
            if (!ImGui.BeginPopup("Marketshare filters"))
                return;

            int count = _params.Filters?.Length ?? 0;
            ImGui.Text($"Filters Selected: {count}");
            ImGui.Separator();
            ItemFilterListHelper.RenderFilterList(
                "##ms_filter_list",
                () => _params.Filters ?? Array.Empty<int>(),
                val => _params.Filters = val);
            if (ImGui.Button("Close"))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        private void StartScan()
        {
            _params.Server = _params.Server.Trim();
            if (string.IsNullOrEmpty(_params.Server))
            {
                lock (_scanLock) { _errorMessage = "Set World first."; }
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
                    var list = await _api.MarketshareAsync(_params, CancellationToken.None).ConfigureAwait(false);
                    lock (_scanLock)
                    {
                        _scanResults = list ?? new List<MarketshareResultItem>();
                        _scanInProgress = false;
                        _resultsWindowOpen = _scanResults.Count > 0;
                    }
                }
                catch (Exception ex)
                {
                    lock (_scanLock)
                    {
                        _errorMessage = ex.Message;
                        _scanInProgress = false;
                    }
                }
            });
        }

        public void Dispose() => _api.Dispose();
    }
}
