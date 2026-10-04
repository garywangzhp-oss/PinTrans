using HanBridge.Core.Translation;

namespace HanBridge.Tests;

public sealed class LanguageGuardsTests
{
    [Theory]
    [InlineData("你好", true)]
    [InlineData("你好 world", true)]
    [InlineData("你", false)]
    [InlineData("hello world", false)]
    [InlineData("12345", false)]
    [InlineData("test@example.com", false)]
    [InlineData("https://example.com/你好", false)]
    [InlineData("C:\\work\\你好.txt", false)]
    [InlineData("git commit -m 你好", false)]
    [InlineData("const value = 你好;", false)]
    [InlineData("sk-abcdefghijklmnopqrstuvwxyz", false)]
    public void ShouldTranslate_AppliesSafetyRules(string text, bool expected)
    {
        var actual = LanguageGuards.ShouldTranslate(text, 200, out _);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("yiqiehuan", true)]
    [InlineData("yi'qie huan", true)]
    [InlineData("a", false)]
    [InlineData("https://example.com", false)]
    [InlineData("sk-abcdefghijklmnopqrstuvwxyz", false)]
    public void ShouldTranslatePinyin_AppliesSafetyRules(string text, bool expected)
    {
        var actual = LanguageGuards.ShouldTranslatePinyin(text, 200, out _);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void ShouldTranslate_RejectsTextAboveLimit()
    {
        var text = new string('你', 201);
        Assert.False(LanguageGuards.ShouldTranslate(text, 200, out var reason));
        Assert.Equal("source-too-long", reason);
    }
}