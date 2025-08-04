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

using ILGPUC.IR.Construction;
using ILGPUC.IR.Types;

namespace ILGPUC.IR.Values;

/// <summary>
/// Represents an internal .Net runtime handle value.
/// </summary>
sealed partial class HandleValue : Value
{
    #region Instance

    /// <summary>
    /// Constructs a new internal .Net runtime handle value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="handle">The managed handle.</param>
    internal HandleValue(
        in ValueInitializer initializer,
        object handle)
        : base(initializer)
    {
        this.AssertNotNull(handle);
        Handle = handle;
        Seal();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the underlying managed handle.
    /// </summary>
    public object Handle { get; }

    #endregion

    #region Methods

    /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
    protected override TypeNode ComputeType(in ValueInitializer initializer) =>
        initializer.Context.HandleType;

    /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
    protected internal override Value Rebuild(
        IRBuilder builder,
        IRRebuilder rebuilder) =>
        builder.CreateRuntimeHandle(Location, Handle);

    /// <summary>
    /// Returns the underlying handle as type <typeparamref name="T"/>.
    /// </summary>
    /// <typeparam name="T">The target type.</typeparam>
    /// <returns>The converted handle.</returns>
    public T GetHandle<T>() => (T)Handle;

    #endregion

    #region Object

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToPrefixString() => "handle";

    /// <summary cref="Value.ToArgString"/>
    protected override string ToArgString() => $"{Handle}";

    #endregion
}
