// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
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
using Microsoft.CodeAnalysis.Text;
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
///
/// Inline lambda kernels are launched the same way with their captured
/// values as arguments. When lifting is enabled, the launch is additionally
/// wrapped in a block that declares the lambda body as a static local
/// function, which makes the kernel resolvable from the emitted assembly,
/// followed by the lifted kernels binding its captured delegates.
///
/// Delegates passed to delegate parameters captured by inline kernels are
/// registered under the keys of their targets at their call sites:
///   Host.UnaryOp(stream, view, v => -v);
/// After:
///   Host.UnaryOp(stream, view,
///     global::ILGPU.Runtime.KernelDelegates.Register&lt;...&gt;(
///       v => -v, "Program:Host.UnaryOp.op#0"));
/// </summary>
sealed class CallSiteRewriter : CSharpSyntaxRewriter
{
    private readonly Dictionary<SyntaxNode, KernelDescriptor> _launchSiteMap = [];
    private readonly Dictionary<SyntaxNode, List<KernelDescriptor>> _boundKernels =
        [];
    private readonly Dictionary<ArgumentSyntax, DelegateRegistration> _registrations;
    private readonly bool _emitLineDirectives;
    private readonly bool _liftInlineKernels;

    /// <summary>
    /// Initializes a new <see cref="CallSiteRewriter"/> that rewrites all launch
    /// call sites for the supplied kernels.
    /// </summary>
    /// <param name="analysis">
    /// The analysis results whose launch sites and delegate registrations will
    /// be rewritten.
    /// </param>
    /// <param name="emitLineDirectives">
    /// When <see langword="true"/>, emits <c>#line</c> directives so the rewritten
    /// code maps back to the original source for debugging.
    /// </param>
    /// <param name="liftInlineKernels">
    /// When <see langword="true"/>, declares the lifted body of each inline
    /// lambda kernel as a static local function next to its launch site.
    /// </param>
    public CallSiteRewriter(
        AnalysisResult analysis,
        bool emitLineDirectives = false,
        bool liftInlineKernels = false)
    {
        _emitLineDirectives = emitLineDirectives;
        _liftInlineKernels = liftInlineKernels;
        _registrations = analysis.DelegateRegistrations;
        foreach (var kernel in analysis.AllKernels)
            _launchSiteMap[kernel.LaunchInfo.Invocation] = kernel;
        foreach (var kernel in analysis.UniqueKernels)
        {
            if (kernel.DelegateKeys is null || kernel.LiftedKernel is null)
                continue;
            var invocation = kernel.LaunchInfo.Invocation;
            if (!_boundKernels.TryGetValue(invocation, out var bound))
                _boundKernels.Add(invocation, bound = []);
            bound.Add(kernel);
        }
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
            var launch = RewriteLaunchCall(node, invocation, kernel);
            if (!_liftInlineKernels || kernel.LiftedKernel is null)
                return launch;

            // { static void InlineKernel_...(...) { ... } Launch(...); }
            var statements = new List<StatementSyntax> { kernel.LiftedKernel };
            if (_boundKernels.TryGetValue(invocation, out var bound))
                statements.AddRange(bound.Select(k => k.LiftedKernel!));
            statements.Add(launch.WithoutLeadingTrivia());
            return SyntaxFactory.Block(statements)
                .WithLeadingTrivia(launch.GetLeadingTrivia());
        }

        return base.VisitExpressionStatement(node);
    }

    /// <summary>
    /// Visits an argument and registers the delegate it passes under the keys
    /// of its target if it is passed to a delegate parameter captured by an
    /// inline kernel.
    /// </summary>
    public override SyntaxNode? VisitArgument(ArgumentSyntax node)
    {
        var visited = (ArgumentSyntax)base.VisitArgument(node)!;
        if (!_registrations.TryGetValue(node, out var registration))
            return visited;

        // KernelDelegates.Register<TDelegate>(..., "key") for each key
        var delegateType = SyntaxFactory.ParseTypeName(
            registration.DelegateType.ToDisplayString(
                SymbolDisplayFormat.FullyQualifiedFormat));
        var expression = visited.Expression.WithoutTrivia();
        foreach (var key in registration.Keys)
        {
            expression = SyntaxFactory.InvocationExpression(
                SyntaxFactory.MemberAccessExpression(
                    SyntaxKind.SimpleMemberAccessExpression,
                    SyntaxFactory.ParseExpression(
                        "global::ILGPU.Runtime.KernelDelegates"),
                    SyntaxFactory.GenericName(
                        SyntaxFactory.Identifier("Register"),
                        SyntaxFactory.TypeArgumentList(
                            SyntaxFactory.SingletonSeparatedList(delegateType)))),
                SyntaxFactory.ArgumentList(SyntaxFactory.SeparatedList(
                [
                    SyntaxFactory.Argument(expression),
                    SyntaxFactory.Argument(SyntaxFactory.LiteralExpression(
                        SyntaxKind.StringLiteralExpression,
                        SyntaxFactory.Literal(key))),
                ])));
        }
        return visited.WithExpression(
            expression.WithTriviaFrom(visited.Expression));
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
        if (kernel.DispatchTypeArguments is { } dispatchTypeArguments)
        {
            // Forward the launch site's type arguments, including those of the
            // kernel's containing types, to the dispatch stub's Launch method
            var typeArgs = new List<TypeSyntax>();
            // Fully qualified, so that names in the launch site's own namespace
            // cannot shadow them and no using directive is needed
            foreach (var ta in dispatchTypeArguments)
            {
                typeArgs.Add(SyntaxFactory.ParseTypeName(
                    ta.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)));
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

        // First arg: the stream expression. As the first token of the launch
        // statement it carries the statement's leading trivia (comments,
        // directives), which must not be repeated inside the argument list.
        var streamExpr = launchInfo.StreamExpression.WithoutTrivia();
        arguments.Add(SyntaxFactory.Argument(streamExpr));

        // Second arg: kernel config
        var configExpr = BuildConfigExpression(
            streamExpr,
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
    /// Surrounds a rewritten statement with #line directives that map it, and
    /// the code following it, back to their original source locations. The
    /// rewritten statement may span fewer lines than the original, so the
    /// mapping is restored right after it. Directives must start a line of
    /// their own, so line breaks are inserted where the statement shares its
    /// lines with other code.
    /// </summary>
    private static ExpressionStatementSyntax PrependLineDirective(
        ExpressionStatementSyntax rewritten,
        ExpressionStatementSyntax originalStmt)
    {
        var tree = originalStmt.SyntaxTree;
        var fullSpan = originalStmt.FullSpan;
        var startSpan = tree.GetLineSpan(new TextSpan(fullSpan.Start, 0));
        var endSpan = tree.GetLineSpan(originalStmt.Span);
        if (!startSpan.IsValid || !endSpan.IsValid)
            return rewritten;

        var originalFile = startSpan.Path;
        if (string.IsNullOrEmpty(originalFile))
            return rewritten;

        // The leading trivia starts the line it is on (1-based) unless the
        // previous token ends on the same line
        var eol = SyntaxFactory.EndOfLine("\n");
        var previous = originalStmt.GetFirstToken().GetPreviousToken();
        bool atLineStart = previous == default
            || previous.TrailingTrivia.Any(SyntaxKind.EndOfLineTrivia);
        var leading = SyntaxFactory.TriviaList(
            SyntaxFactory.Trivia(BuildLineDirective(
                startSpan.StartLinePosition.Line + 1,
                originalFile)),
            eol);
        if (!atLineStart)
            leading = leading.Insert(0, eol);
        leading = leading.AddRange(rewritten.GetLeadingTrivia());

        // The code after the statement continues on the line after the end of
        // the statement, or on its last line if the statement does not end it
        var trailing = rewritten.GetTrailingTrivia();
        int endLine = endSpan.EndLinePosition.Line + 1;
        if (trailing.Any(SyntaxKind.EndOfLineTrivia))
            ++endLine;
        else
            trailing = trailing.Add(eol);
        trailing = trailing
            .Add(SyntaxFactory.Trivia(BuildLineDirective(endLine, originalFile)))
            .Add(eol);

        return rewritten
            .WithLeadingTrivia(leading)
            .WithTrailingTrivia(trailing);
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
    private static CastExpressionSyntax BuildUserExtentExpression(
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
