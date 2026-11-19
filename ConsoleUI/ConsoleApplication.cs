using GameEngine;
using MenuSystem;

namespace ConsoleUI;

internal sealed class ConsoleApplication(
    GameBrain brain,
    IConfigRepository configRepository,
    IGameRepository gameRepository)
{
    private const int BoardCellWidth = 3;

    public void Run()
    {
        MenuResult result;
        do
        {
            result = CreateMainMenu().Run();
        }
        while (result is MenuResult.ReturnToPrevious or MenuResult.ReturnToMain);
    }

    private Menu CreateMainMenu()
    {
        var newGameItem = new MenuItem('1', "New game", () => CreateNewGame());
        var configurationsItem = new MenuItem(
            '2',
            $"Configurations ({configRepository.List().Count})",
            () => CreateConfigurationMenu().Run(1));
        var savedGamesItem = new MenuItem('3', "Saved games", () => CreateSavedGamesMenu().Run(1));
        var rulesItem = new MenuItem('4', "Rules", () =>
        {
            ShowRules();
            return MenuResult.Continue;
        });

        return new Menu("GOMOKU — Main menu", [newGameItem, configurationsItem, savedGamesItem, rulesItem]);
    }

    private MenuResult CreateNewGame()
    {
        var mode = PromptChoice(
            "Play mode: [1] Human vs human, [2] Human vs random AI, [3] Random AI vs AI, [4] Human vs search AI",
            1,
            4);
        var configuration = ChooseConfiguration();
        if (configuration is null)
        {
            return MenuResult.Continue;
        }

        var firstKind = mode == 3 ? PlayerKind.RandomAi : PlayerKind.Human;
        var secondKind = mode switch
        {
            1 => PlayerKind.Human,
            4 => PlayerKind.SearchAi,
            _ => PlayerKind.RandomAi
        };
        var first = new GamePlayer(
            firstKind == PlayerKind.Human ? PromptName("First player", "Player 1") : "Random AI 1",
            'X',
            firstKind);
        var second = new GamePlayer(
            secondKind == PlayerKind.Human ? PromptName("Second player", "Player 2") : "Random AI",
            'O',
            secondKind);
        var name = PromptText("Game name", $"{first.Name} vs {second.Name}");
        var game = brain.StartGame(configuration, name, first, second);
        gameRepository.Save(game);
        PlayGame(game);
        return MenuResult.Continue;
    }

    private Menu CreateConfigurationMenu()
    {
        var manage = new MenuItem(
            '1',
            $"Manage custom configurations ({configRepository.List().Count(configuration => !configuration.IsPreset)})",
            () => CreateCustomConfigurationMenu().Run(2));
        var list = new MenuItem('2', "List configurations", () =>
        {
            ListConfigurations();
            return MenuResult.Continue;
        });
        return new Menu("Configurations", [manage, list]);
    }

    private Menu CreateCustomConfigurationMenu()
    {
        var create = new MenuItem('1', "Create custom configuration", () =>
        {
            CreateConfiguration();
            return MenuResult.Continue;
        });
        var list = new MenuItem('2', "List configurations", () =>
        {
            ListConfigurations();
            return MenuResult.Continue;
        });
        var edit = new MenuItem('3', "Edit custom configuration", () =>
        {
            EditConfiguration();
            return MenuResult.Continue;
        });
        var delete = new MenuItem('4', "Delete custom configuration", () =>
        {
            DeleteConfiguration();
            return MenuResult.Continue;
        });
        return new Menu("Custom configurations", [create, list, edit, delete]);
    }

    private Menu CreateSavedGamesMenu()
    {
        var list = new MenuItem('1', "List and continue a saved game", () =>
        {
            ContinueSavedGame();
            return MenuResult.Continue;
        });
        var delete = new MenuItem('2', "Delete a saved game", () =>
        {
            DeleteSavedGame();
            return MenuResult.Continue;
        });
        list.Title = $"Saved games ({gameRepository.List().Count})";
        return new Menu("Saved games", [list, delete]);
    }

    private GameConfiguration? ChooseConfiguration()
    {
        var configurations = configRepository.List();
        Console.WriteLine();
        Console.WriteLine("Configurations:");
        for (var index = 0; index < configurations.Count; index++)
        {
            Console.WriteLine($"  {index + 1}. {Describe(configurations[index])}");
        }

        Console.WriteLine("  0. Cancel");
        var selection = PromptChoice("Choose configuration", 0, configurations.Count);
        return selection == 0 ? null : configurations[selection - 1];
    }

    private void CreateConfiguration()
    {
        Console.WriteLine();
        var name = PromptText("Configuration name", "My Gomoku");
        var width = PromptPositiveInt("Width");
        var height = PromptPositiveInt("Height");
        var winningLength = PromptPositiveInt("Winning length");
        var wrap = PromptYesNo("Wrap columns to make a cylinder");
        var overline = PromptChoice("Overline: [1] five or more, [2] exactly five", 1, 2) == 1
            ? OverlineRule.AtLeast
            : OverlineRule.Exactly;
        var configuration = new GameConfiguration(Guid.NewGuid(), name, width, height, winningLength, wrap, overline);
        try
        {
            configuration.Validate();
            configRepository.Save(configuration);
            Console.WriteLine("Configuration saved.");
        }
        catch (ArgumentException exception)
        {
            Console.WriteLine($"Configuration rejected: {exception.Message}");
        }
    }

    private void ListConfigurations()
    {
        Console.WriteLine();
        foreach (var configuration in configRepository.List())
        {
            Console.WriteLine($"- {Describe(configuration)}{(configuration.IsPreset ? " [preset]" : "")}");
        }
    }

    private void EditConfiguration()
    {
        var configuration = ChooseCustomConfiguration();
        if (configuration is null)
        {
            return;
        }

        Console.WriteLine($"Editing {configuration.Name}. Leave a number blank to keep it.");
        var name = PromptOptionalText("Name", configuration.Name);
        var width = PromptOptionalPositiveInt("Width", configuration.Width);
        var height = PromptOptionalPositiveInt("Height", configuration.Height);
        var winningLength = PromptOptionalPositiveInt("Winning length", configuration.WinningLength);
        var wrap = PromptOptionalYesNo("Wrap columns", configuration.WrapHorizontal);
        var overline = PromptChoice(
            $"Overline: [1] five or more, [2] exactly five (current {(configuration.OverlineRule == OverlineRule.AtLeast ? 1 : 2)})",
            1,
            2);
        var updated = configuration with
        {
            Name = name,
            Width = width,
            Height = height,
            WinningLength = winningLength,
            WrapHorizontal = wrap,
            OverlineRule = overline == 1 ? OverlineRule.AtLeast : OverlineRule.Exactly
        };

        try
        {
            updated.Validate();
            configRepository.Save(updated);
            Console.WriteLine("Configuration updated.");
        }
        catch (ArgumentException exception)
        {
            Console.WriteLine($"Configuration rejected: {exception.Message}");
        }
    }

    private void DeleteConfiguration()
    {
        var configuration = ChooseCustomConfiguration();
        if (configuration is null)
        {
            return;
        }

        Console.WriteLine(configRepository.Delete(configuration.Id)
            ? "Configuration deleted."
            : "Configuration was not found or is a protected preset.");
    }

    private GameConfiguration? ChooseCustomConfiguration()
    {
        var custom = configRepository.List().Where(configuration => !configuration.IsPreset).ToArray();
        if (custom.Length == 0)
        {
            Console.WriteLine("There are no custom configurations yet.");
            return null;
        }

        for (var index = 0; index < custom.Length; index++)
        {
            Console.WriteLine($"  {index + 1}. {Describe(custom[index])}");
        }

        var selection = PromptChoice("Choose custom configuration", 1, custom.Length);
        return custom[selection - 1];
    }

    private void ContinueSavedGame()
    {
        var games = gameRepository.List();
        if (games.Count == 0)
        {
            Console.WriteLine("There are no saved games.");
            return;
        }

        for (var index = 0; index < games.Count; index++)
        {
            Console.WriteLine(
                $"  {index + 1}. {games[index].Name} — {games[index].Configuration.Name} — " +
                $"{games[index].UpdatedAt.LocalDateTime:g} — {games[index].Outcome}");
        }

        var selection = PromptChoice("Choose saved game", 1, games.Count);
        PlayGame(games[selection - 1]);
    }

    private void DeleteSavedGame()
    {
        var games = gameRepository.List();
        if (games.Count == 0)
        {
            Console.WriteLine("There are no saved games.");
            return;
        }

        for (var index = 0; index < games.Count; index++)
        {
            Console.WriteLine($"  {index + 1}. {games[index].Name}");
        }

        var selection = PromptChoice("Choose saved game to delete", 1, games.Count);
        Console.WriteLine(gameRepository.Delete(games[selection - 1].Id) ? "Game deleted." : "Game was not found.");
    }

    private void PlayGame(GameState initialState)
    {
        var state = initialState;
        var providers = state.Players.ToDictionary(
            player => player.Symbol,
            player => CreateMoveProvider(player.Kind));
        var aiVsAi = state.Players.All(player => player.Kind != PlayerKind.Human);
        while (state.Outcome == GameOutcome.InProgress)
        {
            DrawBoard(state);
            if (aiVsAi)
            {
                Thread.Sleep(250);
            }

            var provider = providers[state.CurrentPlayer.Symbol];
            var move = provider.GetMoveAsync(state, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            var result = brain.MakeMove(state, move.Row, move.Column);
            if (result.Status is MoveStatus.OutOfBounds or MoveStatus.Occupied)
            {
                throw new InvalidOperationException($"Move provider returned an illegal move: {result.Status}.");
            }

            state = result.State;
            gameRepository.Save(state);
        }

        DrawBoard(state);
        if (state.Outcome == GameOutcome.Won)
        {
            Console.WriteLine($"{state.Players.Single(player => player.Symbol == state.WinnerSymbol).Name} wins!");
        }
        else if (state.Outcome == GameOutcome.Draw)
        {
            Console.WriteLine("Draw.");
        }
    }

    private IMoveProvider CreateMoveProvider(PlayerKind kind) => kind switch
    {
        PlayerKind.Human => new ConsoleMoveProvider(),
        PlayerKind.SearchAi => new SearchAiMoveProvider(brain),
        _ => new RandomAiMoveProvider(brain)
    };

    private static void DrawBoard(GameState state)
    {
        Console.WriteLine();
        Console.WriteLine($"{state.Name} — {state.Configuration.Name}");
        Console.Write("   +");
        for (var column = 0; column < state.Configuration.Width; column++)
        {
            Console.Write($"{new string('-', BoardCellWidth)}+");
        }

        Console.WriteLine();
        Console.Write("   |");
        for (var column = 0; column < state.Configuration.Width; column++)
        {
            WriteAxisLabel($"{ConsoleMoveProvider.ColumnLabel(column),BoardCellWidth}", ConsoleColor.Cyan);
            Console.Write('|');
        }

        Console.WriteLine();
        DrawBoardBorder(state.Configuration.Width);

        for (var row = 0; row < state.Configuration.Height; row++)
        {
            WriteAxisLabel($"{row + 1,3}", ConsoleColor.Yellow);
            Console.Write('|');
            for (var column = 0; column < state.Configuration.Width; column++)
            {
                var symbol = state.GetCell(row, column);
                var isLastMove = state.LastMove is { } lastMove
                    && lastMove.Row == row
                    && lastMove.Column == column;
                WriteBoardCell(symbol, isLastMove);
            }

            Console.WriteLine();
            DrawBoardBorder(state.Configuration.Width);
        }

        Console.WriteLine($"Turn: {state.CurrentPlayer.Name} ({ConsoleSymbols.Display(state.CurrentPlayer.Symbol)})");
    }

    private static void DrawBoardBorder(int width)
    {
        Console.Write("   +");
        for (var column = 0; column < width; column++)
        {
            Console.Write($"{new string('-', BoardCellWidth)}+");
        }

        Console.WriteLine();
    }

    private static void WriteAxisLabel(string label, ConsoleColor color)
    {
        var previousColor = Console.ForegroundColor;
        Console.ForegroundColor = color;
        Console.Write(label);
        Console.ForegroundColor = previousColor;
    }

    private static void WriteBoardCell(char symbol, bool highlighted)
    {
        var previousColor = Console.ForegroundColor;
        var previousBackground = Console.BackgroundColor;
        if (highlighted)
        {
            Console.BackgroundColor = ConsoleColor.DarkGreen;
        }

        var contents = symbol switch
        {
            'X' or 'O' => $" {ConsoleSymbols.Display(symbol)}",
            _ => " . "
        };
        Console.Write($"{contents}|");
        Console.ForegroundColor = previousColor;
        Console.BackgroundColor = previousBackground;
    }

    private static void ShowRules()
    {
        Console.WriteLine();
        Console.WriteLine("Place stones in turns. The configured line length wins.");
        Console.WriteLine("Cylinder boards wrap at the left/right edge, including diagonal lines.");
        Console.WriteLine("Overline is either five-or-more or exactly the configured length.");
    }

    private static string Describe(GameConfiguration configuration) =>
        $"{configuration.Name}: {configuration.Width}x{configuration.Height}, line {configuration.WinningLength}, " +
        $"{(configuration.WrapHorizontal ? "cylinder" : "flat")}, {configuration.OverlineRule}";

    private static string PromptName(string label, string defaultValue) => PromptText(label, defaultValue);

    private static string PromptText(string label, string defaultValue)
    {
        Console.Write($"{label} [{defaultValue}]: ");
        var value = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(value) ? defaultValue : value;
    }

    private static string PromptOptionalText(string label, string current)
    {
        Console.Write($"{label} [{current}]: ");
        var value = Console.ReadLine()?.Trim();
        return string.IsNullOrEmpty(value) ? current : value;
    }

    private static int PromptPositiveInt(string label)
    {
        while (true)
        {
            Console.Write($"{label}: ");
            if (int.TryParse(Console.ReadLine(), out var value) && value > 0)
            {
                return value;
            }

            Console.WriteLine("Enter a positive whole number.");
        }
    }

    private static int PromptOptionalPositiveInt(string label, int current)
    {
        while (true)
        {
            Console.Write($"{label} [{current}]: ");
            var input = Console.ReadLine();
            if (string.IsNullOrWhiteSpace(input))
            {
                return current;
            }

            if (int.TryParse(input, out var value) && value > 0)
            {
                return value;
            }

            Console.WriteLine("Enter a positive whole number or leave blank.");
        }
    }

    private static bool PromptYesNo(string label)
    {
        while (true)
        {
            Console.Write($"{label}? [y/n]: ");
            var answer = Console.ReadLine()?.Trim().ToUpperInvariant();
            if (answer is "Y" or "YES")
            {
                return true;
            }

            if (answer is "N" or "NO")
            {
                return false;
            }

            Console.WriteLine("Enter y or n.");
        }
    }

    private static bool PromptOptionalYesNo(string label, bool current)
    {
        while (true)
        {
            Console.Write($"{label} [{(current ? "y" : "n")}]: ");
            var answer = Console.ReadLine()?.Trim().ToUpperInvariant();
            if (string.IsNullOrEmpty(answer))
            {
                return current;
            }

            if (answer is "Y" or "YES")
            {
                return true;
            }

            if (answer is "N" or "NO")
            {
                return false;
            }

            Console.WriteLine("Enter y, n, or leave blank.");
        }
    }

    private static int PromptChoice(string prompt, int minimum, int maximum)
    {
        while (true)
        {
            Console.Write($"{prompt}: ");
            if (int.TryParse(Console.ReadLine(), out var value) && value >= minimum && value <= maximum)
            {
                return value;
            }

            Console.WriteLine($"Enter a number from {minimum} to {maximum}.");
        }
    }
}
