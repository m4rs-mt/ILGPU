// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Global.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.PureValues;
using ILGPUC.IR.Rewriting;

namespace ILGPUC.IR.ModuleValues;

/// <summary>
/// Represents an allocation operation in local memory, shared, or global memory.
/// </summary>
sealed partial class Global :
    ModuleValue,
    IAllocationValue,
    IValueClassInformation
{
    /// <summary>
    /// Returns the global class.
    /// </summary>
    static ValueClass IValueClassInformation.ValueClass => ValueClass.Global;

    private readonly int _initializerMethodIndex;

    /// <summary>
    /// Constructs a new malloc node.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="mallocType">The allocation type.</param>
    /// <param name="addressSpace">The target address space.</param>
    /// <param name="arrayLength">The optional array length.</param>
    /// <param name="valueInitializer">The optional value initializer.</param>
    /// <param name="rawArrayLength">The optional direct array length.</param>
    /// <param name="rawValueInitializer">The optional direct value initializer.</param>
    internal Global(
        in ModuleValueInitializer initializer,
        TypeValue mallocType,
        MemoryAddressSpace addressSpace,
        Method? arrayLength,
        Method? valueInitializer,
        PrimitiveValueBox? rawArrayLength = null,
        PrimitiveValueBox? rawValueInitializer = null)
        : base(
            initializer,
            initializer.Builder.CreatePointerType(
                mallocType,
                addressSpace))
    {
        this.Assert(
            addressSpace == MemoryAddressSpace.Local ||
            addressSpace == MemoryAddressSpace.Shared ||
            addressSpace == MemoryAddressSpace.Constant,
            $"Global requires Local/Shared/Constant address space (got {addressSpace})");

        arrayLength = rawArrayLength.HasValue ? null : arrayLength;
        valueInitializer = rawValueInitializer.HasValue ? null : valueInitializer;

        var values = ValueBuilderList.Create(
            Generation,
            ((arrayLength is not null) ? 1 : 0) +
            (valueInitializer is not null ? 1 : 0));
        if (arrayLength is not null)
            values.Add(arrayLength);
        if (valueInitializer is not null)
        {
            values.Add(valueInitializer);
            _initializerMethodIndex = arrayLength is not null ? 1 : 0;
        }

        ArrayLength = rawArrayLength;
        ValueInitializer = rawValueInitializer;

        Seal(ref values);
    }

    /// <summary>
    /// Returns <see cref="ValueClass.Global"/>.
    /// </summary>
    public override ValueClass ValueClass => ValueClass.Global;

    /// <summary>
    /// Returns the allocation type.
    /// </summary>
    public TypeValue AllocType => GetTypeAs<PointerType>().ElementType;

    /// <summary>
    /// Returns the address space of this allocation.
    /// </summary>
    public MemoryAddressSpace AddressSpace => GetTypeAs<PointerType>().AddressSpace;

    /// <summary>
    /// Returns the array length method (if any)..
    /// </summary>
    public Method? ArrayLengthMethod => Count > 0 ? GetValue<Method>(0) : null;

    /// <summary>
    /// Returns the current <see cref="ArrayLengthMethod"/>.
    /// </summary>
    Value? IAllocationValue.ArrayLengthValue => ArrayLengthMethod;

    /// <summary>
    /// Returns the array length method (if any)..
    /// </summary>
    public Method? ValueInitializerMethod =>
        Count > 0 ? GetValue<Method>(_initializerMethodIndex) : null;

    /// <summary>
    /// Returns the array length box.
    /// </summary>
    public PrimitiveValueBox? ArrayLength { get; }

    /// <summary>
    /// Returns the initializer value box.
    /// </summary>
    public PrimitiveValueBox? ValueInitializer { get; }

    /// <inheritdoc/>
    public Global? Rewrite<TRewriter>(in TRewriter rewriter)
        where TRewriter : IModuleRewriter, allows ref struct =>
        rewriter.Builder.CreateGlobalDirect(
            Location,
            rewriter.RewriteAs<TypeValue>(AllocType),
            AddressSpace,
            ArrayLengthMethod is not null
                ? rewriter.RewriteAs<Method>(ArrayLengthMethod)
                : null,
            ValueInitializerMethod is not null
                ? rewriter.RewriteAs<Method>(ValueInitializerMethod)
                : null,
            ArrayLength,
            ValueInitializer);

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "global";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString()
    {
        var baseString = $"{Type} [{ArrayLengthMethod?.ToReferenceString() ??
            ArrayLength.ToString() ?? "?"}]";
        var initializerString = $"-> {ValueInitializerMethod?.ToReferenceString() ??
            ValueInitializer.ToString() ?? "N/A"}";
        return $"{baseString} {initializerString}";
    }

    /// <summary>
    /// Returns the hash code of the value id.
    /// </summary>
    public override int GetHashCode() => Id.GetHashCode();

    /// <summary>
    /// Returns true if the given value is exactly this value.
    /// </summary>
    public sealed override bool Equals(object? obj) =>
        obj is Global global && global.Id == Id;
}
