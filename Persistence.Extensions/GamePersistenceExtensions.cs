using DAL.EF;
using DAL.Json;
using GameEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Persistence.Extensions;

public static class GamePersistenceExtensions
{
    public static IServiceCollection AddGamePersistence(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddSingleton<GameBrain>();
        var provider = configuration["Persistence"] ?? "Json";
        var dataDirectory = configuration["DataDirectory"];
        if (string.IsNullOrWhiteSpace(dataDirectory))
        {
            dataDirectory = null;
        }
        else if (!Path.IsPathRooted(dataDirectory))
        {
            throw new InvalidOperationException("DataDirectory must be an absolute path.");
        }

        if (string.Equals(provider, "Db", StringComparison.OrdinalIgnoreCase)
            || string.Equals(provider, "Ef", StringComparison.OrdinalIgnoreCase))
        {
            var root = dataDirectory
                ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".gomoku");
            Directory.CreateDirectory(root);
            var databasePath = Path.Combine(root, "gomoku.db");
            services.AddDbContext<GameDbContext>(options => options.UseSqlite($"Data Source={databasePath}"));
            services.AddScoped<ConfigRepositoryEf>();
            services.AddScoped<GameRepositoryEf>();
            services.AddScoped<IConfigRepository>(providerServices => providerServices.GetRequiredService<ConfigRepositoryEf>());
            services.AddScoped<IGameRepository>(providerServices => providerServices.GetRequiredService<GameRepositoryEf>());
            services.AddSingleton(new GamePersistenceSelection("Db"));
        }
        else if (string.Equals(provider, "Json", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IConfigRepository>(_ => new JsonConfigRepository(dataDirectory));
            services.AddSingleton<IGameRepository>(_ => new JsonGameRepository(dataDirectory));
            services.AddSingleton(new GamePersistenceSelection("Json"));
        }
        else
        {
            throw new InvalidOperationException($"Unsupported Persistence value '{provider}'. Use Json or Db.");
        }

        return services;
    }

    public static void InitializeGamePersistence(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        if (scope.ServiceProvider.GetRequiredService<GamePersistenceSelection>().Provider == "Db")
        {
            scope.ServiceProvider.GetRequiredService<GameDbContext>().Database.Migrate();
            scope.ServiceProvider.GetRequiredService<ConfigRepositoryEf>().SeedPresets();
        }
        else
        {
            _ = scope.ServiceProvider.GetRequiredService<IConfigRepository>().List();
        }
    }
}

public sealed record GamePersistenceSelection(string Provider);
