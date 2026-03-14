using System;

namespace SaddlebagExchange.UI
{
    /// <summary>
    /// FFXIV data centers and worlds for the Home server dropdown (matches frontend WorldList).
    /// </summary>
    public static class WorldList
    {
        private static readonly Lazy<(string DataCenter, string World)[]> AllLazy = new(GetAll);
        public static (string DataCenter, string World)[] GetAll()
        {
            return new[]
            {
                ("Aether", "Adamantoise"),
                ("Aether", "Cactuar"),
                ("Aether", "Faerie"),
                ("Aether", "Gilgamesh"),
                ("Aether", "Jenova"),
                ("Aether", "Midgardsormr"),
                ("Aether", "Sargatanas"),
                ("Aether", "Siren"),
                ("Dynamis", "Halicarnassus"),
                ("Dynamis", "Maduin"),
                ("Dynamis", "Marilith"),
                ("Dynamis", "Seraph"),
                ("Dynamis", "Cuchulainn"),
                ("Dynamis", "Kraken"),
                ("Dynamis", "Rafflesia"),
                ("Dynamis", "Golem"),
                ("Primal", "Behemoth"),
                ("Primal", "Excalibur"),
                ("Primal", "Exodus"),
                ("Primal", "Famfrit"),
                ("Primal", "Hyperion"),
                ("Primal", "Lamia"),
                ("Primal", "Leviathan"),
                ("Primal", "Ultros"),
                ("Crystal", "Balmung"),
                ("Crystal", "Brynhildr"),
                ("Crystal", "Coeurl"),
                ("Crystal", "Diabolos"),
                ("Crystal", "Goblin"),
                ("Crystal", "Malboro"),
                ("Crystal", "Mateus"),
                ("Crystal", "Zalera"),
                ("Chaos", "Cerberus"),
                ("Chaos", "Louisoix"),
                ("Chaos", "Moogle"),
                ("Chaos", "Omega"),
                ("Chaos", "Ragnarok"),
                ("Chaos", "Spriggan"),
                ("Chaos", "Phantom"),
                ("Chaos", "Sagittarius"),
                ("Light", "Lich"),
                ("Light", "Odin"),
                ("Light", "Phoenix"),
                ("Light", "Shiva"),
                ("Light", "Twintania"),
                ("Light", "Zodiark"),
                ("Light", "Alpha"),
                ("Light", "Raiden"),
                ("Elemental", "Aegis"),
                ("Elemental", "Atomos"),
                ("Elemental", "Carbuncle"),
                ("Elemental", "Garuda"),
                ("Elemental", "Gungnir"),
                ("Elemental", "Kujata"),
                ("Elemental", "Tonberry"),
                ("Elemental", "Typhon"),
                ("Gaia", "Alexander"),
                ("Gaia", "Bahamut"),
                ("Gaia", "Durandal"),
                ("Gaia", "Fenrir"),
                ("Gaia", "Ifrit"),
                ("Gaia", "Ridill"),
                ("Gaia", "Tiamat"),
                ("Gaia", "Ultima"),
                ("Mana", "Anima"),
                ("Mana", "Asura"),
                ("Mana", "Chocobo"),
                ("Mana", "Hades"),
                ("Mana", "Ixion"),
                ("Mana", "Masamune"),
                ("Mana", "Pandaemonium"),
                ("Mana", "Titan"),
                ("Meteor", "Belias"),
                ("Meteor", "Mandragora"),
                ("Meteor", "Ramuh"),
                ("Meteor", "Shinryu"),
                ("Meteor", "Unicorn"),
                ("Meteor", "Valefor"),
                ("Meteor", "Yojimbo"),
                ("Meteor", "Zeromus"),
                ("Materia", "Bismarck"),
                ("Materia", "Ravana"),
                ("Materia", "Sephirot"),
                ("Materia", "Sophia"),
                ("Materia", "Zurvan")
            };
        }

        public static (string DataCenter, string World)[] All => AllLazy.Value;

        /// <summary>Unique data center names in display order.</summary>
        public static string[] GetDataCenters()
        {
            var seen = new HashSet<string>();
            var list = new List<string>();
            foreach (var (dc, _) in All)
            {
                if (seen.Add(dc))
                    list.Add(dc);
            }
            return list.ToArray();
        }

        /// <summary>World names for the given data center.</summary>
        public static string[] GetWorlds(string dataCenter)
        {
            var list = new List<string>();
            foreach (var (dc, world) in All)
            {
                if (dc == dataCenter)
                    list.Add(world);
            }
            return list.ToArray();
        }

        /// <summary>Data center that contains the given world, or null if not found.</summary>
        public static string? GetDataCenterForWorld(string world)
        {
            if (string.IsNullOrEmpty(world)) return null;
            foreach (var (dc, w) in All)
            {
                if (string.Equals(w, world, StringComparison.OrdinalIgnoreCase))
                    return dc;
            }
            return null;
        }
    }
}
