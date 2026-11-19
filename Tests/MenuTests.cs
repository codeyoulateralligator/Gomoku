using MenuSystem;

namespace Tests;

public sealed class MenuTests
{
    [Fact]
    public void InvalidShortcutIsRejectedAndMenuKeepsRunning()
    {
        var output = new StringWriter();
        var menu = new Menu("Main", [], new StringReader("x\n0\n"), output);

        Assert.Equal(MenuResult.Exit, menu.Run());
        Assert.Contains("Unknown shortcut", output.ToString());
    }

    [Fact]
    public void ReturnToMainUnwindsNestedMenus()
    {
        var deepMenu = new Menu("Deep", [], new StringReader("8\n"), new StringWriter());
        var middleMenu = new Menu(
            "Middle",
            [new MenuItem('1', "Open deep menu", () => deepMenu.Run(2))],
            new StringReader("1\n"),
            new StringWriter());
        var rootMenu = new Menu(
            "Root",
            [new MenuItem('1', "Open middle menu", () => middleMenu.Run(1))],
            new StringReader("1\n"),
            new StringWriter());

        Assert.Equal(MenuResult.ReturnToMain, rootMenu.Run());
    }

    [Fact]
    public void DuplicateAndReservedShortcutsAreRejected()
    {
        Assert.Throws<ArgumentException>(() => new Menu(
            "Duplicate",
            [new MenuItem('a', "A", () => MenuResult.Continue), new MenuItem('A', "Again", () => MenuResult.Continue)]));
        Assert.Throws<ArgumentException>(() => new Menu("Reserved", [new MenuItem('0', "Zero", () => MenuResult.Continue)]));
    }

    [Fact]
    public void MenuItemTitleCanChangeAfterTheMenuIsBuilt()
    {
        var output = new StringWriter();
        var item = new MenuItem('1', "Games (0)", () => MenuResult.Continue);
        var menu = new Menu("Main", [item], new StringReader("0\n0\n"), output);
        item.Title = "Games (3)";

        Assert.Equal(MenuResult.Exit, menu.Run());
        Assert.Contains("Games (3)", output.ToString());
    }

    [Fact]
    public void NavigationItemsAreAddedForEachMenuDepth()
    {
        var rootOutput = new StringWriter();
        new Menu("Root", [], new StringReader("0\n"), rootOutput).Run();
        Assert.Contains("Exit", rootOutput.ToString());
        Assert.DoesNotContain("Return to previous", rootOutput.ToString());

        var childOutput = new StringWriter();
        var childResult = new Menu("Child", [], new StringReader("9\n"), childOutput).Run(1);
        Assert.Equal(MenuResult.ReturnToPrevious, childResult);
        Assert.Contains("Return to previous menu", childOutput.ToString());
        Assert.DoesNotContain("Return to main menu", childOutput.ToString());

        var deepOutput = new StringWriter();
        new Menu("Deep", [], new StringReader("8\n"), deepOutput).Run(2);
        Assert.Contains("Return to previous menu", deepOutput.ToString());
        Assert.Contains("Return to main menu", deepOutput.ToString());
    }
}
