using System.Diagnostics;

namespace GameEngine;

public interface IMoveProvider
{
    ValueTask<(int Row, int Column)> GetMoveAsync(GameState state, CancellationToken cancellationToken);
}

public sealed class RandomAiMoveProvider : IMoveProvider
{
    private readonly GameBrain _brain;
    private readonly Random _random;

    public RandomAiMoveProvider(GameBrain brain, Random? random = null)
    {
        _brain = brain ?? throw new ArgumentNullException(nameof(brain));
        _random = random ?? Random.Shared;
    }

    public ValueTask<(int Row, int Column)> GetMoveAsync(GameState state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var moves = _brain.GetLegalMoves(state);
        if (moves.Count == 0)
        {
            throw new InvalidOperationException("The game has no legal moves.");
        }

        return ValueTask.FromResult(moves[_random.Next(moves.Count)]);
    }
}

public sealed class SearchAiMoveProvider : IMoveProvider
{
    private readonly GameBrain _brain;
    private readonly int _maximumDepth;
    private readonly TimeSpan _timeBudget;

    public SearchAiMoveProvider(GameBrain brain, int maximumDepth = 2, TimeSpan? timeBudget = null)
    {
        _brain = brain ?? throw new ArgumentNullException(nameof(brain));
        if (maximumDepth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        }

        _maximumDepth = maximumDepth;
        _timeBudget = timeBudget ?? TimeSpan.FromMilliseconds(250);
        if (_timeBudget <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(timeBudget));
        }
    }

    public ValueTask<(int Row, int Column)> GetMoveAsync(GameState state, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        var candidates = GetCandidateMoves(state);
        if (candidates.Count == 0)
        {
            throw new InvalidOperationException("The game has no legal moves.");
        }
        var bestMove = candidates[0];
        var rootSymbol = state.CurrentPlayer.Symbol;

        for (var depth = 1; depth <= _maximumDepth; depth++)
        {
            var depthBestMove = bestMove;
            var depthBestScore = double.NegativeInfinity;
            var alpha = double.NegativeInfinity;
            try
            {
                foreach (var move in candidates)
                {
                    CheckBudget(stopwatch, cancellationToken);
                    var next = _brain.MakeMove(state, move.Row, move.Column).State;
                    var score = Search(next, depth - 1, alpha, double.PositiveInfinity, rootSymbol, stopwatch, cancellationToken);
                    if (score > depthBestScore)
                    {
                        depthBestScore = score;
                        depthBestMove = move;
                    }

                    alpha = Math.Max(alpha, depthBestScore);
                }

                bestMove = depthBestMove;
            }
            catch (SearchTimeoutException)
            {
                break;
            }
        }

        return ValueTask.FromResult(bestMove);
    }

    private double Search(
        GameState state,
        int depth,
        double alpha,
        double beta,
        char rootSymbol,
        Stopwatch stopwatch,
        CancellationToken cancellationToken)
    {
        CheckBudget(stopwatch, cancellationToken);
        if (state.Outcome == GameOutcome.Won)
        {
            return state.WinnerSymbol == rootSymbol ? 10_000_000 + depth : -10_000_000 - depth;
        }

        if (state.Outcome == GameOutcome.Draw || depth == 0)
        {
            return Evaluate(state, rootSymbol, stopwatch, cancellationToken);
        }

        var maximizing = state.CurrentPlayer.Symbol == rootSymbol;
        var best = maximizing ? double.NegativeInfinity : double.PositiveInfinity;
        foreach (var move in GetCandidateMoves(state))
        {
            var next = _brain.MakeMove(state, move.Row, move.Column).State;
            var score = Search(next, depth - 1, alpha, beta, rootSymbol, stopwatch, cancellationToken);
            if (maximizing)
            {
                best = Math.Max(best, score);
                alpha = Math.Max(alpha, best);
            }
            else
            {
                best = Math.Min(best, score);
                beta = Math.Min(beta, best);
            }

            if (alpha >= beta)
            {
                break;
            }
        }

        return best;
    }

    private IReadOnlyList<(int Row, int Column)> GetCandidateMoves(GameState state)
    {
        var legalMoves = _brain.GetLegalMoves(state);
        if (state.MoveHistory.Length == 0)
        {
            return [(state.Configuration.Height / 2, state.Configuration.Width / 2)];
        }

        var candidates = new HashSet<(int Row, int Column)>();
        foreach (var move in state.MoveHistory)
        {
            for (var rowOffset = -2; rowOffset <= 2; rowOffset++)
            {
                var row = move.Row + rowOffset;
                if (row < 0 || row >= state.Configuration.Height)
                {
                    continue;
                }

                for (var columnOffset = -2; columnOffset <= 2; columnOffset++)
                {
                    var column = move.Column + columnOffset;
                    if (state.Configuration.WrapHorizontal)
                    {
                        column = (column % state.Configuration.Width + state.Configuration.Width) % state.Configuration.Width;
                    }
                    else if (column < 0 || column >= state.Configuration.Width)
                    {
                        continue;
                    }

                    if (state.GetCell(row, column) == '.')
                    {
                        candidates.Add((row, column));
                    }
                }
            }
        }

        return candidates.Count == 0 ? legalMoves : candidates.OrderBy(move => move.Row).ThenBy(move => move.Column).ToArray();
    }

    private double Evaluate(GameState state, char rootSymbol, Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        var opponentSymbol = state.Players.Single(player => player.Symbol != rootSymbol).Symbol;
        var configuration = state.Configuration;
        (int RowStep, int ColumnStep)[] directions = [(0, 1), (1, 0), (1, 1), (1, -1)];
        double score = 0;

        foreach (var (rowStep, columnStep) in directions)
        {
            for (var row = 0; row < configuration.Height; row++)
            {
                CheckBudget(stopwatch, cancellationToken);
                for (var column = 0; column < configuration.Width; column++)
                {
                    var ownCount = 0;
                    var opponentCount = 0;
                    var valid = true;
                    for (var index = 0; index < configuration.WinningLength; index++)
                    {
                        CheckBudget(stopwatch, cancellationToken);
                        var currentRow = row + rowStep * index;
                        var currentColumn = column + columnStep * index;
                        if (currentRow < 0 || currentRow >= configuration.Height)
                        {
                            valid = false;
                            break;
                        }

                        if (configuration.WrapHorizontal)
                        {
                            currentColumn = (currentColumn % configuration.Width + configuration.Width) % configuration.Width;
                        }
                        else if (currentColumn < 0 || currentColumn >= configuration.Width)
                        {
                            valid = false;
                            break;
                        }

                        var cell = state.GetCell(currentRow, currentColumn);
                        if (cell == rootSymbol)
                        {
                            ownCount++;
                        }
                        else if (cell == opponentSymbol)
                        {
                            opponentCount++;
                        }
                    }

                    if (!valid || ownCount > 0 && opponentCount > 0)
                    {
                        continue;
                    }

                    if (ownCount == configuration.WinningLength
                        && configuration.OverlineRule == OverlineRule.Exactly
                        && IsExtended(state, row, column, rowStep, columnStep, rootSymbol))
                    {
                        continue;
                    }

                    if (opponentCount == configuration.WinningLength
                        && configuration.OverlineRule == OverlineRule.Exactly
                        && IsExtended(state, row, column, rowStep, columnStep, opponentSymbol))
                    {
                        continue;
                    }

                    var count = Math.Max(ownCount, opponentCount);
                    if (count == 0)
                    {
                        continue;
                    }

                    var windowScore = Math.Min(1_000_000d, Math.Pow(7, count));
                    score += ownCount > 0 ? windowScore : -windowScore;
                }
            }
        }

        return score;
    }

    private static bool IsExtended(
        GameState state,
        int startRow,
        int startColumn,
        int rowStep,
        int columnStep,
        char symbol)
    {
        var configuration = state.Configuration;
        if (rowStep == 0 && configuration.WrapHorizontal
            && configuration.WinningLength == configuration.Width)
        {
            return false;
        }

        var endRow = startRow + rowStep * (configuration.WinningLength - 1);
        var endColumn = startColumn + columnStep * (configuration.WinningLength - 1);
        if (configuration.WrapHorizontal)
        {
            endColumn = (endColumn % configuration.Width + configuration.Width) % configuration.Width;
        }

        foreach (var (neighborRow, neighborColumn) in new[]
        {
            (startRow - rowStep, startColumn - columnStep),
            (endRow + rowStep, endColumn + columnStep)
        })
        {
            if (neighborRow < 0 || neighborRow >= configuration.Height)
            {
                continue;
            }

            var currentNeighborColumn = neighborColumn;
            if (configuration.WrapHorizontal)
            {
                currentNeighborColumn = (neighborColumn % configuration.Width + configuration.Width) % configuration.Width;
            }
            else if (currentNeighborColumn < 0 || currentNeighborColumn >= configuration.Width)
            {
                continue;
            }

            if (state.GetCell(neighborRow, currentNeighborColumn) == symbol)
            {
                return true;
            }
        }

        return false;
    }

    private void CheckBudget(Stopwatch stopwatch, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (stopwatch.Elapsed >= _timeBudget)
        {
            throw new SearchTimeoutException();
        }
    }

    private sealed class SearchTimeoutException : Exception
    {
    }
}
