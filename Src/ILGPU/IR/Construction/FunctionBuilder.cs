// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: FunctionBuilder.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading;
using ILGPU.IR.Types;
using System.Reflection;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.IR.Construction
{
    /// <summary>
    /// A builder to builder function values.
    /// </summary>
    public sealed class FunctionBuilder : IFunctionMappingObject
    {
        #region Instance

        private bool parametersSealed = false;
        private ImmutableArray<ValueReference>.Builder parameters;
        private volatile int processing = 0;

        internal FunctionBuilder(IRBuilder irBuilder, FunctionValue function)
        {
            FunctionValue = function;
            IRBuilder = irBuilder;
            parameters = ImmutableArray.CreateBuilder<ValueReference>();
        }

        internal FunctionBuilder(
            IRBuilder irBuilder,
            FunctionValue function,
            TypeNode returnType)
            : this(irBuilder, function)
        {
            AddTopLevelParameters(returnType);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated IR context.
        /// </summary>
        public IRContext Context => IRBuilder.Context;

        /// <summary>
        /// Returns the associated IR builder.
        /// </summary>
        public IRBuilder IRBuilder { get; }

        /// <summary>
        /// Returns the encapsulated function.
        /// </summary>
        public FunctionValue FunctionValue { get; }

        /// <summary>
        /// Returns the associated function handle.
        /// </summary>
        public FunctionHandle Handle
        {
            get
            {
                if (FunctionValue is TopLevelFunction topLevelFunction)
                    return topLevelFunction.Handle;
                return FunctionHandle.Empty;
            }
        }

        /// <summary>
        /// Returns the original source method (may be null).
        /// </summary>
        public MethodBase Source
        {
            get
            {
                if (FunctionValue is TopLevelFunction topLevelFunction)
                    return topLevelFunction.Source;
                return null;
            }
        }

        /// <summary>
        /// Returns the memory parameter (if available).
        /// </summary>
        public Parameter MemoryParam { get; private set; }

        /// <summary>
        /// Returns the return parameter (if available).
        /// </summary>
        public Parameter ReturnParam { get; private set; }

        /// <summary>
        /// Returns the parameter with the given index.
        /// </summary>
        /// <param name="index">The parameter index.</param>
        /// <returns>The resolved parameter.</returns>
        public Parameter this[int index] => parameters[index].DirectTarget as Parameter;

        /// <summary>
        /// Returns the number of parameters.
        /// </summary>
        public int NumParams => parameters.Count;

        /// <summary>
        /// Returns true iff this function builder has been processed.
        /// </summary>
        public bool StartedProcessing => Interlocked.CompareExchange(ref processing, 1, 1) == 1;

        #endregion

        #region Methods

        /// <summary>
        /// Adds top-level parameters to this builder.
        /// </summary>
        /// <param name="returnType">The return type.</param>
        internal void AddTopLevelParameters(TypeNode returnType)
        {
            Debug.Assert(NumParams == 0 && !parametersSealed, "Invalid operation");
            Debug.Assert(returnType != null, "Invalid return type");
            var typeParams = returnType.IsVoidType ?
                ImmutableArray.Create<TypeNode>(IRBuilder.MemoryType) :
                ImmutableArray.Create(IRBuilder.MemoryType, returnType);
            var returnFunctionType = IRBuilder.CreateFunctionType(typeParams);

            MemoryParam = AddParameter(IRBuilder.MemoryType, "mem");
            ReturnParam = AddParameter(returnFunctionType, "ret");
        }

        /// <summary>
        /// Marks this builder for processing.
        /// </summary>
        /// <returns>True, iff this builder has not been marked before.</returns>
        public bool MarkForProcessing()
        {
            return Interlocked.CompareExchange(ref processing, 1, 0) == 0;
        }

        /// <summary>
        /// Verifies whether the function builder can accept additional parameters.
        /// If it is sealed, the method will raise an <see cref="InvalidOperationException"/>.
        /// </summary>
        private void VerifyNotParametersSealed()
        {
            Debug.Assert(!parametersSealed, "Function was already finished");
        }

#if VERIFICATION
        /// <summary>
        /// Verifies whether the function builder is sealed or not.
        /// If it is sealed, the method will raise an <see cref="InvalidOperationException"/>.
        /// </summary>
        private void VerifyNotSealed()
        {
            if (FunctionValue.IsSealed)
                throw new InvalidOperationException("Function was already finished");
        }
#endif

        /// <summary>
        /// Adds a new parameter to the encapsulated function.
        /// </summary>
        /// <param name="type">The parameter type.</param>
        /// <returns>The created parameter.</returns>
        public Parameter AddParameter(TypeNode type)
        {
            return AddParameter(type, null);
        }

        /// <summary>
        /// Adds a new parameter to the encapsulated function.
        /// </summary>
        /// <param name="type">The parameter type.</param>
        /// <param name="name">The parameter name (for debugging purposes).</param>
        /// <returns>The created parameter.</returns>
        public Parameter AddParameter(TypeNode type, string name)
        {
            VerifyNotParametersSealed();

            var param = IRBuilder.CreateParam(type, name);
            parameters.Add(param);
            return param;
        }

        /// <summary>
        /// Inserts a new parameter to the encapsulated function at the beginning.
        /// </summary>
        /// <param name="type">The parameter type.</param>
        /// <returns>The created parameter.</returns>
        public Parameter InsertParameter(TypeNode type)
        {
            return InsertParameter(type, null);
        }

        /// <summary>
        /// Inserts a new parameter to the encapsulated function at the beginning.
        /// </summary>
        /// <param name="type">The parameter type.</param>
        /// <param name="name">The parameter name (for debugging purposes).</param>
        /// <returns>The created parameter.</returns>
        public Parameter InsertParameter(TypeNode type, string name)
        {
            VerifyNotParametersSealed();

            var param = IRBuilder.CreateParam(type, name);
            parameters.Insert(0, param);
            return param;
        }

        /// <summary>
        /// Seals all parameters.
        /// Additional parameters cannot be added after this operation
        /// has been executed.
        /// </summary>
        public void SealParameters()
        {
            VerifyNotParametersSealed();
            parametersSealed = true;

            var parameterTypes = ImmutableArray.CreateBuilder<TypeNode>(parameters.Count);
            for (int i = 0, e = parameters.Count; i < e; ++i)
            {
                var parameter = parameters[i].ResolveAs<Parameter>();
                Debug.Assert(parameter != null, "Invalid parameter state");
                parameterTypes.Add(parameter.Type);
                parameter.Index = i;
            }
            var functionType = IRBuilder.CreateFunctionType(
                parameterTypes.MoveToImmutable());
            FunctionValue.SealFunctionType(functionType);
        }

        /// <summary>
        /// Finishes the construction process of the function.
        /// </summary>
        /// <param name="target">The jump target to continue the execution.</param>
        /// <returns></returns>
        public FunctionValue Seal(ValueReference target)
        {
#if VERIFICATION
            VerifyNotSealed();
#endif
            if (!parametersSealed)
                SealParameters();

            var callTarget = target.ResolveAs<FunctionCall>();
            Debug.Assert(callTarget != null, "The function target must be call operation");

            FunctionValue.Seal(parameters.ToImmutable(), callTarget);
            return FunctionValue;
        }

        /// <summary>
        /// Seals an external function.
        /// </summary>
        /// <param name="builder">The current builder.</param>
        /// <returns>The resulting final external function.</returns>
        public TopLevelFunction SealExternal(IRBuilder builder)
        {
            Debug.Assert(builder != null, "Invalid builder");
            if (!(FunctionValue is TopLevelFunction topLevel) ||
                !topLevel.HasFlags(TopLevelFunctionFlags.External))
                throw new InvalidOperationException("Cannot seal an internal function using external semantics");

            var returnArgsBuilder = ImmutableArray.CreateBuilder<ValueReference>();
            returnArgsBuilder.Add(this[TopLevelFunction.MemoryParameterIndex]);

            var returnParam = this[TopLevelFunction.ReturnParameterIndex];
            var returnParamType = returnParam.Type as FunctionType;
            if (returnParamType.NumChildren > TopLevelFunction.ReturnParameterIndex)
            {
                returnArgsBuilder.Add(builder.CreateUndef(
                    returnParamType.Children[TopLevelFunction.ReturnParameterIndex]));
            }

            var target = builder.CreateFunctionCall(
                returnParam,
                returnArgsBuilder.ToImmutable());
            return Seal(target) as TopLevelFunction;
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns the string representation of the underlying function.
        /// </summary>
        /// <returns>The string representation of the underlying function.</returns>
        public override string ToString()
        {
            return FunctionValue.ToString();
        }

        #endregion
    }
}
