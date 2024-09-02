// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: ILArgumentMapper.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.PointerViews;
using System.Diagnostics;
using System.IO;

namespace ILGPU.Backends.IL
{
    /// <summary>
    /// Constructs mappings for CPU kernels.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public sealed class ILArgumentMapper : ViewArgumentMapper
    {
        #region Nested Types

        /// <summary>
        /// Implements the actual argument mapping.
        /// </summary>
        private readonly struct MappingHandler : IMappingHandler
        {
            /// <summary>
            /// Emits code to set an individual argument.
            /// </summary>
            public readonly void MapArgument<TILEmitter, TSource>(
                in TILEmitter emitter,
                in TSource source,
                int argumentIndex)
                where TILEmitter : struct, IILEmitter
                where TSource : struct, ISource
            { }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new IL argument mapper.
        /// </summary>
        /// <param name="context">The current context.</param>
        public ILArgumentMapper(Context context)
            : base(context)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Creates code that maps the given parameter specification to
        /// a compatible representation.
        /// </summary>
        /// <param name="entryPoint">The entry point.</param>
        public void Map(EntryPoint entryPoint)
        {
            Debug.Assert(entryPoint != null, "Invalid entry point");

            // Map all arguments
            var mappingHandler = new MappingHandler();
            MapArguments(
                new NopILEmitter(),
                mappingHandler,
                entryPoint.Parameters);
        }

        #endregion
    }
}
