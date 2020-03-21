// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: ViewArgumentMapper.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using System;
using System.Reflection.Emit;

namespace ILGPU.Backends.PointerViews
{
    /// <summary>
    /// Maps array views to pointer implementations.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public abstract class ViewArgumentMapper : ArgumentMapper
    {
        #region Instance

        /// <summary>
        /// Constructs a new view argument mapper.
        /// </summary>
        /// <param name="context">The current context.</param>
        protected ViewArgumentMapper(Context context)
            : base(context)
        { }

        #endregion

        #region Methods

        /// <summary>
        /// Maps an internal view type to a pointer implementation type.
        /// </summary>
        protected sealed override void MapViewType<TTargetCollection>(
            Type viewType,
            Type elementType,
            TTargetCollection elements) =>
            ViewImplementation.AppendImplementationTypes(elements);

        /// <summary>
        /// Maps an internal view instance to a pointer instance.
        /// </summary>
        protected sealed override void MapViewInstance<TILEmitter, TSource>(
            in TILEmitter emitter,
            Type elementType,
            TSource source,
            ref Target target)
        {
            // Emit the target address and resolve the implementation type to store
            var implType = ViewImplementation.GetImplementationType(elementType);
            target.EmitLoadTarget(emitter);

            // Load source and create custom view type
            source.EmitLoadSource(emitter);
            emitter.Emit(OpCodes.Ldobj, source.SourceType);
            emitter.EmitNewObject(
                ViewImplementation.GetViewConstructor(implType));

            // Store object
            emitter.Emit(OpCodes.Stobj, implType);
            target.NextTarget();
        }

        #endregion
    }
}
