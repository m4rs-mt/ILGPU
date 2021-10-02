// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXArgumentMapper.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.IL;
using ILGPU.Backends.PointerViews;
using ILGPU.Runtime;
using System;
using System.Diagnostics;

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
        private readonly struct MappingHandler : IStructMappingHandler<(ILLocal, int)>
        {
            /// <summary>
            /// Constructs a new mapping handler.
            /// </summary>
            /// <param name="entryPoint">The parent entry point.</param>
            public MappingHandler(EntryPoint entryPoint)
            {
                EntryPoint = entryPoint;
            }

            /// <summary>
            /// Returns the associated current entry point.
            /// </summary>
            public EntryPoint EntryPoint { get; }

            public bool CanMapKernelLength(out Type indexType)
            {
                indexType = EntryPoint.KernelIndexType;
                return EntryPoint.IsImplictlyGrouped;
            }

            public void MapKernelLength<TILEmitter, TTarget>(
                in TILEmitter emitter,
                in StructureTarget<TTarget> kernelLengthTarget)
                where TILEmitter : struct, IILEmitter
                where TTarget : struct, ITarget
            {
                Debug.Assert(EntryPoint.IsImplictlyGrouped);

                var argumentSource = new ArgumentSource(
                    kernelLengthTarget.TargetType,
                    Kernel.KernelParamDimensionIdx);
                kernelLengthTarget.EmitStoreTarget(emitter, argumentSource);
            }

            /// <summary>
            /// Maps a single PTX argument structure.
            /// </summary>
            public (ILLocal, int) MapArgumentStruct<TILEmitter>(
                in TILEmitter emitter,
                ILLocal local,
                int sizeInBytes)
                where TILEmitter : struct, IILEmitter => (local, sizeInBytes);
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
        /// Creates code that maps the given parameter specification to
        /// a compatible representation.
        /// </summary>
        /// <typeparam name="TILEmitter">The emitter type.</typeparam>
        /// <param name="emitter">The target emitter to write to.</param>
        /// <param name="entryPoint">The entry point.</param>
        /// <returns>A local that stores the native kernel argument pointers.</returns>
        public (ILLocal Local, int BufferSize) Map<TILEmitter>(
            in TILEmitter emitter,
            EntryPoint entryPoint)
            where TILEmitter : struct, IILEmitter
        {
            Debug.Assert(entryPoint != null, "Invalid entry point");

            // Map all arguments
            var mappingHandler = new MappingHandler(entryPoint);
            return MapArgumentsStruct<TILEmitter, MappingHandler, (ILLocal, int)>(
                emitter,
                mappingHandler,
                entryPoint.Parameters);
        }

        #endregion
    }
}
