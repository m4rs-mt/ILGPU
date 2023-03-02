// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityFunctionGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Values;
using ILGPU.Runtime.Velocity;
using System.Net.Http.Headers;
using System.Reflection.Emit;

namespace ILGPU.Backends.Velocity
{
    /// <summary>
    /// A generator for non primary Velocity functions.
    /// </summary>
    /// <typeparam name="TILEmitter">The IL emitter type.</typeparam>
    /// <typeparam name="TVerifier">The view generator type.</typeparam>
    sealed class VelocityFunctionGenerator<TILEmitter, TVerifier> :
        VelocityCodeGenerator<TILEmitter, TVerifier>
        where TILEmitter : struct, IILEmitter
        where TVerifier : IVelocityWarpVerifier, new()
    {
        /// <summary>
        /// The internal return label.
        /// </summary>
        private readonly ILLabel returnLabel;

        /// <summary>
        /// The internal return-value local (if any).
        /// </summary>
        private readonly ILLocal? returnLocal;

        /// <summary>
        /// Creates a new Velocity function generator.
        /// </summary>
        /// <param name="args">The generator args to use.</param>
        /// <param name="method">The current method to generate code for.</param>
        /// <param name="allocas">All allocations of the current method.</param>
        public VelocityFunctionGenerator(
            in GeneratorArgs args,
            Method method,
            Allocas allocas)
            : base(args, method, allocas)
        {
            returnLabel = Emitter.DeclareLabel();
            returnLocal = method.IsVoid
                ? null
                : Emitter.DeclareLocal(
                    TypeGenerator.GetVectorizedType(method.ReturnType));
        }

        /// <summary>
        /// Generates Velocity code for this function.
        /// </summary>
        public override void GenerateCode()
        {
            // Bind the mask parameter
            Emitter.Emit(ArgumentOperation.Load, MaskParameterIndex);
            Emitter.Emit(LocalOperation.Store, GetBlockMask(Method.EntryBlock));

            // Bind all remaining parameters
            for (int i = 0; i < Method.NumParameters; ++i)
            {
                var parameterType = Method.Parameters[i].ParameterType;
                var parameterLocal = DeclareVectorizedTemporary(parameterType);

                Emitter.Emit(ArgumentOperation.Load, i + 1);
                Emitter.Emit(LocalOperation.Store, parameterLocal);

                Alias(Method.Parameters[i], parameterLocal);
            }

            // Emit the remaining code
            GenerateCodeInternal();

            // Emit the actual return part
            Emitter.MarkLabel(returnLabel);
            if (returnLocal.HasValue)
                Emitter.Emit(LocalOperation.Load, returnLocal.Value);
            Emitter.Emit(OpCodes.Ret);
        }

        /// <inheritdoc />
        public override void GenerateCode(ReturnTerminator returnTerminator)
        {
            // Note that this automatically returns a vectorized version
            // of all return values

            // Jump to the next block in case all lanes have been disabled
            Emitter.Emit(
                LocalOperation.Load,
                GetBlockMask(returnTerminator.BasicBlock));
            Emitter.EmitCall(Instructions.AreAllLanesActive);

            // In case not all lanes have completed processing, we will have to skip
            // the actual return statement here
            if (returnLocal.HasValue)
            {
                Load(returnTerminator.ReturnValue);
                Emitter.Emit(LocalOperation.Store, returnLocal.Value);
            }
            Emitter.Emit(OpCodes.Brtrue, returnLabel);
        }
    }
}
