using GameEngine;

namespace DAL.Json;

public sealed class JsonConfigRepository : IConfigRepository
{
    private readonly string _directory;

    public JsonConfigRepository(string? dataDirectory = null)
    {
        _directory = Path.Combine(dataDirectory ?? JsonDataDirectory.Default, "configs");
        Directory.CreateDirectory(_directory);
        SeedPresets();
    }

    public IReadOnlyList<GameConfiguration> List() =>
        Directory.EnumerateFiles(_directory, "*.json")
            .Select(ReadConfiguration)
            .OrderBy(configuration => configuration.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(configuration => configuration.Id)
            .ToArray();

    public GameConfiguration? Get(Guid id)
    {
        var path = PathFor(id);
        return File.Exists(path) ? ReadConfiguration(path) : null;
    }

    public void Save(GameConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        configuration.Validate();
        if (configuration.IsPreset)
        {
            throw new InvalidOperationException("Custom configurations cannot be marked as presets.");
        }

        if (GameConfiguration.Presets.Any(preset => preset.Id == configuration.Id))
        {
            throw new InvalidOperationException("Presets cannot be edited.");
        }

        WriteAtomically(PathFor(configuration.Id), GameStateSerializer.Serialize(configuration));
    }

    public bool Delete(Guid id)
    {
        if (GameConfiguration.Presets.Any(preset => preset.Id == id))
        {
            return false;
        }

        var path = PathFor(id);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    private void SeedPresets()
    {
        foreach (var preset in GameConfiguration.Presets)
        {
            var path = PathFor(preset.Id);
            if (!File.Exists(path))
            {
                WriteAtomically(path, GameStateSerializer.Serialize(preset));
            }
        }
    }

    private GameConfiguration ReadConfiguration(string path)
    {
        var configuration = GameStateSerializer.Deserialize<GameConfiguration>(File.ReadAllText(path));
        configuration.Validate();
        return configuration;
    }

    private string PathFor(Guid id) => Path.Combine(_directory, $"{id:D}.json");

    private static void WriteAtomically(string path, string content)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, content);
        File.Move(temporaryPath, path, overwrite: true);
    }
}
