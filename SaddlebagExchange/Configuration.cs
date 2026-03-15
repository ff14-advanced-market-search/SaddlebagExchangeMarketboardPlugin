using Dalamud.Configuration;

namespace SaddlebagExchange
{
    public sealed class Configuration : IPluginConfiguration
    {
        public int Version { get; set; } = 1;

        public string DefaultHomeServer { get; set; } = string.Empty;
    }
}
