// Copyright (c) Bottlenose Labs Inc. (https://github.com/bottlenoselabs). All rights reserved.
// Licensed under the MIT license. See LICENSE file in the Git repository root directory for full license information.

using System;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace C2CS.Features.WriteCodeCSharp.Domain.CodeGenerator;

[PublicAPI]
public static partial class ExtensionsRoslyn
{
    public static string Format<T>(this T syntaxNode)
        where T : SyntaxNode
    {
        using var workspace = new AdhocWorkspace();
        var compilationUnitFormatted = (T)Formatter.Format(syntaxNode, workspace);
        var code = compilationUnitFormatted.ToFullString();
        var finalCode = MultipleNewLinesRegex().Replace(code, "\n\n");
        return finalCode;
    }

    public static ImmutableArray<MemberDeclarationSyntax> GetManifestResourceMemberDeclarations(this Assembly assembly)
    {
        var builderMembers = ImmutableArray.CreateBuilder<MemberDeclarationSyntax>();

        var manifestResourcesNames = assembly.GetManifestResourceNames();
        foreach (var resourceName in manifestResourcesNames)
        {
            if (!resourceName.EndsWith(".cs", StringComparison.InvariantCulture))
            {
                continue;
            }

            using var stream = assembly.GetManifestResourceStream(resourceName);
            using var streamReader = new StreamReader(stream!);
            var resourceCode = streamReader.ReadToEnd();
            var syntaxTree = ParseSyntaxTree(resourceCode);
            var compilationUnit = (CompilationUnitSyntax)syntaxTree.GetRoot();
            foreach (var member in compilationUnit.Members)
            {
                builderMembers.Add(member);
            }
        }

        return builderMembers.ToImmutable();
    }

    public static T AddRegionStart<T>(this T node, string regionName, bool addDoubleTrailingNewLine)
        where T : SyntaxNode
    {
        var trivia = node.GetLeadingTrivia();
        var index = 0;

        trivia = trivia
            .Insert(index++, CarriageReturnLineFeed)
            .Insert(index++, GetRegionLeadingTrivia(regionName));

        if (addDoubleTrailingNewLine)
        {
            trivia = trivia
                .Insert(index++, CarriageReturnLineFeed)
                .Insert(index, CarriageReturnLineFeed);
        }
        else
        {
            trivia = trivia
                .Insert(index, CarriageReturnLineFeed);
        }

        return node.WithLeadingTrivia(trivia);
    }

    public static T AddRegionEnd<T>(this T node)
        where T : SyntaxNode
    {
        var trivia = node.GetTrailingTrivia();
        var index = 0;

        trivia = trivia
            .Insert(index++, CarriageReturnLineFeed)
            .Insert(index++, CarriageReturnLineFeed);

        trivia = trivia.Insert(index, GetRegionTrailingTrivia());

        return node.WithTrailingTrivia(trivia);
    }

    private static SyntaxTrivia GetRegionLeadingTrivia(string regionName, bool isActive = true)
    {
        return Trivia(
            RegionDirectiveTrivia(isActive)
                .WithEndOfDirectiveToken(
                    Token(
                        TriviaList(PreprocessingMessage($" {regionName}")),
                        SyntaxKind.EndOfDirectiveToken,
                        TriviaList())));
    }

    private static SyntaxTrivia GetRegionTrailingTrivia(bool isActive = true)
    {
        return Trivia(EndRegionDirectiveTrivia(isActive));
    }

    [GeneratedRegex("(\\n){2,}")]
    private static partial Regex MultipleNewLinesRegex();
}
