﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CPUDevice.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Threading;

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Specifies a simulator kind of a <see cref="CPUDevice"/> instance.
    /// </summary>
    public enum CPUDeviceKind
    {
        /// <summary>
        /// A CPU accelerator that simulates a common configuration of a default GPU
        /// simulator with 1 multiprocessor, a warp size of 4 and 4 warps per
        /// multiprocessor.
        /// </summary>
        Default,

        /// <summary>
        /// a CPU accelerator that simulates a common configuration of an NVIDIA GPU
        /// with 1 multiprocessor.
        /// </summary>
        Nvidia,

        /// <summary>
        /// A CPU accelerator that simulates a common configuration of an AMD GPU with
        /// 1 multiprocessor.
        /// </summary>
        AMD,

        /// <summary>
        /// A CPU accelerator that simulates a common configuration of a legacy GCN AMD
        /// GPU with 1 multiprocessor.
        /// </summary>
        LegacyAMD,

        /// <summary>
        /// A CPU accelerator that simulates a common configuration of an Intel GPU
        /// with 1 multiprocessor.
        /// </summary>
        Intel
    }

    /// <summary>
    /// Represents a single CPU device.
    /// </summary>
    [DeviceType(AcceleratorType.CPU)]
    public sealed class CPUDevice : Device
    {
        #region Constants

        /// <summary>
        /// The default warp size of 4 threads per group.
        /// </summary>
        private const int DefaultWarpSize = 4;

        /// <summary>
        /// The default number of 4 warps per multiprocessor.
        /// </summary>
        private const int DefaultNumWarpsPerMultiprocessor = 4;

        /// <summary>
        /// The default number of 1 multiprocessor.
        /// </summary>
        private const int DefaultNumMultiprocessors = 1;

        #endregion

        #region Static

        /// <summary>
        /// A CPU accelerator that simulates a common configuration of a default GPU
        /// simulator with 1 multiprocessor, a warp size of 4 and 4 warps per
        /// multiprocessor.
        /// </summary>
        public static readonly CPUDevice Default =
            new CPUDevice(
                DefaultWarpSize,
                DefaultNumWarpsPerMultiprocessor,
                DefaultNumMultiprocessors);

        /// <summary>
        /// A CPU accelerator that simulates a common configuration of an NVIDIA GPU with
        /// 1 multiprocessor.
        /// </summary>
        public static readonly CPUDevice Nvidia =
            new CPUDevice(
                32,
                32,
                1);

        /// <summary>
        /// A CPU accelerator that simulates a common configuration of an AMD GPU with 1
        /// multiprocessor.
        /// </summary>
        public static readonly CPUDevice AMD =
            new CPUDevice(
                32,
                8,
                1);

        /// A CPU accelerator that simulates a common configuration of a legacy GCN AMD
        /// GPU with 1 multiprocessor.
        public static readonly CPUDevice LegacyAMD =
            new CPUDevice(
                64,
                4,
                1);

        /// <summary>
        /// A CPU accelerator that simulates a common configuration of an Intel GPU with
        /// 1 multiprocessor.
        /// </summary>
        public static readonly CPUDevice Intel =
            new CPUDevice(
                16,
                8,
                1);

        /// <summary>
        /// Maps <see cref="CPUDeviceKind"/> values to
        /// <see cref="CPUDevice"/> instances.
        /// </summary>
        public static readonly ImmutableArray<CPUDevice> All =
            ImmutableArray.Create(new CPUDevice[]
        {
            Default,
            Nvidia,
            AMD,
            LegacyAMD,
            Intel,
        });

        /// <summary>
        /// Gets a specific CPU device.
        /// </summary>
        /// <param name="kind">The CPU device kind.</param>
        /// <returns>The CPU device.</returns>
        public static CPUDevice GetDevice(
            CPUDeviceKind kind) =>
            kind < CPUDeviceKind.Default || kind > CPUDeviceKind.Intel
            ? throw new ArgumentOutOfRangeException(nameof(kind))
            : All[(int)kind];

        /// <summary>
        /// Returns CPU devices.
        /// </summary>
        /// <param name="predicate">
        /// The predicate to include a given device.
        /// </param>
        /// <returns>All CPU devices.</returns>
        public static ImmutableArray<Device> GetDevices(Predicate<CPUDevice> predicate)
        {
            var registry = new DeviceRegistry();
            GetDevices(predicate, registry);
            return registry.ToImmutable();
        }

        /// <summary>
        /// Registers CPU devices.
        /// </summary>
        /// <param name="predicate">
        /// The predicate to include a given device.
        /// </param>
        /// <param name="registry">The registry to add all devices to.</param>
        internal static void GetDevices(
            Predicate<CPUDevice> predicate,
            DeviceRegistry registry)
        {
            if (registry is null)
                throw new ArgumentNullException(nameof(registry));
            if (predicate is null)
                throw new ArgumentNullException(nameof(predicate));

            foreach (var desc in All)
                registry.Register(desc, predicate);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new CPU accelerator description instance.
        /// </summary>
        /// <param name="numThreadsPerWarp">
        /// The number of threads per warp within a group.
        /// </param>
        /// <param name="numWarpsPerMultiprocessor">
        /// The number of warps per multiprocessor.
        /// </param>
        /// <param name="numMultiprocessors">
        /// The number of multiprocessors (number of parallel groups) to simulate.
        /// </param>
        public CPUDevice(
            int numThreadsPerWarp,
            int numWarpsPerMultiprocessor,
            int numMultiprocessors)
        {
            if (numThreadsPerWarp < 2 || !Utilities.IsPowerOf2(numWarpsPerMultiprocessor))
                throw new ArgumentOutOfRangeException(nameof(numThreadsPerWarp));
            if (numWarpsPerMultiprocessor < 1)
                throw new ArgumentOutOfRangeException(nameof(numWarpsPerMultiprocessor));
            if (numMultiprocessors < 1)
                throw new ArgumentOutOfRangeException(nameof(numMultiprocessors));

            // Check for existing limitations with respect to barrier participants
            if (numThreadsPerWarp * numWarpsPerMultiprocessor > short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(numWarpsPerMultiprocessor));
            if (NumMultiprocessors > short.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(numMultiprocessors));

            Name = nameof(CPUAccelerator);
            WarpSize = numThreadsPerWarp;
            MaxNumThreadsPerGroup = numThreadsPerWarp * numWarpsPerMultiprocessor;
            MaxNumThreadsPerMultiprocessor = MaxNumThreadsPerGroup;
            NumMultiprocessors = numMultiprocessors;
            MaxGroupSize = new Index3(
                MaxNumThreadsPerGroup,
                MaxNumThreadsPerGroup,
                MaxNumThreadsPerGroup);

            MemorySize = long.MaxValue;
            MaxGridSize = new Index3(int.MaxValue, int.MaxValue, int.MaxValue);
            MaxSharedMemoryPerGroup = int.MaxValue;
            MaxConstantMemory = int.MaxValue;
            NumThreads = MaxNumThreads * numMultiprocessors;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the number of threads.
        /// </summary>
        public int NumThreads { get; }

        #endregion

        #region Methods

        /// <inheritdoc/>
        protected override Accelerator CreateAcceleratorInternal(Context context) =>
            CreateAccelerator(context);

        /// <summary>
        /// Creates a new CPU accelerator using <see cref="CPUAcceleratorMode.Auto"/>
        /// and default thread priority.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <returns>The created CPU accelerator.</returns>
        public CPUAccelerator CreateAccelerator(Context context) =>
            CreateAccelerator(context, CPUAcceleratorMode.Auto);

        /// <summary>
        /// Creates a new CPU accelerator with default thread priority.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="mode">The CPU accelerator mode.</param>
        /// <returns>The created CPU accelerator.</returns>
        public CPUAccelerator CreateAccelerator(
            Context context,
            CPUAcceleratorMode mode) =>
            CreateAccelerator(context, mode, ThreadPriority.Normal);

        /// <summary>
        /// Creates a new CPU accelerator.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="mode">The CPU accelerator mode.</param>
        /// <param name="threadPriority">
        /// The thread priority of the execution threads.
        /// </param>
        /// <returns>The created CPU accelerator.</returns>
        public CPUAccelerator CreateAccelerator(
            Context context,
            CPUAcceleratorMode mode,
            ThreadPriority threadPriority) =>
            new CPUAccelerator(context, this, mode, threadPriority);

        #endregion

        #region Object

        /// <inheritdoc/>
        public override bool Equals(object obj) =>
            obj is CPUDevice device &&
            device.WarpSize == WarpSize &&
            device.MaxNumThreadsPerGroup == MaxNumThreadsPerGroup &&
            device.NumMultiprocessors == NumMultiprocessors &&
            base.Equals(obj);

        /// <inheritdoc/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ WarpSize ^ MaxNumThreadsPerGroup ^ NumMultiprocessors;

        #endregion
    }
}
