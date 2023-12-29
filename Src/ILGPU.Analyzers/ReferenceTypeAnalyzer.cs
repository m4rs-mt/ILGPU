using ILGPU.Analyzers.Resources;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ILGPU.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ReferenceTypeAnalyzer : KernelAnalyzer
    {
        private static readonly DiagnosticDescriptor GeneralDiagnosticRule = new(
            id: "ILA003",
            title: ILA003_ReferenceTypeInKernel.Title,
            messageFormat: ILA003_ReferenceTypeInKernel.MessageFormat,
            category: DiagnosticCategory.Usage,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: ILA003_ReferenceTypeInKernel.Description
        );

        private static readonly DiagnosticDescriptor ArrayDiagnosticRule = new(
            id: "ILA004",
            title: ILA004_ReferenceTypeArrayInKernel.Title,
            messageFormat: ILA004_ReferenceTypeArrayInKernel.MessageFormat,
            category: DiagnosticCategory.Usage,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description: ILA004_ReferenceTypeArrayInKernel.Description
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
}
