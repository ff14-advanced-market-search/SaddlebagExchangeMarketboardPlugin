using System;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using SaddlebagExchange.Models;

namespace SaddlebagExchange.UI
{
    /// <summary>
    /// Shared UI for the item category filter list used in Reselling Search and Market Overview.
    /// </summary>
    public static class ItemFilterListHelper
    {
        public static void RenderFilterList(
            string childId,
            Func<int[]> getFilters,
            Action<int[]> setFilters,
            float widthPx = 320f,
            float heightPx = 400f)
        {
            float filterW = widthPx * ImGuiHelpers.GlobalScale;
            float filterH = heightPx * ImGuiHelpers.GlobalScale;
            using (var child = ImRaii.Child(childId, new System.Numerics.Vector2(filterW, filterH), true))
            {
                if (child.Success)
                {
                    var filters = getFilters();
                    var filterSet = new System.Collections.Generic.HashSet<int>(filters);
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
                            var list = new System.Collections.Generic.List<int>(filters);
                            if (isChecked)
                            {
                                if (id == 0)
                                {
                                    setFilters(new[] { 0 });
                                    continue;
                                }
                                list.Remove(0);
                                if (!list.Contains(id))
                                    list.Add(id);
                            }
                            else
                            {
                                list.Remove(id);
                            }
                            setFilters(list.ToArray());
                        }
                    }
                }
            }
        }
    }
}
