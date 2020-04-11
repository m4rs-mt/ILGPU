// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ViewImplementationABI.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using System.Collections.Immutable;

namespace ILGPU.Backends.PointerViews
{
    /// <summary>
    /// Represents an ABI that uses the <see cref="ViewImplementation{T}"/> as
    /// native view representation.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public class ViewImplementationABI : ABI
    {
        #region Instance

        /// <summary>
        /// Constructs a new ABI instance.
        /// </summary>
        /// <param name="typeContext">The current type context.</param>
        /// <param name="targetPlatform">The target platform</param>
        public ViewImplementationABI(
            IRTypeContext typeContext,
            TargetPlatform targetPlatform)
            : base(typeContext, targetPlatform,
                  pointerSize => new ABITypeInfo(
                      ImmutableArray.Create(0, pointerSize),
                      pointerSize,
                      2 * pointerSize))
        { }

        #endregion
    }
}
