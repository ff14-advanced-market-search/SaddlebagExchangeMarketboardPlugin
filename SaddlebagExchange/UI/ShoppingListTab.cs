using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Dalamud.Plugin.Services;
using Lumina.Excel.Sheets;
using SaddlebagExchange.Models;
using SaddlebagExchange.Services;

namespace SaddlebagExchange.UI
{
    public sealed class ShoppingListTab : IDisposable
    {
        private const float InputWidth = 260f;
        private const int MaxShoppingItems = 10;
        private const int SearchBufferSize = 128;
        private readonly SaddlebagApiService _api = new();
        private volatile ScanState _state = ScanState.Idle;
        private readonly List<SearchItemEntry> _items = [];
        private readonly List<ShoppingFormItem> _shoppingList = [];
        private string _itemSearchInput = string.Empty;
        private string _selectedDataCenter = string.Empty;
        private string _homeServerBuffer = string.Empty;
        private bool _regionWide;
        private ShoppingListResultsWindow? _resultsWindow;
        private volatile bool _requestOpenResultsWindow;
        private readonly byte[] _searchBuffer = new byte[SearchBufferSize];
        private int _sortColumnIndex = -1;
        private bool _sortAscending = true;
        private bool _showColumnsPopup;
        private int _tableIdCounter;
        private readonly List<int> _columnOrder = [];
        private readonly bool[] _columnVisible = new bool[(int)ShoppingResultColumn._Count];

        private static readonly (int Value, string Label)[] JobOptions =
        {
            (0, "Omnicrafter with max in all jobs"),
            (8, "Carpenter"),
            (9, "Blacksmith"),
            (10, "Armorer"),
            (11, "Goldsmith"),
            (12, "Leatherworker"),
            (13, "Weaver"),
            (14, "Alchemist"),
            (15, "Culinarian")
        };

        private enum ShoppingResultColumn
        {
            ItemName = 0,
            PricePerUnit,
            Quantity,
            World,
            HighQuality,
            _Count
        }

        public ShoppingListTab(IDataManager dataManager)
        {
            LoadItemNames(dataManager);
        }

        public void SetDefaultHomeServer(string? homeServer)
        {
            var s = homeServer ?? string.Empty;
            _homeServerBuffer = s;
            var dc = WorldList.GetDataCenterForWorld(s);
            if (!string.IsNullOrEmpty(dc))
                _selectedDataCenter = dc;
        }

        public void SetResultsWindow(ShoppingListResultsWindow? window) => _resultsWindow = window;

        public void Draw()
        {
            var snapshot = _state;
            string errorMessage = snapshot.Error ?? string.Empty;

            ImGui.Spacing();
            ImGui.Text("Shopping list generator");
            ImGui.Separator();
            ImGui.Spacing();

            ImGui.Text("Find Items");
            ImGui.SameLine();
            DrawHelpMarker("Add up to 10 items to find crafting ingredients for.");

            ImGui.SetNextItemWidth(InputWidth * 1.4f * ImGuiHelpers.GlobalScale);
            ImGui.InputText("##shopping_item_search", ref _itemSearchInput, 128);
            DrawItemSuggestions();

            ImGui.Spacing();
            DrawShoppingListTable();

            ImGui.Checkbox("Use Region Wide Search", ref _regionWide);
            ImGui.SameLine();
            DrawHelpMarker("If enabled, searches region-wide for materials. If disabled, uses your data center scope.");

            DrawHomeServerCombo();

            ImGui.Spacing();
            bool doSearch = ImGui.Button("Search");
            ImGui.SameLine();
            if (!string.IsNullOrEmpty(errorMessage))
                ImGui.TextColored(new System.Numerics.Vector4(1f, 0.4f, 0.4f, 1f), errorMessage);

            if (doSearch)
                StartSearch();

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
                ImGui.Text("Add items and run a search to see results.");
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
            var snapshot = _state;
            if (snapshot.Results.IsDefaultOrEmpty)
            {
                ImGui.Text("Run a search to see results.");
                return;
            }

            EnsureColumnState();
            var visibleCols = _columnOrder.Where(i => _columnVisible[i]).ToList();
            if (visibleCols.Count == 0)
            {
                ImGui.Text("No columns visible. Use Columns to show some.");
                return;
            }

            var results = snapshot.Results;
            string searchFilter = Encoding.UTF8.GetString(_searchBuffer).TrimEnd('\0');
            var sorted = SortResults(results);
            var filtered = string.IsNullOrWhiteSpace(searchFilter)
                ? sorted
                : sorted.Where(r => MatchesSearch(r, searchFilter)).ToList();

            ImGui.Text($"Average cost per craft: {snapshot.AverageCostPerCraft:N0}");
            ImGui.Text($"Total cost: {snapshot.TotalCost:N0}");
            ImGui.Spacing();

            ImGui.InputText("Search", _searchBuffer, ImGuiInputTextFlags.None);
            string countText = string.IsNullOrWhiteSpace(searchFilter)
                ? $"Results: {results.Length} items (click header to sort)"
                : $"Results: {results.Length} items ({filtered.Count} matching)";
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
            var tableSize = new System.Numerics.Vector2(avail.X, Math.Max(220, avail.Y));
            string tableId = "ShoppingListResults##" + _tableIdCounter;
            using (var table = ImRaii.Table(tableId, visibleCols.Count, ImGuiTableFlags.Borders | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable | ImGuiTableFlags.ScrollY | ImGuiTableFlags.ScrollX, tableSize))
            {
                if (!table.Success)
                    return;

                foreach (int colId in visibleCols)
                    ImGui.TableSetupColumn(GetColumnHeader(colId), ImGuiTableColumnFlags.WidthFixed, GetDefaultColumnWidth(colId), (uint)colId);
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
                    ImGui.PushID(rowIndex++);
                    ImGui.TableNextRow();
                    foreach (int colId in visibleCols)
                    {
                        ImGui.TableNextColumn();
                        DrawResultCell(row, colId);
                    }
                    ImGui.PopID();
                }
            }

            if (_showColumnsPopup)
            {
                ImGui.OpenPopup("Shopping column options");
                _showColumnsPopup = false;
            }
            using (var colPopup = ImRaii.Popup("Shopping column options"))
            {
                if (colPopup.Success)
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
                }
            }

            ImGui.Spacing();
            ImGui.Text($"{filtered.Count} results found | Average cost per craft: {snapshot.AverageCostPerCraft:N0} | Total cost: {snapshot.TotalCost:N0}");
        }

        private static int[] GetDefaultColumnOrder() =>
        [
            (int)ShoppingResultColumn.ItemName,
            (int)ShoppingResultColumn.PricePerUnit,
            (int)ShoppingResultColumn.Quantity,
            (int)ShoppingResultColumn.World,
            (int)ShoppingResultColumn.HighQuality
        ];

        private void EnsureColumnState()
        {
            const int n = (int)ShoppingResultColumn._Count;
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

        private static bool MatchesSearch(ShoppingListResultItem row, string search)
        {
            if (string.IsNullOrWhiteSpace(search)) return true;
            var term = search.Trim();
            return (row.Name != null && row.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                || (row.WorldName != null && row.WorldName.Contains(term, StringComparison.OrdinalIgnoreCase))
                || row.ItemId.ToString().Contains(term, StringComparison.OrdinalIgnoreCase);
        }

        private static void DrawResultCell(ShoppingListResultItem row, int colId)
        {
            switch ((ShoppingResultColumn)colId)
            {
                case ShoppingResultColumn.ItemName:
                    ImGui.Text(row.Name ?? row.ItemId.ToString());
                    break;
                case ShoppingResultColumn.PricePerUnit:
                    ImGui.Text(row.PricePerUnit.ToString("N0"));
                    break;
                case ShoppingResultColumn.Quantity:
                    ImGui.Text(row.Quantity.ToString("N0"));
                    break;
                case ShoppingResultColumn.World:
                    ImGui.Text(row.WorldName ?? "-");
                    break;
                case ShoppingResultColumn.HighQuality:
                    ImGui.Text(row.Hq ? "Yes" : "false");
                    break;
            }
        }

        private static string GetColumnHeader(int column)
        {
            return column switch
            {
                (int)ShoppingResultColumn.ItemName => "Item Name",
                (int)ShoppingResultColumn.PricePerUnit => "Price Per Unit",
                (int)ShoppingResultColumn.Quantity => "Quantity",
                (int)ShoppingResultColumn.World => "World",
                (int)ShoppingResultColumn.HighQuality => "High Quality",
                _ => "?"
            };
        }

        private static float GetDefaultColumnWidth(int column)
        {
            return column switch
            {
                (int)ShoppingResultColumn.ItemName => 220f,
                (int)ShoppingResultColumn.PricePerUnit => 120f,
                (int)ShoppingResultColumn.Quantity => 100f,
                (int)ShoppingResultColumn.World => 160f,
                (int)ShoppingResultColumn.HighQuality => 90f,
                _ => 100f
            };
        }

        private List<ShoppingListResultItem> SortResults(IReadOnlyList<ShoppingListResultItem> results)
        {
            if (_sortColumnIndex < 0 || _sortColumnIndex >= (int)ShoppingResultColumn._Count)
                return results.ToList();
            var list = new List<ShoppingListResultItem>(results);
            int dir = _sortAscending ? 1 : -1;
            list.Sort((a, b) =>
            {
                int c = _sortColumnIndex switch
                {
                    (int)ShoppingResultColumn.ItemName => string.Compare(a.Name ?? "", b.Name ?? "", StringComparison.Ordinal),
                    (int)ShoppingResultColumn.PricePerUnit => a.PricePerUnit.CompareTo(b.PricePerUnit),
                    (int)ShoppingResultColumn.Quantity => a.Quantity.CompareTo(b.Quantity),
                    (int)ShoppingResultColumn.World => string.Compare(a.WorldName ?? "", b.WorldName ?? "", StringComparison.Ordinal),
                    (int)ShoppingResultColumn.HighQuality => a.Hq.CompareTo(b.Hq),
                    _ => 0
                };
                return c * dir;
            });
            return list;
        }

        private void DrawItemSuggestions()
        {
            if (string.IsNullOrWhiteSpace(_itemSearchInput))
                return;

            if (_shoppingList.Count >= MaxShoppingItems)
            {
                ImGui.TextDisabled("Maximum 10 items reached.");
                return;
            }

            var term = _itemSearchInput.Trim();
            var matches = _items
                .Where(x => x.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .Take(25)
                .ToList();
            if (matches.Count == 0)
                return;

            var childH = Math.Min(180f, 24f * matches.Count + 8f);
            using (var child = ImRaii.Child("##shopping_item_suggestions", new System.Numerics.Vector2(InputWidth * 1.4f * ImGuiHelpers.GlobalScale, childH), true))
            {
                if (child.Success)
                {
                    foreach (var match in matches)
                    {
                        if (ImGui.Selectable(match.Name))
                        {
                            AddItem(match);
                            _itemSearchInput = string.Empty;
                            break;
                        }
                    }
                }
            }
        }

        private void DrawShoppingListTable()
        {
            if (_shoppingList.Count == 0)
                return;

            using var formTable = ImRaii.Table("##shopping_form_table", 5, ImGuiTableFlags.BordersInnerH | ImGuiTableFlags.RowBg | ImGuiTableFlags.Resizable);
            if (!formTable.Success)
                return;

            ImGui.TableSetupColumn("Item", ImGuiTableColumnFlags.WidthStretch);
            ImGui.TableSetupColumn("Craft Amount", ImGuiTableColumnFlags.WidthFixed, 110f);
            ImGui.TableSetupColumn("High Quality", ImGuiTableColumnFlags.WidthFixed, 90f);
            ImGui.TableSetupColumn("Job", ImGuiTableColumnFlags.WidthFixed, 240f);
            ImGui.TableSetupColumn("Remove Item", ImGuiTableColumnFlags.WidthFixed, 100f);
            ImGui.TableHeadersRow();

            int removeIndex = -1;
            for (int i = 0; i < _shoppingList.Count; i++)
            {
                var row = _shoppingList[i];
                ImGui.PushID($"shop_row_{i}");
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.Text(row.Name);

                ImGui.TableNextColumn();
                int craftAmount = row.CraftAmount;
                ImGui.SetNextItemWidth(80f * ImGuiHelpers.GlobalScale);
                ImGui.InputInt("##craft_amount", ref craftAmount, 1, 5);
                row.CraftAmount = Math.Max(1, craftAmount);

                ImGui.TableNextColumn();
                bool hq = row.Hq;
                ImGui.Checkbox("##hq", ref hq);
                row.Hq = hq;

                ImGui.TableNextColumn();
                int jobIndex = Array.FindIndex(JobOptions, x => x.Value == row.Job);
                if (jobIndex < 0) jobIndex = 0;
                ImGui.SetNextItemWidth(220f * ImGuiHelpers.GlobalScale);
                if (ImGui.Combo("##job", ref jobIndex, JobOptions.Select(x => x.Label).ToArray()))
                    row.Job = JobOptions[jobIndex].Value;

                ImGui.TableNextColumn();
                if (ImGui.Button("Remove"))
                    removeIndex = i;

                _shoppingList[i] = row;
                ImGui.PopID();
            }

            if (removeIndex >= 0)
                _shoppingList.RemoveAt(removeIndex);
        }

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

            ImGui.SetNextItemWidth(InputWidth * ImGuiHelpers.GlobalScale);
            if (ImGui.Combo("Data Center", ref dcIdx, dcs))
                _selectedDataCenter = dcs[dcIdx];

            var worlds = WorldList.GetWorlds(_selectedDataCenter);
            int worldIdx = Array.FindIndex(worlds, w => string.Equals(w, _homeServerBuffer, StringComparison.OrdinalIgnoreCase));
            if (worldIdx < 0) worldIdx = 0;
            ImGui.SetNextItemWidth(InputWidth * ImGuiHelpers.GlobalScale);
            if (ImGui.Combo("Home server", ref worldIdx, worlds))
                _homeServerBuffer = worlds[worldIdx];
        }

        private void AddItem(SearchItemEntry item)
        {
            if (_shoppingList.Count >= MaxShoppingItems)
                return;
            if (_shoppingList.Any(x => x.ItemId == item.ItemId))
                return;

            _shoppingList.Add(new ShoppingFormItem
            {
                ItemId = item.ItemId,
                Name = item.Name,
                CraftAmount = 5,
                Hq = false,
                Job = 0
            });
        }

        private void StartSearch()
        {
            var homeServer = (_homeServerBuffer ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(homeServer))
            {
                _state = _state with { Error = "Set Home server first." };
                return;
            }
            if (_shoppingList.Count == 0)
            {
                _state = _state with { Error = "Add at least one item." };
                return;
            }

            var paramsCopy = new ShoppingListParams
            {
                HomeServer = homeServer,
                RegionWide = _regionWide,
                ShoppingList = _shoppingList.Select(x => new ShoppingInputItem
                {
                    ItemId = x.ItemId,
                    CraftAmount = Math.Max(1, x.CraftAmount),
                    Hq = x.Hq,
                    Job = x.Job
                }).ToArray()
            };

            _state = _state with { Loading = true, Error = string.Empty };
            _ = Task.Run(async () =>
            {
                try
                {
                    var response = await _api.ShoppingListAsync(paramsCopy).ConfigureAwait(false);
                    var results = (response.Data ?? []).ToImmutableArray();
                    _state = new ScanState(false, results, string.Empty, response.AverageCostPerCraft, response.TotalCost);
                    if (results.Length > 0) _requestOpenResultsWindow = true;
                }
                catch (Exception ex)
                {
                    _state = new ScanState(false, ImmutableArray<ShoppingListResultItem>.Empty, ex.Message, 0, 0);
                }
            });
        }

        private void LoadItemNames(IDataManager dataManager)
        {
            try
            {
                var sheet = dataManager.GetExcelSheet<Item>();
                if (sheet == null) return;
                foreach (var row in sheet)
                {
                    if (row.RowId == 0) continue;
                    var name = row.Name.ToString();
                    if (string.IsNullOrWhiteSpace(name)) continue;
                    _items.Add(new SearchItemEntry((int)row.RowId, name));
                }
                _items.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                // If item names fail to load, UI will still render with empty suggestions.
            }
        }

        private static void DrawHelpMarker(string tooltip)
        {
            ImGui.TextDisabled("(?)");
            if (ImGui.IsItemHovered())
                ImGui.SetTooltip(tooltip);
        }

        public void Dispose() => _api.Dispose();

        private readonly record struct SearchItemEntry(int ItemId, string Name);

        private sealed class ShoppingFormItem
        {
            public int ItemId { get; set; }
            public string Name { get; set; } = string.Empty;
            public int CraftAmount { get; set; }
            public bool Hq { get; set; }
            public int Job { get; set; }
        }

        private sealed record ScanState(
            bool Loading,
            ImmutableArray<ShoppingListResultItem> Results,
            string Error,
            long AverageCostPerCraft,
            long TotalCost)
        {
            public static ScanState Idle => new(false, ImmutableArray<ShoppingListResultItem>.Empty, string.Empty, 0, 0);
        }
    }
}
