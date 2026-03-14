using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using SaddlebagExchange.Models;
using SaddlebagExchange.Services;

namespace SaddlebagExchange.UI
{
    public sealed class MarketshareTab
    {
        private const float InputWidth = 200f;

        private readonly SaddlebagApiService _api = new();
        private readonly object _scanLock = new();
        private MarketshareParams _params = new();
        private List<MarketshareResultItem> _scanResults = new();
        private bool _scanInProgress;
        private string _selectedDataCenter = string.Empty;
        private string _errorMessage = string.Empty;
        private bool _showFiltersPopup;
        private bool _resultsWindowOpen;

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
            if (string.IsNullOrEmpty(server)) return;
            _params.Server = server;
            var dc = WorldList.GetDataCenterForWorld(server);
            if (!string.IsNullOrEmpty(dc))
                _selectedDataCenter = dc;
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
                ImGui.SetTooltip("Choose which item categories to include.\nEach option is sent as a filter ID to the API.");
            ImGui.SameLine();
            ImGui.Text("Item categories");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip("Choose which item categories to include.\nEach option is sent as a filter ID to the API.");

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
            {
                DrawResultsTable(results);
                ImGui.End();
            }
        }

        private void DrawResultsTable(List<MarketshareResultItem> results)
        {
            if (results == null || results.Count == 0) return;
            if (ImGui.BeginTable("##marketshare_results", 6, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollY, new System.Numerics.Vector2(-1, -1)))
            {
                ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
                ImGui.TableSetupColumn("Market Value", ImGuiTableColumnFlags.WidthFixed, 100f);
                ImGui.TableSetupColumn("Avg", ImGuiTableColumnFlags.WidthFixed, 80f);
                ImGui.TableSetupColumn("Median", ImGuiTableColumnFlags.WidthFixed, 80f);
                ImGui.TableSetupColumn("Qty Sold", ImGuiTableColumnFlags.WidthFixed, 70f);
                ImGui.TableSetupColumn("", ImGuiTableColumnFlags.WidthFixed, 60f);
                ImGui.TableHeadersRow();

                foreach (var row in results)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(!string.IsNullOrEmpty(row.ItemName) ? row.ItemName : row.ItemId.ToString());
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(row.MarketValue.ToString("N0"));
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(row.Avg.ToString("N0"));
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(row.Median.ToString("N0"));
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(row.QuantitySold.ToString());
                    ImGui.TableNextColumn();
                    if (ImGui.SmallButton("Link"))
                    {
                        try
                        {
                            if (OperatingSystem.IsWindows())
                                Process.Start(new ProcessStartInfo(row.SaddlebagUrl) { UseShellExecute = true });
                        }
                        catch { /* ignore */ }
                    }
                }
                ImGui.EndTable();
            }
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

        private void ApplyPreset(MarketshareParams p)
        {
            _params.Server = p.Server;
            _params.TimePeriod = p.TimePeriod;
            _params.SalesAmount = p.SalesAmount;
            _params.AveragePrice = p.AveragePrice;
            _params.Filters = p.Filters.ToArray();
            _params.SortBy = p.SortBy;
            if (!string.IsNullOrEmpty(p.Server))
            {
                _selectedDataCenter = WorldList.GetDataCenterForWorld(p.Server) ?? string.Empty;
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
                ImGui.OpenPopup("Marketshare filters");
                _showFiltersPopup = false;
            }
            if (!ImGui.BeginPopup("Marketshare filters"))
                return;

            int count = _params.Filters?.Length ?? 0;
            ImGui.Text($"Filters Selected: {count}");
            ImGui.Separator();
            if (ImGui.BeginChild("##ms_filter_list", new System.Numerics.Vector2(320, 400), true))
            {
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
                        if (isChecked) list.Add(id);
                        else list.Remove(id);
                        _params.Filters = list.ToArray();
                    }
                }
                ImGui.EndChild();
            }
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
    }
}
