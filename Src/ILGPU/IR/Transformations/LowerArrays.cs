// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: LowerArrays.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Lowers array values using memory operations.
    /// </summary>
    public sealed class LowerArrays : UnorderedTransformation
    {
        /// <summary>
        /// Constructs a new array lowering transformation
        /// that does not lower arrays with static indices.
        /// </summary>
        public LowerArrays()
            : this(false)
        { }

        /// <summary>
        /// Constructs a new array lowering transformation.
        /// </summary>
        public LowerArrays(bool lowerStaticIndices)
        {
            LowerStaticIndices = lowerStaticIndices;
        }

        /// <summary>
        /// Returns true if arrays with static indices should be lowered.
        /// </summary>
        public bool LowerStaticIndices { get; }

        /// <summary cref="UnorderedTransformation.PerformTransformation(Method.Builder)"/>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            // Detect all array values and their associated operations
            var scope = builder.CreateScope();
            var arrays = FindConvertibleArrays(scope);

            // Transform dynamic arrays that requires dynamic addresses
            // to allocation nodes using memory operations
            foreach (var arrayBinding in arrays)
            {
                var array = arrayBinding.Key;
                var arrayType = array.Type as ArrayType;
                var blockBuilder = builder[array.BasicBlock];

                // Allocate a new raw allocation node
                blockBuilder.InsertPosition = 0;
                var arrayLength = blockBuilder.CreatePrimitiveValue(arrayType.Length);
                var rawArray = blockBuilder.CreateAlloca(
                    arrayLength,
                    arrayType.ElementType,
                    MemoryAddressSpace.Local);
                blockBuilder.Remove(array);

                // Convert all operations to memory operations
                foreach (var operation in arrayBinding.Value)
                {
                    var currentBlockBuilder = builder[operation.BasicBlock];
                    currentBlockBuilder.SetupInsertPosition(operation);

                    var elementAddress = currentBlockBuilder.CreateLoadElementAddress(rawArray, operation.Index);

                    if (operation is SetElement setElement)
                    {
                        currentBlockBuilder.CreateStore(elementAddress, setElement.Value);
                        operation.Replace(rawArray);
                    }
                    else
                    {
                        var load = currentBlockBuilder.CreateLoad(elementAddress);
                        operation.Replace(load);
                    }
                    currentBlockBuilder.Remove(operation);
                }
            }

            return arrays.Count > 0;
        }

        /// <summary>
        /// Finds all convertible array nodes.
        /// </summary>
        /// <param name="scope">The scope in which to search for arrays.</param>
        /// <returns>All detected convertible array nodes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private Dictionary<Value, HashSet<ArrayOperationValue>> FindConvertibleArrays(Scope scope)
        {
            var result = new Dictionary<Value, HashSet<ArrayOperationValue>>();

            foreach (Value value in scope.Values)
            {
                if (value is NullValue array && array.Type is ArrayType)
                {
                    Debug.Assert(!result.ContainsKey(array));

                    var operations = new HashSet<ArrayOperationValue>();
                    if (!RequiresDynamicIndexing(array, operations) & !LowerStaticIndices)
                        continue;
                    result.Add(array, operations);
                }
            }

            return result;
        }

        /// <summary>
        /// Returns true if the given node requires a dynamic indexing feature
        /// which requires memory addressed instead of registers.
        /// </summary>
        /// <param name="node">The current node.</param>
        /// <param name="operations">The set of associated operations that need to be transformed.</param>
        /// <returns>
        /// True, if the given node required dynamic indexing.
        /// </returns>
        private static bool RequiresDynamicIndexing(Value node, HashSet<ArrayOperationValue> operations)
        {
            bool result = false;
            foreach (var use in node.Uses)
            {
                var operation = use.ResolveAs<ArrayOperationValue>();
                if (operation == null || !operations.Add(operation))
                    continue;

                result |= !operation.TryResolveConstantIndex(out var _);
                result |= RequiresDynamicIndexing(operation, operations);
            }
            return result;
        }
    }
}
