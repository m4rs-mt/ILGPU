// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Accelerator.Extensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Runtime.Extensions;
using System;
using System.Collections.Generic;

namespace ILGPU.Runtime;

partial class Accelerator
{
    #region Extensions

    /// <summary>
    /// Lock used for extensions dictionary.
    /// </summary>
    private readonly object _extensionsLock = new();

    /// <summary>
    /// All registered extensions.
    /// </summary>
    private readonly Dictionary<Guid, AcceleratorExtension> _extensions = [];

    /// <summary>
    /// Registers the given exception.
    /// </summary>
    /// <typeparam name="T">The extension type.</typeparam>
    /// <param name="extension">The extension instance to add.</param>
    /// <exception cref="AcceleratorException">
    /// If the given extension is already registered.
    /// </exception>
    public void Register<T>(T extension)
        where T : AcceleratorExtension, IAcceleratorExtension
    {
        lock (_extensionsLock)
        {
            if (!_extensions.TryAdd(T.Id, extension))
            {
                throw new InvalidOperationException(
                    RuntimeErrorMessages.ExtensionAlreadyRegistered);
            }
        }
    }

    /// <summary>
    /// Gets a registered accelerator extension and returns a base class reference.
    /// </summary>
    /// <typeparam name="T">The extension type.</typeparam>
    /// <returns>The extension reference.</returns>
    internal AcceleratorExtension GetAcceleratorExtension<T>()
        where T : IAcceleratorExtension
    {
        lock (_extensionsLock)
            return _extensions[T.Id];
    }

    /// <summary>
    /// Gets a registered accelerator extension.
    /// </summary>
    /// <typeparam name="T">The extension type.</typeparam>
    /// <returns>The extension instance.</returns>
    public T GetExtension<T>() where T : IAcceleratorExtension =>
        GetAcceleratorExtension<T>().GetAsAbstractExtension<T>();

    /// <summary>
    /// Disposes all extensions.
    /// </summary>
    private void DisposeExtensions_Locked()
    {
        foreach (var extension in _extensions.Values)
            extension.Dispose();
        _extensions.Clear();
    }

    #endregion
}

