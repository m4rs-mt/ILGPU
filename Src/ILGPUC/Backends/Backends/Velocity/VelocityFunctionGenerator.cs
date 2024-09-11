// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityFunctionGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.IR;
using ILGPU.IR.Analyses;
using ILGPU.IR.Values;
using System.Reflection.Emit;

namespace ILGPU.Backends.Velocity
{
    /// <summary>
    /// A generator for non primary Velocity functions.
    /// </summary>
    /// <typeparam name="TILEmitter">The IL emitter type.</typeparam>
    sealed class VelocityFunctionGenerator<TILEmitter> : VelocityCodeGenerator<TILEmitter>
        where TILEmitter : struct, IILEmitter
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
        /// The internal target mask counter.
        /// </summary>
        private readonly ILLocal targetMaskCount;

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

            // We use this counter to remember the number of active threads that entered
            // the kernel successfully
            targetMaskCount = Emitter.DeclareLocal(typeof(int));
        }

        /// <summary>
        /// Loads the current global index.
        /// </summary>
        protected override void LoadGlobalIndexScalar() =>
            Emitter.Emit(
                ArgumentOperation.Load,
                VelocityCodeGenerator.GlobalIndexScalar);

        /// <summary>
        /// Loads the current group dimension.
        /// </summary>
        protected override void LoadGroupDimScalar() =>
            Emitter.Emit(
                ArgumentOperation.Load,
                VelocityCodeGenerator.GroupDimIndexScalar);

        /// <summary>
        /// Loads the current grid dimension.
        /// </summary>
        protected override void LoadGridDimScalar() =>
            Emitter.Emit(
                ArgumentOperation.Load,
                VelocityCodeGenerator.GridDimIndexScalar);

        /// <summary>
        /// Generates Velocity code for this function.
        /// </summary>
        public override void GenerateCode()
        {
            // Bind the mask parameter
            Emitter.Emit(
                ArgumentOperation.Load,
                VelocityCodeGenerator.MaskParameterIndex);
            Emitter.Emit(OpCodes.Dup);
            Emitter.Emit(LocalOperation.Store, GetBlockMask(Method.EntryBlock));

            // Determine target mask counter
            Specializer.GetNumberOfActiveLanes(Emitter);
            Emitter.Emit(LocalOperation.Store, targetMaskCount);

            // Bind all remaining parameters
            for (int i = 0; i < Method.NumParameters; ++i)
            {
                var parameterType = Method.Parameters[i].ParameterType;
                var parameterLocal = DeclareVectorizedTemporary(parameterType);

                Emitter.Emit(
                    ArgumentOperation.Load,
                    i + VelocityCodeGenerator.MethodParameterOffset);
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
            void LoadMask()
            {
                Emitter.Emit(
                    LocalOperation.Load,
                    GetBlockMask(returnTerminator.BasicBlock));
            }

            // Jump to the exit block if all lanes are active
            LoadMask();
            Specializer.GetNumberOfActiveLanes(Emitter);
            Emitter.Emit(LocalOperation.Load, targetMaskCount);

            // In case not all lanes have completed processing, we will have to skip
            // the actual return statement here and merge the result
            if (returnLocal.HasValue)
            {
                var targetType = returnLocal.Value.VariableType;
                var tempLocal = EmitMerge(
                    returnTerminator.ReturnValue,
                    () =>
                    {
                        Emitter.Emit(LocalOperation.Load, returnLocal.Value);
                        return targetType;
                    },
                    () =>
                    {
                        Load(returnTerminator.ReturnValue);
                        return targetType;
                    },
                    LoadMask,
                _ => returnLocal.Value);

                if (!tempLocal.HasValue)
                    Emitter.Emit(LocalOperation.Store, returnLocal.Value);
            }

            Emitter.Emit(OpCodes.Beq, returnLabel);

            // Reset the current mask if required
            TryResetBlockLanes(returnTerminator.BasicBlock);
        }
    }
}
