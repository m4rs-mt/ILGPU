// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: Context.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.Runtime.Debugging;
using ILGPU.Util;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;

namespace ILGPU;

/// <summary>
/// Represents the main ILGPU context.
/// </summary>
/// <remarks>Members of this class are thread-safe.</remarks>
public sealed partial class Context : DisposeBase
{
    #region Nested Types

    /// <summary>
    /// Represents an enumerable collection of all devices of a specific type.
    /// </summary>
    /// <typeparam name="TDevice">The device class type.</typeparam>
    public readonly ref struct DeviceCollection<TDevice>
        where TDevice : Device, IDeviceAcceleratorTypeInfo
    {
        #region Nested Types

        /// <summary>
        /// Returns an enumerator to enumerate all registered devices of the parent
        /// type.
        /// </summary>
        public ref struct Enumerator
        {
            private List<Device>.Enumerator _enumerator;

            /// <summary>
            /// Constructs a new use enumerator.
            /// </summary>
            /// <param name="devices">The list of all devices.</param>
            internal Enumerator(List<Device> devices)
            {
                _enumerator = devices.GetEnumerator();
            }

            /// <summary>
            /// Returns the current use.
            /// </summary>
            public TDevice Current => _enumerator.Current.AsNotNullCast<TDevice>();

            /// <summary cref="IEnumerator.MoveNext"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public bool MoveNext() => _enumerator.MoveNext();
        }

        #endregion

        private readonly List<Device> _devices;

        /// <summary>
        /// Constructs a new device collection.
        /// </summary>
        /// <param name="deviceList">The list of all devices.</param>
        internal DeviceCollection(List<Device> deviceList)
        {
            _devices = deviceList;
        }

        /// <summary>
        /// Returns the device type of this collection.
        /// </summary>
        public readonly AcceleratorType AcceleratorType => TDevice.AcceleratorType;

        /// <summary>
        /// Returns the number of registered devices.
        /// </summary>
        public readonly int Count => _devices.Count;

        /// <summary>
        /// Returns the i-th device.
        /// </summary>
        /// <param name="deviceIndex">
        /// The relative device index of the specific device type. 0 here refers to
        /// the first device of this type, 1 to the second, etc.
        /// </param>
        /// <returns>The i-th device.</returns>
        public readonly TDevice this[int deviceIndex]
        {
            get
            {
                if (deviceIndex < 0)
                    throw new ArgumentOutOfRangeException(nameof(deviceIndex));
                return deviceIndex < Count
                    ? _devices[deviceIndex].AsNotNullCast<TDevice>()
                    : throw new NotSupportedException(
                        RuntimeErrorMessages.NotSupportedTargetAccelerator);
            }
        }

        /// <summary>
        /// Returns an enumerator to enumerate all uses devices.
        /// </summary>
        /// <returns>The enumerator.</returns>
        public readonly Enumerator GetEnumerator() => new Enumerator(_devices);
    }

    #endregion

    #region Events

    /// <summary>
    /// Will be called when a new accelerator has been created.
    /// </summary>
    public event EventHandler<Accelerator>? AcceleratorCreated;

    #endregion

    #region Instance

    /// <summary>
    /// An internal mapping of accelerator types to individual devices.
    /// </summary>
    private readonly Dictionary<AcceleratorType, List<Device>> _deviceMapping;

    /// <summary>
    /// Constructs a new ILGPU main context
    /// </summary>
    /// <param name="builder">The parent builder instance.</param>
    /// <param name="devices">The array of accelerator descriptions.</param>
    internal Context(
        Builder builder,
        ImmutableArray<Device> devices)
    {
        InstanceId = InstanceId.CreateNew();
        Properties = builder.InstantiateProperties();

        // Initialize all devices
        Devices = devices;
        if (devices.IsDefaultOrEmpty)
        {
            // Add a default debug device
            Devices = [DebugDevice.Default];
        }

        // Create a mapping
        _deviceMapping = new Dictionary<AcceleratorType, List<Device>>(Devices.Length);
        foreach (var device in Devices)
        {
            if (!_deviceMapping.TryGetValue(device.AcceleratorType, out var devs))
            {
                devs = new List<Device>(8);
                _deviceMapping.Add(device.AcceleratorType, devs);
            }
            devs.Add(device);
        }
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the current instance id.
    /// </summary>
    internal InstanceId InstanceId { get; }

    /// <summary>
    /// All registered devices.
    /// </summary>
    public ImmutableArray<Device> Devices { get; }

    /// <summary>
    /// Returns the context properties.
    /// </summary>
    public ContextProperties Properties { get; }

    #endregion

    #region Methods

    /// <summary>
    /// Gets a specific device of the given type using a relative device index.
    /// </summary>
    /// <typeparam name="TDevice">The device class type.</typeparam>
    /// <param name="deviceIndex">
    /// The relative device index of the specific device type. 0 here refers to the
    /// first device of this type, 1 to the second, etc.
    /// </param>
    /// <returns>The device instance.</returns>
    public TDevice GetDevice<TDevice>(int deviceIndex)
        where TDevice : Device, IDeviceAcceleratorTypeInfo =>
        GetDevices<TDevice>()[deviceIndex];

    /// <summary>
    /// Gets all devices of the given type.
    /// </summary>
    /// <typeparam name="TDevice">The device class type.</typeparam>
    /// <returns>All device instances.</returns>
    public DeviceCollection<TDevice> GetDevices<TDevice>()
        where TDevice : Device, IDeviceAcceleratorTypeInfo =>
        _deviceMapping.TryGetValue(TDevice.AcceleratorType, out var devices)
        ? new DeviceCollection<TDevice>(devices)
        : new DeviceCollection<TDevice>([]);

    /// <summary>
    /// Attempts to return the most optimal single device.
    /// </summary>
    /// <param name="preferCPU">Always returns CPU device 0.</param>
    /// <returns>Selected device.</returns>
    public Device GetPreferredDevice(bool preferCPU) =>
        GetPreferredDevices(preferCPU, matchingDevicesOnly: false).First();

    /// <summary>
    /// Attempts to return the most optimal set of devices.
    /// </summary>
    /// <param name="preferCPU">Always returns first CPU device.</param>
    /// <param name="matchingDevicesOnly">Only returns matching devices.</param>
    /// <returns>Selected devices.</returns>
    public IEnumerable<Device> GetPreferredDevices(
        bool preferCPU,
        bool matchingDevicesOnly)
    {
        if (preferCPU)
        {
            return _deviceMapping.TryGetValue(AcceleratorType.Debug, out var devices)
                ? devices
                : throw new NotSupportedException(
                        RuntimeErrorMessages.NotSupportedTargetAccelerator);
        }

        var sorted = Devices
            .OrderByDescending(d => d.MemorySize)
            .Where(d => d.AcceleratorType != AcceleratorType.Debug)
            .ToList();

        if (sorted.Count > 0)
        {
            if (matchingDevicesOnly)
            {
                Device toMatch = sorted.First();
                return sorted.Where(
                    d =>
                    d.AcceleratorType == toMatch.AcceleratorType &&
                    d.MemorySize == toMatch.MemorySize);
            }
            else
            {
                return sorted;
            }
        }
        else
        {
            return _deviceMapping.TryGetValue(AcceleratorType.Debug, out var devices)
                ? devices
                : throw new NotSupportedException(
                        RuntimeErrorMessages.NotSupportedTargetAccelerator);
        }
    }

    /// <summary>
    /// Raises the corresponding <see cref="AcceleratorCreated"/> event.
    /// </summary>
    /// <param name="accelerator">The new accelerator.</param>
    internal void OnAcceleratorCreated(Accelerator accelerator) =>
        AcceleratorCreated?.Invoke(this, accelerator);

    #endregion

    #region Enumerable

    /// <summary>
    /// Returns an accelerator description enumerator.
    /// </summary>
    public ImmutableArray<Device>.Enumerator GetEnumerator() =>
        Devices.GetEnumerator();

    #endregion

    #region IDisposable

    /// <summary cref="DisposeBase.Dispose(bool)"/>
    protected override void Dispose(bool disposing) =>
        base.Dispose(disposing);

    #endregion
}
