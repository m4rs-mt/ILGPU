// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: IntrinsicTypes.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Intrinsic;

/// <summary>
/// Represents the type of an intrinsic.
/// </summary>
public enum IntrinsicType : int
{
    /// <summary>
    /// An accelerator intrinsic.
    /// </summary>
    Accelerator,

    /// <summary>
    /// A backend intrinsic.
    /// </summary>
    Backend,

    /// <summary>
    /// An atomic intrinsic.
    /// </summary>
    Atomic,

    /// <summary>
    /// A compare intrinsic.
    /// </summary>
    Compare,

    /// <summary>
    /// A convert intrinsic.
    /// </summary>
    Convert,

    /// <summary>
    /// A grid intrinsic.
    /// </summary>
    Grid,

    /// <summary>
    /// A group intrinsic.
    /// </summary>
    Group,

    /// <summary>
    /// An interop intrinsic.
    /// </summary>
    Interop,

    /// <summary>
    /// A math intrinsic.
    /// </summary>
    Math,

    /// <summary>
    /// A view intrinsic.
    /// </summary>
    View,

    /// <summary>
    /// A warp intrinsic.
    /// </summary>
    Warp,

    /// <summary>
    /// A utility intrinsic.
    /// </summary>
    Utility,

    /// <summary>
    /// A language intrinsic.
    /// </summary>
    Language,
}

/// <summary>
/// Marks methods that are built in.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public abstract class IntrinsicAttribute(IntrinsicType intrinsicType) : Attribute
{
    /// <summary>
    /// Returns the type of this intrinsic attribute.
    /// </summary>
    public IntrinsicType IntrinsicType { get; } = intrinsicType;
}
