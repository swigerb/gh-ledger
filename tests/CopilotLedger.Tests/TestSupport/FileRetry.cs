namespace CopilotLedger.Tests.TestSupport;

public static class FileRetry
{
    // Reads the file, retrying every 25 ms for up to 10 seconds while Windows reports it in use
    // (for example while antivirus scans a file that was just written).
    public static async Task<string> ReadAllTextAsync(string path)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (true)
        {
            try
            {
                return await File.ReadAllTextAsync(path);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(25);
            }
        }
    }

    // Like ReadAllTextAsync, for the file's bytes.
    public static async Task<byte[]> ReadAllBytesAsync(string path)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (true)
        {
            try
            {
                return await File.ReadAllBytesAsync(path);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                await Task.Delay(25);
            }
        }
    }

    // Like ReadAllTextAsync, for synchronous helpers such as GitFixture.Create.
    public static string ReadAllText(string path)
    {
        DateTime deadline = DateTime.UtcNow + TimeSpan.FromSeconds(10);
        while (true)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (IOException) when (DateTime.UtcNow < deadline)
            {
                Thread.Sleep(25);
            }
        }
    }
}
