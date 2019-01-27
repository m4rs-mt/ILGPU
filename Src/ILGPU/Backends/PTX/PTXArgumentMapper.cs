// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXArgumentMapper.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.Backends.PointerViews;
using ILGPU.Runtime;
using System;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Constructs mappings for PTX kernels.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed class PTXArgumentMapper : ViewArgumentMapper
    {
        #region Nested Types

        /// <summary>
        /// Implements the actual argument mapping.
        /// </summary>
        private readonly struct MappingHandler : IMappingHandler
        {
            /// <summary>
            /// Constructs a new mapping handler.
            /// </summary>
            /// <param name="argumentLocal">The unsafe target argument array.</param>
            /// <param name="argumentOffset">The target argument offset.</param>
            public MappingHandler(
                ILLocal argumentLocal,
                int argumentOffset)
            {
                ArgumentLocal = argumentLocal;
                ArgumentOffset = argumentOffset;
            }

            /// <summary>
            /// Returns the associated unsafe kernel argument local.
            /// </summary>
            public ILLocal ArgumentLocal { get; }

            /// <summary>
            /// Returns the argument offset.
            /// </summary>
            public int ArgumentOffset { get; }

            /// <summary cref="ArgumentMapper.IMappingHandler.MapArgument{TILEmitter, TSource}(in TILEmitter, TSource, int)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void MapArgument<TILEmitter, TSource>(
                in TILEmitter emitter,
                TSource source,
                int argumentIndex)
                where TILEmitter : IILEmitter
                where TSource : ISource
            {
                // Load and compute target address
                emitter.Emit(LocalOperation.Load, ArgumentLocal);
                emitter.EmitConstant(IntPtr.Size * (argumentIndex + ArgumentOffset));
                emitter.Emit(OpCodes.Conv_I);
                emitter.Emit(OpCodes.Add);

                // Load source address
                source.EmitLoadSource(emitter);
                emitter.Emit(OpCodes.Conv_I);

                // Store target
                emitter.Emit(OpCodes.Stind_I);
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new PTX argument mapper.
        /// </summary>
        /// <param name="context">The current context.</param>
        public PTXArgumentMapper(Context context)
            : base(context)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Stores the kernel length argument of an implicitly grouped kernel.
        /// </summary>
        /// <typeparam name="TILEmitter">The emitter type.</typeparam>
        /// <param name="emitter">The target emitter to write to.</param>
        /// <param name="argumentBuffer">The current local holding the native argument pointers.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void StoreKernelLength<TILEmitter>(
            in TILEmitter emitter,
            ILLocal argumentBuffer)
            where TILEmitter : IILEmitter
        {
            // Load target data pointer
            emitter.Emit(LocalOperation.Load, argumentBuffer);

            // Load source pointer
            emitter.Emit(ArgumentOperation.LoadAddress, Kernel.KernelParamDimensionIdx);
            emitter.Emit(OpCodes.Conv_I);

            // Store target
            emitter.Emit(OpCodes.Stind_I);
        }

        /// <summary>
        /// Creates code that maps the given parameter specification to
        /// a compatible representation.
        /// </summary>
        /// <typeparam name="TILEmitter">The emitter type.</typeparam>
        /// <param name="emitter">The target emitter to write to.</param>
        /// <param name="entryPoint">The entry point.</param>
        /// <returns>A local that stores the native kernel argument pointers.</returns>
        public ILLocal Map<TILEmitter>(in TILEmitter emitter, EntryPoint entryPoint)
            where TILEmitter : IILEmitter
        {
            Debug.Assert(entryPoint != null, "Invalid entry point");

            var local = emitter.DeclareLocal(typeof(byte*));
            var parameters = entryPoint.Parameters;

            // Compute the actual number of kernel arguments
            int numParameters = parameters.NumParameters;
            int parameterOffset = 0;
            if (!entryPoint.IsGroupedIndexEntry)
            {
                ++numParameters;
                ++parameterOffset;
            }

            // Emit a local argument pointer array that stores the native addresses
            // of all arguments
            emitter.EmitConstant(IntPtr.Size * numParameters);
            emitter.Emit(OpCodes.Conv_U);
            emitter.Emit(OpCodes.Localloc);

            // Store pointer in local variable
            emitter.Emit(LocalOperation.Store, local);

            // Store pointers to all mapped arguments
            var mappingHandler = new MappingHandler(local, parameterOffset);
            Map(emitter, mappingHandler, parameters);

            return local;
        }

        #endregion
    }
}
