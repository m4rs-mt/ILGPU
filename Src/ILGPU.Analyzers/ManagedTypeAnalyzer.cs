using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using System;
using System.Diagnostics;

namespace ILGPU.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ManagedTypeAnalyzer : DiagnosticAnalyzer
{
    private ImmutableHashSet<string> kernelLoadNames = ImmutableHashSet.Create(
        "LoadKernel",
        "LoadAutoGroupedKernel",
        "LoadImplicitlyGroupedKernel",
        "LoadStreamKernel",
        "LoadAutoGroupedStreamKernel",
        "LoadImplicitlyGroupedStreamKernel"
    );

    private const string DiagnosticId = "IL0001";

    private static readonly LocalizableString Title = ResourceUtil.GetLocalized(
        nameof(Resources.IL0001Title));

    private static readonly LocalizableString MessageFormat = ResourceUtil.GetLocalized(
        nameof(Resources.IL0001MessageFormat));

    private static readonly LocalizableString Description = ResourceUtil.GetLocalized(
        nameof(Resources.IL0001Description));

    private const string Category = "Usage";

    private static readonly DiagnosticDescriptor Rule = new(DiagnosticId,
        Title,
        MessageFormat,
        Category,
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: Description);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(Rule);

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
            || !kernelLoadNames.Contains(methodSymbol.Name))
            return;

        var arg = invocationOperation.Arguments.FirstOrDefault(x =>
            x.Parameter?.Type.TypeKind == TypeKind.Delegate);  // TODO: any issues here?

        if (arg?.Value is IDelegateCreationOperation // TODO: support expressions that return delegate
            {
                Target: IMethodReferenceOperation refOp // TODO: support lambda
            })
        {
            var methodBodyOp = context.Operation.SemanticModel?.GetOperation(refOp.Method
                .DeclaringSyntaxReferences[0].GetSyntax());

            // TODO: somehow include errors in method headers too
            var managed = methodBodyOp.DescendantsAndSelf()
                .Where(x => !x.Type?.IsUnmanagedType ?? false);

            foreach (var managedUsage in managed)
            {
                var diagnostic =
                    Diagnostic.Create(Rule,
                        managedUsage.Syntax.GetLocation(),
                        managedUsage.Type?.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}
