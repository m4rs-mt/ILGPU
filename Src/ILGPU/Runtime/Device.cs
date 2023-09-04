// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: Device.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Reflection;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents an abstract device object.
    /// </summary>
    public interface IDevice
    {
        #region Properties

        /// <summary>
        /// Returns the name of this device.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Returns the memory size in bytes.
        /// </summary>
        long MemorySize { get; }

        /// <summary>
        /// Returns the max grid size.
        /// </summary>
        Index3D MaxGridSize { get; }

        /// <summary>
        /// Returns the max group size.
        /// </summary>
        Index3D MaxGroupSize { get; }

        /// <summary>
        /// Returns the maximum number of threads in a group.
        /// </summary>
        int MaxNumThreadsPerGroup { get; }

        /// <summary>
        /// Returns the maximum number of shared memory per thread group in bytes.
        /// </summary>
        int MaxSharedMemoryPerGroup { get; }

        /// <summary>
        /// Returns the maximum number of constant memory in bytes.
        /// </summary>
        int MaxConstantMemory { get; }

        /// <summary>
        /// Return the warp size.
        /// </summary>
        int WarpSize { get; }

        /// <summary>
        /// Returns the number of available multiprocessors.
        /// </summary>
        int NumMultiprocessors { get; }

        /// <summary>
        /// Returns the maximum number of threads per multiprocessor.
        /// </summary>
        int MaxNumThreadsPerMultiprocessor { get; }

        /// <summary>
        /// Returns the maximum number of threads of this accelerator.
        /// </summary>
        int MaxNumThreads { get; }

        /// <summary>
        /// Returns the supported capabilities of this accelerator.
        /// </summary>
        CapabilityContext Capabilities { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Prints device information to the given text writer.
        /// </summary>
        /// <param name="writer">The target text writer to write to.</param>
        void PrintInformation(TextWriter writer);

        #endregion
    }

    /// <summary>
    /// Represents a single device object.
    /// </summary>
    /// <remarks>
    /// Note that all derived class have to be annotated with the
    /// <see cref="DeviceTypeAttribute"/> attribute.
    /// </remarks>
    public abstract class Device : IDevice, IAcceleratorBuilder
    {
        #region Instance

        /// <summary>
        /// Constructs a new device.
        /// </summary>
        protected Device()
        {
            AcceleratorType = DeviceTypeAttribute.GetAcceleratorType(GetType());

            // NB: Initialized later by derived classes.
            Capabilities = Utilities.InitNotNullable<CapabilityContext>();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the type of the associated accelerator.
        /// </summary>
        public AcceleratorType AcceleratorType { get; }

        /// <summary>
        /// Returns the name of this device.
        /// </summary>
        public string Name { get; protected set; } = "<Unknown>";

        /// <summary>
        /// Returns the memory size in bytes.
        /// </summary>
        public long MemorySize { get; protected set; }

        /// <summary>
        /// Returns the max grid size.
        /// </summary>
        public Index3D MaxGridSize { get; protected set; }

        /// <summary>
        /// Returns the max group size.
        /// </summary>
        public Index3D MaxGroupSize { get; protected set; }

        /// <summary>
        /// Returns the maximum number of threads in a group.
        /// </summary>
        public int MaxNumThreadsPerGroup { get; protected set; }

        /// <summary>
        /// Returns the maximum shared memory per thread group in bytes.
        /// </summary>
        public int MaxSharedMemoryPerGroup { get; protected set; }

        /// <summary>
        /// Returns the maximum number of constant memory in bytes.
        /// </summary>
        public int MaxConstantMemory { get; protected set; }

        /// <summary>
        /// Return the warp size.
        /// </summary>
        public int WarpSize { get; protected set; }

        /// <summary>
        /// Returns the number of available multiprocessors.
        /// </summary>
        public int NumMultiprocessors { get; protected set; }

        /// <summary>
        /// Returns the maximum number of threads per multiprocessor.
        /// </summary>
        public int MaxNumThreadsPerMultiprocessor { get; protected set; }

        /// <summary>
        /// Returns the maximum number of threads of this accelerator.
        /// </summary>
        public int MaxNumThreads => NumMultiprocessors * MaxNumThreadsPerMultiprocessor;

        /// <summary>
        /// Returns the supported capabilities of this device.
        /// </summary>
        public CapabilityContext Capabilities { get; protected set; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new accelerator instance.
        /// </summary>
        /// <param name="context">The context instance.</param>
        /// <returns>The created accelerator instance.</returns>
        public abstract Accelerator CreateAccelerator(Context context);

        /// <summary>
        /// Prints device information to the given text writer.
        /// </summary>
        /// <param name="writer">The target text writer to write to.</param>
        public void PrintInformation(TextWriter writer)
        {
            if (writer is null)
                throw new ArgumentNullException(nameof(writer));

            PrintHeader(writer);
            PrintGeneralInfo(writer);
        }

        /// <summary>
        /// Prints general header information that should appear at the top.
        /// </summary>
        /// <param name="writer">The target text writer to write to.</param>
        protected virtual void PrintHeader(TextWriter writer)
        {
            writer.Write("Device: ");
            writer.WriteLine(Name);
            writer.Write("  Accelerator Type:                        ");
            writer.WriteLine(AcceleratorType.ToString());
        }

        /// <summary>
        /// Print general GPU specific information to the given text writer.
        /// </summary>
        /// <param name="writer">The target text writer to write to.</param>
        protected virtual void PrintGeneralInfo(TextWriter writer)
        {
            writer.Write("  Warp size:                               ");
            writer.WriteLine(WarpSize);

            writer.Write("  Number of multiprocessors:               ");
            writer.WriteLine(NumMultiprocessors);

            writer.Write("  Max number of threads/multiprocessor:    ");
            writer.WriteLine(MaxNumThreadsPerMultiprocessor);

            writer.Write("  Max number of threads/group:             ");
            writer.WriteLine(MaxNumThreadsPerGroup);

            writer.Write("  Max number of total threads:             ");
            writer.WriteLine(MaxNumThreads);

            writer.Write("  Max dimension of a group size:           ");
            writer.WriteLine(MaxGroupSize.ToString());

            writer.Write("  Max dimension of a grid size:            ");
            writer.WriteLine(MaxGridSize.ToString());

            writer.Write("  Total amount of global memory:           ");
            writer.WriteLine(
                "{0} bytes, {1} MB",
                MemorySize,
                MemorySize / (1024 * 1024));

            writer.Write("  Total amount of constant memory:         ");
            writer.WriteLine(
                "{0} bytes, {1} KB",
                MaxConstantMemory,
                MaxConstantMemory / 1024);

            writer.Write("  Total amount of shared memory per group: ");
            writer.WriteLine(
                "{0} bytes, {1} KB",
                MaxSharedMemoryPerGroup,
                MaxSharedMemoryPerGroup / 1024);
        }

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current device.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>
        /// True, if the given object is equal to the current device.
        /// </returns>
        public override bool Equals(object? obj) =>
            obj is Device device &&
            device.AcceleratorType == AcceleratorType &&
            device.Name == Name;

        /// <summary>
        /// Returns the hash code of this device.
        /// </summary>
        /// <returns>The hash code of this device.</returns>
        public override int GetHashCode() => (int)AcceleratorType;

        /// <summary>
        /// Returns the string representation of this accelerator description.
        /// </summary>
        /// <returns>The string representation of this accelerator.</returns>
        public override string ToString() =>
            $"{Name} [Type: {AcceleratorType}, WarpSize: {WarpSize}, " +
            $"MaxNumThreadsPerGroup: {MaxNumThreadsPerGroup}, " +
            $"MemorySize: {MemorySize}]";

        #endregion
    }

    /// <summary>
    /// Annotates classes derived from <see cref="Device"/>.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = true)]
    public sealed class DeviceTypeAttribute : Attribute
    {
        /// <summary>
        /// Gets the accelerator type of the given device class.
        /// </summary>
        /// <param name="type">The device class type.</param>
        /// <returns>The accelerator type.</returns>
        public static AcceleratorType GetAcceleratorType(Type type)
        {
            if (type is null)
                throw new ArgumentNullException(nameof(type));

            var attribute = type.GetCustomAttribute<DeviceTypeAttribute>();
            return attribute is null
                ? throw new InvalidOperationException(
                    RuntimeErrorMessages.InvalidDeviceTypeAttribute)
                : attribute.AcceleratorType;
        }

        /// <summary>
        /// Constructs a new device type attribute.
        /// </summary>
        /// <param name="acceleratorType">
        /// The accelerator type of the annotated device.
        /// </param>
        public DeviceTypeAttribute(AcceleratorType acceleratorType)
        {
            AcceleratorType = acceleratorType;
        }

        /// <summary>
        /// Returns the associated accelerator type.
        /// </summary>
        public AcceleratorType AcceleratorType { get; }
    }

    /// <summary>
    /// Extension methods for devices.
    /// </summary>
    public static class DeviceExtensions
    {
        /// <summary>
        /// Prints device information to the standard <see cref="Console.Out"/> stream.
        /// </summary>
        /// <param name="device">The device to print.</param>
        public static void PrintInformation(this IDevice device) =>
            device.PrintInformation(Console.Out);
    }

    /// <summary>
    /// A registry for device instances to avoid duplicate registrations.
    /// </summary>
    public sealed class DeviceRegistry
    {
        #region Instance

        /// <summary>
        /// The set of all registered devices.
        /// </summary>
        private readonly HashSet<Device> registered =
            new HashSet<Device>();

        /// <summary>
        /// Stores all registered accelerator device objects.
        /// </summary>
        private readonly
            ImmutableArray<Device>.Builder
            devices =
            ImmutableArray.CreateBuilder<Device>(8);

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of registered devices.
        /// </summary>
        public int Count => devices.Count;

        #endregion

        #region Methods

        /// <summary>
        /// Registers the given device.
        /// </summary>
        /// <param name="device">The device to register.</param>
        public void Register(Device device)
        {
            if (device is null)
                throw new ArgumentNullException(nameof(device));
            if (!registered.Add(device))
                return;

            devices.Add(device);
        }

        /// <summary>
        /// Registers the given device if the predicate evaluates to true.
        /// </summary>
        /// <typeparam name="TDevice">The device class type.</typeparam>
        /// <param name="device">The device to register.</param>
        /// <param name="predicate">
        /// The device predicate to check whether to include the device or not.
        /// </param>
        public void Register<TDevice>(TDevice device, Predicate<TDevice> predicate)
            where TDevice : Device
        {
            if (device is null)
                throw new ArgumentNullException(nameof(device));
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            if (predicate(device))
                Register(device);
        }

        /// <summary>
        /// Converts this registry into an immutable array.
        /// </summary>
        /// <returns>The created immutable array of devices.</returns>
        public ImmutableArray<Device> ToImmutable() => devices.ToImmutable();

        #endregion
    }
}
