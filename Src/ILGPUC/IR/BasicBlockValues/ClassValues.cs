// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ClassValues.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.ModuleValues;

namespace ILGPUC.IR.BasicBlockValues;

/// <summary>
/// An abstract class value.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class ClassValue(in BasicBlockValueInitializer initializer, TypeValue type) :
    MemoryValue(initializer, type);

/// <summary>
/// Represents an operation on object values.
/// </summary>
/// <param name="initializer">The value initializer.</param>
/// <param name="type">
/// The type to use (not part of the generic initializer provided).
/// </param>
abstract class ClassOperationValue(
    in BasicBlockValueInitializer initializer,
    TypeValue type) :
    MemoryValue(initializer, type)
{
    /// <summary>
    /// Returns the object value to load from.
    /// </summary>
    public Value ObjectValue => GetValue<Value>(0);

    /// <summary>
    /// Returns the object type.
    /// </summary>
    public ObjectType ObjectType => ObjectValue.GetTypeAs<ObjectType>();
}
