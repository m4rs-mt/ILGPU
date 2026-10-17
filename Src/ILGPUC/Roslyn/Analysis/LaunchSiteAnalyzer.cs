// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LaunchSiteAnalyzer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.Text;
using System.Collections.Generic;
using System.Linq;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// Walks syntax trees to find all AcceleratorStream.Launch* call sites
/// marked with [ReplaceWithLauncher].
/// </summary>
/// <param name="compilation">The parent compilation.</param>
/// <param name="models">Shared semantic models, if any.</param>
sealed class LaunchSiteAnalyzer(
    CSharpCompilation compilation,
    SemanticModelCache? models = null)
{
    /// <summary>
    /// Returns all diagnostics reported for launches that cannot be rewritten.
    /// </summary>
    public List<KernelDiagnostic> Diagnostics { get; } = [];

    /// <summary>
    /// Scans every syntax tree in the compilation and returns all
    /// <c>AcceleratorStream.Launch*</c> call sites annotated with
    /// <c>[ReplaceWithLauncher]</c>.
    /// </summary>
    /// <returns>
    /// A list of <see cref="KernelLaunchInfo"/> instances, one per launch site found.
    /// </returns>
    public List<KernelLaunchInfo> FindAllLaunchSites()
    {
        var results = new List<KernelLaunchInfo>();
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = models?[tree] ?? compilation.GetSemanticModel(tree);
            var walker = new LaunchSiteWalker(model, results, Diagnostics);
            walker.Visit(tree.GetRoot());
        }
        return results;
    }

    /// <summary>
    /// An internal syntax walker.
    /// </summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="results">The list of results.</param>
    /// <param name="diagnostics">The list of diagnostics.</param>
    private sealed class LaunchSiteWalker(
        SemanticModel model,
        List<KernelLaunchInfo> results,
        List<KernelDiagnostic> diagnostics) : CSharpSyntaxWalker
    {
        /// <summary>
        /// Checks each invocation expression to determine whether it is a
        /// kernel launch site and, if so, records it.
        /// </summary>
        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            base.VisitInvocationExpression(node);

            var symbolInfo = model.GetSymbolInfo(node);
            if (symbolInfo.Symbol is not IMethodSymbol method)
                return;

            if (!IsLaunchMethod(method))
                return;

            // Extract the stream expression (receiver of the method call).
            // Launches that are not called on a member access, such as
            // conditional accesses, cannot be replaced and would throw at
            // runtime.
            if (node.Expression is not MemberAccessExpressionSyntax
                {
                    Expression: var streamExpr,
                })
            {
                ReportNotRewritable(node);
                return;
            }

            // Extract arguments by parameter: (extent/config, kernelDelegate)
            if (model.GetOperation(node) is not IInvocationOperation call
                || GetArgument(call, 0) is not { } extentOrConfig
                || GetArgument(call, 1) is not { } kernelArg)
            {
                return;
            }

            var variant = ClassifyVariant(method);

            results.Add(new KernelLaunchInfo(
                Invocation: node,
                LaunchMethod: method,
                StreamExpression: streamExpr,
                ExtentOrConfig: extentOrConfig,
                KernelArgument: kernelArg,
                Variant: variant));
        }

        /// <summary>
        /// Reports launch methods used as delegates (e.g. <c>stream.Launch</c>
        /// converted to an <c>Action</c>), whose launches cannot be replaced.
        /// </summary>
        public override void VisitMemberAccessExpression(
            MemberAccessExpressionSyntax node)
        {
            base.VisitMemberAccessExpression(node);

            if (!node.Name.Identifier.ValueText.StartsWith(
                    "Launch",
                    System.StringComparison.Ordinal)
                || node.Parent is InvocationExpressionSyntax invocation
                    && invocation.Expression == node
                || model.GetOperation(node) is not IMethodReferenceOperation
                {
                    Method: var method,
                }
                || !IsLaunchMethod(method))
            {
                return;
            }

            diagnostics.Add(new KernelDiagnostic(
                KernelDiagnostic.LaunchNotRewritableId,
                FormatNotRewritable(node.ToString()),
                node.GetLocation()));
        }

        /// <summary>
        /// Returns <see langword="true"/> if the given method is a launch method
        /// of <c>AcceleratorStream</c> marked with <c>[ReplaceWithLauncher]</c>.
        /// </summary>
        private static bool IsLaunchMethod(IMethodSymbol method) =>
            method.ContainingType?.Name == "AcceleratorStream"
            && method.GetAttributes().Any(a =>
                a.AttributeClass?.Name is "ReplaceWithLauncherAttribute"
                    or "ReplaceWithLauncher");

        /// <summary>
        /// Returns the expression passed for the parameter with the given
        /// ordinal, also for named arguments in a different order. Parentheses
        /// around the kernel (the second parameter) are removed.
        /// </summary>
        private static ExpressionSyntax? GetArgument(
            IInvocationOperation call,
            int ordinal)
        {
            var argument = call.Arguments.FirstOrDefault(
                a => a.Parameter?.Ordinal == ordinal);
            if (argument?.Syntax is not ArgumentSyntax { Expression: var expression })
                return null;
            while (ordinal > 0
                && expression is ParenthesizedExpressionSyntax parenthesized)
            {
                expression = parenthesized.Expression;
            }
            return expression;
        }

        /// <summary>
        /// Reports a launch invocation that is not called on a member access.
        /// </summary>
        private void ReportNotRewritable(InvocationExpressionSyntax node)
        {
            // Point at 'stream?.Launch' for conditional accesses
            var location = node.Expression.GetLocation();
            var display = node.Expression.ToString();
            if (node.Expression is MemberBindingExpressionSyntax
                && node.Parent is ConditionalAccessExpressionSyntax conditional)
            {
                var span = TextSpan.FromBounds(
                    conditional.SpanStart,
                    node.Expression.Span.End);
                location = Location.Create(node.SyntaxTree, span);
                display = node.SyntaxTree.GetText().ToString(span);
            }
            diagnostics.Add(new KernelDiagnostic(
                KernelDiagnostic.LaunchNotRewritableId,
                FormatNotRewritable(display),
                location));
        }

        /// <summary>
        /// Formats the message of a launch that cannot be rewritten.
        /// </summary>
        private static string FormatNotRewritable(string launch) =>
            $"Kernel launch '{launch}' cannot be compiled: launches must be " +
            $"direct calls on a stream expression (e.g. 'stream.Launch(...)'). " +
            $"Conditional access and calls through delegates are not supported.";

        /// <summary>
        /// Classifies the launch variant based on the method name and first parameter
        /// type.
        /// </summary>
        private static LaunchVariant ClassifyVariant(IMethodSymbol method)
        {
            var name = method.Name;
            var paramType = method.Parameters[0].Type.Name;

            return name switch
            {
                "Launch" when paramType == "Index1D" => LaunchVariant.Auto1D,
                "Launch" when paramType == "LongIndex1D" => LaunchVariant.AutoLong1D,
                "Launch" when paramType == "KernelConfig" => LaunchVariant.Grouped,
                "Launch" when paramType == "Int32" => LaunchVariant.Auto1D,
                "Launch" when paramType == "Int64" => LaunchVariant.AutoLong1D,
                "Launch" when paramType == "Index2D" => LaunchVariant.Auto2D,
                "Launch" when paramType == "Index3D" => LaunchVariant.Auto3D,
                "Launch2D" when paramType == "Index2D" =>
                    method.IsGenericMethod
                        ? LaunchVariant.Auto2DStride
                        : LaunchVariant.Auto2D,
                "Launch2D" when paramType == "LongIndex2D" =>
                    method.IsGenericMethod
                        ? LaunchVariant.AutoLong2DStride
                        : LaunchVariant.AutoLong2D,
                "Launch3D" when paramType == "Index3D" =>
                    method.IsGenericMethod
                        ? LaunchVariant.Auto3DStride
                        : LaunchVariant.Auto3D,
                "Launch3D" when paramType == "LongIndex3D" =>
                    method.IsGenericMethod
                        ? LaunchVariant.AutoLong3DStride
                        : LaunchVariant.AutoLong3D,
                _ => LaunchVariant.Auto1D,
            };
        }
    }
}
