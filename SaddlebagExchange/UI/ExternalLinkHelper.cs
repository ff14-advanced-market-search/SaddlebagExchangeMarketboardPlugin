using System;
using Dalamud.Utility;

namespace SaddlebagExchange.UI
{
    internal static class ExternalLinkHelper
    {
        public static void OpenHttpUrl(string? url)
        {
            var value = url?.Trim();
            if (string.IsNullOrEmpty(value))
                return;

            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
                return;

            if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                return;

            Util.OpenLink(uri.AbsoluteUri);
        }
    }
}
