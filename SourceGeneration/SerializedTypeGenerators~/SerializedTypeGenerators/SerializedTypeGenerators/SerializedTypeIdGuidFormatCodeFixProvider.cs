using System;
using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Formatting;
using Microsoft.CodeAnalysis.Text;

namespace SerializedTypeGenerators;

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SerializedTypeIdGuidFormatCodeFixProvider))]
[Shared]
public sealed class SerializedTypeIdGuidFormatCodeFixProvider : CodeFixProvider {
    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("STG101");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        foreach (var diagnostic in context.Diagnostics) {
            if (diagnostic.Id != "STG101")
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Replace SerializedTypeId with generated GUID",
                    createChangedDocument: cancellationToken => ReplaceSerializedTypeIdWithGeneratedGuidAsync(context.Document, root, diagnostic, cancellationToken),
                    equivalenceKey: "ReplaceSerializedTypeIdWithGeneratedGuid"),
                diagnostic);
        }
    }

    static async Task<Document> ReplaceSerializedTypeIdWithGeneratedGuidAsync(
        Document document,
        SyntaxNode root,
        Diagnostic diagnostic,
        CancellationToken cancellationToken) {

        cancellationToken.ThrowIfCancellationRequested();

        var attributeSyntax = FindAttributeSyntax(root, diagnostic.Location.SourceSpan);
        if (attributeSyntax is null)
            return document;

        var existingArgument = attributeSyntax.ArgumentList?.Arguments.FirstOrDefault();
        if (existingArgument is null)
            return document;

        var generatedGuidLiteral = SyntaxFactory.LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            SyntaxFactory.Literal(Guid.NewGuid().ToString("D")));

        var updatedArgument = existingArgument.WithExpression(generatedGuidLiteral);
        var updatedAttribute = attributeSyntax.ReplaceNode(existingArgument, updatedArgument)
            .WithAdditionalAnnotations(Formatter.Annotation);

        var updatedRoot = root.ReplaceNode(attributeSyntax, updatedAttribute);
        return await Task.FromResult(document.WithSyntaxRoot(updatedRoot));
    }

    static AttributeSyntax? FindAttributeSyntax(SyntaxNode root, TextSpan locationSpan) {
        var node = root.FindNode(locationSpan, getInnermostNodeForTie: true);
        return node.FirstAncestorOrSelf<AttributeSyntax>();
    }
}

