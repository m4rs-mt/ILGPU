// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: HandleValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPUC.IR.PureValues;

/// <summary>
/// Represents an internal .Net runtime handle value.
/// </summary>
sealed partial class HandleValue : PureValue
{
    /// <summary>
    /// Constructs a new internal .Net runtime handle value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="handle">The managed handle.</param>
    public HandleValue(
        in PureValueInitializer initializer,
        object handle)
        : base(initializer, initializer.ModuleBuilder.HandleType)
    {
        Handle = handle;

        Seal();
    }

    /// <summary>
    /// Returns the underlying managed handle.
    /// </summary>
    public object Handle { get; }

    /// <inheritdoc/>
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter) =>
        rewriter.Builder.CreateRuntimeHandle(Location, Handle);

    /// <summary>
    /// Returns the underlying handle as type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <returns>The converted handle.</returns>
    public T GetHandle<T>() => (T)Handle;

    /// <inheritdoc cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "handle";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() => $"{Handle}";
}
