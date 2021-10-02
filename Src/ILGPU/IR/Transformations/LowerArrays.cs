// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: LowerArrays.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Rewriting;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using System.Diagnostics.CodeAnalysis;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Converts array values into structure values and allocations.
    /// </summary>
    public sealed class LowerArrays : LowerTypes<ArrayType>
    {
        #region Nested Types

        private sealed class ArrayTypeLowering : TypeLowering<ArrayType>
        {
            /// <summary>
            /// Constructs a new array type lowering.
            /// </summary>
            /// <param name="builder">The parent builder.</param>
            /// <param name="targetAddressSpace">The target address spacce.</param>
            public ArrayTypeLowering(
                Method.Builder builder,
                MemoryAddressSpace targetAddressSpace)
                : base(builder)
            {
                TargetAddressSpace = targetAddressSpace;
            }

            /// <summary>
            /// Returns the target address space for all array values.
            /// </summary>
            public MemoryAddressSpace TargetAddressSpace { get; }

            /// <summary>
            /// Returns true if the given type depends on an array type.
            /// </summary>
            public override bool IsTypeDependent(TypeNode type) =>
                type.HasFlags(TypeFlags.ArrayDependent);

            /// <summary>
            /// Converts the array type into a structure of dimensions + 1 elements.
            /// </summary>
            protected override TypeNode ConvertType<TTypeContext>(
                TTypeContext typeContext,
                ArrayType type)
            {
                // Create the structure type builder and initialize the base view.
                var builder = typeContext.CreateStructureType(GetNumFields(type));
                builder.Add(typeContext.CreateViewType(
                    type.ElementType,
                    TargetAddressSpace));

                // Initialize all 32bit dimensions.
                var lengthType = typeContext.GetPrimitiveType(BasicValueType.Int32);
                for (int i = 0, e = type.NumDimensions; i < e; ++i)
                    builder.Add(lengthType);
                return builder.Seal();
            }

            /// <summary>
            /// Returns the number of dimensions of the given array type + 1.
            /// </summary>
            protected override int GetNumFields(ArrayType type) =>
                type.NumDimensions + 1;
        }

        #endregion

        #region Rewriter Helper Methods

        /// <summary>
        /// Gets the view from the given array value.
        /// </summary>
        /// <returns>A reference to the array view.</returns>
        private static Value GetViewFromArray(
            IRBuilder builder,
            Location location,
            Value array) =>
            builder.CreateGetField(
                location,
                array,
                new FieldSpan(0, 1));

        #endregion

        #region Rewriter Methods

        /// <summary>
        /// Lowers new array values into static allocation instances and structures.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ArrayType> typeLowering,
            NewArray value)
        {
            var builder = context.Builder;
            var location = value.Location;
            var targetAddressSpace = (typeLowering as ArrayTypeLowering)
                .TargetAddressSpace;

            // Compute array length
            Value totalLength = builder.CreatePrimitiveValue(location, 1);
            foreach (Value length in value)
            {
                totalLength = builder.CreateArithmetic(
                    location,
                    totalLength,
                    length,
                    BinaryArithmeticKind.Mul);
            }

            // Check for a compile-time known constant
            if (!(totalLength is PrimitiveValue primitiveLength) ||
                primitiveLength.Int32Value < 0)
            {
                throw location.GetNotSupportedException(
                    ErrorMessages.NotSupportNonConstArrayDimension,
                    totalLength.ToString());
            }

            // Create array structure.
            var arrayType = typeLowering
                .ConvertType(value.Type)
                .As<StructureType>(location);
            var result = builder.CreateStructure(location, arrayType);
            Value view;

            // Check for empty array allocations
            if (primitiveLength.HasIntValue(0))
            {
                // Create an empty view.
                view = builder.CreateNewView(
                    location,
                    builder.CreateNull(
                        location,
                        builder.CreatePointerType(
                            value.ElementType,
                            targetAddressSpace)),
                    primitiveLength);
                result.Add(view);

                // Insert a dimension length of 0 in each dimension
                for (int i = 0, e = value.Count; i < e; ++i)
                    result.Add(primitiveLength);
            }
            else
            {
                // Create the allocation and the base view
                var allocation = builder.CreateStaticAllocaArray(
                    location,
                    totalLength,
                    value.ElementType,
                    targetAddressSpace);
                view = builder.CreateNewView(location, allocation, totalLength);
                result.Add(view);

                // Insert dimension information for each array rank
                foreach (Value length in value)
                    result.Add(length);
            }

            // Clear all array elements using explicit stores. We emit the code in
            // the form of an unrolled loop to avoid additional loops from being
            // generated.
            // TODO: replace this loop with a high-level memset operation later on
            var defaultElement = builder.CreateNull(location, value.Type.ElementType);
            for (int i = 0, e = primitiveLength.Int32Value; i < e; ++i)
            {
                var address = builder.CreateLoadElementAddress(
                    location,
                    view,
                    builder.CreatePrimitiveValue(location, i));
                builder.CreateStore(location, address, defaultElement);
            }

            // Replace the current value with a new structure instance
            context.ReplaceAndRemove(value, result.Seal());
        }

        /// <summary>
        /// Lowers leae nodes to linear <see cref="LoadElementAddress"/> values.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ArrayType> typeLowering,
            LoadArrayElementAddress value)
        {
            var builder = context.Builder;
            var location = value.Location;
            Value array = value.ArrayValue;

            // Get and validate the array type
            var structureType = array.Type.As<StructureType>(location);

            // Compute linear address based on the .Net array layouts
            Value elementIndex = builder.CreatePrimitiveValue(location, 0L);

            // (((0 * Width + x) * Height) + y) * Depth + z...
            for (int i = 0, e = structureType.NumFields - 1; i < e; ++i)
            {
                Value length = builder.CreateGetField(
                    location,
                    array,
                    new FieldAccess(1 + i));

                // Create a debug assertion to check for out-of-bounds accesses
                builder.CreateDebugAssert(
                    location,
                    builder.CreateCompare(
                        location,
                        value.Dimensions[i],
                        length,
                        CompareKind.LessThan),
                    builder.CreatePrimitiveValue(
                        location,
                        $"{i}-th array index out of range"));

                // Update index computation
                elementIndex = builder.CreateArithmetic(
                    location,
                    elementIndex,
                    length,
                    BinaryArithmeticKind.Mul);
                elementIndex = builder.CreateArithmetic(
                    location,
                    elementIndex,
                    value.Dimensions[i],
                    BinaryArithmeticKind.Add);
            }

            // Extract the actual view field from the structure and compute the
            // appropriate target element address.
            var view = GetViewFromArray(builder, location, array);
            var lea = builder.CreateLoadElementAddress(
                location,
                view,
                elementIndex);
            context.ReplaceAndRemove(value, lea);
        }


        /// <summary>
        /// Lower array length values to <see cref="GetViewLength"/> values.
        /// </summary>
        [SuppressMessage(
            "Maintainability",
            "CA1508:Avoid dead conditional code",
            Justification = "The dimension check is not dead")]
        private static void Lower(
            RewriterContext context,
            TypeLowering<ArrayType> typeLowering,
            GetArrayLength value)
        {
            var builder = context.Builder;
            var location = value.Location;

            Value length;
            if (value.IsFullLength)
            {
                // Extract the view from the underlying array-implementation structure
                var view = GetViewFromArray(builder, location, value.ArrayValue);
                length = builder.CreateGetViewLength(location, view);
            }
            else
            {
                // Check whether the dimension is a compile-time known constant
                Value dimension = value.Dimension;
                if (!(dimension is PrimitiveValue primitiveValue))
                {
                    throw location.GetNotSupportedException(
                        ErrorMessages.NotSupportNonConstArrayDimension,
                        dimension.ToString());
                }

                // Check whether the field index is out of range
                var structType = value.ArrayValue.Type.As<StructureType>(location);
                int index = primitiveValue.Int32Value;
                if (index < 0 || index >= structType.NumFields - 1)
                {
                    throw location.GetNotSupportedException(
                        ErrorMessages.NotSupportNonConstArrayDimension,
                        index.ToString());
                }

                // BaseView + dimension offset
                length = builder.CreateGetField(
                    location,
                    value.ArrayValue,
                    new FieldAccess(1 + index));
            }

            // Replace the actual length information
            context.ReplaceAndRemove(value, length);
        }

        /// <summary>
        /// Lower array to view casts to direct references to the underyling view.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ArrayType> typeLowering,
            ArrayToViewCast value)
        {
            var builder = context.Builder;
            var location = value.Location;

            // Get the view from the array implementation structure
            Value array = value.Value;
            var view = GetViewFromArray(builder, location, array);

            // Cast this view into the generic address space to match all invariants
            view = builder.CreateAddressSpaceCast(
                location,
                view,
                MemoryAddressSpace.Generic);

            // Replace the current cast with the actual view pointing to the elements
            context.ReplaceAndRemove(value, view);
        }

        #endregion

        #region Rewriter

        /// <summary>
        /// The internal rewriter.
        /// </summary>
        private static readonly Rewriter<TypeLowering<ArrayType>> Rewriter =
            new Rewriter<TypeLowering<ArrayType>>();

        /// <summary>
        /// Initializes all rewriter patterns.
        /// </summary>
        static LowerArrays()
        {
            AddRewriters(Rewriter);

            Rewriter.Add<NewArray>(Register, Lower);
            Rewriter.Add<LoadArrayElementAddress>(Register, Lower);
            Rewriter.Add<GetArrayLength>(Register, Lower);
            Rewriter.Add<ArrayToViewCast>(Register, Lower);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new array type lowering transformation.
        /// </summary>
        /// <param name="targetAddressSpace">
        /// The target address space for all array values.
        /// </param>
        public LowerArrays(MemoryAddressSpace targetAddressSpace)
        {
            TargetAddressSpace = targetAddressSpace;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the target address space for all array values.
        /// </summary>
        public MemoryAddressSpace TargetAddressSpace { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new <see cref="ArrayTypeLowering"/> type converter to convert
        /// internal array types to low-level structure values.
        /// </summary>
        protected override TypeLowering<ArrayType> CreateLoweringConverter(
            Method.Builder builder) =>
            new ArrayTypeLowering(builder, TargetAddressSpace);

        /// <summary>
        /// Applies the array type lowering transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder) =>
            PerformTransformation(builder, Rewriter);

        #endregion
    }
}
