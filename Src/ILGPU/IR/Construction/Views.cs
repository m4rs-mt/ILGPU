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

        /// <summary>
        /// Creates a value that represents the alignment offset in bytes for the given
        /// raw pointer value as integer (see
        /// <see cref="Interop.ComputeAlignmentOffset(long, int)"/> for more information.
        /// </summary>
        /// <param name="location">The current location.</param>
        /// <param name="intPtr">The raw integer pointer value.</param>
        /// <param name="alignmentInBytes">The alignment in bytes.</param>
        /// <returns>The created node.</returns>
        public ValueReference CreateAlignmentOffset(
            Location location,
            Value intPtr,
            Value alignmentInBytes)
        {
            // TODO: this is a software implementation that should be (?) implemented
            // via a built-in IR value. This value could be lowered to the
            // Interop.ComputeAlignmentOffset method in the future to avoid explicit
            // code generation at this point.

            // var baseOffset = (int)ptr & (alignmentInBytes - 1);
            var baseOffset = CreateArithmetic(
                location,
                CreateConvertToInt32(location, intPtr),
                CreateArithmetic(
                    location,
                    alignmentInBytes,
                    CreatePrimitiveValue(location, 1),
                    BinaryArithmeticKind.Sub),
                BinaryArithmeticKind.And);

            // offset = alignmentInBytes - baseOffset;
            var offset = CreateArithmetic(
                location,
                alignmentInBytes,
                baseOffset,
                BinaryArithmeticKind.Sub);

            // (long)(baseOffset == 0 ? baseOffset : offset)
            var zero = CreatePrimitiveValue(location, 0);
            return CreateConvertToInt64(
                location,
                CreatePredicate(
                    location,
                    CreateCompare(
                        location,
                        baseOffset,
                        zero,
                        CompareKind.Equal),
                    zero,
                    offset));
        }
    }
}
