namespace SaddlebagExchange.UI
{
    /// <summary>
    /// Item category filter options for reselling search. Each entry is either a header (id=null) or a filter (id sent to API).
    /// </summary>
    public static class ItemFilterDefs
    {
        public readonly struct FilterEntry
        {
            public readonly int? Id;
            public readonly string Label;
            public bool IsHeader => Id == null;
            public FilterEntry(int? id, string label) { Id = id; Label = label; }
        }

        public static FilterEntry[] GetAll()
        {
            return new[]
            {
                new FilterEntry(0, "Return all items"),
                new FilterEntry(-1, "Items sold by vendors"),
                new FilterEntry(-2, "Supply and Provisioning Mission"),
                new FilterEntry(-3, "Crafter class quest items"),
                new FilterEntry(-4, "NPC Vendor Furniture"),
                new FilterEntry(-5, "Exclude crafted gear (glamor)"),
                new FilterEntry(null, "---"),
                new FilterEntry(1, "Arms"),
                new FilterEntry(9, "Pugilist's Arms"),
                new FilterEntry(10, "Gladiator's Arms"),
                new FilterEntry(11, "Marauder's Arms"),
                new FilterEntry(12, "Archer's Arms"),
                new FilterEntry(13, "Lancer's Arms"),
                new FilterEntry(14, "Thaumaturge's Arms"),
                new FilterEntry(15, "Conjurer's Arms"),
                new FilterEntry(16, "Arcanist's Arms"),
                new FilterEntry(73, "Rogue's Arms"),
                new FilterEntry(76, "Dark Knight's Arms"),
                new FilterEntry(77, "Machinist's Arms"),
                new FilterEntry(78, "Astrologian's Arms"),
                new FilterEntry(83, "Samurai's Arms"),
                new FilterEntry(84, "Red Mage's Arms"),
                new FilterEntry(85, "Scholar's Arms"),
                new FilterEntry(86, "Gunbreaker's Arms"),
                new FilterEntry(87, "Dancer's Arms"),
                new FilterEntry(88, "Reaper's Arms"),
                new FilterEntry(89, "Sage's Arms"),
                new FilterEntry(105, "Blue Mage's Arms"),
                new FilterEntry(91, "Viper's Arms"),
                new FilterEntry(92, "Pictomancer's Arms"),
                new FilterEntry(null, "---"),
                new FilterEntry(2, "Tools"),
                new FilterEntry(19, "Carpenter's Tools"),
                new FilterEntry(20, "Blacksmith's Tools"),
                new FilterEntry(21, "Armorer's Tools"),
                new FilterEntry(22, "Goldsmith's Tools"),
                new FilterEntry(23, "Leatherworker's Tools"),
                new FilterEntry(24, "Weaver's Tools"),
                new FilterEntry(25, "Alchemist's Tools"),
                new FilterEntry(26, "Culinarian's Tools"),
                new FilterEntry(27, "Miner's Tools"),
                new FilterEntry(28, "Botanist's Tools"),
                new FilterEntry(29, "Fisher's Tools"),
                new FilterEntry(30, "Fishing Tackle"),
                new FilterEntry(null, "---"),
                new FilterEntry(3, "Armor"),
                new FilterEntry(17, "Shields"),
                new FilterEntry(31, "Head"),
                new FilterEntry(33, "Body"),
                new FilterEntry(35, "Legs"),
                new FilterEntry(36, "Hands"),
                new FilterEntry(37, "Feet"),
                new FilterEntry(null, "---"),
                new FilterEntry(4, "Accessories"),
                new FilterEntry(39, "Necklaces"),
                new FilterEntry(40, "Earrings"),
                new FilterEntry(41, "Bracelets"),
                new FilterEntry(42, "Rings"),
                new FilterEntry(null, "---"),
                new FilterEntry(5, "Medicines & Meals"),
                new FilterEntry(43, "Medicine"),
                new FilterEntry(44, "Ingredients"),
                new FilterEntry(45, "Meals"),
                new FilterEntry(46, "Seafood"),
                new FilterEntry(null, "---"),
                new FilterEntry(6, "Materials"),
                new FilterEntry(47, "Stone"),
                new FilterEntry(48, "Metal"),
                new FilterEntry(49, "Lumber"),
                new FilterEntry(50, "Cloth"),
                new FilterEntry(51, "Leather"),
                new FilterEntry(52, "Bone"),
                new FilterEntry(53, "Reagents"),
                new FilterEntry(54, "Dyes"),
                new FilterEntry(55, "Weapon Parts"),
                new FilterEntry(null, "---"),
                new FilterEntry(7, "Other"),
                new FilterEntry(56, "Furnishings"),
                new FilterEntry(57, "Materia"),
                new FilterEntry(58, "Crystals"),
                new FilterEntry(59, "Catalysts"),
                new FilterEntry(60, "Miscellany"),
                new FilterEntry(65, "Exterior Fixtures"),
                new FilterEntry(66, "Interior Fixtures"),
                new FilterEntry(67, "Outdoor Furnishings"),
                new FilterEntry(68, "Chairs and Beds"),
                new FilterEntry(69, "Tables"),
                new FilterEntry(70, "Tabletop"),
                new FilterEntry(71, "Wall-mounted"),
                new FilterEntry(72, "Rugs"),
                new FilterEntry(74, "Seasonal Miscellany"),
                new FilterEntry(75, "Minions"),
                new FilterEntry(79, "Airship/Submersible Components"),
                new FilterEntry(80, "Orchestrion Components"),
                new FilterEntry(81, "Gardening Items"),
                new FilterEntry(82, "Paintings"),
                new FilterEntry(90, "Registrable Miscellany")
            };
        }
    }
}
