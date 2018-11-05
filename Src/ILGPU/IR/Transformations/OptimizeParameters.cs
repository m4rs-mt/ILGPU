// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: OptimizeParameters.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represents a parameter optimization transformation.
    /// It removes dead and unused phi parameters.
    /// </summary>
    public sealed class OptimizeParameters : UnorderedTransformation
    {
        /// <summary>
        /// The desired transformations that should run after
        /// applying this transformation.
        /// </summary>
        private const TransformationFlags FollowUpFlags =
            TransformationFlags.MergeCallChains |
            TransformationFlags.TransformToCPS;

        /// <summary>
        /// Constructs a new transformation to simplify control flow.
        /// </summary>
        public OptimizeParameters()
            : base(TransformationFlags.OptimizeParameters, FollowUpFlags, true, true)
        { }

        /// <summary cref="UnorderedTransformation.PerformTransformation(IRBuilder, TopLevelFunction)"/>
        protected override bool PerformTransformation(
            IRBuilder builder,
            TopLevelFunction topLevelFunction)
        {
            var scope = Scope.Create(builder, topLevelFunction);
            var marker = builder.NewNodeMarker();
            topLevelFunction.Mark(marker);

            bool result = false;

            foreach (var function in scope.Functions)
            {
                if (!function.Mark(marker) ||
                    function.IsReturnContinuation(scope))
                    continue;

                // Check parameters
                foreach (var param in function.Parameters)
                {
                    OptimizeParameter(builder, scope, function, param);
                    result |= param.IsReplaced;
                }
            }

            return result;
        }

        /// <summary>
        /// Gathers all function arguments and determines whether the
        /// parameter can be replaced with the associated <see cref="ParameterValue"/>
        /// </summary>
        private struct GatherFunctionArguments : Scope.IFunctionCallVisitor
        {
            public GatherFunctionArguments(Parameter parameter)
            {
                FunctionParameter = parameter;
                ParameterValue = null;
                CanReplace = false;
            }

            /// <summary>
            /// Returns the function parameter.
            /// </summary>
            public Parameter FunctionParameter { get; }

            /// <summary>
            /// Returns the current parameter value.
            /// </summary>
            public Value ParameterValue { get; private set; }

            /// <summary>
            /// Returns true iff the parameter can be replaced.
            /// </summary>
            public bool CanReplace { get; private set; }

            /// <summary cref="Scope.IFunctionCallVisitor.Visit(FunctionCall)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool Visit(FunctionCall functionCall)
            {
                Value argument = functionCall.GetArgument(FunctionParameter.Index);
                if (ParameterValue == argument || FunctionParameter == argument)
                    return true;
                if (ParameterValue == null)
                {
                    ParameterValue = argument;
                    return CanReplace = true;
                }
                else
                    return CanReplace = false;
            }
        }

        /// <summary>
        /// Tries to optimize the given parameter.
        /// </summary>
        /// <param name="builder">The current builder.</param>
        /// <param name="scope">The parent scope.</param>
        /// <param name="parentFunction">The parent function.</param>
        /// <param name="parameter">The current parameter.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void OptimizeParameter(
            IRBuilder builder,
            Scope scope,
            FunctionValue parentFunction,
            Parameter parameter)
        {
            var uses = scope.GetUses(parameter);
            if (!uses.HasAny ||
                uses.TryGetSingleUse(out Use functionUse) &&
                functionUse.Resolve() == parentFunction)
            {
                // Dead parameter -> replace with an empty node
                var undefNode = builder.CreateUndef(parameter.Type);
                parameter.Replace(undefNode);
            }
            else
            {
                // We have to check all passed arguments in order to find a trivial one.
                var functionArgs = new GatherFunctionArguments(parameter);
                scope.VisitUsedFunctionCalls(parentFunction, ref functionArgs);
                if (functionArgs.CanReplace)
                    parameter.Replace(functionArgs.ParameterValue);
            }
        }
    }
}
