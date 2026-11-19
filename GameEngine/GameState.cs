namespace GameEngine;

public enum GameOutcome
{
    InProgress,
    Won,
    Draw
}

public enum PlayerKind
{
    Human,
    RandomAi,
    SearchAi
}

public sealed record GamePlayer(string Name, char Symbol, PlayerKind Kind);

public sealed record GameMove(int Row, int Column, char Symbol, DateTimeOffset PlayedAt);

public sealed record GameState(
    Guid Id,
    string Name,
    GameConfiguration Configuration,
    string[] Board,
    GamePlayer[] Players,
    int CurrentPlayerIndex,
    GameMove[] MoveHistory,
    GameOutcome Outcome,
    char? WinnerSymbol,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt)
{
    public GamePlayer CurrentPlayer => Players[CurrentPlayerIndex];
    public GameMove? LastMove => MoveHistory.Length == 0 ? null : MoveHistory[^1];

    public char GetCell(int row, int column) => Board[row][column];
}
