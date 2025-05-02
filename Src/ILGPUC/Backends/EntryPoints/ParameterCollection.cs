// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ParameterCollection.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Collections.Immutable;

namespace ILGPUC.Backends.EntryPoints;

/// <summary>
/// The parameter specification of an entry point.
/// </summary>
/// <param name="ParameterTypes">The parameter types.</param>
readonly record struct ParameterCollection(ImmutableArray<Type> ParameterTypes)
{
    /// <summary>
    /// Returns the number of parameter types.
    /// </summary>
    public int Count => ParameterTypes.Length;

    /// <summary>
    /// Returns the underlying parameter type (without references).
    /// </summary>
    /// <param name="index">The parameter index.</param>
    /// <returns>The desired parameter type.</returns>
    public Type this[int index] => GetParameterType(ParameterTypes, index);

    /// <summary>
    /// Returns the underlying parameter type (without references).
    /// </summary>
    /// <param name="parameterTypes">The parameter types.</param>
    /// <param name="parameterIndex">The parameter index.</param>
    /// <returns>The desired parameter type.</returns>
    private static Type GetParameterType(
        ImmutableArray<Type> parameterTypes,
        int parameterIndex)
    {
        var type = parameterTypes[parameterIndex];
        return type.IsByRef ? type.GetElementType().AsNotNull() : type;
    }

    /// <summary>
    /// Returns true if the specified parameter is passed by reference.
    /// </summary>
    /// <param name="parameterIndex">The parameter index.</param>
    /// <returns>True, if the specified parameter is passed by reference.</returns>
    public bool IsByRef(int parameterIndex) => ParameterTypes[parameterIndex].IsByRef;

    /// <summary>
    /// Copies the parameter types to the given array.
    /// </summary>
    /// <param name="target">The target array.</param>
    /// <param name="offset">The target offset to copy to.</param>
    public void CopyTo(Type[] target, int offset) =>
        ParameterTypes.CopyTo(target, offset);

    /// <summary>
    /// Returns an enumerator to enumerate all types in the collection.
    /// </summary>
    /// <returns>The enumerator.</returns>
    public ImmutableArray<Type>.Enumerator GetEnumerator() =>
        ParameterTypes.GetEnumerator();
}
