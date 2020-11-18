// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Views.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Util;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Constructs a new view from a pointer and a length.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="pointer">The source pointer.</param>
        /// <param name="length">The length.</param>
        /// <returns>A node that represents the created view.</returns>
        public ValueReference CreateNewView(
            Location location,
            Value pointer,
            Value length)
        {
            location.Assert(
                IRTypeContext.IsViewIndexType(length.BasicValueType));

            return Append(new NewView(
                GetInitializer(location),
                pointer,
                length));
        }

        /// <summary>
        /// Creates a node that resolves the length of the given view.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="view">The source view.</param>
        /// <returns>The created node.</returns>
        public ValueReference CreateGetViewLength(
            Location location,
            Value view) =>
            CreateGetViewLength(location, view, BasicValueType.Int32);

        /// <summary>
        /// Creates a node that resolves the length of the given view.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="view">The source view.</param>
        /// <returns>The created node.</returns>
        public ValueReference CreateGetViewLongLength(
            Location location,
            Value view) =>
            CreateGetViewLength(location, view, BasicValueType.Int64);

        /// <summary>
        /// Creates a node that resolves the length of the given view.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="view">The source view.</param>
        /// <param name="lengthType">The length type.</param>
        /// <returns>The created node.</returns>
        internal ValueReference CreateGetViewLength(
            Location location,
            Value view,
            BasicValueType lengthType)
        {
            location.Assert(view.Type.IsViewType);

            return Append(new GetViewLength(
                GetInitializer(location),
                view,
                lengthType));
        }

        /// <summary>
        /// Creates a node that aligns the given view to a given number of bytes.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="view">The source view.</param>
        /// <param name="alignmentInBytes">The alignment in bytes.</param>
        /// <returns>The created node.</returns>
        public ValueReference CreateAlignViewTo(
            Location location,
            Value view,
            Value alignmentInBytes)
        {
            location.Assert(alignmentInBytes.Type.BasicValueType.IsInt());

            return Append(new AlignViewTo(
                GetInitializer(location),
                view,
                alignmentInBytes));
        }
    }
}
