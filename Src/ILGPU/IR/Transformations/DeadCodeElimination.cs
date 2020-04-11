// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: DeadCodeElimination.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
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
            var scope = builder.CreateScope();
            var toProcess = new Stack<Value>();

            // Mark all terminators and their values as non dead
            scope.ForEachTerminator<TerminatorValue>(terminator =>
            {
                foreach (var node in terminator.Nodes)
                    toProcess.Push(node);
            });

            // Mark all memory values as non dead (except dead loads)
            scope.ForEachValue<MemoryValue>(value =>
            {
                if (value.ValueKind != ValueKind.Load)
                    toProcess.Push(value);
            });

            // Mark all calls as non dead
            scope.ForEachValue<MethodCall>(call => toProcess.Push(call));

            // Mark all nodes as live
            var liveValues = new HashSet<Value>();
            while (toProcess.Count > 0)
            {
                var current = toProcess.Pop();
                if (!liveValues.Add(current))
                    continue;

                foreach (var node in current.Nodes)
                    toProcess.Push(node.Resolve());
            }

            // Remove all dead values
            bool updated = false;
            scope.ForEachValue<Value>(value =>
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
