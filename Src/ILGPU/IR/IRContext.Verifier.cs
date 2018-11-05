// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: IRContext.Verifier.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ILGPU.IR
{
    partial class IRContext
    {
        /// <summary>
        /// Represents a verifier.
        /// </summary>
        readonly struct Verifier
        {
            #region Instance

            private readonly HashSet<Value> visitedValues;
            private readonly Stack<Value> toProcessValues;
            private readonly Stack<TypeNode> toProcessTypes;

            /// <summary>
            /// Constructs a new verifier.
            /// </summary>
            /// <param name="context">The context to verify.</param>
            public Verifier(IRContext context)
            {
                Debug.Assert(context != null, "Invalid context");
                Context = context;

                visitedValues = new HashSet<Value>();
                toProcessValues = new Stack<Value>();
                toProcessTypes = new Stack<TypeNode>();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated context.
            /// </summary>
            public IRContext Context { get; }

            #endregion

            /// <summary>
            /// Verifies the given value.
            /// </summary>
            /// <param name="value">The value to verify.</param>
            public void Verify(Value value)
            {
                while (true)
                {
                    VerifyValue(value);

                    if (toProcessValues.Count < 1)
                        break;
                    value = toProcessValues.Pop();
                }
            }

            private void VerifyValue(Value value)
            {
                if (visitedValues.Contains(value))
                    return;
                visitedValues.Add(value);
#if VERIFICATION
                if (value.Generation != Context.CurrentGeneration)
                    throw new InvalidProgramException($"The value '{value}' has the wrong generation");
#endif
                if (value is FunctionCall call)
                {
                    var target = call.Target.Resolve();
                    if (call.IsTopLevelCall && target is Predicate)
                        throw new InvalidProgramException("A top level call cannot be nested into a predicate");
                }
                foreach (var node in value.Nodes)
                    toProcessValues.Push(node);
                // Verifiy types
                Verify(value.Type);
            }

            /// <summary>
            /// Verifies the given type node.
            /// </summary>
            /// <param name="typeNode">The type node to verify.</param>
            public void Verify(TypeNode typeNode)
            {
                while (true)
                {
                    VerifyTypeNode(typeNode);

                    if (toProcessTypes.Count < 1)
                        break;
                    typeNode = toProcessTypes.Pop();
                }
            }

            private void VerifyTypeNode(TypeNode typeNode)
            {
                if (!Context.unifiedTypes.TryGetValue(typeNode, out TypeNode unifiedNode))
                    throw new InvalidProgramException($"The type node '{typeNode}' could not be found in the context");
                if (typeNode != unifiedNode)
                    throw new InvalidProgramException($"The stored type node '{typeNode}' does not have the same unified node '{unifiedNode}'");
                if (typeNode is ContainerType containerType)
                {
                    foreach (var child in containerType.Children)
                        toProcessTypes.Push(child);
                }
            }
        }

        /// <summary>
        /// Verifies this context and throws an <see cref="InvalidProgramException"/>
        /// in case of an invalid program.
        /// </summary>
        public void Verify()
        {
            var verifier = new Verifier(this);
            foreach (var function in topLevelFunctions)
                verifier.Verify(function);
        }
    }
}
