// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: LowerArrays.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Rewriting;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Lowers array values using memory operations or structure values.
    /// </summary>
    /// <remarks>
    /// This transformation converts array values into tuples of the following format:
    /// (StoragePtr, Dim1, Dim2, ..., DimN)
    /// </remarks>
    public sealed class LowerArrays : LowerTypes<ArrayType>
    {
        #region Type Lowering

        /// <summary>
        /// Lowers array types.
        /// </summary>
        private sealed class ArrayTypeLowering : TypeLowering<ArrayType>
        {
            public const int DimensionOffset = 1;

            public ArrayTypeLowering(Method.Builder builder)
                : base(builder)
            { }

            /// <summary>
            /// Returns the number of fields per array type.
            /// </summary>
            protected override int GetNumFields(ArrayType type) =>
                DimensionOffset + type.Dimensions;

            /// <summary>
            /// Converts the given array type into a structure with two elements.
            /// </summary>
            protected override TypeNode ConvertType<TTypeContext>(
                TTypeContext typeContext,
                ArrayType type)
            {
                var builder = typeContext.CreateStructureType(GetNumFields(type));

                // Append storage pointer
                builder.Add(
                    typeContext.CreatePointerType(
                        type.ElementType,
                        type.AddressSpace));

                // Append dimension types
                for (int i = 0, e = type.Dimensions; i < e; ++i)
                {
                    builder.Add(
                        typeContext.GetPrimitiveType(BasicValueType.Int32));
                }

                return builder.Seal();
            }

            /// <summary cref="TypeLowering{TType}.IsTypeDependent(TypeNode)"/>
            public override bool IsTypeDependent(TypeNode type) =>
                type.HasFlags(TypeFlags.ArrayDependent);
        }

        #endregion

        #region Rewriter Methods

        /// <summary>
        /// Lowers new array values.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ArrayType> typeLowering,
            ArrayValue value)
        {
            var builder = context.Builder;
            var location = value.Location;
            int currentPosition = builder.InsertPosition;

            // Allocate a memory array in the entry block
            var methodBuilder = builder.MethodBuilder;
            var entryBlock = methodBuilder[methodBuilder.EntryBlock];
            var arrayLength = entryBlock.ComputeArrayLength(
                location,
                value.Extent);
            entryBlock.SetupInsertPosition(value);
            var newArray = entryBlock.CreateStaticAllocaArray(
                location,
                arrayLength,
                value.ArrayType.ElementType,
                MemoryAddressSpace.Local);

            // Create resulting structure in current block
            builder.InsertPosition = currentPosition;
            var instance = builder.CreateDynamicStructure(
                location,
                typeLowering.GetNumFields(value.ArrayType));

            // Insert pointer field
            instance.Add(newArray);

            // Insert all dimensions
            for (int i = 0, e = value.ArrayType.Dimensions; i < e; ++i)
            {
                instance.Add(builder.CreateGetField(
                    location,
                    value.Extent,
                    new FieldSpan(i)));
            }

            var newStructure = instance.Seal();
            context.ReplaceAndRemove(value, newStructure);
        }

        /// <summary>
        /// Lowers array extent values.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ArrayType> typeLowering,
            GetArrayExtent value)
        {
            var builder = context.Builder;

            // Create new extent structure based on all dimension entries
            int dimensions = StructureType.GetNumFields(typeLowering[value]);
            var instance = builder.CreateDynamicStructure(value.Location, dimensions);

            // Insert all dimension values
            for (int i = 0; i < dimensions; ++i)
            {
                instance.Add(builder.CreateGetField(
                    value.Location,
                    value.ObjectValue,
                    new FieldSpan(i + ArrayTypeLowering.DimensionOffset)));
            }

            var newStructure = instance.Seal();
            context.ReplaceAndRemove(value, newStructure);
        }

        /// <summary>
        /// Computes a linear address for the given array and index.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static Value GetLinearAddress(
            RewriterContext context,
            Value array,
            Value index,
            out Value ptr)
        {
            var builder = context.Builder;
            ptr = builder.CreateGetField(
                array.Location,
                array,
                new FieldSpan(0));
            var extent = builder.CreateGetField(
                array.Location,
                array,
                new FieldSpan(1));
            return builder.ComputeArrayAddress(
                index.Location,
                index,
                extent,
                ArrayTypeLowering.DimensionOffset);
        }

        /// <summary>
        /// Lowers get element values.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ArrayType> _,
            GetArrayElement value)
        {
            var linearAddress = GetLinearAddress(
                context,
                value.ObjectValue,
                value.Index,
                out var ptr);

            var builder = context.Builder;
            var address = builder.CreateLoadElementAddress(
                value.Location,
                ptr,
                linearAddress);
            var newLoad = builder.CreateLoad(
                value.Location,
                address);
            context.ReplaceAndRemove(value, newLoad);
        }

        /// <summary>
        /// Lowers set element values.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ArrayType> _,
            SetArrayElement value)
        {
            var linearAddress = GetLinearAddress(
                context,
                value.ObjectValue,
                value.Index,
                out var ptr);

            var builder = context.Builder;
            var address = builder.CreateLoadElementAddress(
                value.Location,
                ptr,
                linearAddress);
            var newStore = builder.CreateStore(
                value.Location,
                address,
                value.Value);
            context.ReplaceAndRemove(value, newStore);
        }

        /// <summary>
        /// Lowers address-computation values.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ArrayType> _,
            LoadElementAddress value)
        {
            var linearAddress = GetLinearAddress(
                context,
                value.Source,
                value.Offset,
                out var ptr);

            var newLea = context.Builder.CreateLoadElementAddress(
                value.Location,
                ptr,
                linearAddress);
            context.ReplaceAndRemove(value, newLea);
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

            Rewriter.Add<ArrayValue>(Register, Lower);
            Rewriter.Add<GetArrayExtent>(Register, Lower);
            Rewriter.Add<GetArrayElement>(Register, Lower);
            Rewriter.Add<SetArrayElement>(Register, Lower);
            Rewriter.Add<LoadElementAddress>(
                (converter, value) =>
                    value.IsArrayAccesss && Register(converter, value),
                Lower);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new array lowering transformation.
        /// </summary>
        public LowerArrays() { }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new <see cref="ArrayTypeLowering"/> converter.
        /// </summary>
        protected override TypeLowering<ArrayType> CreateLoweringConverter(
            Method.Builder builder) =>
            new ArrayTypeLowering(builder);

        /// <summary>
        /// Applies the array lowering transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder) =>
            PerformTransformation(builder, Rewriter);

        #endregion
    }
}
