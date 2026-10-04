using System.Text;
using System.Text.RegularExpressions;

namespace HanBridge.Core.Translation;

public static partial class LanguageGuards
{
    [GeneratedRegex(@"(?i)\b[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}\b")]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"(?i)(?:https?://|www\.)\S+")]
    private static partial Regex UrlRegex();

    [GeneratedRegex(@"^(?:[A-Za-z]:\\|\\\\|/)")]
    private static partial Regex FilePathRegex();

    [GeneratedRegex(@"(?i)(?:^|\s)(?:git|npm|pnpm|yarn|dotnet|curl|wget|sudo|ssh|docker|kubectl)\s+\S+")]
    private static partial Regex CommandRegex();

    [GeneratedRegex(@"^[A-Za-z0-9' ]+$")]
    private static partial Regex PinyinRegex();

    [GeneratedRegex(@"(?:[{};]|=>|->|::|</?[A-Za-z][^>]*>)")]
    private static partial Regex CodeRegex();

    [GeneratedRegex(@"(?:sk-[A-Za-z0-9_-]{16,}|Bearer\s+[A-Za-z0-9._-]{16,})", RegexOptions.IgnoreCase)]
    private static partial Regex SecretRegex();

    public static bool ShouldTranslate(string text, int maximumHanCharacters, out string reason)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            reason = "empty";
            return false;
        }

        if (trimmed.Contains('\r') || trimmed.Contains('\n'))
        {
            reason = "multiline";
            return false;
        }

        var hanCount = CountHan(trimmed);
        if (hanCount < 2)
        {
            reason = "too-few-han";
            return false;
        }

        if (hanCount > maximumHanCharacters)
        {
            reason = "source-too-long";
            return false;
        }

        if (!trimmed.Any(ch => char.IsLetterOrDigit(ch) || IsHan(ch)))
        {
            reason = "no-meaningful-text";
            return false;
        }

        if (EmailRegex().IsMatch(trimmed))
        {
            reason = "email";
            return false;
        }

        if (UrlRegex().IsMatch(trimmed))
        {
            reason = "url";
            return false;
        }

        if (FilePathRegex().IsMatch(trimmed))
        {
            reason = "file-path";
            return false;
        }

        if (CommandRegex().IsMatch(trimmed))
        {
            reason = "command";
            return false;
        }

        if (CodeRegex().IsMatch(trimmed))
        {
            reason = "code-like";
            return false;
        }

        if (SecretRegex().IsMatch(trimmed))
        {
            reason = "secret-like";
            return false;
        }

        reason = "ok";
        return true;
    }

    public static bool ShouldTranslatePinyin(string text, int maximumCharacters, out string reason)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            reason = "empty";
            return false;
        }

        if (trimmed.Length > maximumCharacters)
        {
            reason = "source-too-long";
            return false;
        }

        if (!PinyinRegex().IsMatch(trimmed))
        {
            reason = "not-pinyin";
            return false;
        }

        if (trimmed.Count(char.IsLetter) < 2)
        {
            reason = "too-short";
            return false;
        }

        if (EmailRegex().IsMatch(trimmed))
        {
            reason = "email";
            return false;
        }

        if (UrlRegex().IsMatch(trimmed))
        {
            reason = "url";
            return false;
        }

        if (FilePathRegex().IsMatch(trimmed))
        {
            reason = "file-path";
            return false;
        }

        if (CommandRegex().IsMatch(trimmed))
        {
            reason = "command";
            return false;
        }

        if (SecretRegex().IsMatch(trimmed))
        {
            reason = "secret-like";
            return false;
        }

        reason = "ok";
        return true;
    }

    public static int CountHan(string text)
    {
        var count = 0;
        foreach (var rune in text.EnumerateRunes())
        {
            if (IsHan(rune))
            {
                count++;
            }
        }

        return count;
    }

    private static bool IsHan(Rune rune)
    {
        var value = rune.Value;
        return value is >= 0x3400 and <= 0x4DBF
            or >= 0x4E00 and <= 0x9FFF
            or >= 0xF900 and <= 0xFAFF
            or >= 0x20000 and <= 0x2FA1F;
    }

    private static bool IsHan(char value) => IsHan(new Rune(value));
}