// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: FunctionSpecializer.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ILGPU.IR.Construction
{
    /// <summary>
    /// An implementation function specialization context.
    /// </summary>
    public interface IImplFunctionSpecializationContext
    {
        /// <summary>
        /// Returns the dominators.
        /// </summary>
        Dominators Dominators { get; }

        /// <summary>
        /// Returns the current placement.
        /// </summary>
        Placement Placement { get; }
    }

    partial class IRBuilder
    {
        #region Function Specializer

        /// <summary>
        /// Specializes a single call.
        /// </summary>
        /// <param name="parentFunction">The parent function.</param>
        /// <param name="arguments">The call arguments to use.</param>
        /// <param name="functionValue">The called function.</param>
        /// <param name="targetCall">The call node for the specialized target function.</param>
        public FunctionValue SpecializeCall(
            TopLevelFunction parentFunction,
            ImmutableArray<ValueReference> arguments,
            FunctionValue functionValue,
            out FunctionCall targetCall)
        {
            if (parentFunction == null)
                throw new ArgumentNullException(nameof(parentFunction));
            if (functionValue == null)
                throw new ArgumentNullException(nameof(functionValue));

            FunctionValue result;
            lock (functionValue)
            {
                var scope = Scope.Create(this, functionValue);
                var rebuilder = CreateRebuilder(scope, IRRebuilderFlags.RebuildTopLevel);

                // Wire calls
                result = rebuilder.Rebuild();
            }

            lock (parentFunction)
            {
                targetCall = CreateFunctionCall(result, ImmutableArray<ValueReference>.Empty);

                // Replace parameters
                foreach (var parameter in result.Parameters)
                {
                    var argument = arguments[parameter.Index];
                    parameter.Replace(argument);
                }
            }

            return result;
        }

        /// <summary>
        /// Specializes a single call.
        /// </summary>
        /// <param name="parentFunction">The parent function.</param>
        /// <param name="call">The call to specialize.</param>
        /// <param name="functionValue">The called function.</param>
        public FunctionValue SpecializeCall(
            TopLevelFunction parentFunction,
            FunctionCall call,
            FunctionValue functionValue)
        {
            if (call == null)
                throw new ArgumentNullException(nameof(call));

            var result = SpecializeCall(
                parentFunction,
                call.Arguments,
                functionValue,
                out FunctionCall targetCall);
            call.Replace(targetCall);
            return result;
        }

        /// <summary>
        /// Specializes the given node by a call to the given implementation
        /// function and returns the resulting memory parameter.
        /// </summary>
        /// <typeparam name="TContext">The type of the specialization context.</typeparam>
        /// <param name="node">The node to specializ.</param>
        /// <param name="implementationFunction">The implementation function to use.</param>
        /// <param name="context">The current specialization context.</param>
        /// <returns>The resulting memory value of the new function call.</returns>
        public Parameter SpecializeNodeWithImplFunction<TContext>(
            Value node,
            TopLevelFunction implementationFunction,
            in TContext context)
            where TContext : IImplFunctionSpecializationContext
        {
            // Determine dominator and intercept the call to the dominator
            var cfgNode = context.Placement[node];
            var dominator = context.Dominators.GetImmediateDominator(cfgNode);
            var dominatingFunction = dominator.FunctionValue;
            var call = dominatingFunction.Target.ResolveAs<FunctionCall>();
            var callTarget = call.Target;
            var callArguments = call.Arguments;

            var memoryValue = node as MemoryValue;
            var isMemoryValue = memoryValue != null;

            // Build intermediate function
            var intermediateFunction = CreateFunction();
            var returnMemoryParam = intermediateFunction.AddParameter(MemoryType);
            var returnType = implementationFunction.ReturnType;
            var returnBasicType = implementationFunction.ReturnType.BasicValueType;
            Debug.Assert(
                returnBasicType != BasicValueType.None || isMemoryValue,
                "Invalid basic return type or no side effects");

            //var returnType = returnBasicType == BasicValueType.None ?
            //    (TypeNode)VoidType :
            //    CreatePrimitiveType(returnBasicType);
            var returnValueParam = !returnType.IsVoidType ?
                intermediateFunction.AddParameter(returnType) :
                null;

            // Memory reference
            var memoryRef = CreateUndefMemoryReference();

            // Jump to intermediate function
            var intermediateArgsBuilder = ImmutableArray.CreateBuilder<ValueReference>(
                node.Nodes.Length + 2);
            intermediateArgsBuilder.Add(memoryRef);
            intermediateArgsBuilder.Add(intermediateFunction.FunctionValue);

            for (int i = isMemoryValue ? 1 : 0, e = node.Nodes.Length; i < e; ++i)
                intermediateArgsBuilder.Add(node[i]);
            var callIntermediateTarget = CreateFunctionCall(
                implementationFunction,
                isMemoryValue ?
                    intermediateArgsBuilder.ToImmutable() :
                    intermediateArgsBuilder.MoveToImmutable());
            call.Replace(callIntermediateTarget);

            // Jump to actual target
            var callOriginalTarget = CreateFunctionCall(
                callTarget,
                callArguments);
            intermediateFunction.Seal(callOriginalTarget);

            // Replace memory chain value (if required)
            if (isMemoryValue)
            {
                MemoryRef.Replace(
                    this,
                    memoryValue,
                    memoryRef,
                    CreateMemoryReference(returnMemoryParam));
            }

            // Replace node with param
            if (!returnType.IsVoidType)
                node.Replace(returnValueParam);

            return returnMemoryParam;
        }

        #endregion
    }
}
