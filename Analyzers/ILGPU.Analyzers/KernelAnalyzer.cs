using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;

namespace ILGPU.Analyzers;

public abstract class KernelAnalyzer : DiagnosticAnalyzer
{
    private readonly ImmutableHashSet<string> _kernelLoadNames = ImmutableHashSet.Create(
        "LoadKernel",
        "LoadAutoGroupedKernel",
        "LoadImplicitlyGroupedKernel",
        "LoadStreamKernel",
        "LoadAutoGroupedStreamKernel",
        "LoadImplicitlyGroupedStreamKernel"
    );

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
            x.Parameter?.Type.TypeKind == TypeKind.Delegate); // TODO: any issues here?

        if (kernelArg?.Value is
            IDelegateCreationOperation
            delegateOp) // TODO: support expressions that return delegate
        {
            var bodyOp = delegateOp.Target switch
            {
                // TODO: Multiple declaring syntax references?
                IMethodReferenceOperation refOp =>
                    context.Operation.SemanticModel
                    ?.GetOperation(refOp.Method.DeclaringSyntaxReferences[0].GetSyntax()),
                IAnonymousFunctionOperation anonymousOp => anonymousOp.Body,
                _ => null
            };

            if (bodyOp is not null)
            {
                // TODO: called methods should be analyzed as well
                foreach (var descendant in bodyOp.Descendants())
                {
                    AnalyzeKernelOperation(context, descendant);
                }
            }
        }
    }

    /// <summary>
    /// Called for every operation potentially reachable from a kernel.
    /// </summary>
    /// <param name="context">
    /// The analysis context used to report diagnostics.
    /// </param>
    /// <param name="operation">
    /// The operation. Operations for subsequent calls may be parents or descendants of
    /// an operation for a previous call.
    /// </param>
    protected abstract void AnalyzeKernelOperation(
        OperationAnalysisContext context,
        IOperation operation);
}
