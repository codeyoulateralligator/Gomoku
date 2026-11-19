namespace GameEngine;

public sealed record GameConfiguration(
    Guid Id,
    string Name,
    int Width,
    int Height,
    int WinningLength,
    bool WrapHorizontal,
    OverlineRule OverlineRule,
    bool IsPreset = false,
    bool IsTestOnly = false)
{
    private const long MaximumBoardCells = 10_000;

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            throw new ArgumentException("Configuration name cannot be empty.", nameof(Name));
        }

        if (Width < 2 || Height < 2)
        {
            throw new ArgumentOutOfRangeException(nameof(Width), "Board dimensions must be at least 2.");
        }

        if ((long)Width * Height > MaximumBoardCells)
        {
            throw new ArgumentException($"A board cannot contain more than {MaximumBoardCells} cells.");
        }

        if (WinningLength < 2 || WinningLength > Math.Max(Width, Height))
        {
            throw new ArgumentOutOfRangeException(
                nameof(WinningLength),
                "Winning length must be at least 2 and fit in a board dimension.");
        }

        if (WrapHorizontal && WinningLength > Width)
        {
            throw new ArgumentException("On a cylinder, winning length cannot exceed board width.");
        }

        if (!Enum.IsDefined(OverlineRule))
        {
            throw new ArgumentOutOfRangeException(nameof(OverlineRule));
        }
    }

    public static IReadOnlyList<GameConfiguration> Presets { get; } =
    [
        new(Guid.Parse("00000000-0000-0000-0000-000000000101"), "Classic Gomoku", 15, 15, 5, false, OverlineRule.AtLeast, true),
        new(Guid.Parse("00000000-0000-0000-0000-000000000102"), "Cylinder Gomoku", 15, 15, 5, true, OverlineRule.AtLeast, true),
        new(Guid.Parse("00000000-0000-0000-0000-000000000103"), "Exact Five", 15, 15, 5, false, OverlineRule.Exactly, true),
        new(Guid.Parse("00000000-0000-0000-0000-000000000104"), "Compact Test Board", 5, 5, 5, false, OverlineRule.AtLeast, true, true)
    ];
}
