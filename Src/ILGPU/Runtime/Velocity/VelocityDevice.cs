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

using System;
using System.Threading;

namespace ILGPU.Runtime.Velocity
{
    /// <summary>
    /// Represents a software-emulated velocity device for high-performance execution of
    /// tasks on the CPU using vectorization.
    /// </summary>
    [DeviceType(AcceleratorType.Velocity)]
    public sealed class VelocityDevice : Device
    {
        #region Constants

        /// <summary>
        /// The default maximum amount of shared memory in bytes (1024k).
        /// </summary>
        public const int MinSharedMemoryPerGroup = 1 << 20;

        #endregion

        #region Instance

        /// <summary>
        /// Creates a new velocity device with the default amount of shared memory per
        /// group (refer to <see cref="MinSharedMemoryPerGroup"/> for more
        /// information about the default size).
        /// </summary>
        public VelocityDevice()
            : this(Environment.ProcessorCount)
        { }

        /// <summary>
        /// Creates a new velocity device with the default amount of shared memory per
        /// group (refer to <see cref="MinSharedMemoryPerGroup"/> for more
        /// information about the default size).
        /// </summary>
        public VelocityDevice(int numMultiprocessors)
            : this(numMultiprocessors, MinSharedMemoryPerGroup)
        { }

        /// <summary>
        /// Creates a new velocity device using the given amount of shared memory (min
        /// amount is <see cref="MinSharedMemoryPerGroup"/> per group).
        /// </summary>
        /// <param name="maxSharedMemoryPerGroup">
        /// The maximum amount of shared memory per group in bytes.
        /// </param>
        /// <param name="numMultiprocessors">
        /// The number of multiprocessors to use.
        /// </param>
        public VelocityDevice(int numMultiprocessors, int maxSharedMemoryPerGroup)
        {
            if (numMultiprocessors < 1)
                throw new ArgumentOutOfRangeException(nameof(numMultiprocessors));
            if (maxSharedMemoryPerGroup < MinSharedMemoryPerGroup)
                throw new ArgumentOutOfRangeException(nameof(maxSharedMemoryPerGroup));

            Name = nameof(VelocityAccelerator);
            WarpSize = VelocityWarp32.RawVectorLength;
            MinWarpSize = VelocityWarp64.RawVectorLength;
            MaxNumThreadsPerGroup = MaxNumThreadsPerMultiprocessor = WarpSize;
            NumMultiprocessors = numMultiprocessors;
            MaxGroupSize = new Index3D(
                MaxNumThreadsPerGroup,
                1,
                1);

            MemorySize = long.MaxValue;
            MaxGridSize = new Index3D(int.MaxValue, ushort.MaxValue, ushort.MaxValue);
            MaxSharedMemoryPerGroup = maxSharedMemoryPerGroup;
            MaxConstantMemory = int.MaxValue;
            NumThreads = MaxNumThreads;

            // Get the endian type from the global BitConverter class
            IsLittleEndian = BitConverter.IsLittleEndian;

            // Allocate a sufficient amount of local memory per thread equal to
            // the maximum number of shared memory per group in bytes
            MaxLocalMemoryPerThread = maxSharedMemoryPerGroup;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the minimum warp size of this device.
        /// </summary>
        public int MinWarpSize { get; }

        /// <summary>
        /// Returns the number of threads.
        /// </summary>
        public int NumThreads { get; }

        /// <summary>
        /// Returns true if this device operates in little endian mode.
        /// </summary>
        public bool IsLittleEndian { get; }

        /// <summary>
        /// Returns the maximum local memory per thread in bytes.
        /// </summary>
        public int MaxLocalMemoryPerThread { get; }

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
        public VelocityAccelerator CreateVelocityAccelerator(
            Context context) =>
            CreateVelocityAccelerator(context, ThreadPriority.Normal);

        /// <summary>
        /// Creates a new performance CPU accelerator using and the default thread
        /// priority.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="threadPriority">
        /// The thread priority of the execution threads.
        /// </param>
        /// <returns>The created CPU accelerator.</returns>
        public VelocityAccelerator CreateVelocityAccelerator(
            Context context,
            ThreadPriority threadPriority) =>
            new VelocityAccelerator(context, this, threadPriority);

        #endregion

        #region Object

        /// <inheritdoc/>
        public override bool Equals(object obj) =>
            obj is VelocityDevice device &&
            device.MaxSharedMemoryPerGroup == MaxSharedMemoryPerGroup &&
            base.Equals(obj);

        /// <inheritdoc/>
        public override int GetHashCode() => base.GetHashCode() ^ MaxSharedMemoryPerGroup;

        #endregion
    }
}
