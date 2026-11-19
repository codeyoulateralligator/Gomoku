using GameEngine;
using Microsoft.EntityFrameworkCore;

namespace DAL.EF;

public sealed class ConfigRepositoryEf(GameDbContext context) : IConfigRepository
{
    public IReadOnlyList<GameConfiguration> List() =>
        context.Configurations.AsNoTracking()
            .Select(entity => entity.Payload)
            .AsEnumerable()
            .Select(Deserialize)
            .OrderBy(configuration => configuration.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(configuration => configuration.Id)
            .ToArray();

    public GameConfiguration? Get(Guid id)
    {
        var payload = context.Configurations.AsNoTracking()
            .Where(entity => entity.Id == id)
            .Select(entity => entity.Payload)
            .SingleOrDefault();
        return payload is null ? null : Deserialize(payload);
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

        var entity = context.Configurations.Find(configuration.Id);
        if (entity is null)
        {
            entity = new ConfigEntity { Id = configuration.Id };
            context.Configurations.Add(entity);
        }

        entity.Name = configuration.Name;
        entity.IsPreset = configuration.IsPreset;
        entity.Payload = GameStateSerializer.Serialize(configuration);
        context.SaveChanges();
    }

    public bool Delete(Guid id)
    {
        if (GameConfiguration.Presets.Any(preset => preset.Id == id))
        {
            return false;
        }

        var entity = context.Configurations.Find(id);
        if (entity is null)
        {
            return false;
        }

        context.Configurations.Remove(entity);
        context.SaveChanges();
        return true;
    }

    public void SeedPresets()
    {
        foreach (var preset in GameConfiguration.Presets)
        {
            if (context.Configurations.Find(preset.Id) is null)
            {
                context.Configurations.Add(new ConfigEntity
                {
                    Id = preset.Id,
                    Name = preset.Name,
                    IsPreset = true,
                    Payload = GameStateSerializer.Serialize(preset)
                });
            }
        }

        context.SaveChanges();
    }

    private static GameConfiguration Deserialize(string payload)
    {
        var configuration = GameStateSerializer.Deserialize<GameConfiguration>(payload);
        configuration.Validate();
        return configuration;
    }
}
