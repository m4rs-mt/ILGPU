// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2022-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: VelocityDevice.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.Velocity;
using ILGPU.Backends.Velocity.Scalar;
using ILGPU.Util;
using System;

namespace ILGPU.Runtime.Velocity
{
    /// <summary>
    /// The device type of a Velocity device.
    /// </summary>
    public enum VelocityDeviceType
    {
        /// <summary>
        /// Scalar operations to simulate two lanes per warp.
        /// </summary>
        Scalar2,
    }

    /// <summary>
    /// Represents a software-emulated velocity device for high-performance execution of
    /// tasks on the CPU using vectorization.
    /// </summary>
    [DeviceType(AcceleratorType.Velocity)]
    public sealed class VelocityDevice : Device
    {
        #region Static

        private static readonly Type[] VelocitySpecializers = new Type[]
        {
            typeof(Scalar)
        };

        #endregion

        #region Instance

        /// <summary>
        /// Creates a new velocity device using the given device type.
        /// </summary>
        /// <param name="deviceType">The Velocity device type to use.</param>
        public VelocityDevice(VelocityDeviceType deviceType)
        {
            switch (deviceType)
            {
                case VelocityDeviceType.Scalar2:
                    // Scalar is always supported
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(deviceType));
            }

            Name = $"{nameof(VelocityAccelerator)}_{deviceType}";
            DeviceType = deviceType;
            TargetSpecializer = Activator.CreateInstance(
                    VelocitySpecializers[(int)deviceType])
                .AsNotNullCast<VelocityTargetSpecializer>();
            WarpSize = TargetSpecializer.WarpSize;
            MaxNumThreadsPerGroup = MaxNumThreadsPerMultiprocessor = WarpSize;
            NumMultiprocessors = Environment.ProcessorCount;
            MaxGroupSize = new Index3D(
                MaxNumThreadsPerGroup,
                1,
                1);

            MemorySize = long.MaxValue;
            MaxGridSize = new Index3D(int.MaxValue, 1, 1);
            MaxConstantMemory = int.MaxValue;
            NumThreads = MaxNumThreads;

            // Get the endian type from the global BitConverter class
            IsLittleEndian = BitConverter.IsLittleEndian;

            // Allocate a sufficient amount of local memory per thread equal to
            // the maximum number of shared memory per group in bytes (2 MB)
            MaxSharedMemoryPerGroup = 2 << 20;

            // Setup default Velocity capabilities
            Capabilities = new VelocityCapabilityContext();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the current device type.
        /// </summary>
        public VelocityDeviceType DeviceType { get; }

        /// <summary>
        /// Returns the internally used target specializer.
        /// </summary>
        internal VelocityTargetSpecializer TargetSpecializer { get; }

        /// <summary>
        /// Returns the number of threads.
        /// </summary>
        public int NumThreads { get; }

        /// <summary>
        /// Returns true if this device operates in little endian mode.
        /// </summary>
        public bool IsLittleEndian { get; }

        #endregion

        #region Methods

        /// <inheritdoc/>
        public override Accelerator CreateAccelerator(Context context) =>
            CreateVelocityAccelerator(context);

        /// <summary>
        /// Creates a new performance CPU accelerator using and the default thread
        /// priority.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <returns>The created CPU accelerator.</returns>
        public VelocityAccelerator CreateVelocityAccelerator(Context context) =>
            new VelocityAccelerator(context, this);

        #endregion

        #region Object

        /// <inheritdoc/>
        public override bool Equals(object? obj) =>
            obj is VelocityDevice device &&
            device.DeviceType == DeviceType &&
            base.Equals(obj);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            HashCode.Combine(base.GetHashCode(), DeviceType);

        #endregion
    }
}
