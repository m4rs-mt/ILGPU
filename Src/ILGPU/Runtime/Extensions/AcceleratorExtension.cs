// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: AcceleratorExtension.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;

namespace ILGPU.Runtime.Extensions;

/// <summary>
/// Represents an abstract accelerator extension interface that can be queried.
/// </summary>
public interface IAcceleratorExtension
{
    /// <summary>
    /// Returns the unique accelerator extension guid.
    /// </summary>
    static abstract Guid Id { get; }
}

/// <summary>
/// Represents an abstract accelerator extension that can store additional data.
/// </summary>
public abstract class AcceleratorExtension : DisposeBase
{
    /// <summary>
    /// Returns the extension instance as abstract unsafe interface extension.
    /// </summary>
    /// <typeparam name="T">The target instance type.</typeparam>
    internal T GetAsAbstractExtension<T>() where T : IAcceleratorExtension =>
        (T)(this as object);
}
