using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Kehlet.Functional.Generators.UnionGenerator;

public static class Extensions
{
    public static T As<T>(this SyntaxNode node)
        where T : SyntaxNode =>
        (T) node;

    public static T As<T>(this ISymbol node)
        where T : ISymbol =>
        (T) node;

    public static TResult Apply<T, TResult>(this T self, Func<T, TResult> f) => f(self);

    public static TypeDeclarationSyntax GetSyntax(this INamedTypeSymbol symbol) =>
        symbol.DeclaringSyntaxReferences[0].GetSyntax().As<TypeDeclarationSyntax>();

    public static bool IsPartial(this TypeDeclarationSyntax syntaxNode) =>
        syntaxNode.Modifiers.Any(x => x.IsKind(SyntaxKind.PartialKeyword));
    
    public static bool IsStatic(this TypeDeclarationSyntax syntaxNode) =>
        syntaxNode.Modifiers.Any(x => x.IsKind(SyntaxKind.StaticKeyword));

    public static string GetDeclarationSyntax(this TypeDeclarationSyntax syntaxNode) =>
        syntaxNode.Apply(x => x.Modifiers + " " + x.Keyword + " " + x.Identifier);
}
