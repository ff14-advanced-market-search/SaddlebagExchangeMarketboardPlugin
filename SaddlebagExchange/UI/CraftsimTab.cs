using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
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
        private const int SearchBufferSize = 128;
        private readonly SaddlebagApiService _api = new();
        private volatile ScanState<CraftsimResultItem> _state = ScanState<CraftsimResultItem>.Idle;
        private CraftsimParams _params = GetDefaultParams();
        private string _homeServerBuffer = string.Empty;
        private string _selectedDataCenter = string.Empty;
        private bool _showFiltersPopup;
        private bool _showJobsPopup;
        private CraftsimResultsWindow? _resultsWindow;
        private volatile bool _requestOpenResultsWindow;
        private int _sortColumnIndex = -1;
        private bool _sortAscending = true;
        private bool _showColumnsPopup;
        private readonly byte[] _searchBuffer = new byte[SearchBufferSize];
        private int _tableIdCounter;
        private string? _copyNotificationText;
        private DateTime _copyNotificationUntil;
        private readonly List<int> _columnOrder = new();
        private readonly bool[] _columnVisible = new bool[(int)CsResultColumn._Count];

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

        private static readonly (string Label, string Description, CraftsimParams Params)[] Presets =
        {
            ("Default Search", "Default search for profitable items to craft.", new CraftsimParams
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
            }),
            ("Fast Selling Commodity Items", "Search for fast selling commodities items to craft.", new CraftsimParams
            {
                CostMetric = "material_median_cost",
                RevenueMetric = "revenue_region_min_listing",
                SalesPerWeek = 150,
                MedianSalePrice = 500,
                MaxMaterialCost = 10000,
                Jobs = new[] { 0 },
                Filters = new[] { 0, -5 },
                Stars = -1,
                LvlLowerLimit = -1,
                LvlUpperLimit = 1000,
                Yields = 2,
                HideExpertRecipes = true,
                HomeServer = string.Empty
            }),
            ("Food Items", "Find food items that sell fast and in bulk.", new CraftsimParams
            {
                CostMetric = "material_median_cost",
                RevenueMetric = "revenue_home_min_listing",
                SalesPerWeek = 750,
                MedianSalePrice = 1000,
                MaxMaterialCost = 10000,
                Jobs = new[] { 0 },
                Filters = new[] { 5, 43, 44, 45, 46 },
                Stars = -1,
                LvlLowerLimit = -1,
                LvlUpperLimit = 1000,
                Yields = -1,
                HideExpertRecipes = true,
                HomeServer = string.Empty
            }),
            ("Food Items (Trained Eye)", "Find food items that can be crafted 100% HQ using the level 80 crafter skill, Trained Eye.", new CraftsimParams
            {
                CostMetric = "material_median_cost",
                RevenueMetric = "revenue_home_min_listing",
                SalesPerWeek = 400,
                MedianSalePrice = 1000,
                MaxMaterialCost = 10000,
                Jobs = new[] { 0 },
                Filters = new[] { 5, 43, 44, 45, 46 },
                Stars = -1,
                LvlLowerLimit = -1,
                LvlUpperLimit = 90,
                Yields = -1,
                HideExpertRecipes = true,
                HomeServer = string.Empty
            }),
            ("Furniture and Glamour Items", "Find all the worthwhile furniture and glamour items to craft.", new CraftsimParams
            {
                CostMetric = "material_median_cost",
                RevenueMetric = "revenue_home_min_listing",
                SalesPerWeek = 400,
                MedianSalePrice = 50000,
                MaxMaterialCost = 100000,
                Jobs = new[] { 0 },
                Filters = new[] { 7, 56, 57, 58, 59, 60, 65, 66, 67, 68, 69, 70, 71, 72, 74, 75, 79, 80, 81, 82, 90 },
                Stars = -1,
                LvlLowerLimit = -1,
                LvlUpperLimit = 1000,
                Yields = -1,
                HideExpertRecipes = true,
                HomeServer = string.Empty
            }),
            ("Expert Craft Items (Pentameld)", "Find items for pentamelding.", new CraftsimParams
            {
                CostMetric = "material_median_cost",
                RevenueMetric = "revenue_home_min_listing",
                SalesPerWeek = 100,
                MedianSalePrice = 50000,
                MaxMaterialCost = 100000,
                Jobs = new[] { 0 },
                Filters = new[] { 0 },
                Stars = -1,
                LvlLowerLimit = -1,
                LvlUpperLimit = 1000,
                Yields = -1,
                HideExpertRecipes = false,
                HomeServer = string.Empty
            }),
            ("Best Crafted Gear", "Find all the current BiS gear (Diadochos for Combat/Indagators for Crafter/Gatherer).", new CraftsimParams
            {
                CostMetric = "material_median_cost",
                RevenueMetric = "revenue_home_min_listing",
                SalesPerWeek = 500,
                MedianSalePrice = 75000,
                MaxMaterialCost = 200000,
                Jobs = new[] { 0 },
                Filters = new[] { 1, 2, 3, 4 },
                Stars = -1,
                LvlLowerLimit = -1,
                LvlUpperLimit = 1000,
                Yields = -1,
                HideExpertRecipes = true,
                HomeServer = string.Empty
            })
        };

        private enum CsResultColumn
        {
            ItemName = 0,
            ItemId,
            ProfitEst,
            SoldPerWeek,
            YieldsPerCraft,
            Hq,
            CostMinListing,
            CostAvg,
            CostMedian,
            RevenueHomeMin,
            RevenueRegionMin,
            RevenueAvg,
            RevenueMedian,
            Saddlebag,
            Universalis,
            _Count
        }

        private static int[] GetDefaultColumnOrder() => new[]
        {
            (int)CsResultColumn.ItemName,
            (int)CsResultColumn.ProfitEst,
            (int)CsResultColumn.SoldPerWeek,
            (int)CsResultColumn.RevenueHomeMin,
            (int)CsResultColumn.CostMinListing,
            (int)CsResultColumn.RevenueRegionMin,
            (int)CsResultColumn.RevenueMedian,
            (int)CsResultColumn.RevenueAvg,
            (int)CsResultColumn.CostMedian,
            (int)CsResultColumn.CostAvg,
            (int)CsResultColumn.YieldsPerCraft,
            (int)CsResultColumn.Hq,
            (int)CsResultColumn.Saddlebag,
            (int)CsResultColumn.Universalis,
            (int)CsResultColumn.ItemId
        };

        private void EnsureColumnState()
        {
            const int n = (int)CsResultColumn._Count;
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

        private void ApplyPreset(CraftsimParams p)
        {
            _params.CostMetric = p.CostMetric;
            _params.RevenueMetric = p.RevenueMetric;
            _params.SalesPerWeek = p.SalesPerWeek;
            _params.MedianSalePrice = p.MedianSalePrice;
            _params.MaxMaterialCost = p.MaxMaterialCost;
            _params.Jobs = p.Jobs?.ToArray() ?? new[] { 0 };
            _params.Filters = p.Filters?.ToArray() ?? new[] { 0, -5 };
            _params.Stars = p.Stars;
            _params.LvlLowerLimit = p.LvlLowerLimit;
            _params.LvlUpperLimit = p.LvlUpperLimit;
            _params.Yields = p.Yields;
            _params.HideExpertRecipes = p.HideExpertRecipes;
        }

        private void DrawResultsTable(IReadOnlyList<CraftsimResultItem> results)
        {
            EnsureColumnState();
            var visibleCols = _columnOrder.Where(i => _columnVisible[i]).ToList();
            if (visibleCols.Count == 0)
            {
                ImGui.Text("No columns visible. Use Columns to show some.");
                return;
            }

            string searchFilter = Encoding.UTF8.GetString(_searchBuffer).TrimEnd('\0');
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
            var tableSize = new System.Numerics.Vector2(avail.X, Math.Max(220, avail.Y));
            string tableId = "CraftsimResults##" + _tableIdCounter;
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
                ImGui.OpenPopup("Craftsim column options");
                _showColumnsPopup = false;
            }
            if (ImGui.BeginPopup("Craftsim column options"))
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
                        (_columnOrder[i], _columnOrder[i - 1]) = (_columnOrder[i - 1], _columnOrder[i]);
                    ImGui.SameLine();
                    if (ImGui.SmallButton("Down") && i < _columnOrder.Count - 1)
                        (_columnOrder[i], _columnOrder[i + 1]) = (_columnOrder[i + 1], _columnOrder[i]);
                    ImGui.PopID();
                }
                if (ImGui.Button("Close"))
                    ImGui.CloseCurrentPopup();
                ImGui.EndPopup();
            }
        }

        private static bool MatchesSearch(CraftsimResultItem row, string search)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            var term = search.Trim();
            var comparison = StringComparison.OrdinalIgnoreCase;
            return (row.ItemName != null && row.ItemName.Contains(term, comparison))
                || row.ItemId.ToString().Contains(term, comparison);
        }

        private void SetClipboardTextAndNotify(string? text, string? notificationMessage = null)
        {
            if (string.IsNullOrEmpty(text)) return;
            try
            {
                ImGui.SetClipboardText(text);
                if (!string.IsNullOrEmpty(notificationMessage))
                {
                    _copyNotificationText = notificationMessage;
                    _copyNotificationUntil = DateTime.UtcNow + TimeSpan.FromSeconds(2);
                }
            }
            catch { /* ignore */ }
        }

        private static void DrawCell(CraftsimResultItem row, int colId, Action<string?, string?>? copyNotify = null)
        {
            switch ((CsResultColumn)colId)
            {
                case CsResultColumn.ItemName:
                    string name = row.ItemName ?? row.ItemId.ToString();
                    if (ImGui.Selectable(name, false, ImGuiSelectableFlags.None, System.Numerics.Vector2.Zero))
                        copyNotify?.Invoke(name, "Item name copied to clipboard");
                    break;
                case CsResultColumn.ItemId:
                    ImGui.Text(row.ItemId.ToString());
                    break;
                case CsResultColumn.ProfitEst:
                    ImGui.Text(row.ProfitEstimate.ToString("N0"));
                    break;
                case CsResultColumn.SoldPerWeek:
                    ImGui.Text(row.SoldPerWeek.ToString("N0"));
                    break;
                case CsResultColumn.YieldsPerCraft:
                    ImGui.Text(row.YieldsPerCraft.ToString());
                    break;
                case CsResultColumn.Hq:
                    ImGui.Text(row.Hq ? "HQ" : "NQ");
                    break;
                case CsResultColumn.CostMinListing:
                    ImGui.Text(row.CostEstimate.MaterialMinListingCost.ToString("N0"));
                    break;
                case CsResultColumn.CostAvg:
                    ImGui.Text(row.CostEstimate.MaterialAvgCost.ToString("N0"));
                    break;
                case CsResultColumn.CostMedian:
                    ImGui.Text(row.CostEstimate.MaterialMedianCost.ToString("N0"));
                    break;
                case CsResultColumn.RevenueHomeMin:
                    ImGui.Text(row.RevenueEstimate.RevenueHomeMinListing.ToString("N0"));
                    break;
                case CsResultColumn.RevenueRegionMin:
                    ImGui.Text(row.RevenueEstimate.RevenueRegionMinListing.ToString("N0"));
                    break;
                case CsResultColumn.RevenueAvg:
                    ImGui.Text(row.RevenueEstimate.RevenueAvg.ToString("N0"));
                    break;
                case CsResultColumn.RevenueMedian:
                    ImGui.Text(row.RevenueEstimate.RevenueMedian.ToString("N0"));
                    break;
                case CsResultColumn.Saddlebag:
                    if (ImGui.SmallButton("S")) OpenUrl(row.SaddlebagUrl);
                    break;
                case CsResultColumn.Universalis:
                    if (ImGui.SmallButton("U")) OpenUrl(row.UniversalisUrl);
                    break;
                default:
                    break;
            }
        }

        private static string GetColumnHeader(int column)
        {
            return column switch
            {
                (int)CsResultColumn.ItemName => "Item Name",
                (int)CsResultColumn.ItemId => "Item ID",
                (int)CsResultColumn.ProfitEst => "Profit Est",
                (int)CsResultColumn.SoldPerWeek => "Sold Per Week",
                (int)CsResultColumn.YieldsPerCraft => "Yields Per Craft",
                (int)CsResultColumn.Hq => "HQ",
                (int)CsResultColumn.CostMinListing => "Material Min Listing Cost",
                (int)CsResultColumn.CostAvg => "Material Avg Cost",
                (int)CsResultColumn.CostMedian => "Material Median Cost",
                (int)CsResultColumn.RevenueHomeMin => "Revenue Home Min Listing",
                (int)CsResultColumn.RevenueRegionMin => "Revenue Region Min Listing",
                (int)CsResultColumn.RevenueAvg => "Revenue Avg",
                (int)CsResultColumn.RevenueMedian => "Revenue Median",
                (int)CsResultColumn.Saddlebag => "Item Data",
                (int)CsResultColumn.Universalis => "Universalis Link",
                _ => "?"
            };
        }

        private static float GetDefaultColumnWidth(int column)
        {
            return column switch
            {
                (int)CsResultColumn.ItemName => 220f,
                (int)CsResultColumn.ItemId => 80f,
                (int)CsResultColumn.ProfitEst => 120f,
                (int)CsResultColumn.SoldPerWeek => 100f,
                (int)CsResultColumn.YieldsPerCraft => 90f,
                (int)CsResultColumn.Hq => 60f,
                (int)CsResultColumn.CostMinListing => 130f,
                (int)CsResultColumn.CostAvg => 130f,
                (int)CsResultColumn.CostMedian => 130f,
                (int)CsResultColumn.RevenueHomeMin => 140f,
                (int)CsResultColumn.RevenueRegionMin => 140f,
                (int)CsResultColumn.RevenueAvg => 120f,
                (int)CsResultColumn.RevenueMedian => 120f,
                (int)CsResultColumn.Saddlebag => 70f,
                (int)CsResultColumn.Universalis => 70f,
                _ => 100f
            };
        }

        private List<CraftsimResultItem> SortResults(IReadOnlyList<CraftsimResultItem> results)
        {
            if (_sortColumnIndex < 0 || _sortColumnIndex >= (int)CsResultColumn._Count)
                return results.ToList();
            var list = new List<CraftsimResultItem>(results);
            int dir = _sortAscending ? 1 : -1;
            list.Sort((a, b) =>
            {
                int c = _sortColumnIndex switch
                {
                    (int)CsResultColumn.ItemName => string.Compare(a.ItemName ?? "", b.ItemName ?? "", StringComparison.Ordinal),
                    (int)CsResultColumn.ItemId => a.ItemId.CompareTo(b.ItemId),
                    (int)CsResultColumn.ProfitEst => a.ProfitEstimate.CompareTo(b.ProfitEstimate),
                    (int)CsResultColumn.SoldPerWeek => a.SoldPerWeek.CompareTo(b.SoldPerWeek),
                    (int)CsResultColumn.YieldsPerCraft => a.YieldsPerCraft.CompareTo(b.YieldsPerCraft),
                    (int)CsResultColumn.Hq => a.Hq.CompareTo(b.Hq),
                    (int)CsResultColumn.CostMinListing => a.CostEstimate.MaterialMinListingCost.CompareTo(b.CostEstimate.MaterialMinListingCost),
                    (int)CsResultColumn.CostAvg => a.CostEstimate.MaterialAvgCost.CompareTo(b.CostEstimate.MaterialAvgCost),
                    (int)CsResultColumn.CostMedian => a.CostEstimate.MaterialMedianCost.CompareTo(b.CostEstimate.MaterialMedianCost),
                    (int)CsResultColumn.RevenueHomeMin => a.RevenueEstimate.RevenueHomeMinListing.CompareTo(b.RevenueEstimate.RevenueHomeMinListing),
                    (int)CsResultColumn.RevenueRegionMin => a.RevenueEstimate.RevenueRegionMinListing.CompareTo(b.RevenueEstimate.RevenueRegionMinListing),
                    (int)CsResultColumn.RevenueAvg => a.RevenueEstimate.RevenueAvg.CompareTo(b.RevenueEstimate.RevenueAvg),
                    (int)CsResultColumn.RevenueMedian => a.RevenueEstimate.RevenueMedian.CompareTo(b.RevenueEstimate.RevenueMedian),
                    _ => 0
                };
                return c * dir;
            });
            return list;
        }

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
