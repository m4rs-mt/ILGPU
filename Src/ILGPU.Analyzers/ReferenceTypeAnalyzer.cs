// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2023-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: ReferenceTypeAnalyzer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

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
            title: ErrorMessages.RefTypeInKernel_Title,
            messageFormat: ErrorMessages.RefTypeInKernel_Message,
            category: DiagnosticCategory.Usage,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
        );

        private static readonly DiagnosticDescriptor ArrayDiagnosticRule = new(
            id: "ILA004",
            title: ErrorMessages.RefTypeArrInKernel_Title,
            messageFormat: ErrorMessages.RefTypeArrInKernel_Message,
            category: DiagnosticCategory.Usage,
            defaultSeverity: DiagnosticSeverity.Error,
            isEnabledByDefault: true
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
