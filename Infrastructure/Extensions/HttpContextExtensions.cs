namespace Infrastructure.Extensions;

using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;

public static class HttpContextExtensions
{
    extension(HttpContext httpContext)
    {
        public Uri? ListeningAddressUri(PathString path)
        {
            var server = httpContext.RequestServices.GetRequiredService<IServer>();
            var addresses = server.Features.GetRequiredFeature<IServerAddressesFeature>().Addresses;
            var address = addresses.FirstOrDefault(a => a.StartsWith(HttpsPrefix, StringComparison.OrdinalIgnoreCase))
                ?? addresses.FirstOrDefault();
            return IsNullOrWhiteSpace(address) ? null : new Uri(address.TrimEnd('/') + path);
        }
    }

    private static readonly string HttpsPrefix = Uri.UriSchemeHttps + Uri.SchemeDelimiter;
}
