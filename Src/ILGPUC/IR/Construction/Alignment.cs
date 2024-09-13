
// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Alignment.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Creates a node that aligns the given view or pointer to a given number of
        /// bytes.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="viewOrPointer">The source view or pointer.</param>
        /// <param name="alignmentInBytes">The alignment in bytes.</param>
        /// <returns>The created node.</returns>
        public ValueReference CreateAlignTo(
            Location location,
            Value viewOrPointer,
            Value alignmentInBytes)
        {
            location.Assert(viewOrPointer.Type.IsViewOrPointerType);
            location.Assert(alignmentInBytes.Type.BasicValueType.IsInt());

            return Append(new AlignTo(
                GetInitializer(location),
                viewOrPointer,
                alignmentInBytes));
        }

        /// <summary>
        /// Creates a node that treats the given view or pointer as an aligned
        /// view/pointer that is aligned to a given number of bytes.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="viewOrPointer">The source view or pointer.</param>
        /// <param name="alignmentInBytes">The alignment in bytes.</param>
        /// <returns>The created node.</returns>
        public ValueReference CreateAsAligned(
            Location location,
            Value viewOrPointer,
            Value alignmentInBytes)
        {
            location.Assert(viewOrPointer.Type.IsViewOrPointerType);
            location.Assert(alignmentInBytes.Type.BasicValueType.IsInt());

            return Append(new AsAligned(
                GetInitializer(location),
                viewOrPointer,
                alignmentInBytes));
        }
    }
}
