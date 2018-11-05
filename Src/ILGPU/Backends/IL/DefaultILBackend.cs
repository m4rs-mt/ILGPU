// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: DefaultILBackend.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Values;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;

namespace ILGPU.Backends.IL
{
    /// <summary>
    /// The default IL backend that uses the original kernel method.
    /// </summary>
    public class DefaultILBackend : ILBackend
    {
        #region Instance

        /// <summary>
        /// Constructs a new IL backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        protected internal DefaultILBackend(Context context)
            : base(context, 1, null)
        { }

        #endregion

        #region Methods

        /// <summary cref="Backend.PrepareKernel(IRContext, TopLevelFunction, ABI, in ContextImportSpecification)"/>
        protected override void PrepareKernel(
            IRContext kernelContext,
            TopLevelFunction kernelFunction,
            ABI abi,
            in ContextImportSpecification importSpecification)
        { }

        /// <summary cref="ILBackend.GenerateLocals{TEmitter}(EntryPoint, TEmitter, KernelGenerationData, ImmutableArray{FieldInfo}, ILLocal)"/>
        protected override void GenerateLocals<TEmitter>(
            EntryPoint entryPoint,
            TEmitter emitter,
            KernelGenerationData kernelData,
            ImmutableArray<FieldInfo> taskArgumentMapping,
            ILLocal task)
        {
            // Cache all fields in local variables
            var taskArgumentLocals = ImmutableArray.CreateBuilder<ILLocal>(taskArgumentMapping.Length);

            for (int i = 0, e = taskArgumentMapping.Length; i < e; ++i)
            {
                var taskArgument = taskArgumentMapping[i];
                var taskArgumentType = taskArgument.FieldType;

                // Load instance field i
                emitter.Emit(LocalOperation.Load, task);
                emitter.Emit(OpCodes.Ldfld, taskArgumentMapping[i]);

                // Declare local
                taskArgumentLocals.Add(emitter.DeclareLocal(taskArgumentType));

                // Cache field value in local variable
                emitter.Emit(LocalOperation.Store, taskArgumentLocals[i]);
            }
            kernelData.SetupUniforms(taskArgumentLocals.MoveToImmutable());
        }

        /// <summary cref="ILBackend.GenerateCode{TEmitter}(EntryPoint, in BackendContext, TEmitter, KernelGenerationData)"/>
        protected override void GenerateCode<TEmitter>(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            TEmitter emitter,
            KernelGenerationData kernelData)
        {
            // Load index
            emitter.Emit(LocalOperation.Load, kernelData.Index);

            // Load kernel arguments
            foreach (var uniform in kernelData.Uniforms)
                emitter.Emit(LocalOperation.Load, uniform);

            // Invoke kernel
            emitter.EmitCall(entryPoint.MethodInfo);
        }

        #endregion
    }
}
