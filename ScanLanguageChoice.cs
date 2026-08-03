namespace Mantis_backdoor_scanner;

public enum ScanLanguageChoice
{
    AutoDetect = 0,
    Lua = 1,
    Cpp = 2,
    Rust = 3,
    CSharp = 4,
    DotNetModule = 5,
    Expression2 = 6,
}

public static class ScanLanguageChoiceExtensions
{
    public static string DisplayName(this ScanLanguageChoice value)
    {
        return value switch
        {
            ScanLanguageChoice.AutoDetect => "Auto-detect",
            ScanLanguageChoice.Lua => "Lua",
            ScanLanguageChoice.Cpp => "C++",
            ScanLanguageChoice.Rust => "Rust",
            ScanLanguageChoice.CSharp => "C#",
            ScanLanguageChoice.DotNetModule => ".NET Module",
            ScanLanguageChoice.Expression2 => "Expression 2",
            _ => value.ToString(),
        };
    }
}
