using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Utility;
using SaddlebagExchange.Models;
using SaddlebagExchange.Services;

namespace SaddlebagExchange.UI
{
    public sealed class CraftsimTab : IDisposable
    {
        private const float InputWidth = 220f;
        private readonly SaddlebagApiService _api = new();
        private volatile ScanState<CraftsimResultItem> _state = ScanState<CraftsimResultItem>.Idle;
        private CraftsimParams _params = GetDefaultParams();
        private string _homeServerBuffer = string.Empty;
        private string _selectedDataCenter = string.Empty;
        private bool _showFiltersPopup;
        private bool _showJobsPopup;
        private CraftsimResultsWindow? _resultsWindow;
        private volatile bool _requestOpenResultsWindow;

        private static readonly (string Value, string Label)[] CostMetricOptions =
        {
            ("material_median_cost", "Regional Median Price"),
            ("material_avg_cost", "Regional Average Price"),
            ("material_min_listing_cost", "Regional Minimum Price")
        };

        private static readonly (string Value, string Label)[] RevenueMetricOptions =
        {
            ("revenue_home_min_listing", "Home Minimum Price"),
            ("revenue_region_min_listing", "Regional Minimum Price"),
            ("revenue_median", "Regional Median Price"),
            ("revenue_avg", "Regional Average Price")
        };

        private static readonly (int Value, string Label)[] JobOptions =
        {
            (0, "Omnicrafter"),
            (8, "Carpenter"),
            (9, "Blacksmith"),
            (10, "Armorer"),
            (11, "Goldsmith"),
            (12, "Leatherworker"),
            (13, "Weaver"),
            (14, "Alchemist"),
            (15, "Culinarian")
        };

        private static CraftsimParams GetDefaultParams() => new()
        {
            CostMetric = "material_median_cost",
            RevenueMetric = "revenue_region_min_listing",
            SalesPerWeek = 100,
            MedianSalePrice = 50000,
            MaxMaterialCost = 100000,
            Jobs = new[] { 0 },
            Filters = new[] { 0, -5 },
            Stars = -1,
            LvlLowerLimit = -1,
            LvlUpperLimit = 1000,
            Yields = -1,
            HideExpertRecipes = true,
            HomeServer = string.Empty
        };

        public void SetDefaultHomeServer(string? homeServer)
        {
            var s = homeServer ?? string.Empty;
            _params.HomeServer = s;
            _homeServerBuffer = s;
            var dc = WorldList.GetDataCenterForWorld(s);
            if (!string.IsNullOrEmpty(dc))
                _selectedDataCenter = dc;
        }

        public void Draw()
        {
            var snapshot = _state;
            string errorMessage = snapshot.Error ?? string.Empty;

            ImGui.Spacing();
            ImGui.Text("Craftsim");
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Search");
            ImGui.Separator();

            int costMetricIndex = Array.FindIndex(CostMetricOptions, o => o.Value == _params.CostMetric);
            if (costMetricIndex < 0) costMetricIndex = 0;
            ImGui.SetNextItemWidth(InputWidth);
            if (ImGui.Combo("Cost metric", ref costMetricIndex, CostMetricOptions.Select(o => o.Label).ToArray(), CostMetricOptions.Length))
                _params.CostMetric = CostMetricOptions[costMetricIndex].Value;

            int revenueMetricIndex = Array.FindIndex(RevenueMetricOptions, o => o.Value == _params.RevenueMetric);
            if (revenueMetricIndex < 0) revenueMetricIndex = 0;
            ImGui.SetNextItemWidth(InputWidth);
            if (ImGui.Combo("Revenue metric", ref revenueMetricIndex, RevenueMetricOptions.Select(o => o.Label).ToArray(), RevenueMetricOptions.Length))
                _params.RevenueMetric = RevenueMetricOptions[revenueMetricIndex].Value;

            int salesPerWeek = _params.SalesPerWeek;
            ImGui.SetNextItemWidth(InputWidth);
            ImGui.InputInt("Min sales per week", ref salesPerWeek, 10, 50);
            _params.SalesPerWeek = Math.Max(0, salesPerWeek);

            int medianSalePrice = _params.MedianSalePrice;
            ImGui.SetNextItemWidth(InputWidth);
            ImGui.InputInt("Min median sale price", ref medianSalePrice, 1000, 10000);
            _params.MedianSalePrice = Math.Max(0, medianSalePrice);

            int maxMaterialCost = _params.MaxMaterialCost;
            ImGui.SetNextItemWidth(InputWidth);
            ImGui.InputInt("Max material cost", ref maxMaterialCost, 1000, 10000);
            _params.MaxMaterialCost = Math.Max(0, maxMaterialCost);

            int stars = _params.Stars;
            ImGui.SetNextItemWidth(InputWidth);
            ImGui.InputInt("Stars (-1 any)", ref stars, 1, 2);
            _params.Stars = Math.Max(-1, stars);

            int lvlLower = _params.LvlLowerLimit;
            ImGui.SetNextItemWidth(InputWidth);
            ImGui.InputInt("Min level (-1 any)", ref lvlLower, 1, 5);
            _params.LvlLowerLimit = Math.Max(-1, lvlLower);

            int lvlUpper = _params.LvlUpperLimit;
            ImGui.SetNextItemWidth(InputWidth);
            ImGui.InputInt("Max level", ref lvlUpper, 1, 5);
            _params.LvlUpperLimit = Math.Max(2, lvlUpper);

            int yields = _params.Yields;
            ImGui.SetNextItemWidth(InputWidth);
            ImGui.InputInt("Yields (-1 any)", ref yields, 1, 2);
            _params.Yields = Math.Max(-1, yields);

            bool hideExpert = _params.HideExpertRecipes;
            ImGui.Checkbox("Hide expert recipes", ref hideExpert);
            _params.HideExpertRecipes = hideExpert;

            int filterCount = _params.Filters?.Length ?? 0;
            if (ImGui.Button($"Filters ({filterCount})"))
                _showFiltersPopup = true;

            ImGui.SameLine();
            int jobsCount = _params.Jobs?.Length ?? 0;
            if (ImGui.Button($"Jobs ({jobsCount})"))
                _showJobsPopup = true;

            DrawHomeServerCombo();

            ImGui.Spacing();
            bool doSearch = ImGui.Button("Search");
            ImGui.SameLine();
            if (!string.IsNullOrEmpty(errorMessage))
                ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f), errorMessage);

            if (doSearch)
                StartScan();

            DrawFiltersPopup();
            DrawJobsPopup();

            ImGui.Spacing();
            ImGui.Separator();
            ImGui.Spacing();

            if (snapshot.Loading)
            {
                ImGui.Text("Searching...");
                return;
            }

            if (snapshot.Results.IsDefaultOrEmpty)
            {
                ImGui.Text("Run a search to see results.");
                return;
            }

            if (_requestOpenResultsWindow && _resultsWindow != null)
            {
                _resultsWindow.IsOpen = true;
                _requestOpenResultsWindow = false;
            }
        }

        public void DrawResultsContent()
        {
            var results = _state.Results;
            if (results.IsDefaultOrEmpty)
            {
                ImGui.Text("Run a search to see results.");
                return;
            }
            DrawResultsTable(results);
        }

        public void SetResultsWindow(CraftsimResultsWindow? window) => _resultsWindow = window;

        private void DrawHomeServerCombo()
        {
            var all = WorldList.GetAll();
            if (all.Length == 0)
            {
                ImGui.InputText("Home server", ref _homeServerBuffer, 64);
                return;
            }

            var dcs = WorldList.GetDataCenters();
            if (string.IsNullOrEmpty(_selectedDataCenter) || !dcs.Contains(_selectedDataCenter))
                _selectedDataCenter = dcs[0];

            int dcIdx = Array.IndexOf(dcs, _selectedDataCenter);
            if (dcIdx < 0) dcIdx = 0;

            ImGui.SetNextItemWidth(InputWidth);
            if (ImGui.Combo("Data Center", ref dcIdx, dcs, dcs.Length))
                _selectedDataCenter = dcs[dcIdx];

            var worlds = WorldList.GetWorlds(_selectedDataCenter);
            int worldIdx = Array.FindIndex(worlds, w => string.Equals(w, _homeServerBuffer, StringComparison.OrdinalIgnoreCase));
            if (worldIdx < 0) worldIdx = 0;

            ImGui.SetNextItemWidth(InputWidth);
            if (ImGui.Combo("Home server", ref worldIdx, worlds, worlds.Length))
            {
                _homeServerBuffer = worlds[worldIdx];
                _params.HomeServer = _homeServerBuffer;
            }
        }

        private void DrawFiltersPopup()
        {
            if (_showFiltersPopup)
            {
                ImGui.OpenPopup("Craftsim filters");
                _showFiltersPopup = false;
            }
            if (!ImGui.BeginPopup("Craftsim filters"))
                return;

            int count = _params.Filters?.Length ?? 0;
            ImGui.Text($"Filters Selected: {count}");
            ImGui.Separator();
            ItemFilterListHelper.RenderFilterList(
                "##craftsim_filter_list",
                () => _params.Filters ?? Array.Empty<int>(),
                val => _params.Filters = val);
            if (ImGui.Button("Close"))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        private void DrawJobsPopup()
        {
            if (_showJobsPopup)
            {
                ImGui.OpenPopup("Craftsim jobs");
                _showJobsPopup = false;
            }
            if (!ImGui.BeginPopup("Craftsim jobs"))
                return;

            var selected = new HashSet<int>(_params.Jobs ?? Array.Empty<int>());
            foreach (var (value, label) in JobOptions)
            {
                bool isChecked = selected.Contains(value);
                if (ImGui.Checkbox(label, ref isChecked))
                {
                    if (value == 0 && isChecked)
                    {
                        _params.Jobs = new[] { 0 };
                        continue;
                    }

                    var list = (_params.Jobs ?? Array.Empty<int>()).ToList();
                    if (isChecked)
                    {
                        list.Remove(0);
                        if (!list.Contains(value))
                            list.Add(value);
                    }
                    else
                    {
                        list.Remove(value);
                        if (list.Count == 0)
                            list.Add(0);
                    }
                    _params.Jobs = list.ToArray();
                }
            }

            if (ImGui.Button("Close"))
                ImGui.CloseCurrentPopup();
            ImGui.EndPopup();
        }

        private void StartScan()
        {
            _params.HomeServer = _homeServerBuffer.Trim();
            if (string.IsNullOrEmpty(_params.HomeServer))
            {
                _state = _state with { Error = "Set Home server first." };
                return;
            }

            var paramsCopy = new CraftsimParams
            {
                HomeServer = _params.HomeServer,
                CostMetric = _params.CostMetric,
                RevenueMetric = _params.RevenueMetric,
                SalesPerWeek = _params.SalesPerWeek,
                MedianSalePrice = _params.MedianSalePrice,
                MaxMaterialCost = _params.MaxMaterialCost,
                Jobs = _params.Jobs?.ToArray() ?? new[] { 0 },
                Filters = _params.Filters?.ToArray() ?? new[] { 0, -5 },
                Stars = _params.Stars,
                LvlLowerLimit = _params.LvlLowerLimit,
                LvlUpperLimit = _params.LvlUpperLimit,
                Yields = _params.Yields,
                HideExpertRecipes = _params.HideExpertRecipes
            };

            _state = _state with { Loading = true, Error = string.Empty };
            _ = Task.Run(async () =>
            {
                try
                {
                    var list = await _api.CraftsimAsync(paramsCopy).ConfigureAwait(false);
                    var results = (list ?? new List<CraftsimResultItem>()).ToImmutableArray();
                    _state = new ScanState<CraftsimResultItem>(false, results, string.Empty);
                    if (results.Length > 0) _requestOpenResultsWindow = true;
                }
                catch (Exception ex)
                {
                    _state = new ScanState<CraftsimResultItem>(false, ImmutableArray<CraftsimResultItem>.Empty, ex.Message);
                }
            });
        }

        private static void DrawResultsTable(IReadOnlyList<CraftsimResultItem> results)
        {
            var avail = ImGui.GetContentRegionAvail();
            var tableSize = new System.Numerics.Vector2(avail.X, Math.Max(220, avail.Y));
            if (!ImGui.BeginTable("CraftsimResults", 10, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY | ImGuiTableFlags.ScrollX, tableSize))
                return;

            ImGui.TableSetupColumn("Item");
            ImGui.TableSetupColumn("Item ID");
            ImGui.TableSetupColumn("Profit Est");
            ImGui.TableSetupColumn("Sold/Week");
            ImGui.TableSetupColumn("Yields");
            ImGui.TableSetupColumn("HQ");
            ImGui.TableSetupColumn("Material Cost");
            ImGui.TableSetupColumn("Revenue");
            ImGui.TableSetupColumn("Saddlebag");
            ImGui.TableSetupColumn("Universalis");
            ImGui.TableHeadersRow();

            int rowIndex = 0;
            foreach (var row in results)
            {
                ImGui.PushID(rowIndex++);
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.ItemName ?? row.ItemId.ToString());

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.ItemId.ToString());

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(FormatGil(row.ProfitEstimate));

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.SoldPerWeek.ToString());

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.YieldsPerCraft.ToString());

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(row.Hq ? "HQ" : "NQ");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(FormatGil(GetCostValue(row)));

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(FormatGil(GetRevenueValue(row)));

                ImGui.TableNextColumn();
                if (ImGui.SmallButton("Open##sb"))
                    OpenUrl(row.SaddlebagUrl);

                ImGui.TableNextColumn();
                if (ImGui.SmallButton("Open##uni"))
                    OpenUrl(row.UniversalisUrl);

                ImGui.PopID();
            }

            ImGui.EndTable();
        }

        private static long GetCostValue(CraftsimResultItem row)
        {
            return Math.Min(
                row.CostEstimate.MaterialMinListingCost,
                Math.Min(row.CostEstimate.MaterialAvgCost, row.CostEstimate.MaterialMedianCost));
        }

        private static long GetRevenueValue(CraftsimResultItem row)
        {
            return Math.Max(
                row.RevenueEstimate.RevenueHomeMinListing,
                Math.Max(row.RevenueEstimate.RevenueAvg, Math.Max(row.RevenueEstimate.RevenueMedian, row.RevenueEstimate.RevenueRegionMinListing)));
        }

        private static string FormatGil(long value) => string.Format("{0:N0}", value);

        private static void OpenUrl(string? url)
        {
            if (string.IsNullOrEmpty(url)) return;
            Util.OpenLink(url);
        }

        public void Dispose() => _api.Dispose();

        private sealed record ScanState<T>(bool Loading, ImmutableArray<T> Results, string Error)
        {
            public static ScanState<T> Idle => new(false, ImmutableArray<T>.Empty, string.Empty);
        }
    }
}
