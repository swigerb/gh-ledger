using System.Diagnostics.CodeAnalysis;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace CopilotLedger.Cli.Audit;

// Where audit --local-llm sends its request: a loopback address, never a name to resolve.
public sealed record LocalEndpoint(
    Uri BaseUri,          // the URL as given, normalized by System.Uri
    IPAddress Address,    // the pinned loopback address; 127.0.0.1 for localhost
    Uri RequestUri,       // the POST target: Address, the port, and BaseUri's path + /chat/completions
    string? HostHeader);  // localhost[:port] for localhost, so the server and TLS see the name; else null

// The endpoints gh ledger audit --local-llm may send to: http or https on localhost, an address in 127.0.0.0/8, or [::1].
// localOffload.additionalHosts in the rates never widens it. Public for tests.
public static partial class LocalEndpointPolicy
{
    // Checks url in order; the first failure is the error, which never repeats the URL. A DNS host must be exactly the ASCII
    // name localhost (Host, not IdnHost, whose IDN mapping depends on the globalization mode); an IPv6 host must be ::1 with
    // no scope (IdnHost keeps the scope that Host drops).
    public static bool TryValidate(
        string? url,
        [NotNullWhen(true)] out LocalEndpoint? endpoint,
        [NotNullWhen(false)] out string? error)
    {
        endpoint = null;
        string text = url?.Trim() ?? string.Empty;
        if (!SchemePrefix().IsMatch(text) || !Uri.TryCreate(text, UriKind.Absolute, out Uri? uri))
        {
            error = "use an absolute http or https URL, such as http://127.0.0.1:11434/v1.";
            return false;
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            error = "use http or https.";
            return false;
        }

        if (uri.UserInfo.Length > 0)
        {
            error = "remove the user name and password.";
            return false;
        }

        if (uri.Query.Length > 0 || uri.Fragment.Length > 0)
        {
            error = "remove the query and the fragment.";
            return false;
        }

        if (uri.Port is < 1 or > 65535)
        {
            error = "use a port from 1 to 65535.";
            return false;
        }

        IPAddress address;
        string? hostHeader = null;
        if (uri.HostNameType == UriHostNameType.Dns && uri.Host == "localhost")
        {
            address = IPAddress.Loopback;
            hostHeader = uri.IsDefaultPort ? "localhost" : $"localhost:{uri.Port}";
        }
        else if (uri.HostNameType == UriHostNameType.IPv4 &&
                 IPAddress.TryParse(uri.Host, out IPAddress? ipv4) &&
                 ipv4.AddressFamily == AddressFamily.InterNetwork &&
                 ipv4.GetAddressBytes()[0] == 127)
        {
            address = ipv4;
        }
        else if (uri.HostNameType == UriHostNameType.IPv6 && uri.IdnHost == "::1")
        {
            address = IPAddress.IPv6Loopback;
        }
        else
        {
            error = "use localhost, an address in 127.0.0.0/8, or [::1]; other hosts aren't allowed.";
            return false;
        }

        string pinned = address.AddressFamily == AddressFamily.InterNetworkV6 ? "[::1]" : address.ToString();
        Uri request = new($"{uri.Scheme}://{pinned}:{uri.Port}{uri.AbsolutePath.TrimEnd('/')}/chat/completions");
        endpoint = new LocalEndpoint(uri, address, request, hostHeader);
        error = null;
        return true;
    }

    // A URL names its scheme and "://", so a path (which System.Uri on Linux reads as a file URI) or "host:port" is rejected
    // as not a URL.
    [GeneratedRegex("^[A-Za-z][A-Za-z0-9+.-]*://")]
    private static partial Regex SchemePrefix();
}
