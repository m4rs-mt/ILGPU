// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: SpecializeViews.PointerViewImpl.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Backends.IL;
using ILGPU.Backends.PTX;
using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Represents a transformation specializer that lowers all view nodes to pointer-based implementations.
    /// This is especially useful in the <see cref="ILBackend"/> and the <see cref="PTXBackend"/>.
    /// These backends rely on this transformation in order to lower the abstract concept of a view
    /// to a primitive that can be mapped to assembly instructions.
    /// </summary>
    public readonly struct PointerViewImplSpecializer : IViewSpecializer
    {
        #region Constants

        /// <summary>
        /// Represents the element index of the pointer information.
        /// </summary>
        public const int PointerViewImplPointerIndex = 0;

        /// <summary>
        /// Represents the element index of the length information.
        /// </summary>
        public const int PointerViewImplLengthIndex = 1;

        /// <summary>
        /// The internal field names of the pointer structure.
        /// </summary>
        private static readonly ImmutableArray<string> FieldNames = ImmutableArray.Create(
            "pointer",
            "length");

        #endregion

        #region Static

        /// <summary>
        /// Creates a new view specializer.
        /// </summary>
        /// <param name="abi">The required ABI specification.</param>
        /// <returns>The created specializer.</returns>
        public static PointerViewImplSpecializer Create(ABI abi) =>
            new PointerViewImplSpecializer(abi);

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new pointer-view implementation specializer.
        /// </summary>
        /// <param name="abi">The required ABI specification.</param>
        public PointerViewImplSpecializer(ABI abi)
        {
            ABI = abi ?? throw new ArgumentNullException(nameof(abi));
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated ABI specification.
        /// </summary>
        public ABI ABI { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new view type.
        /// </summary>
        /// <param name="builder">The current builder.</param>
        /// <param name="elementType">The view element type.</param>
        /// <param name="addressSpace">The view address space.</param>
        /// <returns>The view type.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode CreateViewType(
            IRBuilder builder,
            TypeNode elementType,
            MemoryAddressSpace addressSpace)
        {
            // Construct a new structure type consisting of a pointer
            // and an address space
            var elementPointer = builder.CreatePointerType(
                elementType,
                addressSpace);
            return builder.CreateStructureType(
                ImmutableArray.Create<TypeNode>(
                    elementPointer,
                    builder.CreatePrimitiveType(BasicValueType.Int32)),
                FieldNames,
                null);
        }

        /// <summary cref="IViewSpecializer.CreateImplementationType{TContext}(in TContext, TypeNode, MemoryAddressSpace)"/>
        TypeNode IViewSpecializer.CreateImplementationType<TContext>(
            in TContext context,
            TypeNode elementType,
            MemoryAddressSpace addressSpace) =>
            CreateViewType(context.Builder, elementType, addressSpace);

        /// <summary>
        /// Creates a new view.
        /// </summary>
        /// <param name="builder">The current builder.</param>
        /// <param name="pointer">The base pointer.</param>
        /// <param name="length">The view length.</param>
        /// <returns>The created view.</returns>
        private static Value CreateView(IRBuilder builder, Value pointer, Value length)
        {
            var pointerType = pointer.Type as PointerType;
            var implType = CreateViewType(
                builder,
                pointerType.ElementType,
                pointerType.AddressSpace);

            var viewValue = builder.CreateNull(implType);
            viewValue = builder.CreateSetField(
                viewValue,
                PointerViewImplPointerIndex,
                pointer);
            viewValue = builder.CreateSetField(
                viewValue,
                PointerViewImplLengthIndex,
                length);
            return viewValue;
        }

        /// <summary cref="IViewSpecializer.Specialize{TContext}(in TContext, NewView)"/>
        Value IViewSpecializer.Specialize<TContext>(
            in TContext context,
            NewView newView) =>
            CreateView(context.Builder, newView.Pointer, newView.Length);

        /// <summary cref="IViewSpecializer.Specialize{TContext}(in TContext, GetViewLength)"/>
        Value IViewSpecializer.Specialize<TContext>(
            in TContext context,
            GetViewLength getViewLength) =>
            context.Builder.CreateGetField(getViewLength.View, PointerViewImplLengthIndex);

        /// <summary cref="IViewSpecializer.Specialize{TContext}(in TContext, SubViewValue)"/>
        Value IViewSpecializer.Specialize<TContext>(
            in TContext context,
            SubViewValue subViewValue)
        {
            var builder = context.Builder;

            var pointer = builder.CreateGetField(
                subViewValue.Source,
                PointerViewImplPointerIndex);
            var address = builder.CreateLoadElementAddress(
                pointer,
                subViewValue.Offset);

            var length = subViewValue.Length;
            return CreateView(context.Builder, address, length);
        }

        /// <summary cref="IViewSpecializer.Specialize{TContext}(in TContext, LoadElementAddress)"/>
        Value IViewSpecializer.Specialize<TContext>(
            in TContext context,
            LoadElementAddress viewElementAddress)
        {
            var builder = context.Builder;

            if (viewElementAddress.IsPointerAccess)
                return null;

            var pointer = builder.CreateGetField(
                viewElementAddress.Source,
                PointerViewImplPointerIndex);

            var address = builder.CreateLoadElementAddress(
                pointer,
                viewElementAddress.ElementIndex);

            return address;
        }

        /// <summary cref="IViewSpecializer.Specialize{TContext}(in TContext, AddressSpaceCast)"/>
        Value IViewSpecializer.Specialize<TContext>(
            in TContext context,
            AddressSpaceCast addressSpaceCast)
        {
            var builder = context.Builder;

            if (addressSpaceCast.IsPointerCast)
            {
                return builder.CreateAddressSpaceCast(
                    addressSpaceCast.Value,
                    addressSpaceCast.TargetAddressSpace);
            }
            else
            {
                var pointer = builder.CreateGetField(
                    addressSpaceCast.Value,
                    PointerViewImplPointerIndex);
                var length = builder.CreateGetField(
                    addressSpaceCast.Value,
                    PointerViewImplLengthIndex);

                var cast = builder.CreateAddressSpaceCast(
                    pointer,
                    addressSpaceCast.TargetAddressSpace);

                return CreateView(builder, cast, length);
            }
        }

        /// <summary cref="IViewSpecializer.Specialize{TContext}(in TContext, ViewCast)"/>
        Value IViewSpecializer.Specialize<TContext>(
            in TContext context,
            ViewCast viewCast)
        {
            var builder = context.Builder;

            var pointer = builder.CreateGetField(
                viewCast.Value,
                PointerViewImplPointerIndex);
            var length = builder.CreateGetField(
                viewCast.Value,
                PointerViewImplLengthIndex);

            var sourceElementType = viewCast.SourceElementType;
            var targetElementType = viewCast.TargetElementType;

            var sourceElementSize = ABI.GetSizeOf(sourceElementType);
            var targetElementSize = ABI.GetSizeOf(targetElementType);

            pointer = builder.CreatePointerCast(
                pointer,
                targetElementType);

            var sourceExtent = builder.CreateArithmetic(
                length,
                builder.CreatePrimitiveValue(sourceElementSize),
                BinaryArithmeticKind.Mul,
                ArithmeticFlags.Unsigned);

            var newLength = builder.CreateArithmetic(
                sourceExtent,
                builder.CreatePrimitiveValue(targetElementSize),
                BinaryArithmeticKind.Div,
                ArithmeticFlags.Unsigned);

            return CreateView(builder, pointer, newLength);
        }

        #endregion
    }
}
