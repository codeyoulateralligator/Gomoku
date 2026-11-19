using GameEngine;
using Microsoft.EntityFrameworkCore;

namespace DAL.EF;

public sealed class GameRepositoryEf(GameDbContext context) : IGameRepository
{
    public IReadOnlyList<GameState> List() =>
        context.Games.AsNoTracking()
            .AsEnumerable()
            .OrderByDescending(entity => entity.UpdatedAt)
            .ThenBy(entity => entity.Id)
            .Select(entity => entity.Payload)
            .Select(Deserialize)
            .ToArray();

    public GameState? Get(Guid id)
    {
        var payload = context.Games.AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => entity.Payload)
            .SingleOrDefault();
        return payload is null ? null : Deserialize(payload);
    }

    public void Save(GameState game)
    {
        ArgumentNullException.ThrowIfNull(game);
        game.Configuration.Validate();

        var entity = context.Games.Find(game.Id);
        if (entity is null)
        {
            entity = new GameEntity { Id = game.Id };
            context.Games.Add(entity);
        }

        entity.Name = game.Name;
        entity.UpdatedAt = game.UpdatedAt;
        entity.Payload = GameStateSerializer.Serialize(game);
        context.SaveChanges();
    }

    public bool Delete(Guid id)
    {
        var entity = context.Games.Find(id);
        if (entity is null)
        {
            return false;
        }

        context.Games.Remove(entity);
        context.SaveChanges();
        return true;
    }

    private static GameState Deserialize(string payload)
    {
        var game = GameStateSerializer.Deserialize<GameState>(payload);
        game.Configuration.Validate();
        return game;
    }
}
