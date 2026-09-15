using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Altinn.Studio.Designer.Configuration;
using Altinn.Studio.Designer.Helpers;

namespace Altinn.Studio.Designer.TypedHttpClients.ImageClient;

/// <summary>
/// Builds the primary handler for <see cref="ImageClient"/>. Requests are issued to user supplied URLs,
/// so redirects are never followed and every resolved address is checked against non-public ranges
/// right before connecting, which also covers DNS names resolving to internal addresses.
/// </summary>
public static class ImageClientHandlerFactory
{
    private static readonly TimeSpan s_connectTimeout = TimeSpan.FromSeconds(10);

    public static HttpMessageHandler Create(UrlValidationSettings urlValidationSettings)
    {
        var handler = new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            UseProxy = false,
            ConnectTimeout = s_connectTimeout,
        };

        if (!urlValidationSettings.AllowPrivateNetworkTargets)
        {
            handler.ConnectCallback = ConnectToPublicAddressAsync;
        }

        return handler;
    }

    private static async ValueTask<Stream> ConnectToPublicAddressAsync(
        SocketsHttpConnectionContext context,
        CancellationToken cancellationToken
    )
    {
        DnsEndPoint endPoint = context.DnsEndPoint;
        IPAddress[] resolvedAddresses = await Dns.GetHostAddressesAsync(endPoint.Host, cancellationToken);
        IPAddress[] publicAddresses = resolvedAddresses.Where(PublicNetworkAddressHelper.IsPublicAddress).ToArray();

        if (publicAddresses.Length == 0 || publicAddresses.Length != resolvedAddresses.Length)
        {
            throw new HttpRequestException($"Host '{endPoint.Host}' resolves to a non-public network address.");
        }

        var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
        try
        {
            await socket.ConnectAsync(publicAddresses, endPoint.Port, cancellationToken);
            return new NetworkStream(socket, ownsSocket: true);
        }
        catch
        {
            socket.Dispose();
            throw;
        }
    }
}
