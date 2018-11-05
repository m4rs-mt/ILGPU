// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: MergeCallChains.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Merges multiple sequential calls (a call chain) into a single call.
    /// </summary>
    public sealed class MergeCallChains : UnorderedTransformation
    {
        /// <summary>
        /// The desired transformations that should run after
        /// applying this transformation.
        /// </summary>
        private const TransformationFlags FollowUpFlags = TransformationFlags.MergeNopCalls;

        /// <summary>
        /// Constructs a new transformation to merge call chains.
        /// </summary>
        public MergeCallChains()
            : base(TransformationFlags.MergeCallChains, FollowUpFlags)
        { }

        /// <summary cref="UnorderedTransformation.PerformTransformation(IRBuilder, TopLevelFunction)"/>
        protected override bool PerformTransformation(
            IRBuilder builder,
            TopLevelFunction topLevelFunction)
        {
            var scope = Scope.Create(builder, topLevelFunction);
            var processing = new Stack<Value>(100);
            var marker = builder.NewNodeMarker();

            Value current = topLevelFunction.Target;
            bool result = false;
            FunctionCall start = null;

            do
            {
                if (!current.Mark(marker))
                {
                    start = null;
                    continue;
                }

                if (current is FunctionValue functionValue)
                    current = functionValue.Target;

                if (current is FunctionCall call)
                {
                    var callTarget = call.Target.Resolve();
                    var functionCallTarget = callTarget as FunctionValue;
                    if (functionCallTarget == null ||
                        functionCallTarget.IsTopLevel ||
                        !scope.GetUses(functionCallTarget).HasExactlyOne)
                    {
                        // Non-trivial target... try to merge
                        if (Merge(builder, start, call))
                        {
                            current = (start.Resolve() as FunctionCall).Target;
                            result = true;
                        }
                        start = null;
                    }
                    else if (start == null)
                    {
                        start = call;
                    }

                    processing.Push(call.Target);
                }
                else
                {
                    // Continue the search
                    foreach (var nodeRef in current)
                    {
                        var node = nodeRef.Resolve();
                        if ((node is FunctionValue funcValue && !funcValue.IsTopLevel) ||
                            node is FunctionCall ||
                            node is Predicate ||
                            node is SelectPredicate)
                            processing.Push(node);
                    }
                }
            }
            while (processing.Count > 0 && (current = processing.Pop()) != null) ;

            return result;
        }

        /// <summary>
        /// Merges a detected sequential call chain.
        /// </summary>
        /// <param name="builder">The parent builder.</param>
        /// <param name="start">The chain start.</param>
        /// <param name="end">The chain end.</param>
        /// <returns>True, iff the chain could be merged.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool Merge(IRBuilder builder, FunctionCall start, FunctionCall end)
        {
            if (start == null || start == end)
                return false;

            // Propagate all arguments
            var argumentLookup = new List<Value>(10);
            for (Value current = start; current != end; )
            {
                if (current is FunctionValue functionValue)
                {
                    // Replace parameters with the provided arguments
                    foreach (var parameter in functionValue.Parameters)
                    {
                        var paramValue = argumentLookup[parameter.Index];
                        Debug.Assert(paramValue != null, "Invalid parameter value");
                        parameter.Replace(paramValue);
                    }

                    current = functionValue.Target;
                }
                else
                {
                    var call = current as FunctionCall;
                    Debug.Assert(call != null, "Invalid sequential call chain");

                    // Setup arguments
                    argumentLookup.Clear();
                    for (int i = 0, e = call.NumArguments; i < e; ++i)
                        argumentLookup.Add(call.GetArgument(i));

                    current = call.Target;
                }
            }

            // Wire calls
            var arguments = ImmutableArray.CreateBuilder<ValueReference>(end.NumArguments);
            foreach (var arg in end.Arguments)
                arguments.Add(arg.Resolve());
            var newCall = builder.CreateFunctionCall(end.Target, arguments.ToImmutable());
            start.Replace(newCall);

            return true;
        }
    }
}
