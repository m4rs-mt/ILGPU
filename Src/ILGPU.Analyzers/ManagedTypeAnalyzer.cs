// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: ManagedTypeAnalyzer.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Operations;
using ILGPU.Analyzers.Resources;

namespace ILGPU.Analyzers
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ManagedTypeAnalyzer : KernelAnalyzer
    {
        private static readonly DiagnosticDescriptor GeneralDiagnosticRule = new(
            id: "ILA003",
            title: ErrorMessages.ManagedTypeInKernel_Title,
            messageFormat: ErrorMessages.ManagedTypeInKernel_Message,
            category: DiagnosticCategory.Usage,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        private static readonly DiagnosticDescriptor ArrayDiagnosticRule = new(
            id: "ILA004",
            title: ErrorMessages.ManagedTypeArrayInKernel_Title,
            messageFormat: ErrorMessages.ManagedTypeArrayInKernel_Message,
            category: DiagnosticCategory.Usage,
            defaultSeverity: DiagnosticSeverity.Warning,
            isEnabledByDefault: true
        );

        private const string ILGPUAssemblyName = "ILGPU";
        private const string AlgorithmsAssemblyName = "ILGPU.Algorithms";

        public override ImmutableArray<DiagnosticDescriptor>
            SupportedDiagnostics { get; } =
            ImmutableArray.Create(GeneralDiagnosticRule, ArrayDiagnosticRule);

        protected override void AnalyzeKernelBody(OperationAnalysisContext context,
            IOperation bodyOp)
        {
            Stack<IOperation> bodies = new Stack<IOperation>();
            // To catch recursion
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

                    var invokedSymbol = GetInvokedSymbolIfExists(descendant);
                    if (invokedSymbol is null) continue;

                    if (IsILGPUSymbol(invokedSymbol)) continue;

                    var methodBodyOp =
                        MethodUtil.GetMethodBody(semanticModel, invokedSymbol);
                    if (methodBodyOp is null) continue;

                    if (!seenBodies.Contains(methodBodyOp))
                    {
                        bodies.Push(methodBodyOp);
                        seenBodies.Add(methodBodyOp);
                    }
                }
            }
        }

        private void AnalyzeKernelOperation(OperationAnalysisContext context,
            IOperation op)
        {
            if (op.Type is null)
                return;

            if (op.Type.IsUnmanagedType)
                return;

            if (IsILGPUSymbol(op.Type))
                return;

            if (op.Type.SpecialType == SpecialType.System_String)
                return;

            if (op.Type is IArrayTypeSymbol arrayTypeSymbol)
            {
                if (arrayTypeSymbol.ElementType.IsUnmanagedType)
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

        /// <summary>
        /// Gets the symbol for a method a given operation invokes, if the operation
        /// invokes anything at all. Only looks at direct invocations, meaning descendants
        /// will not be explored.
        /// </summary>
        /// <param name="op">The operation to analyze.</param>
        /// <returns>
        /// The method symbol representing the method <c>op</c> invokes. This could be a
        /// regular method or a constructor. Null if <c>op</c> doesn't invoke anything
        /// directly.
        /// </returns>
        private IMethodSymbol? GetInvokedSymbolIfExists(IOperation op) =>
            op switch
            {
                IInvocationOperation invocationOperation => invocationOperation
                    .TargetMethod,
                IObjectCreationOperation
                {
                    Constructor: not null
                } creationOperation => creationOperation.Constructor,
                _ => null
            };

        /// <summary>
        /// Whether a given symbol is a symbol from an ILGPU assembly.
        /// </summary>
        /// <param name="symbol">The symbol to check.</param>
        /// <returns>
        /// Whether <c>symbol</c> is in the ILGPU or ILGPU.Algorithms assemblies.
        /// </returns>
        private static bool IsILGPUSymbol(ISymbol symbol)
        {
            return symbol.ContainingAssembly?.Name is ILGPUAssemblyName
                or AlgorithmsAssemblyName;
        }
    }
}