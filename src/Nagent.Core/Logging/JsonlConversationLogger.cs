using System.Text.Json;

namespace Nagent.Core.Logging;

public sealed class JsonlConversationLogger : IConversationLogger, IAsyncDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private StreamWriter _writer;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public string LogFilePath { get; private set; }

    private JsonlConversationLogger(StreamWriter writer, string logFilePath)
    {
        _writer = writer;
        LogFilePath = logFilePath;
    }

    public static JsonlConversationLogger Create(string workingPath)
    {
        var logsDir = Path.Combine(workingPath, ".agents", "logs");
        Directory.CreateDirectory(logsDir);
        var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.jsonl";
        var logFilePath = Path.Combine(logsDir, fileName);
        return OpenExisting(logFilePath);
    }

    public static JsonlConversationLogger OpenExisting(string logFilePath)
    {
        var logDir = Path.GetDirectoryName(logFilePath);
        if (!string.IsNullOrEmpty(logDir))
        {
            Directory.CreateDirectory(logDir);
        }

        var writer = new StreamWriter(File.Open(logFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
        {
            AutoFlush = true
        };
        return new JsonlConversationLogger(writer, logFilePath);
    }

    public async Task LogAsync(object entry, CancellationToken cancellationToken = default)
    {
        var line = JsonSerializer.Serialize(entry, JsonOptions);
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await _writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task RotateSessionAsync(string workingPath, CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            await _writer.DisposeAsync();
            var logsDir = Path.Combine(workingPath, ".agents", "logs");
            Directory.CreateDirectory(logsDir);
            var fileName = $"{DateTime.UtcNow:yyyyMMdd-HHmmss-fff}.jsonl";
            LogFilePath = Path.Combine(logsDir, fileName);
            _writer = new StreamWriter(File.Open(LogFilePath, FileMode.Append, FileAccess.Write, FileShare.Read))
            {
                AutoFlush = true
            };
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _writer.DisposeAsync();
        _lock.Dispose();
    }
}
