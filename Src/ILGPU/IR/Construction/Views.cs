// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Views.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Diagnostics;

namespace ILGPU.IR.Construction
{
    partial class IRBuilder
    {
        /// <summary>
        /// Constructs a new view from a pointer and a length.
        /// </summary>
        /// <param name="pointer">The source pointer.</param>
        /// <param name="length">The length.</param>
        /// <returns>A node that represents the created view.</returns>
        public ValueReference CreateNewView(
            Value pointer,
            Value length)
        {
            Debug.Assert(pointer != null, "Invalid pointer node");
            Debug.Assert(length != null, "Invalid length node");

            var pointerType = pointer.Type as PointerType;
            Debug.Assert(pointerType != null, "Invalid pointer type");
            Debug.Assert(length.BasicValueType == BasicValueType.Int32, "Invalid length type");

            var viewType = CreateViewType(pointerType.ElementType, pointerType.AddressSpace);
            return CreateUnifiedValue(new NewView(
                Generation,
                pointer,
                length,
                viewType));
        }

        /// <summary>
        /// Creates a node that resolves the length of the given view.
        /// </summary>
        /// <param name="view">The source view.</param>
        /// <returns>The created node.</returns>
        public ValueReference CreateGetViewLength(Value view)
        {
            Debug.Assert(view != null, "Invalid view node");
            Debug.Assert(view.Type.IsViewType, "Invalid view type");

            return CreateUnifiedValue(new GetViewLength(
                Generation,
                view,
                CreatePrimitiveType(BasicValueType.Int32)));
        }
    }
}
