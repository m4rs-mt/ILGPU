// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Accelerator.Current.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Intrinsic;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime;

partial class Accelerator
{
    #region Thread Static

    /// <summary>
    /// Represents the current accelerator.
    /// </summary>
    [ThreadStatic]
    private static Accelerator? _currentAccelerator;

    /// <summary>
    /// Returns the current group runtime context.
    /// </summary>
    public static Accelerator? Current
    {
        get => _currentAccelerator;
        private set => _currentAccelerator = value;
    }

    #endregion

    #region Static

    /// <summary>
    /// Returns the current accelerator type.
    /// </summary>
    /// <remarks>
    /// Note that this static property is only accessible within kernels.
    /// </remarks>
    public static AcceleratorType CurrentType
    {
        [AcceleratorIntrinsic]
        get => throw new InvalidKernelOperationException();
    }

    #endregion

    #region Methods

    /// <summary>
    /// Makes this accelerator the current one for this thread.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Bind()
    {
        if (Current == this) return;
        Current?.OnUnbind();
        Current = this;
        OnBind();
    }

    /// <summary>
    /// Makes this accelerator the current one for this thread and
    /// returns a <see cref="ScopedAcceleratorBinding"/> object that allows
    /// to easily recover the old binding.
    /// </summary>
    /// <returns>A scoped binding object.</returns>
    public ScopedAcceleratorBinding BindScoped() => new(this);

    /// <summary>
    /// Will be invoked when this accelerator will the current one.
    /// </summary>
    protected abstract void OnBind();

    /// <summary>
    /// Will be invoked when this accelerator is no longer the current one.
    /// </summary>
    protected abstract void OnUnbind();

    #endregion
}

/// <summary>
/// Represents a temporary binding of an accelerator object. The old binding can be
/// recovered by either <see cref="Recover"/> or the <see cref="Dispose"/> method.
/// </summary>
public struct ScopedAcceleratorBinding :
    IDisposable,
    IEquatable<ScopedAcceleratorBinding>
{
    #region Instance

    /// <summary>
    /// Constructs a new scoped binding.
    /// </summary>
    /// <param name="accelerator">The new accelerator.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ScopedAcceleratorBinding(Accelerator accelerator)
    {
        OldAccelerator = Accelerator.Current;
        if (OldAccelerator == accelerator)
            OldAccelerator = null;
        else
            accelerator.Bind();
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the old accelerator that was the current one
    /// before the current binding operation (if any).
    /// </summary>
    public Accelerator? OldAccelerator { get; private set; }

    /// <summary>
    /// Returns true if an old accelerator has to be recovered.
    /// </summary>
    public readonly bool IsRecoverable => OldAccelerator != null;

    #endregion

    #region Methods

    /// <summary>
    /// Recovers the old accelerator and resets the internal state.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Recover()
    {
        if (!IsRecoverable)
            return;
        OldAccelerator?.Bind();
        OldAccelerator = null;
    }

    #endregion

    #region IEquatable

    /// <summary>
    /// Returns true if the given binding is equal to the current binding.
    /// </summary>
    /// <param name="other">The other binding.</param>
    /// <returns>
    /// True, if the given binding is equal to the current binding.
    /// </returns>
    public readonly bool Equals(ScopedAcceleratorBinding other) => this == other;

    #endregion

    #region IDisposable

    /// <summary>
    /// Recovers the old accelerator and resets the internal state.
    /// </summary>
    /// <remarks>
    /// The dispose method is useful in combination with using statements.
    /// </remarks>
    public void Dispose() => Recover();

    #endregion

    #region Object

    /// <summary>
    /// Returns true if the given object is equal to the current binding.
    /// </summary>
    /// <param name="obj">The other object.</param>
    /// <returns>
    /// True, if the given object is equal to the current binding.
    /// </returns>
    public readonly override bool Equals(object? obj) =>
        obj is ScopedAcceleratorBinding binding && Equals(binding);

    /// <summary>
    /// Returns the hash code of this binding.
    /// </summary>
    /// <returns>The hash code of this binding.</returns>
    public readonly override int GetHashCode() =>
        OldAccelerator?.GetHashCode() ?? 0;

    /// <summary>
    /// Returns the string representation of this binding.
    /// </summary>
    /// <returns>The string representation of this binding.</returns>
    public readonly override string ToString() =>
        OldAccelerator?.ToString() ?? "<NoBinding>";

    #endregion

    #region Operators

    /// <summary>
    /// Returns true if the first and second binding are the same.
    /// </summary>
    /// <param name="first">The first binding.</param>
    /// <param name="second">The second binding.</param>
    /// <returns>True, if the first and second binding are the same.</returns>
    public static bool operator ==(
        ScopedAcceleratorBinding first,
        ScopedAcceleratorBinding second) =>
        first.OldAccelerator == second.OldAccelerator;

    /// <summary>
    /// Returns true if the first and second binding are not the same.
    /// </summary>
    /// <param name="first">The first binding.</param>
    /// <param name="second">The second binding.</param>
    /// <returns>True, if the first and second binding are not the same.</returns>
    public static bool operator !=(
        ScopedAcceleratorBinding first,
        ScopedAcceleratorBinding second) =>
        !(first == second);

    #endregion
}
