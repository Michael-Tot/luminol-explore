using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Lua;

static class Ansi
{
    // Reset everything
    public const string Reset = "\e[0m";
    public const string EraseLine = "\e[K";

    // Styles
    public const string Bold          = "\e[1m";
    public const string Dim           = "\e[2m";
    public const string Italic        = "\e[3m";
    public const string Underline     = "\e[4m";
    public const string Blink         = "\e[5m";
    public const string Inverse       = "\e[7m";
    public const string Hidden        = "\e[8m";
    public const string Strikethrough = "\e[9m";
    public const string Overline      = "\e[53m";

    // Style resets
    public const string NoBold          = "\e[22m"; // also turns off dim
    public const string NoDim           = "\e[22m"; // also turns off bold
    public const string NoItalic        = "\e[23m";
    public const string NoUnderline     = "\e[24m";
    public const string NoBlink         = "\e[25m";
    public const string NoInverse       = "\e[27m";
    public const string NoHidden        = "\e[28m";
    public const string NoStrikethrough = "\e[29m";
    public const string NoOverline      = "\e[55m";

    // Color resets
    public const string ResetFG    = "\e[39m";
    public const string ResetBG    = "\e[49m";
    public const string ResetColor = "\e[39;49m";

    public static string FG(int r, int g, int b) => $"\e[38;2;{r};{g};{b}m";
    public static string BG(int r, int g, int b) => $"\e[48;2;{r};{g};{b}m";
}

static class Theme
{
    public static bool Enabled { get; set; } = 
        !Console.IsOutputRedirected &&
        string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NO_COLOR"));

    // Code Blocks
    public static string CodeBackground { get; } = Ansi.BG(28, 30, 36);

    // Highlight Colors
    public static string HighlightKeyword	{ get; } = Ansi.FG(230, 130, 190);
    public static string HighlightString	{ get; } = Ansi.FG(143, 209, 143);
    public static string HighlightNumber	{ get; } = Ansi.FG(232, 160, 122);
    public static string HighlightComment	{ get; } = Ansi.FG(124, 129, 144);
    public static string HighlightLocal	    { get; } = Ansi.FG(167, 150, 255);
    public static string HighlightParam	    { get; } = Ansi.FG(201, 190, 255);
    public static string HighlightIter	    { get; } = Ansi.FG(201, 190, 255);
    public static string HighlightGlobal	{ get; } = Ansi.FG(125, 207, 216);
    public static string HighlightDanger	{ get; } = Ansi.FG(229, 72, 77);

    // Semantic colors
    public static readonly string Glow  = Ansi.FG(60, 155, 245);
    public static readonly string Ident = Ansi.FG(140, 120, 240);
    public static readonly string Success = Ansi.FG(70, 195, 110);
    public static readonly string Warn  = Ansi.FG(220, 150, 40);
    public static readonly string Error = Ansi.FG(225, 65, 70);

    // Glyphs
    public const string Dot    = "·";
    public const char   Line   = '─';
    public const string Arrow  = "→";
    public const string Branch = "│ ";
    public const string Return = "└ ";

    public const int Width = 60;

    // Wrappers: each turns on one style and turns only that style back off
    static string Wrap(string on, string off, string text) => $"{on}{text}{off}";

    public static string Faint(string text)       => Wrap(Ansi.Dim, Ansi.NoDim, text);
    public static string Strong(string text)      => Wrap(Ansi.Bold, Ansi.NoBold, text);
    public static string Italicized(string text)  => Wrap(Ansi.Italic, Ansi.NoItalic, text);
    public static string Underlined(string text)  => Wrap(Ansi.Underline, Ansi.NoUnderline, text);
    public static string Blinking(string text)    => Wrap(Ansi.Blink, Ansi.NoBlink, text);
    public static string Inverted(string text)    => Wrap(Ansi.Inverse, Ansi.NoInverse, text);
    public static string Concealed(string text)   => Wrap(Ansi.Hidden, Ansi.NoHidden, text);
    public static string Struck(string text)      => Wrap(Ansi.Strikethrough, Ansi.NoStrikethrough, text);
    public static string Overlined(string text)   => Wrap(Ansi.Overline, Ansi.NoOverline, text);

    public static string Colored(string color, string text) => Wrap(color, Ansi.ResetFG, text);
    public static string OnBackground(string bg, string text) => Wrap(bg, Ansi.ResetBG, text);

    /// <summary>
    /// A fixed-width rule: "── title ────────". The title must be plain text,
    /// since escape codes inside it would count toward the visible length.
    /// </summary>
    public static string Rule(string title, int width = Width)
    {
        var head = $"{Line}{Line} {title} ";
        var fill = Math.Max(3, width - head.Length);
        return head + new string(Line, fill);
    }

    /// <summary>Major section: bold glowing rule.</summary>
    public static string Header(string title) => Strong(Colored(Glow, Rule(title)));

    /// <summary>Minor section: faint rule.</summary>
    public static string SubHeader(string title)
    {
        var head = $"{Line}{Line} ";
        var fill = Math.Max(3, Width - head.Length - title.Length - 1);
        return $"{Faint(head)}{Colored(Glow, title)} {Faint(new string(Line, fill))}";
    }

    /// <summary>Joins parts with a spaced dot: "Pass 1 · Concat fold".</summary>
    public static string Dotted(params string[] parts) => string.Join($" {Dot} ", parts);

    // Printing
    public static void Print(string text = "")   => Console.WriteLine(text);
    public static void PrintHeader(string title) => Print(Header(title));
    public static void PrintSub(string title)    => Print(SubHeader(title));
    public static void PrintFaint(string text)   => Print(Faint(text));
    public static void PrintStrong(string text)  => Print(Strong(text));
    public static void PrintGlow(string text)    => Print(Colored(Glow, text));
    public static void PrintWarn(string text)    => Print(Colored(Warn, text));
    public static void PrintError(string text)   => Print(Colored(Error, text));
    public static void PrintSuccess(string text) => Print(Colored(Success, text));
    public static void PrintCode(SyntaxNode node) => Print(node.ToHighlighted());
    public static void PrintDiagnostics(SyntaxNode node)
    {
        var appendixString = string.Join("\n", node.GetDiagnostics().Select(d => d.Severity == DiagnosticSeverity.Error ? Colored(Error, d.ToString()) : Colored(Warn, d.ToString())));

        if (appendixString.Length > 0)
            Print(appendixString);
    }
    public static void PrintCode(string section, SyntaxNode node, bool major = false, bool newLine = true) 
    {
        PrintSection(section, node.ToHighlighted(), major, false);
        PrintDiagnostics(node);

        if (newLine)
            Print();
    }

    /// <summary>Header, body, blank line: the shape of the Input/Output sections.</summary>
    public static void PrintSection(string title, string body, bool major = false, bool newLine = true)
    {
        Print(major ? Header(title) : SubHeader(title));
        Print(body);

        if (newLine)
            Print();
    }

    public static string CodeBlock(string code)
    {
        var lines = code.ReplaceLineEndings("\n").Split('\n');
        return string.Join("\n", lines.Select(l => OnBackground(CodeBackground, $" {l}{Ansi.EraseLine}")));
    }

    public static string ColorTrivia(SyntaxTrivia trivia) => trivia.Kind() switch
    {
        SyntaxKind.SingleLineCommentTrivia
            or SyntaxKind.MultiLineCommentTrivia
            or SyntaxKind.ShebangTrivia  => Italicized(Colored(HighlightComment, trivia.ToFullString())),

        SyntaxKind.SkippedTokensTrivia   => Colored(Error, trivia.ToFullString()),

        _                                => trivia.ToFullString()
    };

    public static string ColorToken(SyntaxToken token) => token.Kind() switch
    {
        var k when SyntaxFacts.IsKeyword(k)      => Colored(HighlightKeyword, token.Text),

        SyntaxKind.StringLiteralToken
            or SyntaxKind.HashStringLiteralToken => Colored(HighlightString, token.Text),

        SyntaxKind.NumericLiteralToken           => Colored(HighlightNumber, token.Text),

        _                                        => token.Text
    };

}