// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: OneValue.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

namespace ILGPU;

/// <summary>
/// A value reference that is only valid in the first lane of a warp.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="value">The value reference.</param>
public readonly ref struct FirstLaneRef<T>(ref T value) where T : struct
{
    private readonly ref T _value = ref value;

    /// <summary>
    /// Returns the underlying value reference.
    /// </summary>
    public ref T Value => ref _value;
}

/// <summary>
/// A value that is only valid in the first lane of a warp.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="value">The value.</param>
public readonly struct FirstLaneValue<T>(T value) where T : struct
{
    /// <summary>
    /// Returns the underlying value.
    /// </summary>
    public T Value { get; } = value;
}

/// <summary>
/// A value reference that is only valid in the first thread of a group.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="value">The value reference.</param>
public readonly ref struct FirstThreadRef<T>(ref T value) where T : struct
{
    private readonly ref T _value = ref value;

    /// <summary>
    /// Returns the underlying value reference.
    /// </summary>
    public ref T Value => ref _value;
}

/// <summary>
/// A value that is only valid in the first thread of a group.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="value">The value.</param>
public readonly struct FirstThreadValue<T>(T value) where T : struct
{
    /// <summary>
    /// Returns the underlying value.
    /// </summary>
    public T Value { get; } = value;
}

/// <summary>
/// A value reference that is only valid in the last thread of a group.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="value">The value reference.</param>
public readonly ref struct LastThreadRef<T>(ref T value) where T : struct
{
    private readonly ref T _value = ref value;

    /// <summary>
    /// Returns the underlying value reference.
    /// </summary>
    public ref T Value => ref _value;
}

/// <summary>
/// A value that is only valid in the last thread of a group.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <param name="value">The value.</param>
public readonly struct LastThreadValue<T>(T value) where T : struct
{
    /// <summary>
    /// Returns the underlying value.
    /// </summary>
    public T Value { get; } = value;
}
