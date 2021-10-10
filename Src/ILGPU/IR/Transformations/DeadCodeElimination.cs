﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: DeadCodeElimination.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
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
            var blocks = builder.SourceBlocks;
            var toProcess = new Stack<Value>();

            // Mark all terminators and their values as non dead
            blocks.ForEachTerminator<TerminatorValue>(terminator =>
            {
                foreach (var node in terminator.Nodes)
                    toProcess.Push(node);
            });

            // Mark all memory values as non dead (except dead loads)
            blocks.ForEachValue<MemoryValue>(value =>
            {
                if (value.ValueKind != ValueKind.Load)
                    toProcess.Push(value);
            });

            // Mark all calls as non dead
            blocks.ForEachValue<MethodCall>(call => toProcess.Push(call));

            // Mark all inline language statements as non dead
            blocks.ForEachValue<LanguageEmitValue>(emit => toProcess.Push(emit));

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
