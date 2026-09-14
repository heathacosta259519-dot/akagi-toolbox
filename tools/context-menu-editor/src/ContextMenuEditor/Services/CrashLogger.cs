using System.Text;

namespace ContextMenuEditor.Services;

public static class CrashLogger
{
    private static readonly object Gate = new();

    public static string LogPath => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ContextMenuEditor",
        "error.log");

    public static void Log(Exception exception)
    {
        try
        {
            lock (Gate)
            {
                var directory = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var builder = new StringBuilder();
                builder.AppendLine("==== " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " ====");
                builder.AppendLine(exception.ToString());
                builder.AppendLine();
                File.AppendAllText(LogPath, builder.ToString(), new UTF8Encoding(false));
            }
        }
        catch (Exception logFailure) when (logFailure is IOException or UnauthorizedAccessException)
        {
        }
    }
}
