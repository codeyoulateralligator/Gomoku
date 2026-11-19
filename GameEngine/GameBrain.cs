namespace GameEngine;

public enum MoveStatus
{
    Played,
    Won,
    Draw,
    OutOfBounds,
    Occupied,
    GameFinished
}

public sealed record MoveResult(MoveStatus Status, GameState State);

public sealed class GameBrain
{
    public GameState StartGame(
        GameConfiguration configuration,
        string gameName,
        GamePlayer firstPlayer,
        GamePlayer secondPlayer)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(firstPlayer);
        ArgumentNullException.ThrowIfNull(secondPlayer);
        configuration.Validate();

        if (string.IsNullOrWhiteSpace(gameName))
        {
            throw new ArgumentException("Game name cannot be empty.", nameof(gameName));
        }

        if (firstPlayer.Symbol == secondPlayer.Symbol)
        {
            throw new ArgumentException("Players must use different symbols.");
        }

        var now = DateTimeOffset.UtcNow;
        return new GameState(
            Guid.NewGuid(),
            gameName,
            configuration,
            Enumerable.Repeat(new string('.', configuration.Width), configuration.Height).ToArray(),
            [firstPlayer, secondPlayer],
            0,
            [],
            GameOutcome.InProgress,
            null,
            now,
            now);
    }

    public MoveResult MakeMove(GameState state, int row, int column)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.Outcome != GameOutcome.InProgress)
        {
            return new MoveResult(MoveStatus.GameFinished, state);
        }

        if (row < 0 || row >= state.Configuration.Height
            || column < 0 || column >= state.Configuration.Width)
        {
            return new MoveResult(MoveStatus.OutOfBounds, state);
        }

        if (state.GetCell(row, column) != '.')
        {
            return new MoveResult(MoveStatus.Occupied, state);
        }

        var player = state.CurrentPlayer;
        var board = (string[])state.Board.Clone();
        var rowCells = board[row].ToCharArray();
        rowCells[column] = player.Symbol;
        board[row] = new string(rowCells);

        var playedAt = DateTimeOffset.UtcNow;
        var moves = state.MoveHistory
            .Append(new GameMove(row, column, player.Symbol, playedAt))
            .ToArray();

        var won = IsWinningMove(state.Configuration, board, row, column, player.Symbol);
        var draw = !won && board.All(cells => !cells.Contains('.'));
        var outcome = won ? GameOutcome.Won : draw ? GameOutcome.Draw : GameOutcome.InProgress;
        var updated = state with
        {
            Board = board,
            MoveHistory = moves,
            CurrentPlayerIndex = outcome == GameOutcome.InProgress ? 1 - state.CurrentPlayerIndex : state.CurrentPlayerIndex,
            Outcome = outcome,
            WinnerSymbol = won ? player.Symbol : null,
            UpdatedAt = playedAt
        };

        return new MoveResult(
            won ? MoveStatus.Won : draw ? MoveStatus.Draw : MoveStatus.Played,
            updated);
    }

    public IReadOnlyList<(int Row, int Column)> GetLegalMoves(GameState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        if (state.Outcome != GameOutcome.InProgress)
        {
            return [];
        }

        var moves = new List<(int Row, int Column)>();
        for (var row = 0; row < state.Configuration.Height; row++)
        {
            for (var column = 0; column < state.Configuration.Width; column++)
            {
                if (state.GetCell(row, column) == '.')
                {
                    moves.Add((row, column));
                }
            }
        }

        return moves;
    }

    public bool IsWinningMove(GameConfiguration configuration, IReadOnlyList<string> board, int row, int column, char symbol)
    {
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(board);
        configuration.Validate();

        if (row < 0 || row >= configuration.Height || column < 0 || column >= configuration.Width)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        if (board.Count != configuration.Height
            || board.Any(cells => cells.Length != configuration.Width))
        {
            throw new ArgumentException("Board dimensions do not match the configuration.", nameof(board));
        }

        if (board[row][column] != symbol)
        {
            return false;
        }

        (int RowStep, int ColumnStep)[] directions = [(0, 1), (1, 0), (1, 1), (1, -1)];
        foreach (var (rowStep, columnStep) in directions)
        {
            var count = 1
                + CountInDirection(configuration, board, row, column, rowStep, columnStep, symbol)
                + CountInDirection(configuration, board, row, column, -rowStep, -columnStep, symbol);

            if (rowStep == 0 && configuration.WrapHorizontal)
            {
                count = Math.Min(count, configuration.Width);
            }

            if (configuration.OverlineRule == OverlineRule.AtLeast
                ? count >= configuration.WinningLength
                : count == configuration.WinningLength)
            {
                return true;
            }
        }

        return false;
    }

    private static int CountInDirection(
        GameConfiguration configuration,
        IReadOnlyList<string> board,
        int row,
        int column,
        int rowStep,
        int columnStep,
        char symbol)
    {
        var count = 0;
        var currentRow = row;
        var currentColumn = column;
        var maximumSteps = rowStep == 0 ? configuration.Width - 1 : configuration.Height - 1;

        for (var step = 0; step < maximumSteps; step++)
        {
            currentRow += rowStep;
            currentColumn += columnStep;

            if (currentRow < 0 || currentRow >= configuration.Height)
            {
                break;
            }

            if (configuration.WrapHorizontal)
            {
                currentColumn = (currentColumn % configuration.Width + configuration.Width) % configuration.Width;
            }
            else if (currentColumn < 0 || currentColumn >= configuration.Width)
            {
                break;
            }

            if (board[currentRow][currentColumn] != symbol)
            {
                break;
            }

            count++;
        }

        return count;
    }
}
