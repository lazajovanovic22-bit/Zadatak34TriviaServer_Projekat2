namespace Zadatak34TriviaServer_Projekat2.Services;

public sealed class Logger : IDisposable
{
    private readonly object _logLock = new();
    private readonly StreamWriter _writer;

    public Logger(string filePath)
    {
        _writer = new StreamWriter(filePath, append: true) { AutoFlush = true };
    }

    public void Info(string message) => Write("INFO", message);
    public void Warning(string message) => Write("WARN", message);
    public void Error(string message) => Write("ERROR", message);

    private void Write(string level, string message)
    {
        lock (_logLock)
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [Thread {Environment.CurrentManagedThreadId}] [{level}] {message}";
            Console.WriteLine(line);
            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        lock (_logLock) _writer.Dispose();
    }
}
