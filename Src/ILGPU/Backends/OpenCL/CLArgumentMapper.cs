// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: CLArgumentMapper.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.IL;
using ILGPU.Backends.SeparateViews;
using ILGPU.Runtime;
using ILGPU.Runtime.OpenCL;
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
            nameof(CLAPI.SetKernelArgumentUnsafeWithKernel),
            BindingFlags.Public | BindingFlags.Static);

        #endregion

        #region Nested Types

        /// <summary>
        /// Implements the actual argument mapping.
        /// </summary>
        private readonly struct MappingHandler : IMappingHandler
        {
            /// <summary>
            /// A source mapper.
            /// </summary>
            /// <typeparam name="TSource">The internal source type.</typeparam>
            private readonly struct MapperSource<TSource> : ISource
                where TSource : ISource
            {
                /// <summary>
                /// Constructs a new source mapper.
                /// </summary>
                /// <param name="source">The underlying source.</param>
                public MapperSource(TSource source)
                {
                    Source = source;
                }

                /// <summary>
                /// Returns the associated source.
                /// </summary>
                public TSource Source { get; }

                /// <summary cref="ArgumentMapper.ISource.SourceType"/>
                public Type SourceType => Source.SourceType;

                /// <summary cref="ArgumentMapper.ISource.EmitLoadSource{TILEmitter}(in TILEmitter)"/>
                public void EmitLoadSource<TILEmitter>(in TILEmitter emitter)
                    where TILEmitter : IILEmitter =>
                    Source.EmitLoadSource(emitter);
            }

            /// <summary>
            /// Constructs a new mapping handler.
            /// </summary>
            /// <param name="parent">The parent mapper.</param>
            /// <param name="kernelLocal">The local variable holding the associated kernel reference.</param>
            /// <param name="resultLocal">The local variable holding the result API status.</param>
            /// <param name="startIndex">The start argument index.</param>
            public MappingHandler(
                CLArgumentMapper parent,
                ILLocal kernelLocal,
                ILLocal resultLocal,
                int startIndex)
            {
                Parent = parent;
                KernelLocal = kernelLocal;
                ResultLocal = resultLocal;
                StartIndex = startIndex;
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

            /// <summary>
            /// Returns the start argument index.
            /// </summary>
            public int StartIndex { get; }

            /// <summary cref="ArgumentMapper.IMappingHandler.MapArgument{TILEmitter, TSource}(in TILEmitter, TSource, int)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void MapArgument<TILEmitter, TSource>(
                in TILEmitter emitter,
                TSource source,
                int argumentIndex)
                where TILEmitter : IILEmitter
                where TSource : ISource =>
                Parent.SetKernelArgument(
                    emitter,
                    KernelLocal,
                    ResultLocal,
                    StartIndex + argumentIndex,
                    new MapperSource<TSource>(source));
        }

        /// <summary>
        /// Implements the actual argument mapping.
        /// </summary>
        private readonly struct ViewMappingHandler : ISeparateViewMappingHandler
        {
            /// <summary>
            /// A source mapper.
            /// </summary>
            /// <typeparam name="TSource">The internal source type.</typeparam>
            private readonly struct MapperSource<TSource> : ISource
                where TSource : ISource
            {
                /// <summary>
                /// Constructs a new source mapper.
                /// </summary>
                /// <param name="source">The underlying source.</param>
                /// <param name="viewParameter">The view parameter.</param>
                public MapperSource(
                    TSource source,
                    in SeparateViewEntryPoint.ViewParameter viewParameter)
                {
                    Source = source;
                    Parameter = viewParameter;
                }

                /// <summary>
                /// Returns the associated source.
                /// </summary>
                public TSource Source { get; }

                /// <summary cref="ArgumentMapper.ISource.SourceType"/>
                public Type SourceType => typeof(IntPtr);

                /// <summary>
                /// The associated parameter.
                /// </summary>
                public SeparateViewEntryPoint.ViewParameter Parameter { get; }

                /// <summary cref="ArgumentMapper.ISource.EmitLoadSource{TILEmitter}(in TILEmitter)"/>
                public void EmitLoadSource<TILEmitter>(in TILEmitter emitter)
                    where TILEmitter : IILEmitter
                {
                    // Load source
                    Source.EmitLoadSource(emitter);

                    // Extract native pointer
                    emitter.EmitCall(
                        ViewImplementation.GetNativePtrMethod(Parameter.ElementType));

                    // Store the resolved pointer in a local variable in order to pass
                    // the reference to the local to the actual set-argument method.
                    var tempLocal = emitter.DeclareLocal(typeof(IntPtr));
                    emitter.Emit(LocalOperation.Store, tempLocal);
                    emitter.Emit(LocalOperation.LoadAddress, tempLocal);
                }
            }

            /// <summary>
            /// Constructs a new mapping handler.
            /// </summary>
            /// <param name="parent">The parent mapper.</param>
            /// <param name="kernelLocal">The local variable holding the associated kernel reference.</param>
            /// <param name="resultLocal">The local variable holding the result API status.</param>
            public ViewMappingHandler(
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

            /// <summary cref="ArgumentMapper.ISeparateViewMappingHandler.MapViewArgument{TILEmitter, TSource}(in TILEmitter, in TSource, in SeparateViewEntryPoint.ViewParameter, int)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void MapViewArgument<TILEmitter, TSource>(
                in TILEmitter emitter,
                in TSource source,
                in SeparateViewEntryPoint.ViewParameter viewParameter,
                int viewArgumentIndex)
                where TILEmitter : IILEmitter
                where TSource : ISource =>
                Parent.SetKernelArgument(
                    emitter,
                    KernelLocal,
                    ResultLocal,
                    viewArgumentIndex,
                    new MapperSource<TSource>(source, viewParameter));
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
        /// Emits code that sets an OpenCL kernel argument.
        /// </summary>
        /// <typeparam name="TILEmitter">The emitter type.</typeparam>
        /// <typeparam name="TSource">The value source type.</typeparam>
        /// <param name="emitter">The current emitter.</param>
        /// <param name="kernelLocal">The local variable holding the associated kernel reference.</param>
        /// <param name="resultLocal">The local variable holding the result API status.</param>
        /// <param name="argumentIndex">The argument index.</param>
        /// <param name="source">The value source.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void SetKernelArgument<TILEmitter, TSource>(
            in TILEmitter emitter,
            ILLocal kernelLocal,
            ILLocal resultLocal,
            int argumentIndex,
            in TSource source)
            where TILEmitter : IILEmitter
            where TSource : struct, ISource
        {
            // Load kernel reference
            emitter.Emit(LocalOperation.Load, kernelLocal);

            // Load target argument index
            emitter.EmitConstant(argumentIndex);

            // Load size of the argument value
            var size = GetSizeOf(source.SourceType);
            emitter.EmitConstant(size);

            // Load source address
            source.EmitLoadSource(emitter);

            // Set argument
            emitter.EmitCall(SetKernelArgumentMethod);

            // Merge API results
            emitter.Emit(LocalOperation.Load, resultLocal);
            emitter.Emit(OpCodes.Or);
            emitter.Emit(LocalOperation.Store, resultLocal);
        }

        /// <summary>
        /// Creates code that maps all parameters of the given entry point using
        /// OpenCL API calls.
        /// </summary>
        /// <typeparam name="TILEmitter">The emitter type.</typeparam>
        /// <param name="emitter">The target emitter to write to.</param>
        /// <param name="kernel">A local that holds the kernel driver reference.</param>
        /// <param name="entryPoint">The entry point.</param>
        public void Map<TILEmitter>(
            in TILEmitter emitter,
            ILLocal kernel,
            SeparateViewEntryPoint entryPoint)
            where TILEmitter : IILEmitter
        {
            Debug.Assert(entryPoint != null, "Invalid entry point");

            // Declare local
            var resultLocal = emitter.DeclareLocal(typeof(int));
            emitter.Emit(OpCodes.Ldc_I4_0);
            emitter.Emit(LocalOperation.Store, resultLocal);

            // Map all views
            var viewMappingHandler = new ViewMappingHandler(
                this,
                kernel,
                resultLocal);
            MapViews(emitter, viewMappingHandler, entryPoint);

            // Map implicit kernel length (if required)
            int parameterOffset = entryPoint.NumViewParameters;
            if (!entryPoint.IsGroupedIndexEntry)
            {
                var lengthSource = new ArgumentSource(
                    entryPoint.KernelIndexType,
                    Kernel.KernelParamDimensionIdx);
                SetKernelArgument(
                    emitter,
                    kernel,
                    resultLocal,
                    parameterOffset,
                    lengthSource);
                ++parameterOffset;
            }

            // Map all remaining arguments
            var mappingHandler = new MappingHandler(
                this,
                kernel,
                resultLocal,
                parameterOffset);
            Map(emitter, mappingHandler, entryPoint.Parameters);

            // Check mapping result
            emitter.Emit(LocalOperation.Load, resultLocal);
            emitter.EmitCall(CLAccelerator.ThrowIfFailedMethod);
        }

        #endregion
    }
}
