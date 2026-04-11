using System;
using System.Collections.Generic;
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

[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SerializedTypeIdEligibilityCodeFixProvider))]
[Shared]
public sealed class SerializedTypeIdEligibilityCodeFixProvider : CodeFixProvider {
    const string SERIALIZED_TYPE_ID_ATTRIBUTE_SIMPLE_NAME = "SerializedTypeId";
    const string SERIALIZED_TYPE_ID_ATTRIBUTE_QUALIFIED_NAME = "Hissal.UnityTypeSerializer.SerializedTypeId";
    const string UNITY_TYPE_SERIALIZER_NAMESPACE = "Hissal.UnityTypeSerializer";

    public override ImmutableArray<string> FixableDiagnosticIds => ImmutableArray.Create("STG100");

    public override FixAllProvider GetFixAllProvider() => WellKnownFixAllProviders.BatchFixer;

    public override async Task RegisterCodeFixesAsync(CodeFixContext context) {
        var root = await context.Document.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
        if (root is null)
            return;

        foreach (var diagnostic in context.Diagnostics) {
            if (diagnostic.Id != "STG100")
                continue;

            var declaration = FindTargetDeclaration(root, diagnostic.Location.SourceSpan);
            if (declaration is null)
                continue;

            context.RegisterCodeFix(
                CodeAction.Create(
                    title: "Add SerializedTypeId with generated GUID",
                    createChangedDocument: cancellationToken => AddSerializedTypeIdAttributeAsync(context.Document, root, declaration, cancellationToken),
                    equivalenceKey: "AddSerializedTypeIdWithGeneratedGuid"),
                diagnostic);
        }
    }

    static async Task<Document> AddSerializedTypeIdAttributeAsync(
        Document document,
        SyntaxNode root,
        SyntaxNode declaration,
        CancellationToken cancellationToken) {

        cancellationToken.ThrowIfCancellationRequested();

        if (HasSerializedTypeIdAttribute(declaration))
            return document;

        var useSimpleAttributeName = HasUsingForUnityTypeSerializerNamespace(root, declaration);
        var attributeName = useSimpleAttributeName
            ? SyntaxFactory.IdentifierName(SERIALIZED_TYPE_ID_ATTRIBUTE_SIMPLE_NAME)
            : SyntaxFactory.ParseName(SERIALIZED_TYPE_ID_ATTRIBUTE_QUALIFIED_NAME);

        var generatedGuidLiteral = SyntaxFactory.LiteralExpression(
            SyntaxKind.StringLiteralExpression,
            SyntaxFactory.Literal(Guid.NewGuid().ToString("D")));

        var attribute = SyntaxFactory.Attribute(
            attributeName,
            SyntaxFactory.AttributeArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.AttributeArgument(generatedGuidLiteral))));

        var attributeList = SyntaxFactory.AttributeList(SyntaxFactory.SingletonSeparatedList(attribute));

        var updatedDeclaration = declaration switch {
            BaseTypeDeclarationSyntax baseTypeDeclaration =>
                baseTypeDeclaration.AddAttributeLists(attributeList).WithAdditionalAnnotations(Formatter.Annotation),
            DelegateDeclarationSyntax delegateDeclaration =>
                delegateDeclaration.AddAttributeLists(attributeList).WithAdditionalAnnotations(Formatter.Annotation),
            _ => declaration
        };

        var updatedRoot = root.ReplaceNode(declaration, updatedDeclaration);
        var updatedDocument = document.WithSyntaxRoot(updatedRoot);
        return await Task.FromResult(updatedDocument);
    }


    static SyntaxNode? FindTargetDeclaration(SyntaxNode root, TextSpan locationSpan) {
        var node = root.FindNode(locationSpan, getInnermostNodeForTie: true);

        return node.FirstAncestorOrSelf<BaseTypeDeclarationSyntax>()
            ?? (SyntaxNode?)node.FirstAncestorOrSelf<DelegateDeclarationSyntax>();
    }

    static bool HasUsingForUnityTypeSerializerNamespace(SyntaxNode root, SyntaxNode declaration) {
        if (root is not CompilationUnitSyntax compilationUnit)
            return false;

        if (ContainsUnityTypeSerializerUsing(compilationUnit.Usings))
            return true;

        foreach (var namespaceDeclaration in declaration.Ancestors().OfType<BaseNamespaceDeclarationSyntax>()) {
            if (ContainsUnityTypeSerializerUsing(namespaceDeclaration.Usings))
                return true;
        }

        return false;
    }

    static bool ContainsUnityTypeSerializerUsing(IEnumerable<UsingDirectiveSyntax> usings) {
        foreach (var usingDirective in usings) {
            if (usingDirective.Alias is not null)
                continue;

            if (usingDirective.StaticKeyword != default)
                continue;

            if (string.Equals(usingDirective.Name.ToString(), UNITY_TYPE_SERIALIZER_NAMESPACE, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    static bool HasSerializedTypeIdAttribute(SyntaxNode declaration) {
        var attributeLists = declaration switch {
            BaseTypeDeclarationSyntax baseTypeDeclaration => baseTypeDeclaration.AttributeLists,
            DelegateDeclarationSyntax delegateDeclaration => delegateDeclaration.AttributeLists,
            _ => default
        };

        foreach (var attributeList in attributeLists) {
            foreach (var attribute in attributeList.Attributes) {
                if (IsSerializedTypeIdAttributeName(attribute.Name))
                    return true;
            }
        }

        return false;
    }

    static bool IsSerializedTypeIdAttributeName(NameSyntax nameSyntax) {
        var identifier = GetRightMostIdentifier(nameSyntax);
        return string.Equals(identifier, "SerializedTypeId", StringComparison.Ordinal)
            || string.Equals(identifier, "SerializedTypeIdAttribute", StringComparison.Ordinal);
    }

    static string GetRightMostIdentifier(NameSyntax nameSyntax) {
        return nameSyntax switch {
            IdentifierNameSyntax identifierName => identifierName.Identifier.ValueText,
            QualifiedNameSyntax qualifiedName => GetRightMostIdentifier(qualifiedName.Right),
            AliasQualifiedNameSyntax aliasQualifiedName => GetRightMostIdentifier(aliasQualifiedName.Name),
            _ => nameSyntax.ToString()
        };
    }
}

