// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: Context.Builder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.Cuda;
using ILGPU.Runtime.Debugging;
using ILGPU.Runtime.OpenCL;
using System;

namespace ILGPU;

partial class Context
{
    #region Builder

    /// <summary>
    /// A context builder class.
    /// </summary>
    /// <remarks>
    /// If no accelerators will be added to this builder, the resulting context
    /// will use the default debug accelerator <see cref="DebugDevice.Default"/>.
    /// </remarks>
    public sealed class Builder : ContextProperties
    {
        #region Instance

        internal Builder() { }

        #endregion

        #region Properties

        /// <summary>
        /// All accelerator descriptions that have been registered.
        /// </summary>
        internal DeviceRegistry DeviceRegistry { get; } = new DeviceRegistry();

        #endregion

        #region Methods

        /// <summary>
        /// Enables all supported accelerators.
        /// </summary>
        /// <returns>The current builder instance.</returns>
        public Builder Default() => AllAccelerators();

        /// <summary>
        /// Enables all supported accelerators.
        /// </summary>
        /// <returns>The current builder instance.</returns>
        public Builder AllAccelerators() => this.OpenCL().Cuda();

        /// <summary>
        /// Enables all accelerators that fulfill the given predicate.
        /// </summary>
        /// <param name="predicate">
        /// The predicate to include a given accelerator description.
        /// </param>
        /// <returns>The current builder instance.</returns>
        public Builder AllAccelerators(
            Predicate<Device> predicate) =>
            this.OpenCL(predicate).Cuda(predicate);

        /// <summary>
        /// Specifies the page locking mode.
        /// </summary>
        /// <param name="mode">The locking mode to use.</param>
        /// <returns>The current builder instance.</returns>
        public Builder PageLocking(PageLockingMode mode)
        {
            PageLockingMode = mode;
            return this;
        }

        /// <summary>
        /// Turns on profiling of all streams.
        /// </summary>
        /// <returns>The current builder instance.</returns>
        public Builder Profiling()
        {
            EnableProfiling = true;
            return this;
        }

        /// <summary>
        /// Converts this builder instance into a context instance.
        /// </summary>
        /// <returns>The created context instance.</returns>
        public Context ToContext() => new(this, DeviceRegistry.ToImmutable());

        #endregion

        #region Extensibility

        /// <summary>
        /// Sets an extension property.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="value">The value to store.</param>
        public new void SetExtensionProperty<T>(string key, T value)
            where T : struct =>
            base.SetExtensionProperty(key, value);

        #endregion
    }

    /// <summary>
    /// Creates a new builder instance.
    /// </summary>
    /// <returns>The builder instance.</returns>
    public static Builder Create() => new();

    /// <summary>
    /// Creates a new context instance.
    /// </summary>
    /// <param name="buildingCallback">The user defined builder callback.</param>
    /// <returns>The created context.</returns>
    public static Context Create(Action<Builder> buildingCallback)
    {
        if (buildingCallback is null)
            throw new ArgumentNullException(nameof(buildingCallback));

        var builder = Create();
        buildingCallback(builder);
        return builder.ToContext();
    }

    /// <summary>
    /// Creates a default context by invoking the <see cref="Builder.Default()"/>
    /// method on the temporary builder instance.
    /// </summary>
    /// <returns>The created context.</returns>
    public static Context CreateDefault() => Create(builder => builder.Default());

    #endregion
}
