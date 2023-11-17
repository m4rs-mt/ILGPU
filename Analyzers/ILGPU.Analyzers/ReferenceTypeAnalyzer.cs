using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ILGPU.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ReferenceTypeAnalyzer : KernelAnalyzer
{
    private static readonly DiagnosticDescriptor GeneralDiagnosticRule = new("IL0001",
        ResourceUtil.GetLocalized(nameof(Resources.IL0001Title)),
        ResourceUtil.GetLocalized(nameof(Resources.IL0001MessageFormat)),
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: ResourceUtil.GetLocalized(nameof(Resources.IL0001Description))
    );

    private static readonly DiagnosticDescriptor ArrayDiagnosticRule = new("IL0002",
        ResourceUtil.GetLocalized(nameof(Resources.IL0002Title)),
        ResourceUtil.GetLocalized(nameof(Resources.IL0002MessageFormat)),
        "Usage",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: ResourceUtil.GetLocalized(nameof(Resources.IL0002Description))
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        ImmutableArray.Create(GeneralDiagnosticRule, ArrayDiagnosticRule);

    protected override void AnalyzeKernelOperation(OperationAnalysisContext context,
        IOperation operation)
    {
        if (operation.Type is null)
        {
            return;
        }

        if (operation.Type.IsValueType)
        {
            return;
        }

        if (operation.Type is IArrayTypeSymbol arrayTypeSymbol)
        {
            if (!arrayTypeSymbol.ElementType.IsValueType)
            {
                var arrayDiagnostic =
                    Diagnostic.Create(ArrayDiagnosticRule,
                        operation.Syntax.GetLocation(),
                        operation.Type.ToDisplayString(),
                        arrayTypeSymbol.ElementType.ToDisplayString());
                context.ReportDiagnostic(arrayDiagnostic);
            }

            return;
        }

        var generalDiagnostic =
            Diagnostic.Create(GeneralDiagnosticRule,
                operation.Syntax.GetLocation(),
                operation.Type.ToDisplayString());
        context.ReportDiagnostic(generalDiagnostic);
    }
}
