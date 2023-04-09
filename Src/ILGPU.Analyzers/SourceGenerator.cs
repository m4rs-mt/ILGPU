// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: SourceGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Analyzers.Resources;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;
using SF = Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ILGPU.Analyzers
{
    [Generator]
    public class SourceGenerator : IIncrementalGenerator
    {
        #region Statics

        /// <summary>
        /// Delimiter used to separate nested structure names.
        /// </summary>
        private const string Delimiter = "_";

        private static readonly DiagnosticDescriptor TargetNotPartial = new(
            id: "ILA001",
            title: ErrorMessages.StructMustBePartial_Title,
            messageFormat: ErrorMessages.StructMustBePartial_Message,
            category: ErrorMessages.Usage_Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        private static readonly DiagnosticDescriptor ContainingTypeNotPartial = new(
            id: "ILA002",
            title: ErrorMessages.ContainingTypeMustBePartial_Title,
            messageFormat: ErrorMessages.ContainingTypeMustBePartial_Message,
            category: ErrorMessages.Usage_Category,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true);

        #endregion

        /// <inheritdoc cref="IIncrementalGenerator.Initialize" />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            var source = context.SyntaxProvider.ForAttributeWithMetadataName(
                "ILGPU.CodeGeneration.GeneratedStructureOfArraysAttribute",
                static (node, token) => node is StructDeclarationSyntax,
                static (context, token) => context);

            context.RegisterSourceOutput(source, EmitGeneratedStructureOfArraysAttribute);
        }

        /// <summary>
        /// Generates the source code for GeneratedStructureOfArraysAttribute.
        /// </summary>
        private static void EmitGeneratedStructureOfArraysAttribute(
            SourceProductionContext sourceProductionContext,
            GeneratorAttributeSyntaxContext generatorAttributeSyntaxContext)
        {
            // The attribute can only be applied to structures.
            var targetNode =
                (StructDeclarationSyntax)generatorAttributeSyntaxContext.TargetNode;
            var targetSymbol =
                (INamedTypeSymbol)generatorAttributeSyntaxContext.TargetSymbol;

            // The attribute does not allow multiples, so we only expect one.
            var attribute = generatorAttributeSyntaxContext.Attributes[0]!;
            var elementType = (INamedTypeSymbol)attribute.ConstructorArguments[0].Value!;
            var numElements = (int)attribute.ConstructorArguments[1].Value!;

            // The attribute can only be applied to partial structures.
            if (!targetNode.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                sourceProductionContext.ReportDiagnostic(
                    Diagnostic.Create(
                        TargetNotPartial,
                        targetNode.Identifier.GetLocation(),
                        targetSymbol.Name));
                return;
            }

            // If a nested type, then all containing types also need to be partial.
            if (!GetParentNodes(
                targetNode,
                (diagnostic) => sourceProductionContext.ReportDiagnostic(diagnostic),
                out var parentNodes))
                return;

            // Generate the resulting structure with field members. Since we are using
            // fixed-sized buffers, we also need to add the 'unsafe' keyword.
            var members = GetElementMembers(
                elementType,
                numElements,
                string.Empty);

            var structDeclaration =
                SF.StructDeclaration(targetSymbol.Name)
                .WithModifiers(
                    SF.TokenList(new[]
                    {
                        SF.Token(SyntaxKind.UnsafeKeyword),
                        SF.Token(SyntaxKind.PartialKeyword)
                    }))
                .WithMembers(SF.List(members));

            // Enclose in parent declarations.
            var resultDeclaration = EncloseDeclaration(structDeclaration, parentNodes);

            // Generate source code.
            var sourceText =
                SF.CompilationUnit()
                .WithMembers(SF.SingletonList(resultDeclaration))
                .NormalizeWhitespace()
                .ToFullString();

            var filename = targetSymbol
                .ToDisplayString(
                    SymbolDisplayFormat.FullyQualifiedFormat
                    .WithGlobalNamespaceStyle(SymbolDisplayGlobalNamespaceStyle.Omitted))
                .Replace(".", "_");

            sourceProductionContext.AddSource($"{filename}.g.cs", sourceText);
        }

        /// <summary>
        /// Retrieves the parent declarations, when the structure is a nested type.
        /// </summary>
        /// <param name="structNode">The structure definition.</param>
        /// <param name="reportDiagnostic">Callback to raise diagnostic errors.</param>
        /// <param name="parentNodes">Filled in with the parent nodes.</param>
        /// <returns>True, if there are no issues with the parent nodes.</returns>
        private static bool GetParentNodes(
            StructDeclarationSyntax structNode,
            Action<Diagnostic> reportDiagnostic,
            out List<SyntaxNode> parentNodes)
        {
            parentNodes = new List<SyntaxNode>();

            var parentNode = structNode.Parent;
            while (parentNode != null)
            {
                if (parentNode is StructDeclarationSyntax structDeclaration)
                {
                    if (!structDeclaration.Modifiers
                        .Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    {
                        reportDiagnostic(Diagnostic.Create(
                            ContainingTypeNotPartial,
                            structDeclaration.Identifier.GetLocation(),
                            structDeclaration.Identifier.ToString(),
                            structNode.Identifier.ToString()));
                        return false;
                    }
                    parentNodes.Add(parentNode);
                }
                else if (parentNode is ClassDeclarationSyntax classDeclaration)
                {
                    if (!classDeclaration.Modifiers
                        .Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
                    {
                        reportDiagnostic(Diagnostic.Create(
                            ContainingTypeNotPartial,
                            classDeclaration.Identifier.GetLocation(),
                            classDeclaration.Identifier.ToString(),
                            structNode.Identifier.ToString()));
                        return false;
                    }
                    parentNodes.Add(parentNode);
                }
                else if (parentNode is NamespaceDeclarationSyntax)
                {
                    parentNodes.Add(parentNode);
                    break;
                }
                else if (parentNode is CompilationUnitSyntax)
                {
                    break;
                }
                else
                {
                    throw new NotSupportedException();
                }

                parentNode = parentNode.Parent;
            }

            return true;
        }

        /// <summary>
        /// Creates a list of member declarations for the given structure.
        /// </summary>
        /// <param name="elementSymbol">The structure symbol.</param>
        /// <param name="numElements">The number of elements.</param>
        /// <param name="prefixFieldName">The prefix to use for field names.</param>
        /// <returns></returns>
        private static List<MemberDeclarationSyntax> GetElementMembers(
            INamedTypeSymbol elementSymbol,
            int numElements,
            string prefixFieldName)
        {
            var resultMembers = new List<MemberDeclarationSyntax>();

            foreach (var member in elementSymbol.GetMembers())
            {
                // Ignore static members.
                if (member.IsStatic)
                    continue;

                // Ignore non-field members.
                if (member is not IFieldSymbol fieldSymbol)
                    continue;

                // Ignore field members using reference types.
                if (!fieldSymbol.Type.IsValueType)
                    continue;

                if (fieldSymbol.Type.IsPrimitiveType() || fieldSymbol.IsFixedSizeBuffer)
                {
                    // Create a field declaration for our new structure that is a
                    // fixed-size buffer, based on the number of elements. If the
                    // existing field is itself a fixed-buffer, multiply out the
                    // total elements.
                    ITypeSymbol bufferType;
                    int bufferSize;

                    if (fieldSymbol.IsFixedSizeBuffer)
                    {
                        var pointerTypeSymbol = (IPointerTypeSymbol)fieldSymbol.Type;
                        bufferType = pointerTypeSymbol.PointedAtType;
                        bufferSize = numElements * fieldSymbol.FixedSize;
                    }
                    else
                    {
                        bufferType = fieldSymbol.Type;
                        bufferSize = numElements;
                    }

                    var fieldDecl = CreateFieldMember(
                        $"{prefixFieldName}{fieldSymbol.Name}",
                        fieldSymbol.DeclaredAccessibility,
                        bufferType,
                        bufferSize);
                    resultMembers.Add(fieldDecl);
                }
                else
                {
                    // Fixed-size buffers only support primitive types. For structures,
                    // flatten the members and add each of them.
                    var childMembers = GetElementMembers(
                        (INamedTypeSymbol)fieldSymbol.Type,
                        numElements,
                        $"{prefixFieldName}{fieldSymbol.Name}{Delimiter}");
                    resultMembers.AddRange(childMembers);
                }
            }

            return resultMembers;
        }

        /// <summary>
        /// Creates the member declaration for the given field.
        /// </summary>
        /// <param name="fieldName">The name of the field.</param>
        /// <param name="accessibility">The field accessibility.</param>
        /// <param name="fieldSymbol">The field symbol.</param>
        /// <param name="numElements">The number of elements.</param>
        /// <returns>The member declaration.</returns>
        private static MemberDeclarationSyntax CreateFieldMember(
            string fieldName,
            Accessibility accessibility,
            ITypeSymbol fieldSymbol,
            int numElements)
        {
            var fieldType = fieldSymbol.SpecialType.ToSyntaxKind();
            var fieldSize = SF.LiteralExpression(
                SyntaxKind.NumericLiteralExpression,
                SF.Literal(numElements));

            var fieldDecl =
                SF.FieldDeclaration(
                    SF.VariableDeclaration(
                        SF.PredefinedType(SF.Token(fieldType)))
                    .WithVariables(
                        SF.SingletonSeparatedList(
                            SF.VariableDeclarator(SF.Identifier(fieldName))
                            .WithArgumentList(
                                SF.BracketedArgumentList(
                                    SF.SingletonSeparatedList(
                                        SF.Argument(fieldSize)))))))
                .WithModifiers(
                    SF.TokenList(new[]
                    {
                        SF.Token(accessibility.ToSyntaxKind()),
                        SF.Token(SyntaxKind.FixedKeyword)
                    }));

            return fieldDecl;
        }

        /// <summary>
        /// Add the parent class/struct/namespace declarations.
        /// </summary>
        /// <param name="structNode">The struct node.</param>
        /// <param name="parentNodes">The parent nodes.</param>
        /// <returns>The namespace declaration node.</returns>
        private static MemberDeclarationSyntax EncloseDeclaration(
            StructDeclarationSyntax structNode,
            List<SyntaxNode> parentNodes)
        {
            MemberDeclarationSyntax declaration = structNode;

            // Enclose in parent declarations.
            foreach (var parentNode in parentNodes)
            {
                if (parentNode is StructDeclarationSyntax structDeclaration)
                {
                    declaration =
                        SF.StructDeclaration(structDeclaration.Identifier)
                        .WithModifiers(SF.TokenList(SF.Token(SyntaxKind.PartialKeyword)))
                        .WithMembers(SF.SingletonList(declaration));
                }
                else if (parentNode is ClassDeclarationSyntax classDeclaration)
                {
                    declaration =
                        SF.ClassDeclaration(classDeclaration.Identifier)
                        .WithModifiers(SF.TokenList(SF.Token(SyntaxKind.PartialKeyword)))
                        .WithMembers(SF.SingletonList(declaration));
                }
                else if (parentNode is NamespaceDeclarationSyntax namespaceDeclaration)
                {
                    declaration =
                        SF.NamespaceDeclaration(namespaceDeclaration.Name)
                        .WithMembers(SF.SingletonList(declaration));
                }
                else
                {
                    throw new NotImplementedException();
                }
            }

            return declaration;
        }
    }
}
