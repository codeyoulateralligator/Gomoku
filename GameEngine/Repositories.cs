namespace GameEngine;

public interface IConfigRepository
{
    IReadOnlyList<GameConfiguration> List();
    GameConfiguration? Get(Guid id);
    void Save(GameConfiguration configuration);
    bool Delete(Guid id);
}

public interface IGameRepository
{
    IReadOnlyList<GameState> List();
    GameState? Get(Guid id);
    void Save(GameState game);
    bool Delete(Guid id);
}
