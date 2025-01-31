// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ClassValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.Types;

namespace ILGPUC.IR.Values;

/// <summary>
/// An abstract class value.
/// </summary>
abstract class ClassValue : Value
{
    #region Instance

    /// <summary>
    /// Constructs a new abstract class value.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    internal ClassValue(in ValueInitializer initializer)
        : base(initializer)
    { }

    #endregion
}

/// <summary>
/// Represents an operation on object values.
/// </summary>
abstract class ClassOperationValue : MemoryValue
{
    #region Instance

    /// <summary>
    /// Constructs a new abstract object operation.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    internal ClassOperationValue(in ValueInitializer initializer)
        : base(initializer)
    { }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the object value to load from.
    /// </summary>
    public ValueReference ObjectValue => this[0];

    /// <summary>
    /// Returns the object type.
    /// </summary>
    public ObjectType ObjectType => ObjectValue.Type.AsNotNullCast<ObjectType>();

    #endregion
}
