// ---------------------------------------------------------------------------------------
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
using ILGPU.Runtime.CPU;
using ILGPU.Util;
using System.Collections.Immutable;
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
            : base(context, new CPUCapabilityContext(), BackendFlags.None, 1, null)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Generates the actual kernel invocation call.
        /// </summary>
        protected override void GenerateCode<TEmitter>(
            EntryPoint entryPoint,
            in BackendContext backendContext,
            TEmitter emitter,
            in ILLocal task,
            in ILLocal index,
            ImmutableArray<ILLocal> locals)
        {
            // Load placeholder 'this' argument to satisfy IL evaluation stack
            if (entryPoint.MethodInfo.IsNotCapturingLambda())
                emitter.Emit(OpCodes.Ldnull);

            if (entryPoint.IsImplictlyGrouped)
            {
                // Load index
                emitter.Emit(LocalOperation.Load, index);
            }

            // Load kernel arguments
            foreach (var local in locals)
                emitter.Emit(LocalOperation.Load, local);

            // Invoke kernel
            emitter.EmitCall(entryPoint.MethodInfo);
        }

        #endregion
    }
}
