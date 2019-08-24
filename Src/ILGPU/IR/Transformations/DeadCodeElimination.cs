// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: DeadCodeElimination.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Analyses;
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
        /// <summary>
        /// Constructs a new DCE transformation.
        /// </summary>
        public DeadCodeElimination() { }

        /// <summary cref="UnorderedTransformation.PerformTransformation(Method.Builder)"/>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            var scope = builder.CreateScope();
            var liveValues = FindLiveValues(scope);

            bool result = false;
            foreach (Value value in scope.Values)
            {
                if (liveValues.Contains(value))
                    continue;

                Debug.Assert(!(value is MemoryValue), "Invalid memory value");
                var blockBuilder = builder[value.BasicBlock];
                blockBuilder.Remove(value);
                result = true;
            }

            return result;
        }

        /// <summary>
        /// Detects all live values in the given scope.
        /// </summary>
        /// <param name="scope">The current scope.</param>
        /// <returns>The resolved set of live values.</returns>
        private static HashSet<Value> FindLiveValues(Scope scope)
        {
            var liveValues = new HashSet<Value>();

            var toProcess = new Stack<Value>();
            foreach (var block in scope)
            {
                var terminator = block.Terminator.ResolveAs<TerminatorValue>();
                foreach (var node in terminator.Nodes)
                    toProcess.Push(node);
            }

            // Mark all memory values as non dead
            // Mark all calls as non dead
            foreach (Value value in scope.Values)
            {
                switch (value)
                {
                    case MemoryValue _:
                    case MethodCall _:
                        toProcess.Push(value);
                        break;
                }
            }

            while (toProcess.Count > 0)
            {
                var current = toProcess.Pop();
                if (!liveValues.Add(current))
                    continue;

                foreach (var node in current.Nodes)
                    toProcess.Push(node.Resolve());
            }

            return liveValues;
        }
    }
}
