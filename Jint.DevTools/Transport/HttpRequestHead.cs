using System.Runtime.InteropServices;
using System.Text;

namespace Jint.DevTools.Transport;

/// <summary>
/// One HTTP/1.1 request head: the method, the path, and the few headers the upgrade needs.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately not an HTTP server. What arrives on this port is either a WebSocket upgrade or one of the
/// half-dozen <c>/json</c> documents a client reads before it connects, and the whole of that is a request
/// line plus headers. Nothing here reads a body, follows a chunked encoding or keeps a connection alive.
/// </para>
/// <para>
/// The alternative was <c>HttpListener</c>, which on Windows registers a URL prefix with <c>http.sys</c> and
/// wants an elevation-time reservation for anything but the default; a library a host embeds cannot ask for
/// that. A <c>TcpListener</c> plus this needs nothing from the operating system that a socket does not.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Auto)]
internal readonly record struct HttpRequestHead(string Method, string Path, string? UpgradeKey)
{
    /// <summary>Whether this request asks to become a WebSocket.</summary>
    internal bool IsWebSocketUpgrade => UpgradeKey is not null;

    /// <summary>
    /// Reads a request head from <paramref name="stream"/>, or answers <see langword="null"/> when the
    /// client went away or sent something that is not one.
    /// </summary>
    /// <remarks>
    /// Read one byte at a time, which is not an oversight: a buffered read would consume the first WebSocket
    /// frames along with the headers, and <c>WebSocket.CreateFromStream</c> has
    /// nowhere to put bytes that were read before it existed. A request head is a few hundred bytes and this
    /// happens once per connection.
    /// </remarks>
    internal static async ValueTask<HttpRequestHead?> ReadAsync(Stream stream, int maxBytes, CancellationToken cancellationToken)
    {
        var buffer = new byte[Math.Min(maxBytes, 16 * 1024)];
        var count = 0;
        var single = new byte[1];

        while (count < buffer.Length)
        {
            var read = await stream.ReadAsync(single.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
            if (read == 0)
            {
                return null;
            }

            buffer[count++] = single[0];

            if (count >= 4 &&
                buffer[count - 4] == (byte) '\r' && buffer[count - 3] == (byte) '\n' &&
                buffer[count - 2] == (byte) '\r' && buffer[count - 1] == (byte) '\n')
            {
                return Parse(Encoding.ASCII.GetString(buffer, 0, count - 4));
            }
        }

        return null;
    }

    private static HttpRequestHead? Parse(string head)
    {
        var lines = head.Split("\r\n", StringSplitOptions.None);
        if (lines.Length == 0)
        {
            return null;
        }

        var parts = lines[0].Split(' ');
        if (parts.Length < 2)
        {
            return null;
        }

        var upgrades = false;
        string? key = null;

        for (var i = 1; i < lines.Length; i++)
        {
            var separator = lines[i].IndexOf(':', StringComparison.Ordinal);
            if (separator <= 0)
            {
                continue;
            }

            var name = lines[i].AsSpan(0, separator).Trim();
            var value = lines[i].AsSpan(separator + 1).Trim();

            if (name.Equals("Upgrade", StringComparison.OrdinalIgnoreCase) && value.Contains("websocket", StringComparison.OrdinalIgnoreCase))
            {
                upgrades = true;
            }
            else if (name.Equals("Sec-WebSocket-Key", StringComparison.OrdinalIgnoreCase))
            {
                key = value.ToString();
            }
        }

        return new HttpRequestHead(parts[0], parts[1], upgrades ? key : null);
    }
}
