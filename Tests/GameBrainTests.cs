using GameEngine;

namespace Tests;

public sealed class GameBrainTests
{
    private readonly GameBrain _brain = new();

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(1, -1)]
    public void DetectsWinsInEveryDirection(int rowStep, int columnStep)
    {
        var configuration = Config(width: 9, height: 9, winningLength: 5);
        var board = EmptyBoard(configuration);
        var startRow = rowStep < 0 ? 6 : 2;
        var startColumn = columnStep < 0 ? 6 : 2;
        for (var index = 0; index < configuration.WinningLength; index++)
        {
            board[startRow + rowStep * index][startColumn + columnStep * index] = 'X';
        }

        Assert.True(_brain.IsWinningMove(configuration, ToRows(board), startRow + 2 * rowStep, startColumn + 2 * columnStep, 'X'));
    }

    [Fact]
    public void CylinderAllowsHorizontalAndDiagonalLinesAcrossTheEdge()
    {
        var configuration = Config(width: 7, height: 7, wrapHorizontal: true);
        var board = EmptyBoard(configuration);
        for (var index = 0; index < 5; index++)
        {
            board[3][(5 + index) % configuration.Width] = 'X';
        }

        Assert.True(_brain.IsWinningMove(configuration, ToRows(board), 3, 0, 'X'));

        board = EmptyBoard(configuration);
        for (var index = 0; index < 5; index++)
        {
            board[1 + index][(5 + index) % configuration.Width] = 'O';
        }

        Assert.True(_brain.IsWinningMove(configuration, ToRows(board), 3, 0, 'O'));
        Assert.False(_brain.IsWinningMove(configuration with { WrapHorizontal = false }, ToRows(board), 3, 0, 'O'));
    }

    [Fact]
    public void FullCylinderRowWinsWhenItsLengthEqualsTheConfiguredLine()
    {
        var configuration = Config(width: 5, height: 5, winningLength: 5, wrapHorizontal: true);
        var board = EmptyBoard(configuration);
        Array.Fill(board[2], 'X');

        Assert.True(_brain.IsWinningMove(configuration, ToRows(board), 2, 0, 'X'));
    }

    [Fact]
    public void ExactFiveRejectsAnOverlineWhileAtLeastRuleAcceptsIt()
    {
        var board = EmptyBoard(Config(width: 9, height: 9));
        for (var column = 1; column <= 6; column++)
        {
            board[4][column] = 'X';
        }

        var rows = ToRows(board);
        Assert.False(_brain.IsWinningMove(Config(width: 9, height: 9, overlineRule: OverlineRule.Exactly), rows, 4, 3, 'X'));
        Assert.True(_brain.IsWinningMove(Config(width: 9, height: 9, overlineRule: OverlineRule.AtLeast), rows, 4, 3, 'X'));
    }

    [Fact]
    public void RejectsIllegalMovesWithoutChangingTheTurn()
    {
        var game = NewGame(Config(width: 5, height: 5, winningLength: 5));
        var placed = _brain.MakeMove(game, 0, 0);
        Assert.Equal(MoveStatus.Played, placed.Status);
        Assert.Equal('O', placed.State.CurrentPlayer.Symbol);

        var occupied = _brain.MakeMove(placed.State, 0, 0);
        Assert.Equal(MoveStatus.Occupied, occupied.Status);
        Assert.Equal('O', occupied.State.CurrentPlayer.Symbol);

        var outOfBounds = _brain.MakeMove(placed.State, 5, 0);
        Assert.Equal(MoveStatus.OutOfBounds, outOfBounds.Status);
        Assert.Equal('O', outOfBounds.State.CurrentPlayer.Symbol);
    }

    [Fact]
    public void DetectsDrawOnAFullBoardWithoutALine()
    {
        var game = NewGame(Config(width: 3, height: 2, winningLength: 3));
        (int Row, int Column)[] moves = [(0, 0), (0, 1), (0, 2), (1, 2), (1, 1), (1, 0)];
        MoveResult? result = null;
        foreach (var (row, column) in moves)
        {
            result = _brain.MakeMove(game, row, column);
            Assert.NotEqual(MoveStatus.Won, result.Status);
            game = result.State;
        }

        Assert.Equal(MoveStatus.Draw, result!.Status);
        Assert.Equal(GameOutcome.Draw, game.Outcome);
    }

    [Fact]
    public void ValidatesImpossibleConfigurations()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Config(width: 2, height: 2, winningLength: 5).Validate());
        Assert.Throws<ArgumentException>(() =>
            Config(width: 4, height: 6, winningLength: 5, wrapHorizontal: true).Validate());
        Assert.Throws<ArgumentException>(() => Config(width: 10_001, height: 2, winningLength: 5).Validate());
    }

    [Fact]
    public async Task RandomProviderReturnsOnlyLegalMovesAndCanBeSeeded()
    {
        foreach (var configuration in GameConfiguration.Presets)
        {
            var game = NewGame(configuration);
            for (var seed = 0; seed < 20; seed++)
            {
                var first = await new RandomAiMoveProvider(_brain, new Random(seed))
                    .GetMoveAsync(game, CancellationToken.None);
                var second = await new RandomAiMoveProvider(_brain, new Random(seed))
                    .GetMoveAsync(game, CancellationToken.None);

                Assert.Equal(first, second);
                Assert.Contains(first, _brain.GetLegalMoves(game));
            }
        }
    }

    [Fact]
    public async Task SearchProviderTakesAnImmediateWinWithinItsTimeBudget()
    {
        var configuration = Config(width: 7, height: 7, winningLength: 4);
        var game = NewGame(configuration);
        (int Row, int Column)[] tacticalMoves = [(3, 1), (0, 0), (3, 2), (0, 2), (3, 3), (0, 4)];
        foreach (var (row, column) in tacticalMoves)
        {
            game = _brain.MakeMove(game, row, column).State;
        }

        var timer = System.Diagnostics.Stopwatch.StartNew();
        var move = await new SearchAiMoveProvider(_brain, maximumDepth: 3, timeBudget: TimeSpan.FromSeconds(2))
            .GetMoveAsync(game, CancellationToken.None);
        timer.Stop();
        var result = _brain.MakeMove(game, move.Row, move.Column);

        Assert.Equal(MoveStatus.Won, result.Status);
        Assert.True(timer.Elapsed < TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task RandomProvidersFinishEveryPresetWithinTheBoardMoveCap()
    {
        foreach (var configuration in GameConfiguration.Presets)
        {
            var game = NewGame(configuration);
            var providers = new[]
            {
                new RandomAiMoveProvider(_brain, new Random(17)),
                new RandomAiMoveProvider(_brain, new Random(29))
            };
            var moveCap = configuration.Width * configuration.Height;
            var moveCount = 0;

            while (game.Outcome == GameOutcome.InProgress && moveCount < moveCap)
            {
                var (row, column) = await providers[game.CurrentPlayerIndex]
                    .GetMoveAsync(game, CancellationToken.None);
                var result = _brain.MakeMove(game, row, column);
                Assert.Contains(result.Status, [MoveStatus.Played, MoveStatus.Won, MoveStatus.Draw]);
                game = result.State;
                moveCount++;
            }

            Assert.NotEqual(GameOutcome.InProgress, game.Outcome);
            Assert.True(moveCount <= moveCap);
        }
    }

    private GameState NewGame(GameConfiguration configuration) =>
        _brain.StartGame(configuration, "Test", new GamePlayer("A", 'X', PlayerKind.Human), new GamePlayer("B", 'O', PlayerKind.Human));

    private static GameConfiguration Config(
        int width = 9,
        int height = 9,
        int winningLength = 5,
        bool wrapHorizontal = false,
        OverlineRule overlineRule = OverlineRule.AtLeast) =>
        new(Guid.NewGuid(), "Test", width, height, winningLength, wrapHorizontal, overlineRule);

    private static char[][] EmptyBoard(GameConfiguration configuration) =>
        Enumerable.Range(0, configuration.Height)
            .Select(_ => Enumerable.Repeat('.', configuration.Width).ToArray())
            .ToArray();

    private static string[] ToRows(char[][] board) => board.Select(row => new string(row)).ToArray();
}
