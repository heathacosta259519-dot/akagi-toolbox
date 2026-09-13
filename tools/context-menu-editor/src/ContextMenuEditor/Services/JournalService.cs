using System.Text.Json;
using ContextMenuEditor.Models;

namespace ContextMenuEditor.Services;

public sealed class JournalService
{
    private readonly string _path;

    public JournalService()
        : this(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ContextMenuEditor",
            "journal.ndjson"))
    {
    }

    public JournalService(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _path = path;
    }

    public string Location => _path;

    public void Append(JournalRecord record)
    {
        File.AppendAllText(_path, JsonSerializer.Serialize(record) + Environment.NewLine);
    }

    public IReadOnlyList<JournalRecord> ReadAll()
    {
        if (!File.Exists(_path))
        {
            return [];
        }

        var records = new List<JournalRecord>();
        foreach (var line in File.ReadLines(_path))
        {
            if (line.Trim().Length == 0)
            {
                continue;
            }

            try
            {
                var record = JsonSerializer.Deserialize<JournalRecord>(line);
                if (record != null)
                {
                    records.Add(record);
                }
            }
            catch (JsonException)
            {
            }
        }

        return records;
    }
}
