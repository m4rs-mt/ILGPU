// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: LowerArrays.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Analyses;
using ILGPU.IR.Construction;
using ILGPU.IR.Rewriting;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Immutable;
using System.Diagnostics;
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
        #region Nested Types

        /// <summary>
        /// Lowers array types.
        /// </summary>
        private sealed class ArrayTypeLowering : TypeLowering<ArrayType>
        {
            public const int DimensionOffset = 1;

            public ArrayTypeLowering(Method.Builder builder)
                : base(builder)
            { }

            /// <summary cref="TypeConverter{TType}.GetNumFields(TType)"/>
            protected override int GetNumFields(ArrayType type) =>
                DimensionOffset + type.Dimensions;

            /// <summary cref="TypeConverter{TType}.ConvertType{TTypeContext}(TTypeContext, TType)"/>
            protected override TypeNode ConvertType<TTypeContext>(
                TTypeContext typeContext,
                ArrayType type)
            {
                var fieldTypes = ImmutableArray.CreateBuilder<TypeNode>(GetNumFields(type));
                // Append storage pointer
                fieldTypes.Add(
                    typeContext.CreatePointerType(
                        type.ElementType,
                        type.AddressSpace));

                // Append dimension types
                for (int i = 0, e = type.Dimensions; i < e; ++i)
                    fieldTypes.Add(typeContext.GetPrimitiveType(BasicValueType.Int32));

                return typeContext.CreateStructureType(fieldTypes.MoveToImmutable());
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
            int currentPosition = builder.InsertPosition;

            // Allocate a memory array in the entry block
            var methodBuilder = builder.MethodBuilder;
            var entryBlock = methodBuilder[methodBuilder.EntryBlock];
            entryBlock.InsertPosition = 0;
            var arrayLength = entryBlock.ComputeArrayLength(value.Extent);
            var newArray = entryBlock.CreateAlloca(
                arrayLength,
                value.ArrayType.ElementType,
                MemoryAddressSpace.Local);

            // Create resulting structure in current block
            builder.InsertPosition = currentPosition;
            var fields = ImmutableArray.CreateBuilder<ValueReference>(
                typeLowering.GetNumFields(value.ArrayType));

            // Insert pointer field
            fields.Add(newArray);

            // Insert all dimensions
            for (int i = 0, e = value.ArrayType.Dimensions; i < e; ++i)
                fields.Add(builder.CreateGetField(value.Extent, new FieldSpan(i)));

            var newStructure = builder.CreateStructure(fields.MoveToImmutable());
            context.ReplaceAndRemove(value, newStructure);
        }

        /// <summary>
        /// Lowers array extent values.
        /// </summary>
        private static void Lower(
            RewriterContext context,
            TypeLowering<ArrayType> _,
            GetArrayExtent value)
        {
            var builder = context.Builder;

            // Create new extent structure based on all dimension entries
            int dimensions = value.ArrayType.Dimensions;
            var fields = ImmutableArray.CreateBuilder<ValueReference>(dimensions);

            // Insert all dimension values
            for (int i = 0, e = value.ArrayType.Dimensions; i < e; ++i)
                fields.Add(builder.CreateGetField(
                    value,
                    new FieldSpan(i + ArrayTypeLowering.DimensionOffset)));

            var newStructure = builder.CreateStructure(fields.MoveToImmutable());
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
            ptr = builder.CreateGetField(array, new FieldSpan(0));
            var extent = builder.CreateGetField(array, new FieldSpan(1));
            return builder.ComputeArrayAddress(
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
            var address = builder.CreateLoadElementAddress(ptr, linearAddress);
            var newLoad = builder.CreateLoad(address);
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
            var address = builder.CreateLoadElementAddress(ptr, linearAddress);
            var newStore = builder.CreateStore(address, value.Value);
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
            Debug.Assert(value.IsArrayAccesss, "Invalid array access");
            var linearAddress = GetLinearAddress(
                context,
                value.Source,
                value.ElementIndex,
                out var ptr);

            var newLea = context.Builder.CreateLoadElementAddress(ptr, linearAddress);
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
            Rewriter.Add<ArrayValue>(Lower);
            Rewriter.Add<GetArrayExtent>(Lower);
            Rewriter.Add<GetArrayElement>(Lower);
            Rewriter.Add<SetArrayElement>(Lower);
            Rewriter.Add<LoadElementAddress>((_, value) => value.IsArrayAccesss, Lower);
        }

        #endregion

        /// <summary>
        /// Constructs a new array lowering transformation.
        /// </summary>
        public LowerArrays() { }

        /// <summary cref="LowerTypes{TType}.CreateLoweringConverter(Method.Builder, Scope)"/>
        protected override TypeLowering<ArrayType> CreateLoweringConverter(
            Method.Builder builder,
            Scope _) => new ArrayTypeLowering(builder);

        /// <summary cref="UnorderedTransformation.PerformTransformation(Method.Builder)"/>
        protected override bool PerformTransformation(Method.Builder builder) =>
            PerformTransformation(builder, Rewriter);
    }
}
