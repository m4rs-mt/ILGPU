// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: IntrinsicProvider.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.CodeGeneration;
using ILGPU.Runtime;

namespace ILGPU.Intrinsic;

/// <summary>
/// Represents a provider kind specifying what kind of values a provider can supply.
/// </summary>
public enum IntrinsicProviderKind
{
    /// <summary>
    /// A value can be provided for each thread. The resulting view is configured to be
    /// globally accessible via <see cref="Grid.GlobalThreadIndex"/>.
    /// </summary>
    ForEachThread,

    /// <summary>
    /// A value can be provided for each thread and the resulting view is configured
    /// to be accessible via <see cref="Group.Index"/>.
    /// </summary>
    ForEachThreadPerGroup,

    /// <summary>
    /// A value can be provided for each first lane in a warp.
    /// </summary>
    ForEachWarp,

    /// <summary>
    /// A value can be provided for each first thread in a group.
    /// </summary>
    ForEachGroup,

    /// <summary>
    /// A value can be provided for each kernel (for one thread in a kernel).
    /// </summary>
    ForEachKernel,
}

/// <summary>
/// Contains provider kinds.
/// </summary>
public static class IntrinsicProviderKinds
{
    /// <summary>
    /// A static provider kind description.
    /// </summary>
    public interface IIntrinsicProviderKind
    {
        /// <summary>
        /// Returns the desired provider kind.
        /// </summary>
        public static abstract IntrinsicProviderKind Kind { get; }
    }

    /// <summary>
    /// Represents a provider kind for each thread.
    /// </summary>
    public readonly struct ForEachThread : IIntrinsicProviderKind
    {
        /// <summary>
        /// Returns <see cref="IntrinsicProviderKind.ForEachThread"/>.
        /// </summary>
        public static IntrinsicProviderKind Kind => IntrinsicProviderKind.ForEachThread;
    }

    /// <summary>
    /// Represents a provider kind for each thread per group.
    /// </summary>
    public readonly struct ForEachThreadPerGroup : IIntrinsicProviderKind
    {
        /// <summary>
        /// Returns <see cref="IntrinsicProviderKind.ForEachThreadPerGroup"/>.
        /// </summary>
        public static IntrinsicProviderKind Kind =>
            IntrinsicProviderKind.ForEachThreadPerGroup;
    }

    /// <summary>
    /// Represents a provider kind for each warp.
    /// </summary>
    public readonly struct ForEachWarp : IIntrinsicProviderKind
    {
        /// <summary>
        /// Returns <see cref="IntrinsicProviderKind.ForEachWarp"/>.
        /// </summary>
        public static IntrinsicProviderKind Kind => IntrinsicProviderKind.ForEachWarp;
    }

    /// <summary>
    /// Represents a provider kind for each group.
    /// </summary>
    public readonly struct ForEachGroup : IIntrinsicProviderKind
    {
        /// <summary>
        /// Returns <see cref="IntrinsicProviderKind.ForEachGroup"/>.
        /// </summary>
        public static IntrinsicProviderKind Kind => IntrinsicProviderKind.ForEachGroup;
    }

    /// <summary>
    /// Represents a provider kind for each kernel.
    /// </summary>
    public readonly struct ForEachKernel : IIntrinsicProviderKind
    {
        /// <summary>
        /// Returns <see cref="IntrinsicProviderKind.ForEachKernel"/>.
        /// </summary>
        public static IntrinsicProviderKind Kind => IntrinsicProviderKind.ForEachKernel;
    }
}

/// <summary>
/// The required interface for all views supplied by providers.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
public interface IIntrinsicProviderView<T> where T : unmanaged;

/// <summary>
/// Represents an intrinsic provider of automatically supplied values through internal
/// compiler support.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
/// <typeparam name="TView">The view type.</typeparam>
public interface IIntrinsicProvider<T, TView>
    where T : unmanaged
    where TView : struct
{
    /// <summary>
    /// Returns true if a value provided by this provider can be reused. Returning false
    /// means that a new value needs to be provided each time the provider is called.
    /// </summary>
    static abstract bool ViewCanBeReused { get; }

    /// <summary>
    /// Returns true if a value provided by this provider can be reused. Returning false
    /// means that a new value needs to be provided each time the provider is called.
    /// </summary>
    static abstract bool ValueCanBeReused { get; }

    /// <summary>
    /// Allocates a memory buffer for underlying operations.
    /// </summary>
    /// <param name="stream">The accelerator stream to use.</param>
    /// <param name="maxExtent">The maximum extent to use.</param>
    /// <returns>The allocated memory buffer to hold all required values.</returns>
    static abstract MemoryBuffer Allocate(AcceleratorStream stream, long maxExtent);

    /// <summary>
    /// Initializes a memory buffer for underlying operations.
    /// </summary>
    /// <param name="stream">The accelerator stream to use.</param>
    /// <param name="buffer">The buffer to to use for initialization.</param>
    /// <param name="extent">The extent to to use for initialization.</param>
    static abstract void Init(AcceleratorStream stream, MemoryBuffer buffer, long extent);

    /// <summary>
    /// Gets a view from a memory buffer for underlying operations.
    /// </summary>
    /// <param name="buffer">The accelerator stream to use.</param>
    /// <param name="extent">The extent to to use for view access.</param>
    static abstract TView GetView(MemoryBuffer buffer, long extent);
}

/// <summary>
/// Contains intrinsic functions that trigger compiler-internal helpers.
/// </summary>
public static class IntrinsicProvider
{
    /// <summary>
    /// Triggers ILGPUC to generate code for automatically provided value within kernels.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <typeparam name="TView">The view type.</typeparam>
    /// <typeparam name="TProvider">The provider type to use.</typeparam>
    /// <typeparam name="TKind">The intrinsic provider kind to use.</typeparam>
    /// <returns>An access view to manage automatically provided values.</returns>
    /// <exception cref="InvalidKernelOperationException">
    /// Cannot be called outside of ILGPU kernels.
    /// </exception>
    [InteropIntrinsic, DelayCodeGeneration]
    public static TView Provide<T, TView, TProvider, TKind>()
        where T : unmanaged
        where TView : struct, IIntrinsicProviderView<T>
        where TProvider : IIntrinsicProvider<T, TView>
        where TKind : struct, IntrinsicProviderKinds.IIntrinsicProviderKind =>
        throw new InvalidKernelOperationException();
}
