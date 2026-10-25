using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Lua;
using Loretta.CodeAnalysis.Lua.Syntax;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Timers;
using System.Xml.Linq;
using static Theme;

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;

if (args.Contains("--no-color")) Theme.Enabled = false;
if (args.Contains("--color")) Theme.Enabled = true;

Console.OutputEncoding = Encoding.UTF8; // box-drawing characters
if (Debugger.IsAttached)
    Console.SetOut(new DebugOutputWriter(Console.Out));

const string file = "string_concat.lua";

var path = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "samples", file);
var src = args.Length > 0 ? args[0] : Path.Combine("samples", file);

var t0 = Stopwatch.GetTimestamp();
var t = t0;
Print($"Reading {path}."); 
var source = File.ReadAllText(path);
PrintSuccess($"Finished reading file in {Stopwatch.GetElapsedTime(t).TotalMilliseconds:F1} ms");
// GMod preset = LuaJIT 2.0 + GLua extras (!=, &&, ||, //-comments, continue).
var options = new LuaParseOptions(LuaSyntaxOptions.GMod);
var tree = LuaSyntaxTree.ParseText(source, options, src);
var initialRoot = tree.GetRoot();
var root = initialRoot;

static void Dump(SyntaxNodeOrToken item, int depth)
{
    var guides = Faint(string.Concat(Enumerable.Repeat(Branch, depth)));

    if (item.IsToken)
    {
        var token = item.AsToken();
        Print($"{guides}{Faint(token.Kind().ToString())} {Colored(Ident, $"'{token.Text}'")}");
        return;
    }

    var node = item.AsNode()!;
    Print($"{guides}{node.GetType().Name} {Faint(node.Kind().ToString())}");
    foreach (var child in node.ChildNodesAndTokens())
        Dump(child, depth + 1);
}

PrintCode("First Parse", root, true);

// TODO: Convert this to a PassResults object instead of adding more refs. We want to catch errors too.
int counter = 0;
int failedCounter = 0;
double totalTime = 0;
root = root
    .Pass<StringConcatFoldRewriter>("Concat fold", ref totalTime, ref counter);

Print();

PrintHeader($"Completed {counter} {(counter == 1 ? "Pass" : "Passes")} Successfully.");
PrintCode("Input", initialRoot, major: true);
PrintCode("Output", root, major: true, false);
PrintSuccess($"✓ { Dotted(
        "Program complete!", 
        $"Took {Stopwatch.GetElapsedTime(t0).TotalMilliseconds:F1} ms",
        $"Time spent rewriting was {totalTime:F1} ms"
    )
}");

if (Debugger.IsAttached)
    Console.ReadKey(intercept: true);
sealed partial class DebugOutputWriter(TextWriter console) : TextWriter
{
    static readonly Regex AnsiPattern = new(@"\e\[[0-9;?]*[A-Za-z]", RegexOptions.Compiled);

    public override Encoding Encoding => console.Encoding;

    public override void Write(char value)
    {
        console.Write(value);
        Debug.Write(value.ToString());
    }

    public override void Write(string? value)
    {
        console.Write(value);
        Debug.Write(AnsiPattern.Replace(value ?? "", ""));
    }

    public override void Write(char[] buffer, int index, int count)
        => Write(new string(buffer, index, count));

    public override void Flush() => console.Flush();
}

internal abstract class TracingLuaSyntaxRewriter(bool trace = false) : LuaSyntaxRewriter
{
    int _depth;
    string Pad => string.Concat(Enumerable.Repeat(Branch, _depth));
    readonly StringBuilder _log = new();
    public string TraceLog => _log.ToString();

    public sealed override SyntaxNode? Visit(SyntaxNode? node)
    {
        if (!trace || node is not ExpressionSyntax)
            return base.Visit(node);

        Print($"{Faint($"{Pad}Visit({node.Kind()})")} {node}");
        _depth++;
        var result = base.Visit(node);
        _depth--;

        var changed = !ReferenceEquals(result, node);
        var shown   = changed ? Strong(Colored(Glow, $"{result}")) : $"{result}";
        var status  = changed ? Colored(Glow, "rewritten") : Faint("unchanged");
        _log.AppendLine($"{Faint($"{Pad}{Return}return")} {shown}   {status}");
        return result;
    }
}

internal class StringConcatFoldRewriter : TracingLuaSyntaxRewriter
{
    public StringConcatFoldRewriter() : base(true) { }
    public override SyntaxNode? VisitBinaryExpression(BinaryExpressionSyntax node)
    {
        var visited = (BinaryExpressionSyntax)base.VisitBinaryExpression(node)!;

        if (!visited.IsKind(SyntaxKind.ConcatExpression))
            return visited;

        if (visited is
            {
                Left: BinaryExpressionSyntax { Right: LiteralExpressionSyntax { Token.Value: string leftStr } leftLit } leftExpr,
                Right: LiteralExpressionSyntax { Token.Value: string rightStr } rightLit
            } && leftExpr.IsKind(SyntaxKind.ConcatExpression))
        {
            return leftExpr.WithRight(
                SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(leftStr + rightStr))
                .WithLeadingTrivia(leftLit.GetLeadingTrivia())
                .WithTrailingTrivia(rightLit.GetTrailingTrivia()));
        }
        else if (visited is {
                Left: LiteralExpressionSyntax { Token.Value: string left },
                Right: LiteralExpressionSyntax { Token.Value: string right }
            })
        {
            return SyntaxFactory.LiteralExpression(SyntaxKind.StringLiteralExpression, SyntaxFactory.Literal(left + right)).WithTriviaFrom(visited);
        }

        return visited;
    }
}

static class SyntaxNodeExtensions
{
    internal static SyntaxNode Pass<T>(this SyntaxNode inputNode, string name, ref double totalRewriteTime, ref int counter, Func<SyntaxNode, T> create, bool useCounter = true) where T : TracingLuaSyntaxRewriter
    {
        var oldTree = inputNode.SyntaxTree;
        counter++;

        PrintHeader(useCounter ? Dotted($"Pass {counter}", name) : name);

        var start = Stopwatch.GetTimestamp();
        var rewriter = create(inputNode);
        var rewritten = rewriter.Visit(inputNode)!;
        var resultTree = LuaSyntaxTree.ParseText(rewritten.ToFullString(), (LuaParseOptions)oldTree.Options, oldTree.FilePath, oldTree.Encoding);
        var elapsed = Stopwatch.GetElapsedTime(start);
        totalRewriteTime = totalRewriteTime + elapsed.TotalMilliseconds;
        var outputNode = resultTree.GetRoot();

        var trace = rewriter.TraceLog;
        if (trace.Length > 0)
        {
            Print(trace);
            Print();
        }

        PrintCode("Before", inputNode);
        PrintCode("After", outputNode, newLine: false);

        var errorsBefore = inputNode.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error);
        var errorsAfter  = outputNode.GetDiagnostics().Count(d => d.Severity == DiagnosticSeverity.Error);
        var newErrors    = errorsAfter - errorsBefore;
        var time         = elapsed.TotalMilliseconds.ToString("F1", CultureInfo.InvariantCulture);

        if (newErrors <= 0)
            PrintSuccess($"✓ {Dotted(name, $"{time} ms", "no new errors")}");
        else
            PrintError($"✗ {Dotted(name, $"{time} ms", $"{newErrors} new error{(newErrors == 1 ? "" : "s")}")}");

        Print();

        return outputNode;
    }

    internal static SyntaxNode Pass<T>(this SyntaxNode root, string name, ref double totalRewriteTime, ref int counter) where T : TracingLuaSyntaxRewriter, new()
        => root.Pass(name, ref totalRewriteTime, ref counter, _ => new T(), true);

    internal static SyntaxNode Pass<T>(this SyntaxNode root, string name, ref double totalRewriteTime) where T : TracingLuaSyntaxRewriter, new()
    {
        int unused = 0;
        return root.Pass(name, ref totalRewriteTime, ref unused, _ => new T(), false);
    }

    internal static SyntaxNode Pass<T>(this SyntaxNode root, string name, ref double totalRewriteTime, Func<SyntaxNode, T> create) where T : TracingLuaSyntaxRewriter, new()
    {
        int unused = 0;
        return Pass(root, name, ref totalRewriteTime, ref unused, create, false);
    }

    internal static string ToHighlighted(this SyntaxNode root) => ToHighlighted(root, new Script(ImmutableArray.Create(root.SyntaxTree)));

    internal static string ToHighlighted(this SyntaxNode root, Script script)
    {
        StringBuilder highlightedString = new();

        foreach (var token in root.DescendantTokens())   // includes EndOfFile, whose leading trivia is any trailing comments
        {
            foreach (var trivia in token.LeadingTrivia) highlightedString.Append(ColorTrivia(trivia));
            highlightedString.Append(ColorToken(token));
            foreach (var trivia in token.TrailingTrivia) highlightedString.Append(ColorTrivia(trivia));
        }

        return Theme.CodeBlock(highlightedString.ToString());
    }
}
