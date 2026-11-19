using GameEngine;

namespace DAL.Json;

public sealed class JsonGameRepository : IGameRepository
{
    private readonly string _directory;

    public JsonGameRepository(string? dataDirectory = null)
    {
        _directory = Path.Combine(dataDirectory ?? JsonDataDirectory.Default, "games");
        Directory.CreateDirectory(_directory);
    }

    public IReadOnlyList<GameState> List() =>
        Directory.EnumerateFiles(_directory, "*.json")
            .Select(ReadGame)
            .OrderByDescending(game => game.UpdatedAt)
            .ThenBy(game => game.Id)
            .ToArray();

    public GameState? Get(Guid id)
    {
        var path = PathFor(id);
        return File.Exists(path) ? ReadGame(path) : null;
    }

    public void Save(GameState game)
    {
        ArgumentNullException.ThrowIfNull(game);
        game.Configuration.Validate();
        WriteAtomically(PathFor(game.Id), GameStateSerializer.Serialize(game));
    }

    public bool Delete(Guid id)
    {
        var path = PathFor(id);
        if (!File.Exists(path))
        {
            return false;
        }

        File.Delete(path);
        return true;
    }

    private static GameState ReadGame(string path)
    {
        var game = GameStateSerializer.Deserialize<GameState>(File.ReadAllText(path));
        game.Configuration.Validate();
        return game;
    }

    private string PathFor(Guid id) => Path.Combine(_directory, $"{id:D}.json");

    private static void WriteAtomically(string path, string content)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        File.WriteAllText(temporaryPath, content);
        File.Move(temporaryPath, path, overwrite: true);
    }
}
