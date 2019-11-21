// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
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

        /// <summary cref="ArgumentMapper.MapViewType(Type, Type)"/>
        protected sealed override Type MapViewType(Type viewType, Type elementType) =>
            ViewImplementation.GetImplementationType(elementType);

        /// <summary cref="ArgumentMapper.MapViewInstance{TILEmitter, TSource, TTarget}(in TILEmitter, TSource, TTarget)"/>
        protected sealed override void MapViewInstance<TILEmitter, TSource, TTarget>(
            in TILEmitter emitter,
            TSource source,
            TTarget target)
        {
            var targetType = target.TargetType;

            // Load target address
            target.EmitLoadTarget(emitter);

            // Load source and create custom view type
            source.EmitLoadSource(emitter);
            emitter.Emit(OpCodes.Ldobj, source.SourceType);
            emitter.EmitNewObject(
                ViewImplementation.GetViewConstructor(targetType));

            // Store target
            emitter.Emit(OpCodes.Stobj, target.TargetType);
        }
    }
}
