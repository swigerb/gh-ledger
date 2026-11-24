using System.Buffers;
using System.Security.Cryptography;

namespace CopilotLedger.Core.Import;

// Splits the first maxBytes bytes of a JSON Lines stream into lines, reading 64 KiB at a time, and hands each complete line
// to a callback. A line longer than maxLineBytes is skipped without being kept, and a last line without "\n" is reported but
// never handed over. Blank lines are skipped silently. Line numbers are 1-based and count every "\n". Every byte read is also
// added to hash, when it isn't null.
internal static class JsonlLineReader
{
    private const int ChunkBytes = 64 * 1024;

    // line is valid only during the call. Returns false to stop reading.
    internal delegate bool LineHandler(ReadOnlySpan<byte> line, long lineNumber, long byteOffset);

    internal static void Read(
        Stream stream,
        long maxBytes,
        int maxLineBytes,
        LineHandler onLine,
        Action<long, long> onOversized,
        Action<long, long> onUnterminated,
        IncrementalHash? hash,
        CancellationToken cancellationToken)
    {
        byte[] chunk = ArrayPool<byte>.Shared.Rent(ChunkBytes);
        byte[] line = ArrayPool<byte>.Shared.Rent(ChunkBytes);
        int lineLength = 0;
        bool oversized = false;
        long lineNumber = 1;
        long lineOffset = 0;
        long position = 0;
        try
        {
            while (position < maxBytes)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = stream.Read(chunk, 0, (int)Math.Min(ChunkBytes, maxBytes - position));
                if (read == 0)
                {
                    break;
                }

                hash?.AppendData(chunk, 0, read);
                int start = 0;
                while (start < read)
                {
                    int newline = Array.IndexOf(chunk, (byte)'\n', start, read - start);
                    int end = newline < 0 ? read : newline;
                    if (!oversized)
                    {
                        int count = end - start;
                        if (lineLength + count > maxLineBytes)
                        {
                            oversized = true;
                            lineLength = 0;
                        }
                        else
                        {
                            if (lineLength + count > line.Length)
                            {
                                byte[] larger = ArrayPool<byte>.Shared.Rent(
                                    Math.Min(maxLineBytes, Math.Max(line.Length * 2, lineLength + count)));
                                Buffer.BlockCopy(line, 0, larger, 0, lineLength);
                                ArrayPool<byte>.Shared.Return(line);
                                line = larger;
                            }

                            Buffer.BlockCopy(chunk, start, line, lineLength, count);
                            lineLength += count;
                        }
                    }

                    if (newline < 0)
                    {
                        break;
                    }

                    if (oversized)
                    {
                        onOversized(lineNumber, lineOffset);
                    }
                    else
                    {
                        ReadOnlySpan<byte> complete = line.AsSpan(0, lineLength);
                        if (complete.Length > 0 && complete[^1] == (byte)'\r')
                        {
                            complete = complete[..^1];
                        }

                        if (!IsBlank(complete) && !onLine(complete, lineNumber, lineOffset))
                        {
                            return;
                        }
                    }

                    lineNumber++;
                    lineOffset = position + newline + 1;
                    lineLength = 0;
                    oversized = false;
                    start = newline + 1;
                }

                position += read;
            }

            if (oversized)
            {
                onOversized(lineNumber, lineOffset);
            }
            else if (!IsBlank(line.AsSpan(0, lineLength)))
            {
                onUnterminated(lineNumber, lineOffset);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(chunk);
            ArrayPool<byte>.Shared.Return(line);
        }
    }

    private static bool IsBlank(ReadOnlySpan<byte> line) =>
        !line.ContainsAnyExcept((byte)' ', (byte)'\t', (byte)'\r');
}
