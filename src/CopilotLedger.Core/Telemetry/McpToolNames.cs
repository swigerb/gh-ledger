using System.Diagnostics.CodeAnalysis;
using System.Security.Cryptography;
using System.Text;

namespace CopilotLedger.Core.Telemetry;

// Copilot CLI's live execute_tool span names a tool from an MCP server by gen_ai.tool.name = the lowercase hexadecimal
// SHA-256 of the server's name in UTF-8, "/", and the SHA-256 of the tool's name in UTF-8: 129 characters. The session log
// names the same call in clear, so import learns the readable name of each hashed one, and reports show it.
public static class McpToolNames
{
    public const int HashedLength = 129;

    // serverName's tool toolName as Copilot CLI's live spans name it. Throws ArgumentNullException for null.
    public static string Hash(string serverName, string toolName)
    {
        ArgumentNullException.ThrowIfNull(serverName);
        ArgumentNullException.ThrowIfNull(toolName);

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(serverName))) + "/" +
               Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(toolName)));
    }

    // 64 lowercase hexadecimal digits, "/", and 64 more.
    public static bool IsHashed([NotNullWhen(true)] string? name)
    {
        if (name is null || name.Length != HashedLength || name[64] != '/')
        {
            return false;
        }

        for (int index = 0; index < HashedLength; index++)
        {
            if (index == 64)
            {
                continue;
            }

            char character = name[index];
            if (character is not (>= '0' and <= '9' or >= 'a' and <= 'f'))
            {
                return false;
            }
        }

        return true;
    }

    // "MCP tool " + the first 8 digits + "/" + the first 8 digits after the "/". Throws ArgumentException unless IsHashed.
    public static string ShortForm(string hashedName)
    {
        if (!IsHashed(hashedName))
        {
            throw new ArgumentException("The name isn't a hashed MCP tool name.", nameof(hashedName));
        }

        return "MCP tool " + hashedName[..8] + "/" + hashedName[65..73];
    }

    // What reports show for a stored tool name: for a hashed name, the name a session log gave it (learned, by hashed name),
    // else its ShortForm; any other name, and null, as it is. Throws ArgumentNullException for a null learned.
    public static string? Display(string? name, IReadOnlyDictionary<string, string> learned)
    {
        ArgumentNullException.ThrowIfNull(learned);

        return !IsHashed(name)
            ? name
            : learned.TryGetValue(name, out string? readable) ? readable : ShortForm(name);
    }
}
