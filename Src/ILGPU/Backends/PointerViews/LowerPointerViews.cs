// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: LowerPointerViews.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Rewriting;
using ILGPU.IR.Transformations;
using ILGPU.IR.Types;
using ILGPU.IR.Values;

namespace ILGPU.Backends.PointerViews
{
    /// <summary>
    /// Lowers view instances into pointer view implementations.
    /// </summary>
    public sealed class LowerPointerViews : LowerViews
    {
        #region Type Lowering

        /// <summary>
        /// Converts view types into pointer-based structure types.
        /// </summary>
        private sealed class PointerViewLowering : ViewTypeLowering
        {
            public PointerViewLowering(Method.Builder builder)
                : base(builder)
            { }

            /// <summary>
            /// Returns the number of fields per view type.
            /// </summary>
            protected override int GetNumFields(ViewType type) => 2;

            /// <summary>
            /// Converts the given view type into a structure with two elements.
            /// </summary>
            protected override TypeNode ConvertType<TTypeContext>(
                TTypeContext typeContext,
                ViewType type)
            {
                var builder = typeContext.CreateStructureType(2);
                builder.Add(typeContext.CreatePointerType(
                    type.ElementType,
                    type.AddressSpace));
                builder.Add(typeContext.GetPrimitiveType(
                    BasicValueType.Int64));
                return builder.Seal();
            }
        }

        #endregion

        #region Rewriter Methods

        /// <summary>
        /// Lowers a new view.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ViewType> _,
            NewView value)
        {
            var builder = context.Builder;
            var longLength = builder.CreateConvertToInt64(
                value.Location,
                value.Length);
            var viewInstance = builder.CreateDynamicStructure(
                value.Location,
                value.Pointer,
                longLength);
            context.ReplaceAndRemove(value, viewInstance);
        }

        /// <summary>
        /// Lowers get-view-length property.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ViewType> _,
            GetViewLength value)
        {
            var builder = context.Builder;
            var length = builder.CreateGetField(
                value.Location,
                value.View,
                new FieldSpan(1));

            // Convert to a 32bit length value
            if (value.Is32BitProperty)
            {
                length = builder.CreateConvertToInt32(
                    value.Location,
                    length);
            }

            context.ReplaceAndRemove(value, length);
        }

        /// <summary>
        /// Lowers a sub-view value.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ViewType> _,
            SubViewValue value)
        {
            var builder = context.Builder;
            var location = value.Location;
            var pointer = builder.CreateGetField(
                location,
                value.Source,
                new FieldSpan(0));
            var newPointer = builder.CreateLoadElementAddress(
                location,
                pointer,
                value.Offset);

            var length = value.Length;
            if (length.BasicValueType != BasicValueType.Int64)
            {
                length = builder.CreateConvertToInt64(
                    value.Location,
                    length);
            }

            var subView = builder.CreateDynamicStructure(
                location,
                newPointer,
                length);
            context.ReplaceAndRemove(value, subView);
        }

        /// <summary>
        /// Lowers an address-space cast.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ViewType> _,
            AddressSpaceCast value)
        {
            var builder = context.Builder;
            var location = value.Location;
            var pointer = builder.CreateGetField(
                location,
                value.Value,
                new FieldSpan(0));
            var length = builder.CreateGetField(
                location,
                value.Value,
                new FieldSpan(1));

            var newPointer = builder.CreateAddressSpaceCast(
                location,
                pointer,
                value.TargetAddressSpace);
            var newInstance = builder.CreateDynamicStructure(
                location,
                newPointer,
                length);
            context.ReplaceAndRemove(value, newInstance);
        }

        /// <summary>
        /// Lowers a view cast.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ViewType> typeLowering,
            ViewCast value)
        {
            var builder = context.Builder;
            var location = value.Location;
            var pointer = builder.CreateGetField(
                location,
                value.Value,
                new FieldSpan(0));
            var length = builder.CreateGetField(
                location,
                value.Value,
                new FieldSpan(1));

            // New pointer
            var newPointer = builder.CreatePointerCast(
                location,
                pointer,
                value.TargetElementType);

            // Compute new length:
            // newLength = length * sourceElementSize / targetElementSize;
            var sourceElementType = (typeLowering[value] as ViewType).ElementType;
            var sourceElementSize = builder.CreateLongSizeOf(
                location,
                sourceElementType);
            var targetElementSize = builder.CreateLongSizeOf(
                location,
                value.TargetElementType);
            var newLength = builder.CreateArithmetic(
                location,
                builder.CreateArithmetic(
                    location,
                    length,
                    sourceElementSize,
                    BinaryArithmeticKind.Mul),
                targetElementSize, BinaryArithmeticKind.Div);

            var newInstance = builder.CreateDynamicStructure(
                location,
                newPointer,
                newLength);
            context.ReplaceAndRemove(value, newInstance);
        }

        /// <summary>
        /// Lowers a lea operation.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ViewType> _,
            LoadElementAddress value)
        {
            var builder = context.Builder;
            var location = value.Location;
            var pointer = builder.CreateGetField(
                location,
                value.Source,
                new FieldSpan(0));
            var newLea = builder.CreateLoadElementAddress(
                location,
                pointer,
                value.Offset);
            context.ReplaceAndRemove(value, newLea);
        }

        /// <summary>
        /// Lowers an align-view-to operation.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ViewType> typeLowering,
            AlignViewTo value)
        {
            var builder = context.Builder;
            var location = value.Location;

            // Extract basic view information from the converted structure
            var pointer = builder.CreateGetField(
                location,
                value.View,
                new FieldSpan(0));
            var length = builder.CreateGetField(
                location,
                value.View,
                new FieldSpan(1));

            // Create IR code that represents the required operations to create aligned
            // views (see ArrayView.AlignToInternal for more information).
            var viewType = typeLowering[value].As<ViewType>(value);

            // long elementsToSkip = Min(
            //      AlignmentOffset(ptr, alignmentInBytes) / elementSize,
            //      Length);

            var elementsToSkip = builder.CreateArithmetic(
                location,
                builder.CreateAlignmentOffset(
                    location,
                    builder.CreatePointerAsIntCast(
                        location,
                        pointer,
                        BasicValueType.Int64),
                    value.AlignmentInBytes),
                builder.CreateSizeOf(location, viewType.ElementType),
                BinaryArithmeticKind.Div);

            elementsToSkip = builder.CreateArithmetic(
                location,
                elementsToSkip,
                length,
                BinaryArithmeticKind.Min);

            // Build the final result structure instance
            var resultBuilder = builder.CreateDynamicStructure(location);

            // Create the prefix view that starts at the original pointer offset and
            // includes elementsToSkip many elements.
            {
                resultBuilder.Add(pointer);
                resultBuilder.Add(elementsToSkip);
            }

            // Create the main view that starts at the original pointer offset +
            // elementsToSkip and has a length of remainingLength
            {
                var mainPointer = builder.CreateLoadElementAddress(
                    location,
                    pointer,
                    elementsToSkip);
                resultBuilder.Add(mainPointer);
                var remainingLength = builder.CreateArithmetic(
                    location,
                    length,
                    elementsToSkip,
                    BinaryArithmeticKind.Sub);
                resultBuilder.Add(remainingLength);
            }

            var result = resultBuilder.Seal();
            context.ReplaceAndRemove(value, result);
        }

        #endregion

        #region Rewriter

        /// <summary>
        /// The internal rewriter.
        /// </summary>
        private static readonly Rewriter<TypeLowering<ViewType>> Rewriter =
            new Rewriter<TypeLowering<ViewType>>();

        /// <summary>
        /// Initializes all rewriter patterns.
        /// </summary>
        static LowerPointerViews()
        {
            AddRewriters(
                Rewriter,
                Lower,
                Lower,
                Lower,
                Lower,
                Lower,
                Lower,
                Lower);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new pointer view lowering transformation.
        /// </summary>
        public LowerPointerViews() { }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new <see cref="PointerViewLowering"/> converter.
        /// </summary>
        protected override TypeLowering<ViewType> CreateLoweringConverter(
            Method.Builder builder) =>
            new PointerViewLowering(builder);

        /// <summary>
        /// Applies the pointer view lowering transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder) =>
            PerformTransformation(builder, Rewriter);

        #endregion
    }
}
