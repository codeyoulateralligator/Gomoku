using DAL.EF;
using DAL.Json;
using GameEngine;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Tests;

public sealed class RepositoryContractTests
{
    [Theory]
    [InlineData("Json")]
    [InlineData("Ef")]
    public void ConfigurationsAreSeededRoundTrippedAndCrudWorks(string kind)
    {
        using var harness = new RepositoryHarness(kind);
        var repository = harness.Configurations;
        Assert.Equal(GameConfiguration.Presets.Count, repository.List().Count);
        Assert.False(repository.Delete(GameConfiguration.Presets[0].Id));

        var configuration = new GameConfiguration(Guid.NewGuid(), "Cylinder exact", 8, 7, 5, true, OverlineRule.Exactly);
        repository.Save(configuration);
        Assert.Equal(configuration, repository.Get(configuration.Id));
        Assert.Contains(repository.List(), value => value.Id == configuration.Id);
        Assert.True(repository.Delete(configuration.Id));
        Assert.Null(repository.Get(configuration.Id));
    }

    [Theory]
    [InlineData("Json")]
    [InlineData("Ef")]
    public void SavedGameRoundTripPreservesAllContinuationState(string kind)
    {
        using var harness = new RepositoryHarness(kind);
        var brain = new GameBrain();
        var configuration = new GameConfiguration(Guid.NewGuid(), "Cylinder exact", 8, 7, 5, true, OverlineRule.Exactly);
        var state = brain.StartGame(
            configuration,
            "Saved game",
            new GamePlayer("Human", 'X', PlayerKind.Human),
            new GamePlayer("AI", 'O', PlayerKind.RandomAi));
        state = brain.MakeMove(state, 2, 7).State;
        harness.Games.Save(state);

        var loaded = harness.Games.Get(state.Id);
        Assert.NotNull(loaded);
        Assert.Equal(state.Id, loaded.Id);
        Assert.Equal(state.Name, loaded.Name);
        Assert.Equal(state.Configuration, loaded.Configuration);
        Assert.Equal(state.Board, loaded.Board);
        Assert.Equal(state.CurrentPlayerIndex, loaded.CurrentPlayerIndex);
        Assert.Equal(state.Players, loaded.Players);
        Assert.Equal(state.MoveHistory, loaded.MoveHistory);
        Assert.Equal(state.Outcome, loaded.Outcome);
        Assert.Equal(state.UpdatedAt, loaded.UpdatedAt);
        Assert.Contains(harness.Games.List(), game => game.Id == state.Id);
        Assert.True(harness.Games.Delete(state.Id));
        Assert.Null(harness.Games.Get(state.Id));
    }

    [Fact]
    public void EfDatabaseMigratesAutomatically()
    {
        using var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        var options = new DbContextOptionsBuilder<GameDbContext>()
            .UseSqlite(connection)
            .Options;
        using var context = new GameDbContext(options);
        context.Database.Migrate();

        Assert.True(context.Database.CanConnect());
        Assert.NotEmpty(context.Database.GetAppliedMigrations());
        Assert.NotNull(context.Configurations);
        new ConfigRepositoryEf(context).SeedPresets();
        Assert.Equal(GameConfiguration.Presets.Count, context.Configurations.Count());
    }

    private sealed class RepositoryHarness : IDisposable
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), $"gomoku-tests-{Guid.NewGuid():N}");
        private readonly SqliteConnection? _connection;
        private readonly GameDbContext? _context;

        public RepositoryHarness(string kind)
        {
            if (kind == "Json")
            {
                Configurations = new JsonConfigRepository(_directory);
                Games = new JsonGameRepository(_directory);
                return;
            }

            _connection = new SqliteConnection("Data Source=:memory:");
            _connection.Open();
            var options = new DbContextOptionsBuilder<GameDbContext>()
                .UseSqlite(_connection)
                .Options;
            _context = new GameDbContext(options);
            _context.Database.EnsureCreated();
            var configs = new ConfigRepositoryEf(_context);
            configs.SeedPresets();
            Configurations = configs;
            Games = new GameRepositoryEf(_context);
        }

        public IConfigRepository Configurations { get; }
        public IGameRepository Games { get; }

        public void Dispose()
        {
            _context?.Dispose();
            _connection?.Dispose();
            if (Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }
    }
}
