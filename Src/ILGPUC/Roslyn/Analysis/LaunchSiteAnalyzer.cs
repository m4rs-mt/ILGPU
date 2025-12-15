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
using System.Collections.Generic;
using System.Linq;

namespace ILGPUC.Roslyn.Analysis;

/// <summary>
/// Walks syntax trees to find all AcceleratorStream.Launch* call sites
/// marked with [ReplaceWithLauncher].
/// </summary>
/// <param name="compilation">The parent compilation.</param>
sealed class LaunchSiteAnalyzer(CSharpCompilation compilation)
{
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
            var model = compilation.GetSemanticModel(tree);
            var walker = new LaunchSiteWalker(model, results);
            walker.Visit(tree.GetRoot());
        }
        return results;
    }

    /// <summary>
    /// An internal syntax walker.
    /// </summary>
    /// <param name="model">The semantic model.</param>
    /// <param name="results">The list of results.</param>
    private sealed class LaunchSiteWalker(
        SemanticModel model,
        List<KernelLaunchInfo> results) : CSharpSyntaxWalker
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

            var containingType = method.ContainingType;
            if (containingType?.Name != "AcceleratorStream")
                return;

            // Check for [ReplaceWithLauncher] attribute
            bool hasReplaceAttr = method.GetAttributes().Any(a =>
                a.AttributeClass?.Name is "ReplaceWithLauncherAttribute"
                    or "ReplaceWithLauncher");
            if (!hasReplaceAttr)
                return;

            // Extract the stream expression (receiver of the method call)
            ExpressionSyntax? streamExpr = node.Expression switch
            {
                MemberAccessExpressionSyntax memberAccess => memberAccess.Expression,
                _ => null,
            };
            if (streamExpr == null)
                return;

            // Extract arguments: (extent/config, kernelDelegate)
            var args = node.ArgumentList.Arguments;
            if (args.Count < 2)
                return;

            var extentOrConfig = args[0].Expression;
            var kernelArg = args[1].Expression;

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
