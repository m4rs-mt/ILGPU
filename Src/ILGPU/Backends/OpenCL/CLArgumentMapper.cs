// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: CLArgumentMapper.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.Backends.PointerViews;
using ILGPU.Runtime.OpenCL.API;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace ILGPU.Backends.OpenCL
{
    /// <summary>
    /// Constructs mappings for CL kernels.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed class CLArgumentMapper : ViewArgumentMapper
    {
        #region Static

        /// <summary>
        /// The method to set OpenCL kernel arguments.
        /// </summary>
        private static readonly MethodInfo SetKernelArgumentMethod = typeof(CLAPI).GetMethod(
            nameof(CLAPI.SetKernelArgumentUnsafe),
            BindingFlags.Public | BindingFlags.Static);

        #endregion

        #region Nested Types

        /// <summary>
        /// Implements the actual argument mapping.
        /// </summary>
        private readonly struct MappingHandler : IMappingHandler
        {
            /// <summary>
            /// Constructs a new mapping handler.
            /// </summary>
            /// <param name="parent">The parent mapper.</param>
            /// <param name="kernelLocal">The local variable holding the associated kernel reference.</param>
            /// <param name="resultLocal">The local variable holding the result API status.</param>
            public MappingHandler(
                CLArgumentMapper parent,
                ILLocal kernelLocal,
                ILLocal resultLocal)
            {
                Parent = parent;
                KernelLocal = kernelLocal;
                ResultLocal = resultLocal;
            }

            /// <summary>
            /// Returns the underlying ABI.
            /// </summary>
            public CLArgumentMapper Parent { get; }

            /// <summary>
            /// Returns the associated kernel local.
            /// </summary>
            public ILLocal KernelLocal { get; }

            /// <summary>
            /// Returns the associated result variable which is
            /// used to accumulate all intermediate method return values.
            /// </summary>
            public ILLocal ResultLocal { get; }

            /// <summary cref="ArgumentMapper.IMappingHandler.MapArgument{TILEmitter, TSource}(in TILEmitter, TSource, int)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void MapArgument<TILEmitter, TSource>(
                in TILEmitter emitter,
                TSource source,
                int argumentIndex)
                where TILEmitter : IILEmitter

                where TSource : ISource
            {
                // Load kernel reference
                emitter.Emit(LocalOperation.Load, KernelLocal);

                // Load target argument index
                emitter.EmitConstant(argumentIndex);

                // Load size of the argument value
                var size = Parent.GetSizeOf(source.SourceType);
                emitter.EmitConstant(size);

                // Load source address
                source.EmitLoadSource(emitter);

                // Set argument
                emitter.EmitCall(SetKernelArgumentMethod);

                // Merge API results
                emitter.Emit(LocalOperation.Load, ResultLocal);
                emitter.Emit(OpCodes.Or);
                emitter.Emit(LocalOperation.Store, ResultLocal);
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new OpenCL argument mapper.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="abi">The current ABI.</param>
        public CLArgumentMapper(Context context, ABI abi)
            : base(context)
        {
            ABI = abi ?? throw new ArgumentNullException(nameof(abi));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated OpenCL ABI.
        /// </summary>
        public ABI ABI { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Returns the ABI size of the given managed type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The interop size in bytes.</returns>
        private int GetSizeOf(Type type)
        {
            var typeNode = Context.TypeContext.CreateType(type);
            return ABI.GetSizeOf(typeNode);
        }

        /// <summary>
        /// Creates code that maps all parameters of the given entry point using
        /// OpenCL API calls.
        /// </summary>
        /// <typeparam name="TILEmitter">The emitter type.</typeparam>
        /// <param name="emitter">The target emitter to write to.</param>
        /// <param name="kernel">A local that holds the kernel driver reference.</param>
        /// <param name="entryPoint">The entry point.</param>
        public void Map<TILEmitter>(in TILEmitter emitter, ILLocal kernel, EntryPoint entryPoint)
            where TILEmitter : IILEmitter
        {
            Debug.Assert(entryPoint != null, "Invalid entry point");

            // Declare local
            var resultLocal = emitter.DeclareLocal(typeof(int));
            emitter.Emit(OpCodes.Ldc_I4_0);
            emitter.Emit(LocalOperation.Store, resultLocal);

            var mappingHandler = new MappingHandler(this, kernel, resultLocal);
            Map(emitter, mappingHandler, entryPoint.Parameters);
        }

        #endregion
    }
}
