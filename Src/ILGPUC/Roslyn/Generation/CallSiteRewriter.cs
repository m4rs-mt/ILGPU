// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CallSiteRewriter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Roslyn.Analysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Collections.Generic;
using System.Linq;

namespace ILGPUC.Roslyn.Generation;

/// <summary>
/// Rewrites Launch call sites to use generated CompiledKernel.Launch methods.
///
/// Before:
///   stream.Launch(1024, _ => MainKernel(a.View, b.View, c.View));
/// After:
///   MainKernel_CompiledKernel.Launch(
///     stream,
///     stream.ComputeKernelConfig(1024),
///     a.View,
///     b.View,
///     c.View);
/// </summary>
sealed class CallSiteRewriter : CSharpSyntaxRewriter
{
    private readonly Dictionary<SyntaxNode, KernelDescriptor> _launchSiteMap = [];
    private readonly bool _emitLineDirectives;

    /// <summary>
    /// Initializes a new <see cref="CallSiteRewriter"/> that rewrites all launch
    /// call sites for the supplied kernels.
    /// </summary>
    /// <param name="kernels">
    /// Kernel descriptors whose launch sites will be rewritten.
    /// </param>
    /// <param name="emitLineDirectives">
    /// When <see langword="true"/>, emits <c>#line</c> directives so the rewritten
    /// code maps back to the original source for debugging.
    /// </param>
    public CallSiteRewriter(
        List<KernelDescriptor> kernels,
        bool emitLineDirectives = false)
    {
        _emitLineDirectives = emitLineDirectives;
        foreach (var kernel in kernels)
            _launchSiteMap[kernel.LaunchInfo.Invocation] = kernel;
    }

    /// <summary>
    /// Visits an expression statement and rewrites it if it is a recognized
    /// kernel launch call site.
    /// </summary>
    public override SyntaxNode? VisitExpressionStatement(
        ExpressionStatementSyntax node)
    {
        if (node.Expression is InvocationExpressionSyntax invocation
            && _launchSiteMap.TryGetValue(invocation, out var kernel))
        {
            return RewriteLaunchCall(node, invocation, kernel);
        }

        return base.VisitExpressionStatement(node);
    }

    /// <summary>
    /// Rewrites a single kernel launch call site to call the generated
    /// <c>*_CompiledKernel.Launch</c> static method.
    /// </summary>
    private ExpressionStatementSyntax RewriteLaunchCall(
        ExpressionStatementSyntax originalStmt,
        InvocationExpressionSyntax original,
        KernelDescriptor kernel)
    {
        var launchInfo = kernel.LaunchInfo;

        // Build: KernelName_CompiledKernel.Launch[<T, ...>](stream, config, args...)
        SimpleNameSyntax launchName;
        if (kernel.KernelMethod is { IsGenericMethod: true } gm
            && gm.TypeArguments.Any(t => t is ITypeParameterSymbol))
        {
            // Forward generic type arguments to the stub Launch method
            var typeArgs = new List<TypeSyntax>();
            foreach (var ta in gm.TypeArguments)
            {
                typeArgs.Add(SyntaxFactory.ParseTypeName(
                    LauncherStubGenerator.FormatTypeNamePublic(ta)));
            }
            launchName = SyntaxFactory.GenericName(
                SyntaxFactory.Identifier("Launch"),
                SyntaxFactory.TypeArgumentList(
                    SyntaxFactory.SeparatedList(typeArgs)));
        }
        else
        {
            launchName = SyntaxFactory.IdentifierName("Launch");
        }
        var targetExpr = SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            SyntaxFactory.IdentifierName($"{kernel.KernelName}_CompiledKernel"),
            launchName);

        var arguments = new List<ArgumentSyntax>();

        // First arg: the stream expression
        arguments.Add(SyntaxFactory.Argument(launchInfo.StreamExpression));

        // Second arg: kernel config
        var configExpr = BuildConfigExpression(
            launchInfo.StreamExpression,
            launchInfo.ExtentOrConfig,
            kernel.Variant);
        arguments.Add(SyntaxFactory.Argument(configExpr));

        // Third arg: userExtent (the actual number of data elements)
        var userExtentExpr = BuildUserExtentExpression(
            launchInfo.ExtentOrConfig,
            configExpr,
            kernel.Variant);
        arguments.Add(SyntaxFactory.Argument(userExtentExpr));

        // For multi-dim launches, pass per-dimension sizes so the CPU
        // launcher can reconstruct Index2D/Index3D from linear indices.
        AddDimensionArguments(arguments, launchInfo.ExtentOrConfig, kernel.Variant);

        // Remaining args: kernel parameters (the captured args from the lambda)
        foreach (var param in kernel.Parameters)
        {
            arguments.Add(SyntaxFactory.Argument(param.SourceExpression));
        }

        var argList = SyntaxFactory.ArgumentList(
            SyntaxFactory.SeparatedList(arguments))
            .NormalizeWhitespace();

        var newInvocation = SyntaxFactory.InvocationExpression(targetExpr, argList);

        var rewritten = SyntaxFactory.ExpressionStatement(newInvocation)
            .WithSemicolonToken(originalStmt.SemicolonToken)
            .WithLeadingTrivia(originalStmt.GetLeadingTrivia())
            .WithTrailingTrivia(originalStmt.GetTrailingTrivia());

        if (_emitLineDirectives)
            rewritten = PrependLineDirective(rewritten, originalStmt);

        return rewritten;
    }

    /// <summary>
    /// Prepends a #line directive to map a rewritten statement back to its
    /// original source location for debugging.
    /// </summary>
    private static ExpressionStatementSyntax PrependLineDirective(
        ExpressionStatementSyntax rewritten,
        ExpressionStatementSyntax originalStmt)
    {
        var location = originalStmt.GetLocation();
        var lineSpan = location.GetLineSpan();
        if (!lineSpan.IsValid)
            return rewritten;

        var originalLine = lineSpan.StartLinePosition.Line + 1; // 1-based
        var originalFile = lineSpan.Path;
        if (string.IsNullOrEmpty(originalFile))
            return rewritten;

        var directive = BuildLineDirective(originalLine, originalFile);
        var lineTrivia = SyntaxFactory.Trivia(directive);
        var eol = SyntaxFactory.EndOfLine("\n");

        var existingTrivia = rewritten.GetLeadingTrivia();
        var newTrivia = SyntaxFactory.TriviaList(lineTrivia, eol)
            .AddRange(existingTrivia);

        return rewritten.WithLeadingTrivia(newTrivia);
    }

    /// <summary>
    /// Adds a #line 1 "originalPath" directive at the top of a syntax tree
    /// so the entire rewritten file maps back to the original source.
    /// </summary>
    public static SyntaxTree AddTopOfFileLineDirective(
        SyntaxTree tree, string originalFilePath)
    {
        var root = tree.GetRoot();
        var firstToken = root.GetFirstToken();
        if (firstToken == default)
            return tree;

        var directive = BuildLineDirective(1, originalFilePath);
        var lineTrivia = SyntaxFactory.Trivia(directive);
        var eol = SyntaxFactory.EndOfLine("\n");

        var existingTrivia = firstToken.LeadingTrivia;
        var newTrivia = SyntaxFactory.TriviaList(lineTrivia, eol)
            .AddRange(existingTrivia);

        var newRoot = root.ReplaceToken(
            firstToken, firstToken.WithLeadingTrivia(newTrivia));

        return tree.WithRootAndOptions(newRoot, tree.Options);
    }

    /// <summary>
    /// Builds a #line N "file" directive with correct whitespace between tokens.
    /// </summary>
    private static LineDirectiveTriviaSyntax BuildLineDirective(
        int lineNumber, string filePath)
    {
        var space = SyntaxFactory.TriviaList(SyntaxFactory.Space);

        var lineToken = SyntaxFactory.Literal(
            space, lineNumber.ToString(), lineNumber, SyntaxFactory.TriviaList());

        var fileToken = SyntaxFactory.Literal(
            space, $"\"{filePath}\"", filePath, SyntaxFactory.TriviaList());

        return SyntaxFactory.LineDirectiveTrivia(lineToken, fileToken, isActive: true);
    }

    /// <summary>
    /// Builds the kernel config argument: passes the extent through
    /// <c>stream.ComputeKernelConfig(extent)</c> for auto-size variants,
    /// or passes a pre-built <c>KernelConfig</c> directly for grouped launches.
    /// </summary>
    private static ExpressionSyntax BuildConfigExpression(
        ExpressionSyntax streamExpr,
        ExpressionSyntax extentOrConfig,
        LaunchVariant variant)
    {
        // For grouped launches, the config is passed directly
        if (variant == LaunchVariant.Grouped)
            return extentOrConfig;

        // For multi-dimensional auto launches, flatten the extent to
        // a scalar before passing to ComputeKernelConfig(long)
        ExpressionSyntax scalarExtent = variant switch
        {
            LaunchVariant.Auto2D or LaunchVariant.AutoLong2D
                or LaunchVariant.Auto2DStride
                or LaunchVariant.AutoLong2DStride =>
                CastToLong(SyntaxFactory.BinaryExpression(
                    SyntaxKind.MultiplyExpression,
                    MemberAccess(extentOrConfig, "X"),
                    MemberAccess(extentOrConfig, "Y"))),
            LaunchVariant.Auto3D or LaunchVariant.AutoLong3D
                or LaunchVariant.Auto3DStride
                or LaunchVariant.AutoLong3DStride =>
                CastToLong(SyntaxFactory.BinaryExpression(
                    SyntaxKind.MultiplyExpression,
                    SyntaxFactory.BinaryExpression(
                        SyntaxKind.MultiplyExpression,
                        MemberAccess(extentOrConfig, "X"),
                        MemberAccess(extentOrConfig, "Y")),
                    MemberAccess(extentOrConfig, "Z"))),
            _ => extentOrConfig,
        };

        // stream.ComputeKernelConfig(scalarExtent)
        return SyntaxFactory.InvocationExpression(
            SyntaxFactory.MemberAccessExpression(
                SyntaxKind.SimpleMemberAccessExpression,
                streamExpr,
                SyntaxFactory.IdentifierName("ComputeKernelConfig")),
            SyntaxFactory.ArgumentList(
                SyntaxFactory.SingletonSeparatedList(
                    SyntaxFactory.Argument(scalarExtent))));
    }

    /// <summary>
    /// Builds the <c>userExtent</c> argument that passes the original number of
    /// data elements to the generated Launch method. For auto-size variants this
    /// is the user's extent cast to <c>long</c>; for grouped launches it is
    /// <c>config.GridSize * config.GroupSize</c>.
    /// </summary>
    private static ExpressionSyntax BuildUserExtentExpression(
        ExpressionSyntax extentOrConfig,
        ExpressionSyntax configExpr,
        LaunchVariant variant)
    {
        return variant switch
        {
            // 1D auto: (long)(int)(extent) — cast through int to avoid
            // ambiguous Index1D → long conversion (implicit int vs explicit uint)
            LaunchVariant.Auto1D =>
                CastToLong(CastToInt(extentOrConfig)),

            // 1D long auto: (long)(extent) — already long/LongIndex1D
            LaunchVariant.AutoLong1D =>
                CastToLong(extentOrConfig),

            // 2D auto: (long)(extent.X * extent.Y)
            LaunchVariant.Auto2D or LaunchVariant.AutoLong2D
                or LaunchVariant.Auto2DStride or LaunchVariant.AutoLong2DStride =>
                CastToLong(SyntaxFactory.BinaryExpression(
                    SyntaxKind.MultiplyExpression,
                    MemberAccess(extentOrConfig, "X"),
                    MemberAccess(extentOrConfig, "Y"))),

            // 3D auto: (long)(extent.X * extent.Y * extent.Z)
            LaunchVariant.Auto3D or LaunchVariant.AutoLong3D
                or LaunchVariant.Auto3DStride or LaunchVariant.AutoLong3DStride =>
                CastToLong(SyntaxFactory.BinaryExpression(
                    SyntaxKind.MultiplyExpression,
                    SyntaxFactory.BinaryExpression(
                        SyntaxKind.MultiplyExpression,
                        MemberAccess(extentOrConfig, "X"),
                        MemberAccess(extentOrConfig, "Y")),
                    MemberAccess(extentOrConfig, "Z"))),

            // Grouped: (long)(config.GridSize * config.GroupSize)
            LaunchVariant.Grouped =>
                CastToLong(SyntaxFactory.BinaryExpression(
                    SyntaxKind.MultiplyExpression,
                    MemberAccess(configExpr, "GridSize"),
                    MemberAccess(configExpr, "GroupSize"))),

            _ => CastToLong(extentOrConfig),
        };
    }

    /// <summary>
    /// Wraps an expression in <c>(int)(...)</c>.
    /// </summary>
    private static CastExpressionSyntax CastToInt(ExpressionSyntax expr)
    {
        return SyntaxFactory.CastExpression(
            SyntaxFactory.PredefinedType(
                SyntaxFactory.Token(SyntaxKind.IntKeyword)),
            SyntaxFactory.ParenthesizedExpression(expr));
    }

    /// <summary>
    /// Wraps an expression in <c>(long)(...)</c>.
    /// </summary>
    private static CastExpressionSyntax CastToLong(ExpressionSyntax expr)
    {
        return SyntaxFactory.CastExpression(
            SyntaxFactory.PredefinedType(
                SyntaxFactory.Token(SyntaxKind.LongKeyword)),
            SyntaxFactory.ParenthesizedExpression(expr));
    }

    /// <summary>
    /// Adds per-dimension size arguments for multi-dimensional launch variants.
    /// For 2D: adds (int)(extent.X) and (int)(extent.Y).
    /// For 3D: adds X, Y, and Z.
    /// </summary>
    private static void AddDimensionArguments(
        List<ArgumentSyntax> arguments,
        ExpressionSyntax extentOrConfig,
        LaunchVariant variant)
    {
        switch (variant)
        {
            case LaunchVariant.Auto2D or LaunchVariant.AutoLong2D
                or LaunchVariant.Auto2DStride or LaunchVariant.AutoLong2DStride:
                arguments.Add(SyntaxFactory.Argument(
                    CastToInt(MemberAccess(extentOrConfig, "X"))));
                arguments.Add(SyntaxFactory.Argument(
                    CastToInt(MemberAccess(extentOrConfig, "Y"))));
                break;

            case LaunchVariant.Auto3D or LaunchVariant.AutoLong3D
                or LaunchVariant.Auto3DStride or LaunchVariant.AutoLong3DStride:
                arguments.Add(SyntaxFactory.Argument(
                    CastToInt(MemberAccess(extentOrConfig, "X"))));
                arguments.Add(SyntaxFactory.Argument(
                    CastToInt(MemberAccess(extentOrConfig, "Y"))));
                arguments.Add(SyntaxFactory.Argument(
                    CastToInt(MemberAccess(extentOrConfig, "Z"))));
                break;
        }
    }

    /// <summary>
    /// Builds a member access expression: <c>expr.member</c>.
    /// </summary>
    private static MemberAccessExpressionSyntax MemberAccess(
        ExpressionSyntax expr, string member)
    {
        return SyntaxFactory.MemberAccessExpression(
            SyntaxKind.SimpleMemberAccessExpression,
            expr,
            SyntaxFactory.IdentifierName(member));
    }
}
