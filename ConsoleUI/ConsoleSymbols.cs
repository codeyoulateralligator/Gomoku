namespace ConsoleUI;

internal static class ConsoleSymbols
{
    public static string Display(char symbol) => symbol switch
    {
        'X' => "🍌",
        'O' => "🍆",
        _ => symbol.ToString()
    };
}
