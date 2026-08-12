using System.Text.Json;

namespace ZJS2310.App.Infrastructure;

public sealed class JsonFileStore<T> where T : class
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    private readonly string _path;

    public JsonFileStore(string path) => _path = path;

    public T? Load()
    {
        if (!File.Exists(_path))
        {
            return null;
        }

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<T>(json, Options);
    }

    public void Save(T value)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        var temporaryPath = _path + ".tmp";
        File.WriteAllText(temporaryPath, JsonSerializer.Serialize(value, Options));
        File.Move(temporaryPath, _path, true);
    }
}
