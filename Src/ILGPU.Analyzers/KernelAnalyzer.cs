// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2023-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelAnalyzer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Collections.Concurrent;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Linq;

namespace ILGPU.Analyzers
{
    /// <summary>
    /// Base analyzer that can be used to implement diagnostic analyzers that operate on
    /// kernel method bodies. 
    /// </summary>
    public abstract class KernelAnalyzer : DiagnosticAnalyzer
    {
        private readonly ImmutableHashSet<string> kernelLoadNames =
            ImmutableHashSet.Create(
                "LoadKernel",
                "LoadAutoGroupedKernel",
                "LoadImplicitlyGroupedKernel",
                "LoadStreamKernel",
                "LoadAutoGroupedStreamKernel",
                "LoadImplicitlyGroupedStreamKernel"
            );

        private ConcurrentDictionary<IOperation, bool> seen = new();

        /// <summary>
        /// Called for every kernel body.
        /// </summary>
        /// <param name="context">
        /// The analysis context used to report diagnostics.
        /// </param>
        /// <param name="bodyOp">The operation.</param>
        protected abstract void AnalyzeKernelBody(
            OperationAnalysisContext context,
            IOperation bodyOp);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            seen.Clear();

            // Subscribe to semantic (compile time) action invocation
            // Subscribe only to method invocations (we want to find the kernel load call)
            context.RegisterOperationAction(AnalyzeOperation, OperationKind.Invocation);
        }

        private void AnalyzeOperation(OperationAnalysisContext context)
        {
            if (seen.ContainsKey(context.Operation)) return;

            if (context.Operation is not IInvocationOperation invocationOperation ||
                context.Operation.Syntax is not InvocationExpressionSyntax)
                return;

            var methodSymbol = invocationOperation.TargetMethod;

            if (methodSymbol.MethodKind != MethodKind.Ordinary
                || !kernelLoadNames.Contains(methodSymbol.Name))
                return;

            var kernelArg = invocationOperation.Arguments.FirstOrDefault(x =>
                x.Parameter?.Type.TypeKind == TypeKind.Delegate);

            // TODO: support expressions that return delegate (probably requires dataflow)
            if (kernelArg?.Value is
                IDelegateCreationOperation
                delegateOp)
            {
                // We should always have a semantic model since we subscribed
                // to semantic analysis
                var semanticModel = context.Operation.SemanticModel!;

                var bodyOp = delegateOp.Target switch
                {
                    IMethodReferenceOperation refOp => MethodUtil.GetMethodBody(
                        semanticModel,
                        refOp.Method),
                    IAnonymousFunctionOperation anonymousOp => anonymousOp.Body,
                    _ => null,
                };

                if (bodyOp is not null)
                {
                    // If we couldn't add it, another thread got here first.
                    bool added = seen.TryAdd(bodyOp, true);
                    if (added)
                    {
                        AnalyzeKernelBody(context, bodyOp);
                    }
                }
            }
        }
    }
}