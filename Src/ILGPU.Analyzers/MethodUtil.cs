// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: MethodUtil.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using Microsoft.CodeAnalysis;

namespace ILGPU.Analyzers
{
    public static class MethodUtil
    {
        /// <summary>
        /// Gets the body operation of a given method.
        /// </summary>
        /// <param name="model">
        /// The semantic model for the compilation that includes <c>symbol</c>.
        /// </param>
        /// <param name="symbol">The method symbol to get the body of.</param>
        /// <returns>
        /// The body of the method represented by <c>symbol</c>. If <c>symbol</c> is a
        /// partial method, the operation representing the implementation is returned.
        /// Null if the operation could not be resolved, for example, if <c>symbol</c>
        /// is partial and there is no implementation part. 
        /// </returns>
        public static IOperation? GetMethodBody(SemanticModel model, IMethodSymbol symbol)
        {
            return symbol switch
            {
                {
                    IsPartialDefinition: false,
                    DeclaringSyntaxReferences: var refs,
                } => refs.Length > 0 ? GetOperationIfInTree(model, refs[0].GetSyntax()) : null,
                {
                    PartialImplementationPart: { DeclaringSyntaxReferences: var refs },
                } => refs.Length > 0 ? GetOperationIfInTree(model, refs[0].GetSyntax()) : null,
                _ => null
            };
        }

        private static IOperation? GetOperationIfInTree(SemanticModel model, SyntaxNode node)
        {
            var root = model.SyntaxTree.GetRoot();
            return root.Contains(node) ? model.GetOperation(node) : null;
        }
    }
}