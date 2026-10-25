using Loretta.CodeAnalysis;
using Loretta.CodeAnalysis.Lua;
using Loretta.CodeAnalysis.Lua.Syntax;

// Usage: dotnet run -- [file.lua]   (defaults to samples/hello.lua)

const string file = "exercise.lua";
var path = args.Length > 0 ? args[0] : Path.Combine(AppContext.BaseDirectory, "samples", file);
var source = File.ReadAllText(path);
    
// GMod preset = LuaJIT 2.0 + GLua extras (!=, &&, ||, //-comments, continue).
var options = new LuaParseOptions(LuaSyntaxOptions.GMod);
var tree = LuaSyntaxTree.ParseText(source, options, path);
var root = tree.GetRoot();

foreach (var diag in tree.GetDiagnostics())
    Console.WriteLine($"[diag] {diag}");


static void Dump(SyntaxNodeOrToken item, int depth)
{
    var indent = new string(' ', depth * 2);

    if (item.IsToken)
    {
        var token = item.AsToken();
        Console.WriteLine($"T {indent}{token.GetType().Name} {token.Kind()} '{token.Text}'");
        return;
    }

    var node = item.AsNode()!;
    Console.WriteLine($"N {indent} {node.GetType().Name} {node.Kind()}");
    foreach (var child in node.ChildNodesAndTokens())
        Dump(child, depth + 1);
}

var rewritten = new ExerciseRewriter().Visit(root);


Console.WriteLine($"\nInput:\n{root.ToString()}\n\nOutput:\n{rewritten.ToString()}\n");

abstract class TracingLuaSyntaxRewriter(bool trace = false) : LuaSyntaxRewriter
{
    int _depth;
    string Pad => string.Concat(Enumerable.Repeat("│ ", _depth));

    public sealed override SyntaxNode? Visit(SyntaxNode? node)
    {
        if (!trace || node is not ExpressionSyntax)
            return base.Visit(node);

        Console.WriteLine($"{Pad}Visit({node.Kind()}) {node}");
        _depth++;
        var result = base.Visit(node);
        _depth--;
        Console.WriteLine($"{Pad}└ return {result}   ({(ReferenceEquals(result, node) ? "unchanged" : "rewritten")})");
        return result;
    }
}

internal class ExerciseRewriter(bool trace = true) : TracingLuaSyntaxRewriter(trace)
{
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