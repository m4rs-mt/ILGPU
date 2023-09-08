// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityKernelFunctionGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.IL;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Values;
using System;
using System.Reflection.Emit;

namespace ILGPU.Backends.Velocity
{
    /// <summary>
    /// A generator for primary Velocity kernels.
    /// </summary>
    /// <typeparam name="TILEmitter">The IL emitter type.</typeparam>
    sealed class VelocityKernelFunctionGenerator<TILEmitter> :
        VelocityCodeGenerator<TILEmitter>
        where TILEmitter : struct, IILEmitter
    {
        #region Constants

        public const int GlobalGroupDimIndex = 1;
        public const int GlobalGridDimIndex = 2;
        public const int GlobalStartIndex = 3;
        public const int GlobalEndIndex = 4;
        public const int GlobalParametersIndex = 5;

        #endregion

        private readonly ILLabel localExitMarker;
        private readonly ILLocal targetMaskCount;
        private readonly ILLocal globalIndex;

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

            // Generate an exit marker to jump to when the kernel function returns
            localExitMarker = Emitter.DeclareLabel();

            // We use this counter to remember the number of active threads that entered
            // the kernel successfully
            targetMaskCount = Emitter.DeclareLocal(typeof(int));

            // Declare our global thread index local
            globalIndex = Emitter.DeclareLocal(typeof(int));
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
        /// Loads the current global index.
        /// </summary>
        protected override void LoadGlobalIndexScalar() =>
            Emitter.Emit(LocalOperation.Load, globalIndex);

        /// <summary>
        /// Loads the current group dimension.
        /// </summary>
        protected override void LoadGroupDimScalar() =>
            Emitter.Emit(ArgumentOperation.Load, GlobalGroupDimIndex);

        /// <summary>
        /// Loads the current grid dimension.
        /// </summary>
        protected override void LoadGridDimScalar() =>
            Emitter.Emit(ArgumentOperation.Load, GlobalGridDimIndex);

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

            // Declare a local index counter and initialize it with the start index
            Emitter.Emit(ArgumentOperation.Load, GlobalStartIndex);
            Emitter.Emit(LocalOperation.Store, globalIndex);

            // Setup the current main kernel mask based on the current group size
            var baseGroupMask = Emitter.DeclareLocal(Specializer.WarpType32);
            Specializer.LoadLaneIndexVector32(Emitter);
            Emitter.Emit(ArgumentOperation.Load, GlobalGroupDimIndex);
            Specializer.ConvertScalarTo32(Emitter, VelocityWarpOperationMode.I);
            Specializer.Compare32(
                Emitter,
                CompareKind.LessThan,
                VelocityWarpOperationMode.I);
            Emitter.Emit(LocalOperation.Store, baseGroupMask);

            // Build our execution loop header
            var iterationHeader = Emitter.DeclareLabel();
            Emitter.MarkLabel(iterationHeader);

            // Compare our global index against the exclusive end index
            var exitMarker = Emitter.DeclareLabel();
            Emitter.Emit(LocalOperation.Load, globalIndex);
            Emitter.Emit(ArgumentOperation.Load, GlobalEndIndex);
            Emitter.Emit(OpCodes.Bge, exitMarker);

            // Build our execution body
            {
                // Bind the current implicitly grouped kernel index (if any)
                var offsetVector = Emitter.DeclareLocal(Specializer.WarpType32);
                if (EntryPoint.IsImplicitlyGrouped)
                    Alias(Method.Parameters[0], offsetVector);

                // Compute the current global index
                Emitter.Emit(LocalOperation.Load, globalIndex);
                Specializer.ConvertScalarTo32(Emitter, VelocityWarpOperationMode.I);
                Specializer.LoadLaneIndexVector32(Emitter);
                Specializer.BinaryOperation32(
                    Emitter,
                    BinaryArithmeticKind.Add,
                    VelocityWarpOperationMode.I);
                Emitter.Emit(LocalOperation.Store, offsetVector);

                // Adjust the current main kernel mask based on the user grid size
                Emitter.Emit(LocalOperation.Load, offsetVector);
                Emitter.Emit(ArgumentOperation.Load, GlobalEndIndex);
                Specializer.ConvertScalarTo32(Emitter, VelocityWarpOperationMode.I);
                Specializer.Compare32(
                    Emitter,
                    CompareKind.LessThan,
                    VelocityWarpOperationMode.I);
                Emitter.Emit(LocalOperation.Load, baseGroupMask);
                Specializer.IntersectMask32(Emitter);

                var entryPointMask = GetBlockMask(Method.EntryBlock);
                Emitter.Emit(OpCodes.Dup);
                Emitter.Emit(LocalOperation.Store, entryPointMask);

                // Determine the target mask count
                Specializer.GetNumberOfActiveLanes(Emitter);
                Emitter.Emit(LocalOperation.Store, targetMaskCount);

                // Emit the actual kernel code
                GenerateCodeInternal();

                // Emit the exit marker
                Emitter.MarkLabel(localExitMarker);
            }

            // Increase the processing index
            Emitter.Emit(LocalOperation.Load, globalIndex);
            Emitter.Emit(ArgumentOperation.Load, GlobalGroupDimIndex);
            Emitter.Emit(OpCodes.Conv_I8);
            Emitter.Emit(OpCodes.Add);
            Emitter.Emit(LocalOperation.Store, globalIndex);

            // Branch to the loop header
            Emitter.Emit(OpCodes.Br, iterationHeader);

            // Return
            Emitter.MarkLabel(exitMarker);
            Emitter.Emit(OpCodes.Ret);
        }

        /// <inheritdoc />
        public override void GenerateCode(ReturnTerminator returnTerminator)
        {
            returnTerminator.Assert(returnTerminator.IsVoidReturn);

            // Jump to the exit block if all lanes are active
            Emitter.Emit(
                LocalOperation.Load,
                GetBlockMask(returnTerminator.BasicBlock));
            Specializer.GetNumberOfActiveLanes(Emitter);
            Emitter.Emit(LocalOperation.Load, targetMaskCount);
            Emitter.Emit(OpCodes.Beq, localExitMarker);
        }
    }
}
