using FramePilot.Core.Infrastructure;

namespace FramePilot.Core.Tests;

public class CommandLineSplitterTests
{
    [Fact]
    public void Split_SeparatesByWhitespace()
    {
        var arguments = CommandLineSplitter.Split("--windowed -foo bar");

        Assert.Equal(3, arguments.Count);
        Assert.Equal("--windowed", arguments[0]);
        Assert.Equal("-foo", arguments[1]);
        Assert.Equal("bar", arguments[2]);
    }

    [Fact]
    public void Split_KeepsQuotedValuesTogether()
    {
        var arguments = CommandLineSplitter.Split("--path \"C:\\Games\\My Game\\game.exe\"");

        Assert.Equal(2, arguments.Count);
        Assert.Equal("--path", arguments[0]);
        Assert.Equal(@"C:\Games\My Game\game.exe", arguments[1]);
    }

    [Fact]
    public void Split_HandlesEscapedQuotes()
    {
        var arguments = CommandLineSplitter.Split("--title \"say \"\"hi\"\"\"");

        Assert.Single(arguments);
        Assert.Equal("say \"hi\"", arguments[0]);
    }

    [Fact]
    public void Split_WithEmptyInput_ReturnsEmptyList()
    {
        Assert.Empty(CommandLineSplitter.Split(null));
        Assert.Empty(CommandLineSplitter.Split("   "));
    }

    [Fact]
    public void Split_DoesNotEvaluateTheValues()
    {
        var arguments = CommandLineSplitter.Split("--name \"a & b\"");

        Assert.Single(arguments);
        Assert.Equal("a & b", arguments[0]);
    }
}
