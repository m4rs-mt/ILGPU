using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Linq;

namespace ILGPU.Analyzers
{
    public abstract class KernelAnalyzer : DiagnosticAnalyzer
    {
        private readonly ImmutableHashSet<string> _kernelLoadNames =
            ImmutableHashSet.Create(
                "LoadKernel",
                "LoadAutoGroupedKernel",
                "LoadImplicitlyGroupedKernel",
                "LoadStreamKernel",
                "LoadAutoGroupedStreamKernel",
                "LoadImplicitlyGroupedStreamKernel"
                );

        /// <summary>
        /// Called for every operation potentially reachable from a kernel.
        /// </summary>
        /// <param name="context">
        /// The analysis context used to report diagnostics.
        /// </param>
        /// <param name="operation">
        /// The operation. Operations for subsequent calls may be parents or descendants
        /// of an operation for a previous call.
        /// </param>
        protected abstract void AnalyzeKernelOperation(
            OperationAnalysisContext context,
            IOperation operation);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();

            // Subscribe to semantic (compile time) action invocation, e.g. method invocation.
            context.RegisterOperationAction(AnalyzeOperation, OperationKind.Invocation);
        }

        private void AnalyzeOperation(OperationAnalysisContext context)
        {
            if (context.Operation is not IInvocationOperation invocationOperation ||
                context.Operation.Syntax is not InvocationExpressionSyntax)
                return;

            var methodSymbol = invocationOperation.TargetMethod;

            if (methodSymbol.MethodKind != MethodKind.Ordinary
                || !_kernelLoadNames.Contains(methodSymbol.Name))
                return;

            var kernelArg = invocationOperation.Arguments.FirstOrDefault(x =>
                x.Parameter?.Type.TypeKind == TypeKind.Delegate);

            // TODO: support expressions that return delegate (probably requires dataflow)
            if (kernelArg?.Value is
                IDelegateCreationOperation
                delegateOp)
            {
                var semanticModel = context.Operation.SemanticModel;

                var bodyOp = delegateOp.Target switch
                {
                    IMethodReferenceOperation refOp => GetMethodBody(semanticModel,
                        refOp.Method),
                    IAnonymousFunctionOperation anonymousOp => anonymousOp.Body,
                    _ => null
                };

                RecursivelyAnalyzeKernelOperations(context, bodyOp);
            }
        }

        // TODO: iterative using stack
        private void RecursivelyAnalyzeKernelOperations(OperationAnalysisContext context,
            IOperation? bodyOp,
            HashSet<IOperation>? seen = null)
        {
            seen ??= new HashSet<IOperation>();

            if (bodyOp is null) return;
            if (seen.Contains(bodyOp)) return;

            var semanticModel = context.Operation.SemanticModel;

            foreach (var descendant in bodyOp.DescendantsAndSelf())
            {
                seen.Add(descendant);
                AnalyzeKernelOperation(context, descendant);

                // Okay, so this doesn't cover every possible way other code can be called
                // through a kernel. But this is in general quite difficult to do. We can
                // improve things over time as necessary, perhaps. Thankfully, if we
                // accept a degree of false positives, we probably don't need to solve the
                // halting problem :)
                if (descendant is IInvocationOperation kernelMethodCallOperation)
                {
                    var innerBodyOp = GetMethodBody(semanticModel,
                        kernelMethodCallOperation.TargetMethod);
                    RecursivelyAnalyzeKernelOperations(context, innerBodyOp, seen);
                }
            }
        }

        private IOperation? GetMethodBody(SemanticModel? model, IMethodSymbol symbol) =>
            symbol switch
            {
                { IsPartialDefinition: false } => model?.GetOperation(
                    symbol.DeclaringSyntaxReferences[0].GetSyntax()),
                { PartialImplementationPart: not null } => model?.GetOperation(
                    symbol.PartialImplementationPart.DeclaringSyntaxReferences[0]
                        .GetSyntax()),
                _ => null
            };
    }
}
