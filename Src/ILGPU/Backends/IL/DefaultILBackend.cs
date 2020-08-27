﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: DefaultILBackend.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Util;
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;

namespace ILGPU.Backends.IL
{
    /// <summary>
    /// The default IL backend thach das it uses the original kernel method.
    /// </summary>
    public class DefaultILBackend : ILBackend
    {
        #region Instance

        /// <summary>
        /// Constructs a new IL backend.
        /// </summary>
        /// <param name="context">The context to use.</param>
        protected internal DefaultILBackend(Context context)
            : base(context, BackendFlags.None, 1, null)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Generates code that caches all task fields in local variables.
        /// </summary>
        protected override void GenerateLocals<TEmitter>(
            EntryPoint entryPoint,
            TEmitter emitter,
            KernelGenerationData kernelData,
            ImmutableArray<FieldInfo> taskArgumentMapping,
            ILLocal task)
        {
            // Cache all fields in local variables
            var taskArgumentLocals = ImmutableArray.CreateBuilder<ILLocal>(
                taskArgumentMapping.Length);

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

        /// <summary>
        /// Generates the actual kernel invocation call.
        /// </summary>
        protected override void GenerateCode<TEmitter>(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            TEmitter emitter,
            KernelGenerationData kernelData)
        {
            // Load placeholder 'this' argument to satisfy IL evaluation stack
            if (entryPoint.MethodInfo.IsNotCapturingLambda())
                emitter.Emit(OpCodes.Ldnull);

            if (entryPoint.IsImplictlyGrouped)
            {
                // Load index
                emitter.Emit(LocalOperation.Load, kernelData.Index);
            }

            // Load kernel arguments
            foreach (var uniform in kernelData.Uniforms)
                emitter.Emit(LocalOperation.Load, uniform);

            // Invoke kernel
            emitter.EmitCall(entryPoint.MethodInfo);
        }

        #endregion
    }
}
