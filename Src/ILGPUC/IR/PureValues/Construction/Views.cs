// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Views.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Util;
using ILGPUC.IR.ModuleValues;
using ILGPUC.Util;
using System.Reflection.Emit;

namespace ILGPUC.IR.PureValues.Construction;

partial class PureValueBuilder
{
    /// <summary>
    /// Constructs a new view from a pointer and a length.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="pointer">The source pointer.</param>
    /// <param name="length">The length.</param>
    /// <returns>A node that represents the created view.</returns>
    public Value? CreateNewView(Location location, Value? pointer, Value? length)
    {
        if (pointer is null || length is null) return null;

        location.Assert(length.BasicValueType.IsViewIndexType());

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
    public Value CreateGetViewLength(Location location, Value? view) =>
        CreateGetViewLength(location, view, BasicValueType.Int32);

    /// <summary>
    /// Creates a node that resolves the length of the given view.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="view">The source view.</param>
    /// <returns>The created node.</returns>
    public Value CreateGetViewLongLength(Location location, Value? view) =>
        CreateGetViewLength(location, view, BasicValueType.Int64);

    /// <summary>
    /// Creates a node that resolves the length of the given view.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="view">The source view.</param>
    /// <param name="lengthType">The length type.</param>
    /// <returns>The created node.</returns>
    public Value CreateGetViewLength(
        Location location,
        Value? view,
        BasicValueType lengthType)
    {
        if (view is null) return CreatePrimitiveValue(location, lengthType, 0);

        location.Assert(view.Type is ViewType);

        // Fold trivial cases
        if (view is NewView newView)
            return CreateConvert(location, newView.Length, lengthType).AsNotNull();

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
    public Value CreateGetViewStride(Location location)
    {
        var denseType = ModuleBuilder.CreateType(typeof(Stride1D.Dense));
        return CreateNull(location, denseType);
    }

    /// <summary>
    /// Computes a new sub view from a given view.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="source">The source.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="length">The length.</param>
    /// <returns>A node that represents the new sub view.</returns>
    public Value? CreateSubView(
        Location location,
        Value? source,
        Value? offset,
        Value? length)
    {
        if (source is null) return null;
        if (length is null) return source;

        offset ??= CreatePrimitiveValue(location, length.BasicValueType, 0);

        location.Assert(
            source.Type is ViewType &&
            offset.BasicValueType.IsViewIndexType() &&
            length.BasicValueType.IsViewIndexType());

        return Append(new SubView(
            GetInitializer(location),
            source,
            offset,
            length));
    }

    /// <summary>
    /// Computes the address of a single element in the scope of a view or a pointer.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="source">The source view.</param>
    /// <param name="elementIndex">The element index to load.</param>
    /// <returns>A node that represents the element address.</returns>
    public Value? CreateLoadElementAddress(
        Location location,
        Value? source,
        Value? elementIndex)
    {
        if (source is null) return null;
        if (elementIndex is null) return source;

        // Remove unnecessary pointer casts
        if (elementIndex is IntAsPointerCast cast)
            elementIndex = cast.Source;

        // Assert a valid indexing type from here on
        location.Assert(elementIndex.BasicValueType.IsViewIndexType());

        // After LowerViews, a LEA source may be a lowered view struct
        // {PointerType, Int64} (e.g. from a MethodCall). Extract the
        // pointer field so downstream code sees an AddressSpaceType.
        if (source.Type is StructureType st &&
            st.NumFields >= 1 &&
            st.Fields[0] is AddressSpaceType)
        {
            source = CreateGetField(location, source, new FieldSpan(0));
        }

        var addressSpaceType = source.Type.AsNotNullCast<AddressSpaceType>();
        location.AssertNotNull(addressSpaceType);

        // Fold nested conversion operations that do not change the semantics
        if (elementIndex is ConvertValue convertValue &&
            convertValue.Value.BasicValueType == BasicValueType.Int32 &&
            convertValue.BasicValueType == BasicValueType.Int64)
        {
            elementIndex = convertValue.Value;
        }

        // Fold primitive pointer arithmetic that does not change anything
        return source.Type is PointerType && elementIndex.IsPrimitiveValue(0)
            ? source
            : Append(new LoadElementAddress(
                GetInitializer(location),
                source,
                elementIndex));
    }

    /// <summary>
    /// Computes the address of a single field.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="source">The source.</param>
    /// <param name="fieldSpan">The associated field span (if any).</param>
    /// <returns>A node that represents the field address.</returns>
    public Value? CreateLoadFieldAddress(
        Location location,
        Value? source,
        FieldSpan fieldSpan)
    {
        if (source is null) return null;

        // Simplify pseudo-structure accesses
        var pointerType = source.Type.As<PointerType>();
        if (pointerType.ElementType is not StructureType && fieldSpan.Span < 2)
        {
            return source;
        }
        else if (pointerType.ElementType is StructureType structureType &&
            fieldSpan.Index == 0 && structureType.NumFields == fieldSpan.Span)
        {
            return source;
        }

        // Fold nested field addresses
        return source is LoadFieldAddress lfa
            ? CreateLoadFieldAddress(
                location,
                lfa.Source,
                lfa.FieldSpan.Narrow(fieldSpan))
            : Append(new LoadFieldAddress(
                GetInitializer(location),
                source,
                fieldSpan));
    }

    /// <summary>
    /// Creates a builder to compute an array element address.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="array">The array instance.</param>
    /// <returns>The target array element address.</returns>
    public LoadArrayElementAddress.Builder CreateLoadArrayElementAddress(
        Location location,
        Value array)
    {
        location.AssertNotNull(array);
        return new LoadArrayElementAddress.Builder(this, location, array);
    }

    /// <summary>
    /// Creates a laea value.
    /// </summary>
    /// <param name="location">The current location.</param>
    /// <param name="values">
    /// The array value and a single value index for each array dimension.
    /// </param>
    /// <returns>The array element address.</returns>
    internal Value FinishLoadArrayElementAddress(
        Location location,
        ref ValueBuilderList values) =>
        Append(new LoadArrayElementAddress(
            GetInitializer(location),
            ref values));
}
