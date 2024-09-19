// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: DeadCodeElimination.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using ILGPU.Util;
using System.Collections.Generic;
using System.Diagnostics;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represents a DCE transformation.
    /// </summary>
    public sealed class DeadCodeElimination : UnorderedTransformation
    {
        #region Instance

        /// <summary>
        /// Constructs a new DCE transformation.
        /// </summary>
        public DeadCodeElimination() { }

        #endregion

        #region Methods

        /// <summary>
        /// Applies a DCE transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            var blocks = builder.SourceBlocks;
            var toProcess = InlineList<Value>.Create(blocks.Count << 2);

            // Mark all terminators and their values as non dead
            foreach (var block in blocks)
            {
                foreach (Value value in block)
                {
                    // Mark all memory values as non dead (except dead loads)
                    if (value is MemoryValue memoryValue &&
                        memoryValue.ValueKind != ValueKind.Load)
                    {
                        toProcess.Add(memoryValue);
                    }
                }

                // Register all terminator value dependencies
                foreach (Value node in block.Terminator.AsNotNull())
                    toProcess.Add(node);
            }

            // Mark all nodes as live
            var liveValues = new HashSet<Value>();
            while (toProcess.Count > 0)
            {
                var current = toProcess.Pop();
                if (!liveValues.Add(current))
                    continue;

                foreach (var node in current.Nodes)
                    toProcess.Add(node.Resolve());
            }

            // Remove all dead values
            bool updated = false;
            blocks.ForEachValue<Value>(value =>
            {
                if (liveValues.Contains(value))
                    return;

                Debug.Assert(
                    !(value is MemoryValue) || value.ValueKind == ValueKind.Load,
                    "Invalid memory value");
                var blockBuilder = builder[value.BasicBlock];
                blockBuilder.Remove(value);

                updated = true;
            });

            return updated;
        }

        #endregion
    }
}
