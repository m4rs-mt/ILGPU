// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: NormalizeCalls.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represents a call normalization transformation.
    /// </summary>
    public sealed class NormalizeCalls : UnorderedTransformation
    {
        /// <summary>
        /// The desired transformations that should run after
        /// applying this transformation.
        /// </summary>
        private const TransformationFlags FollowUpFlags =
            TransformationFlags.MergeCallChains;

        /// <summary>
        /// Constructs a new CPS transformer.
        /// </summary>
        public NormalizeCalls()
            : base(TransformationFlags.NormalizeCalls, FollowUpFlags, true, false)
        { }

        readonly struct CallTargetVisitor : FunctionCall.ITargetVisitor
        {
            public CallTargetVisitor(
                List<FunctionValue> simpleTargets,
                List<FunctionValue> mainTargets)
            {
                SimpleTargets = simpleTargets;
                MainTargets = mainTargets;
            }

            /// <summary>
            /// All simple local calls.
            /// </summary>
            public List<FunctionValue> SimpleTargets { get; }

            /// <summary>
            /// All main top-level functions.
            /// </summary>
            public List<FunctionValue> MainTargets { get; }

            /// <summary cref="FunctionCall.ITargetVisitor.VisitCallTarget(Value)"/>
            public bool VisitCallTarget(Value callTarget)
            {
                if (callTarget is TopLevelFunction topLevelFunction)
                    MainTargets.Add(topLevelFunction);
                else if (callTarget is FunctionValue functionValue)
                    SimpleTargets.Add(functionValue);
                return true;
            }
        }

        /// <summary cref="UnorderedTransformation.PerformTransformation(IRBuilder, TopLevelFunction)"/>
        protected override bool PerformTransformation(
            IRBuilder builder,
            TopLevelFunction topLevelFunction)
        {
            var scope = Scope.Create(builder, topLevelFunction);

            var callsToAdjust = new List<FunctionCall>();
            var simpleTargets = new List<FunctionValue>();
            var mainTargets = new List<FunctionValue>();

            foreach (var node in scope)
            {
                if (node is FunctionCall call &&
                    call.Target.ResolveAs<Conditional>() != null)
                {
                    simpleTargets.Clear();
                    mainTargets.Clear();
                    var visitor = new CallTargetVisitor(
                        simpleTargets,
                        mainTargets);
                    call.VisitCallTargets(ref visitor);

                    if (mainTargets.Count > 0 && simpleTargets.Count > 0)
                        callsToAdjust.Add(call);
                }
            }

            if (callsToAdjust.Count < 1)
                return false;

            foreach (var call in callsToAdjust)
            {
                var target = call.Target.ResolveAs<Conditional>();
                var callArgs = call.Arguments;
                Value newCall;
                if (target is Predicate predicate)
                {
                    var trueValue = builder.CreateTempTarget(predicate.TrueValue, callArgs);
                    var falseValue = builder.CreateTempTarget(predicate.FalseValue, callArgs);
                    newCall = builder.CreateFunctionCall(
                        builder.CreatePredicate(
                            predicate.Condition,
                            trueValue,
                            falseValue),
                        ImmutableArray<ValueReference>.Empty);
                }
                else if (target is SelectPredicate selectPredicate)
                {
                    var args = ImmutableArray.CreateBuilder<ValueReference>(
                        selectPredicate.Arguments.Length);
                    foreach (var arg in selectPredicate.Arguments)
                    {
                        var argValue = builder.CreateTempTarget(
                            arg,
                            callArgs);
                        args.Add(argValue);
                    }
                    newCall = builder.CreateFunctionCall(
                        builder.CreateSelectPredicate(
                            selectPredicate.Condition,
                            args.MoveToImmutable()),
                        ImmutableArray<ValueReference>.Empty);
                }
                else
                    throw new NotSupportedException("Not supported predicate type");
                call.Replace(newCall);
            }

            return true;
        }
    }
}
