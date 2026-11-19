using GameEngine;

namespace ConsoleUI;

internal sealed class ConsoleMoveProvider : IMoveProvider
{
    public ValueTask<(int Row, int Column)> GetMoveAsync(GameState state, CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Console.Write($"{state.CurrentPlayer.Name} ({ConsoleSymbols.Display(state.CurrentPlayer.Symbol)}) move (e.g. H8): ");
            var input = Console.ReadLine();
            if (input is null)
            {
                throw new OperationCanceledException("Console input ended during a human turn.", cancellationToken);
            }

            if (TryParseCoordinate(input, state.Configuration.Width, state.Configuration.Height, out var row, out var column))
            {
                if (state.GetCell(row, column) != '.')
                {
                    Console.WriteLine("That square is already occupied. Your turn was not consumed.");
                    continue;
                }

                return ValueTask.FromResult((row, column));
            }

            Console.WriteLine("Invalid coordinate. Enter a column label and row number, for example H8.");
        }
    }

    internal static string ColumnLabel(int column)
    {
        var label = "";
        for (var value = column + 1; value > 0; value = (value - 1) / 26)
        {
            label = (char)('A' + (value - 1) % 26) + label;
        }

        return label;
    }

    private static bool TryParseCoordinate(string input, int width, int height, out int row, out int column)
    {
        row = -1;
        column = -1;
        var text = input.Trim().ToUpperInvariant();
        var index = 0;
        long parsedColumn = 0;
        while (index < text.Length && text[index] is >= 'A' and <= 'Z')
        {
            parsedColumn = parsedColumn * 26 + text[index] - 'A' + 1;
            if (parsedColumn > width)
            {
                return false;
            }

            index++;
        }

        if (index == 0
            || index == text.Length
            || !int.TryParse(text.AsSpan(index), out var rowNumber)
            || rowNumber < 1
            || rowNumber > height)
        {
            return false;
        }

        column = (int)parsedColumn - 1;
        row = rowNumber - 1;
        return true;
    }
}
