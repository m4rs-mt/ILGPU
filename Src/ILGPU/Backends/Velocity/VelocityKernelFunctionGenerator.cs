// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityKernelFunctionGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.IL;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Values;
using ILGPU.Runtime.Velocity;
using System;
using System.Reflection.Emit;

namespace ILGPU.Backends.Velocity
{
    /// <summary>
    /// A generator for primary Velocity kernels.
    /// </summary>
    /// <typeparam name="TILEmitter">The IL emitter type.</typeparam>
    /// <typeparam name="TVerifier">The view generator type.</typeparam>
    sealed class VelocityKernelFunctionGenerator<TILEmitter, TVerifier> :
        VelocityCodeGenerator<TILEmitter, TVerifier>
        where TILEmitter : struct, IILEmitter
        where TVerifier : IVelocityWarpVerifier, new()
    {
        #region Constants

        public const int GlobalStartParameterIndex = 0;
        public const int GlobalEndParameterIndex = 1;
        public const int GlobalParametersIndex = 2;

        #endregion

        private readonly ILLabel nextMarker;

        /// <summary>
        /// Creates a new Velocity kernel generator.
        /// </summary>
        /// <param name="args">The generator args to use.</param>
        /// <param name="method">The current method to generate code for.</param>
        /// <param name="allocas">All allocations of the current method.</param>
        public VelocityKernelFunctionGenerator(
            in GeneratorArgs args,
            Method method,
            Allocas allocas)
            : base(args, method, allocas)
        {
            EntryPoint = args.EntryPoint;
            ParametersType = args.Module.ParametersType;

            // Generate an next marker to jump to when the kernel function returns
            nextMarker = Emitter.DeclareLabel();
        }

        /// <summary>
        /// Returns the current entry point.
        /// </summary>
        public EntryPoint EntryPoint { get; }

        /// <summary>
        /// Returns the current parameters type.
        /// </summary>
        public Type ParametersType { get; }

        /// <summary>
        /// Generates Velocity code for this kernel.
        /// </summary>
        public override void GenerateCode()
        {
            // Extract all arguments of the actual parameters object
            var parametersLocal = Emitter.DeclareLocal(ParametersType);
            Emitter.Emit(ArgumentOperation.Load, GlobalParametersIndex);
            Emitter.Emit(OpCodes.Castclass, ParametersType);
            Emitter.Emit(LocalOperation.Store, parametersLocal);

            // Load all parameters by mapping them to local variables
            for (
                int i = EntryPoint.KernelIndexParameterOffset;
                i < Method.NumParameters;
                ++i)
            {
                var parameterType = Method.Parameters[i].ParameterType;
                var parameterLocal = DeclareVectorizedTemporary(parameterType);

                Emitter.Emit(LocalOperation.Load, parametersLocal);
                Emitter.LoadField(
                    ParametersType,
                    i - EntryPoint.KernelIndexParameterOffset);
                Emitter.Emit(LocalOperation.Store, parameterLocal);

                Alias(Method.Parameters[i], parameterLocal);
            }

            // Load the actual counters and start the processing loop
            var headerMarker = Emitter.DeclareLabel();
            var exitMarker = Emitter.DeclareLabel();

            // Create initial offset variable
            var offsetVariable = Emitter.DeclareLocal(typeof(int));
            Emitter.Emit(ArgumentOperation.Load, GlobalStartParameterIndex);
            Emitter.Emit(LocalOperation.Store, offsetVariable);

            // Create cached group size information
            var groupDim = Emitter.DeclareLocal(typeof(int));
            Emitter.EmitCall(VelocityMultiprocessor.GetCurrentGroupDimScalarMethodInfo);
            Emitter.Emit(LocalOperation.Store, groupDim);

            // Bind the current implicitly grouped kernel index (if any)
            ILLocal? offsetVector = null;
            if (EntryPoint.IsImplicitlyGrouped)
            {
                offsetVector = Emitter.DeclareLocal(typeof(VelocityWarp32));
                Alias(Method.Parameters[0], offsetVector.Value);
            }

            // Create the loop header
            Emitter.MarkLabel(headerMarker);

            // Perform range check
            Emitter.Emit(LocalOperation.Load, offsetVariable);
            Emitter.Emit(ArgumentOperation.Load, GlobalEndParameterIndex);
            Emitter.Emit(OpCodes.Clt);
            Emitter.Emit(OpCodes.Brfalse, exitMarker);

            // The actual loop body
            {
                // Adjust linear index
                Emitter.Emit(LocalOperation.Load, offsetVariable);
                Emitter.EmitCall(VelocityMultiprocessor.SetCurrentLinearIdxMethod);

                // Check whether the current linear index allows us to activate certain
                // lanes that are smaller than the global end parameter index
                Emitter.EmitCall(VelocityMultiprocessor.GetCurrentLinearIdxMethod);

                // Check whether we need to bind an internal offset vector
                if (offsetVector.HasValue)
                {
                    Emitter.Emit(OpCodes.Dup);
                    Emitter.Emit(LocalOperation.Store, offsetVector.Value);
                }

                // Perform range check
                Emitter.Emit(ArgumentOperation.Load, GlobalEndParameterIndex);
                Emitter.EmitCall(Instructions.GetConstValueOperation32(
                    VelocityWarpOperationMode.I));
                Emitter.EmitCall(Instructions.GetCompareOperation32(
                    CompareKind.LessThan,
                    VelocityWarpOperationMode.I));

                // Get local group sizes and perform range check for all lanes
                Emitter.EmitCall(Instructions.LaneIndexVectorOperation32);
                Emitter.Emit(LocalOperation.Load, groupDim);
                Emitter.EmitCall(Instructions.GetConstValueOperation32(
                    VelocityWarpOperationMode.I));
                Emitter.EmitCall(Instructions.GetCompareOperation32(
                    CompareKind.LessThan,
                    VelocityWarpOperationMode.I));

                // Convert into a single lane mask and store the converted mask
                Emitter.EmitCall(Instructions.GetBinaryOperation32(
                    BinaryArithmeticKind.And,
                    VelocityWarpOperationMode.I));
                Emitter.EmitCall(Instructions.ToMaskOperation32);
                Emitter.Emit(LocalOperation.Store, GetBlockMask(Method.EntryBlock));

                // Emit the actual kernel code
                GenerateCodeInternal();
            }

            // Increment the current offset by adding the current warp size
            Emitter.MarkLabel(nextMarker);

            Emitter.Emit(LocalOperation.Load, offsetVariable);
            Emitter.Emit(LocalOperation.Load, groupDim);
            Emitter.Emit(OpCodes.Add);
            Emitter.Emit(LocalOperation.Store, offsetVariable);

            // Branch back to the header
            Emitter.Emit(OpCodes.Br, headerMarker);

            // Emit the exit marker
            Emitter.MarkLabel(exitMarker);

            // Return
            Emitter.Emit(OpCodes.Ret);
        }

        /// <inheritdoc />
        public override void GenerateCode(ReturnTerminator returnTerminator)
        {
            returnTerminator.Assert(returnTerminator.IsVoidReturn);

            // Jump to the next block in case all lanes have been disabled
            Emitter.Emit(
                LocalOperation.Load,
                GetBlockMask(returnTerminator.BasicBlock));
            Emitter.EmitCall(Instructions.AreAllLanesActive);
            Emitter.Emit(OpCodes.Brtrue, nextMarker);
        }
    }
}
