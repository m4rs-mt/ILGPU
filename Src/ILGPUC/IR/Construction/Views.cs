// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Views.cs
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

            // Fold trivial cases
            if (view is NewView newView)
            {
                return CreateConvert(
                    location,
                    newView.Length,
                    lengthType);
            }

            return Append(new GetViewLength(
                GetInitializer(location),
                view,
                lengthType));
        }

        /// <summary>
        /// Creates a node that gets the stride of an intrinsic array view.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <returns>The created node.</returns>
        internal ValueReference CreateGetViewStride(Location location)
        {
            var denseType = CreateType(typeof(Stride1D.Dense));
            return CreateNull(location, denseType);
        }
    }
}
