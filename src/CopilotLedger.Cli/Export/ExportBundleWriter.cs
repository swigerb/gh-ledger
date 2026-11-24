using System.IO.Compression;
using System.Security.Cryptography;

namespace CopilotLedger.Cli.Export;

// One file of the bundle and how to write it. It must write the same bytes each time it's called.
internal sealed record ExportBundleEntry(string Path, Func<Stream, CancellationToken, Task> WriteAsync);

// A bundle file's exact length and SHA-256 (64 lowercase hexadecimal characters).
internal sealed record ExportFileHash(string Path, long Bytes, string Sha256);

// The bundle: a ZIP whose entries, dates, order, and compression input don't depend on the machine or the clock.
internal static class ExportBundleWriter
{
    public static readonly DateTimeOffset EntryTime = new(1980, 1, 1, 0, 0, 0, TimeSpan.Zero);
    public const int ChunkBytes = 65_536;

    // Writes a new ZIP at path: manifest.json (from the other entries' hashes), then the entries in ordinal order of Path.
    // Each entry is written twice: once to hash it, then into the ZIP, where its hash is checked again. Returns the hashes.
    public static async Task<IReadOnlyList<ExportFileHash>> WriteAsync(
        string path,
        IReadOnlyList<ExportBundleEntry> entries,
        Func<IReadOnlyList<ExportFileHash>, byte[]> manifest,
        CancellationToken cancellationToken)
    {
        ExportBundleEntry[] ordered = [.. entries.OrderBy(entry => entry.Path, StringComparer.Ordinal)];
        List<ExportFileHash> hashes = [];
        foreach (ExportBundleEntry entry in ordered)
        {
            using HashingStream hashing = new(Stream.Null);
            await entry.WriteAsync(hashing, cancellationToken);
            hashes.Add(hashing.Hash(entry.Path));
        }

        byte[] manifestBytes = manifest(hashes);
        await using FileStream file = new(path, FileMode.Create, FileAccess.Write, FileShare.None);
        using ZipArchive zip = new(file, ZipArchiveMode.Create, leaveOpen: true);
        await WriteEntryAsync(zip, "manifest.json", (stream, token) => stream.WriteAsync(manifestBytes, token).AsTask(), cancellationToken);
        for (int index = 0; index < ordered.Length; index++)
        {
            ExportFileHash written = await WriteEntryAsync(zip, ordered[index].Path, ordered[index].WriteAsync, cancellationToken);
            if (written != hashes[index])
            {
                throw new InvalidOperationException("An export file changed while the bundle was written.");
            }
        }

        return hashes;
    }

    private static async Task<ExportFileHash> WriteEntryAsync(
        ZipArchive zip,
        string path,
        Func<Stream, CancellationToken, Task> write,
        CancellationToken cancellationToken)
    {
        ZipArchiveEntry entry = zip.CreateEntry(path, CompressionLevel.Optimal);
        entry.LastWriteTime = EntryTime;
        await using Stream compressed = entry.Open();
        ChunkingStream chunks = new(compressed, ChunkBytes);
        using HashingStream hashing = new(chunks);
        await write(hashing, cancellationToken);
        await chunks.CompleteAsync(cancellationToken);
        return hashing.Hash(path);
    }

    // A write-only stream that hashes and counts what it forwards to inner, which it doesn't own.
    private sealed class HashingStream(Stream inner) : Stream
    {
        private readonly IncrementalHash _hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        private long _bytes;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public ExportFileHash Hash(string path) =>
            new(path, _bytes, Convert.ToHexStringLower(_hash.GetCurrentHash()));

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            _hash.AppendData(buffer);
            _bytes += buffer.Length;
            inner.Write(buffer);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _hash.AppendData(buffer.Span);
            _bytes += buffer.Length;
            await inner.WriteAsync(buffer, cancellationToken);
        }

        public override void Flush() => inner.Flush();

        public override Task FlushAsync(CancellationToken cancellationToken) => inner.FlushAsync(cancellationToken);

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _hash.Dispose();
            }

            base.Dispose(disposing);
        }
    }

    // Forwards writes to inner only in whole chunks of size bytes, and the rest at CompleteAsync, so deflate sees the same
    // calls for the same bytes however they were written. Flushing forwards nothing.
    private sealed class ChunkingStream(Stream inner, int size) : Stream
    {
        private readonly byte[] _buffer = new byte[size];
        private int _count;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            while (buffer.Length > 0)
            {
                int taken = Math.Min(size - _count, buffer.Length);
                buffer[..taken].CopyTo(_buffer.AsSpan(_count));
                _count += taken;
                buffer = buffer[taken..];
                if (_count == size)
                {
                    inner.Write(_buffer);
                    _count = 0;
                }
            }
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            WriteAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            while (buffer.Length > 0)
            {
                int taken = Math.Min(size - _count, buffer.Length);
                buffer[..taken].CopyTo(_buffer.AsMemory(_count));
                _count += taken;
                buffer = buffer[taken..];
                if (_count == size)
                {
                    await inner.WriteAsync(_buffer, cancellationToken);
                    _count = 0;
                }
            }
        }

        // Writes the last partial chunk.
        public async Task CompleteAsync(CancellationToken cancellationToken)
        {
            if (_count > 0)
            {
                await inner.WriteAsync(_buffer.AsMemory(0, _count), cancellationToken);
                _count = 0;
            }
        }

        public override void Flush()
        {
        }

        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
