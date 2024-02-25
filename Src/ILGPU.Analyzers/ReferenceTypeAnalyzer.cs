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
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Operations;

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

        public override ImmutableArray<DiagnosticDescriptor>
            SupportedDiagnostics { get; } =
            ImmutableArray.Create(GeneralDiagnosticRule, ArrayDiagnosticRule);

        protected override void AnalyzeKernelBody(OperationAnalysisContext context,
            IOperation bodyOp)
        {
            Stack<IOperation> bodies = new Stack<IOperation>();
            // To catch mutual recursion
            HashSet<IOperation> seenBodies = new HashSet<IOperation>();
            bodies.Push(bodyOp);

            // We will always have a semantic model because KernelAnalyzer subscribes
            // to semantic analysis
            var semanticModel = context.Operation.SemanticModel!;

            while (bodies.Count != 0)
            {
                var op = bodies.Pop();

                foreach (var descendant in op.DescendantsAndSelf())
                {
                    AnalyzeKernelOperation(context, descendant);

                    var innerBodyOp = GetInvokedOp(semanticModel, descendant);
                    if (innerBodyOp is null) continue;

                    if (!seenBodies.Contains(innerBodyOp))
                    {
                        bodies.Push(innerBodyOp);
                        seenBodies.Add(innerBodyOp);
                    }
                }
            }
        }

        private void AnalyzeKernelOperation(OperationAnalysisContext context,
            IOperation op)
        {
            if (op.Type is null)
                return;

            if (op.Type.IsValueType)
                return;

            if (op.Type is IArrayTypeSymbol arrayTypeSymbol)
            {
                if (arrayTypeSymbol.ElementType.IsValueType)
                    return;

                string first = arrayTypeSymbol.ToDisplayString();
                string second = arrayTypeSymbol.ElementType.ToDisplayString();

                var arrayDiagnostic =
                    Diagnostic.Create(ArrayDiagnosticRule,
                        op.Syntax.GetLocation(), first, second);
                context.ReportDiagnostic(arrayDiagnostic);
            }
            else
            {
                var generalDiagnostic =
                    Diagnostic.Create(GeneralDiagnosticRule,
                        op.Syntax.GetLocation(),
                        op.Type.ToDisplayString());
                context.ReportDiagnostic(generalDiagnostic);
            }
        }

        private IOperation? GetInvokedOp(SemanticModel model, IOperation op)
        {
            // TODO: Are there more ways code can be called?
            if (op is IInvocationOperation invocationOperation)
            {
                return MethodUtil.GetMethodBody(model, invocationOperation.TargetMethod);
            }

            return null;
        }
    }
}