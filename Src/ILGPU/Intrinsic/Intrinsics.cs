// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Intrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;

namespace ILGPU.Intrinsic;

/// <summary>
/// Marks accelerator methods that are built in.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
sealed class AcceleratorIntrinsicAttribute() :
    IntrinsicAttribute(IntrinsicType.Accelerator);

/// <summary>
/// Marks backend methods that are built in.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
sealed class BackendIntrinsicAttribute() : IntrinsicAttribute(IntrinsicType.Backend);

/// <summary>
/// Marks math methods that are built in.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
sealed class MathIntrinsicAttribute() : IntrinsicAttribute(IntrinsicType.Math);

/// <summary>
/// Marks compare methods that are built in.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
sealed class CompareIntrinsicAttribute() : IntrinsicAttribute(IntrinsicType.Compare);

/// <summary>
/// Marks convert methods that are built in.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
sealed class ConvertIntrinsicAttribute() : IntrinsicAttribute(IntrinsicType.Convert);

/// <summary>
/// Marks atomic methods that are built in.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
sealed class AtomicIntrinsicAttribute() : IntrinsicAttribute(IntrinsicType.Atomic);

/// <summary>
/// Marks view methods that are built in.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
sealed class ViewIntrinsicAttribute() : IntrinsicAttribute(IntrinsicType.View);

/// <summary>
/// Marks warp methods that are built in.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
sealed class WarpIntrinsicAttribute() : IntrinsicAttribute(IntrinsicType.Warp);

/// <summary>
/// Marks group methods that are built in.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
sealed class GroupIntrinsicAttribute() : IntrinsicAttribute(IntrinsicType.Group);

/// <summary>
/// Marks grid methods that are built in.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
sealed class GridIntrinsicAttribute() : IntrinsicAttribute(IntrinsicType.Grid);

/// <summary>
/// Marks interop methods that are built in.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
sealed class InteropIntrinsicAttribute() : IntrinsicAttribute(IntrinsicType.Interop);

/// <summary>
/// Marks utility methods that are built in.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
sealed class UtilityIntrinsicAttribute() : IntrinsicAttribute(IntrinsicType.Utility);

/// <summary>
/// Marks language methods that are built in.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
sealed class LanguageIntrinsicAttribute() : IntrinsicAttribute(IntrinsicType.Language);
