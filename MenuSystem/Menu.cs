namespace MenuSystem;

public sealed class Menu
{
    private readonly string _title;
    private readonly IReadOnlyList<MenuItem> _items;
    private readonly TextReader _input;
    private readonly TextWriter _output;

    public Menu(
        string title,
        IEnumerable<MenuItem> items,
        TextReader? input = null,
        TextWriter? output = null)
    {
        _title = title ?? throw new ArgumentNullException(nameof(title));
        _items = items?.ToArray() ?? throw new ArgumentNullException(nameof(items));
        _input = input ?? Console.In;
        _output = output ?? Console.Out;
        ValidateShortcuts();
    }

    public MenuResult Run(int depth = 0)
    {
        var items = BuildItems(depth);
        while (true)
        {
            _output.WriteLine();
            _output.WriteLine(_title);
            foreach (var item in items)
            {
                _output.WriteLine($"  [{item.Shortcut}] {item.Title}");
            }

            _output.Write("> ");
            var input = _input.ReadLine();
            if (input is null)
            {
                return MenuResult.Exit;
            }

            var shortcut = input.Trim();
            if (shortcut.Length != 1)
            {
                _output.WriteLine("Unknown shortcut. Try again.");
                continue;
            }

            var selected = items.SingleOrDefault(item =>
                item.Shortcut == char.ToUpperInvariant(shortcut[0]));
            if (selected is null)
            {
                _output.WriteLine("Unknown shortcut. Try again.");
                continue;
            }

            var result = selected.Action();
            if (result == MenuResult.Continue)
            {
                continue;
            }

            return result;
        }
    }

    private IReadOnlyList<MenuItem> BuildItems(int depth)
    {
        var items = _items.ToList();
        items.Add(new MenuItem('0', "Exit", () => MenuResult.Exit));
        if (depth >= 1)
        {
            items.Add(new MenuItem('9', "Return to previous menu", () => MenuResult.ReturnToPrevious));
        }

        if (depth >= 2)
        {
            items.Add(new MenuItem('8', "Return to main menu", () => MenuResult.ReturnToMain));
        }

        return items;
    }

    private void ValidateShortcuts()
    {
        var shortcuts = new HashSet<char>();
        foreach (var item in _items)
        {
            if (!shortcuts.Add(item.Shortcut))
            {
                throw new ArgumentException($"Duplicate menu shortcut '{item.Shortcut}'.", nameof(_items));
            }
        }

        foreach (var reserved in new[] { '0', '9', '8' })
        {
            if (shortcuts.Contains(reserved))
            {
                throw new ArgumentException($"Shortcut '{reserved}' is reserved for menu navigation.", nameof(_items));
            }
        }
    }
}
