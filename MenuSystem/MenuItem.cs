namespace MenuSystem;

public sealed class MenuItem
{
    public MenuItem(char shortcut, string title, Func<MenuResult> action)
    {
        Shortcut = char.ToUpperInvariant(shortcut);
        Title = title ?? throw new ArgumentNullException(nameof(title));
        Action = action ?? throw new ArgumentNullException(nameof(action));
    }

    public char Shortcut { get; }
    public string Title { get; set; }
    public Func<MenuResult> Action { get; }
}
