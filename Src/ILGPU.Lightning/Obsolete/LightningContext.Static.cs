// -----------------------------------------------------------------------------
//                              ILGPU.Lightning
//                   Copyright (c) 2017 ILGPU Lightning Project
//                                www.ilgpu.net
//
// File: LightningContext.Static.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPU.Runtime.CPU;
using ILGPU.Runtime.Cuda;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Threading;

namespace ILGPU.Lightning
{
    partial class LightningContext
    {
        /// <summary>
        /// Represents the default flags of a new lightning context.
        /// </summary>
        [Obsolete("Use Accelerator.DefaultFlags. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public static readonly CompileUnitFlags DefaultFlags = Accelerator.DefaultFlags;

        /// <summary>
        /// Represents the default flags of a new lightning context.
        /// </summary>
        [Obsolete("Use Accelerator.FastMathFlags. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public static readonly CompileUnitFlags FastMathFlags = Accelerator.FastMathFlags;

        /// <summary>
        /// Represents the default flags of a new lightning context.
        /// </summary>
        [Obsolete("Use Accelerator.FastMath32BitFloatsFlags. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public static readonly CompileUnitFlags FastMath32BitFloatsFlags = Accelerator.FastMath32BitFloatsFlags;

        /// <summary>
        /// Returns a list of available accelerators.
        /// </summary>
        [Obsolete("Use Accelerator.Accelerators. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        public static IReadOnlyList<AcceleratorId> Accelerators => Accelerator.Accelerators;

        #region Generic Construction

        /// <summary>
        /// Constructs a LightningContext with an associated accelerator id.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="acceleratorId">The specified accelerator id.</param>
        [Obsolete("Use new CPUAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateContext(Context context, AcceleratorId acceleratorId)
        {
            return new LightningContext(Accelerator.Create(context, acceleratorId), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated accelerator id.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="acceleratorId">The specified accelerator id.</param>
        /// <param name="flags">The compile-unit flags.</param>
        [Obsolete("Use new CPUAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateContext(
            Context context,
            AcceleratorId acceleratorId,
            CompileUnitFlags flags)
        {
            return new LightningContext(Accelerator.Create(context, acceleratorId), flags, true);
        }

        #endregion

        #region CPU Construction

        /// <summary>
        /// Constructs a LightningContext with an associated new CPU runtime.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        [Obsolete("Use new CPUAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCPUContext(Context context)
        {
            return new LightningContext(new CPUAccelerator(context), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new CPU runtime.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="flags">The compile-unit flags.</param>
        [Obsolete("Use new CPUAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCPUContext(Context context, CompileUnitFlags flags)
        {
            return new LightningContext(new CPUAccelerator(context), flags, true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new CPU runtime.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        [Obsolete("Use new CPUAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCPUContext(Context context, int numThreads)
        {
            return new LightningContext(new CPUAccelerator(context, numThreads), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new CPU runtime.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        /// <param name="flags">The compile-unit flags.</param>
        [Obsolete("Use new CPUAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCPUContext(Context context, int numThreads, CompileUnitFlags flags)
        {
            return new LightningContext(new CPUAccelerator(context, numThreads), flags, true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new CPU runtime.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        /// <param name="warpSize">The number of threads per warp.</param>
        [Obsolete("Use new CPUAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCPUContext(Context context, int numThreads, int warpSize)
        {
            return new LightningContext(new CPUAccelerator(context, numThreads, warpSize), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new CPU runtime.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        /// <param name="warpSize">The number of threads per warp.</param>
        /// <param name="flags">The compile-unit flags.</param>
        [Obsolete("Use new CPUAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCPUContext(Context context, int numThreads, int warpSize, CompileUnitFlags flags)
        {
            return new LightningContext(new CPUAccelerator(context, numThreads, warpSize), flags, true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new CPU runtime.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        /// <param name="threadPriority">The thread priority of the execution threads.</param>
        [Obsolete("Use new CPUAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCPUContext(Context context, int numThreads, ThreadPriority threadPriority)
        {
            return new LightningContext(new CPUAccelerator(context, numThreads, threadPriority), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new CPU runtime.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        /// <param name="threadPriority">The thread priority of the execution threads.</param>
        /// <param name="flags">The compile-unit flags.</param>
        [Obsolete("Use new CPUAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCPUContext(Context context, int numThreads, ThreadPriority threadPriority, CompileUnitFlags flags)
        {
            return new LightningContext(new CPUAccelerator(context, numThreads, threadPriority), flags, true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new CPU runtime.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        /// <param name="warpSize">The number of threads per warp.</param>
        /// <param name="threadPriority">The thread priority of the execution threads.</param>
        [Obsolete("Use new CPUAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCPUContext(Context context, int numThreads, int warpSize, ThreadPriority threadPriority)
        {
            return new LightningContext(new CPUAccelerator(context, numThreads, warpSize, threadPriority), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new CPU runtime.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="numThreads">The number of threads for paralllel processing.</param>
        /// <param name="warpSize">The number of threads per warp.</param>
        /// <param name="threadPriority">The thread priority of the execution threads.</param>
        /// <param name="flags">The compile-unit flags.</param>
        [Obsolete("Use new CPUAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCPUContext(Context context, int numThreads, int warpSize, ThreadPriority threadPriority, CompileUnitFlags flags)
        {
            return new LightningContext(new CPUAccelerator(context, numThreads, warpSize, threadPriority), flags, true);
        }

        #endregion

        #region Cuda Construction

        /// <summary>
        /// Constructs a LightningContext with an associated new Cuda accelerator targeting the default device.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        [Obsolete("Use new CudaAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCudaContext(Context context)
        {
            return new LightningContext(new CudaAccelerator(context), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new Cuda accelerator targeting the default device.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="flags">The compile-unit flags.</param>
        [Obsolete("Use new CudaAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCudaContext(Context context, CompileUnitFlags flags)
        {
            return new LightningContext(new CudaAccelerator(context), flags, true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new Cuda accelerator.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="deviceId">The target device id.</param>
        [Obsolete("Use new CudaAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCudaContext(Context context, int deviceId)
        {
            return new LightningContext(new CudaAccelerator(context, deviceId), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new Cuda accelerator.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="deviceId">The target device id.</param>
        /// <param name="flags">The compile-unit flags.</param>
        [Obsolete("Use new CudaAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCudaContext(Context context, int deviceId, CompileUnitFlags flags)
        {
            return new LightningContext(new CudaAccelerator(context, deviceId), flags, true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new Cuda accelerator.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="deviceId">The target device id.</param>
        /// <param name="acceleratorFlags">The accelerator flags.</param>
        [Obsolete("Use new CudaAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCudaContext(Context context, int deviceId, CudaAcceleratorFlags acceleratorFlags)
        {
            return new LightningContext(new CudaAccelerator(context, deviceId, acceleratorFlags), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new Cuda accelerator.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="deviceId">The target device id.</param>
        /// <param name="acceleratorFlags">The accelerator flags.</param>
        /// <param name="flags">The compile-unit flags.</param>
        [Obsolete("Use new CudaAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCudaContext(Context context, int deviceId, CudaAcceleratorFlags acceleratorFlags, CompileUnitFlags flags)
        {
            return new LightningContext(new CudaAccelerator(context, deviceId, acceleratorFlags), flags, true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new Cuda accelerator.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="d3d11Device">A pointer to a valid D3D11 device.</param>
        [Obsolete("Use new CudaAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCudaContext(Context context, IntPtr d3d11Device)
        {
            return new LightningContext(new CudaAccelerator(context, d3d11Device), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new Cuda accelerator.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="d3d11Device">A pointer to a valid D3D11 device.</param>
        /// <param name="flags">The compile-unit flags.</param>
        [Obsolete("Use new CudaAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCudaContext(Context context, IntPtr d3d11Device, CompileUnitFlags flags)
        {
            return new LightningContext(new CudaAccelerator(context, d3d11Device), flags, true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new Cuda accelerator.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="d3d11Device">A pointer to a valid D3D11 device.</param>
        /// <param name="acceleratorFlags">The accelerator flags.</param>
        [Obsolete("Use new CudaAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCudaContext(Context context, IntPtr d3d11Device, CudaAcceleratorFlags acceleratorFlags)
        {
            return new LightningContext(new CudaAccelerator(context, d3d11Device, acceleratorFlags), true);
        }

        /// <summary>
        /// Constructs a LightningContext with an associated new Cuda accelerator.
        /// Note that the associated runtime accelerator does not have to be disposed manually.
        /// </summary>
        /// <param name="context">The ILGPU context.</param>
        /// <param name="d3d11Device">A pointer to a valid D3D11 device.</param>
        /// <param name="acceleratorFlags">The accelerator flags.</param>
        /// <param name="flags">The compile-unit flags.</param>
        [Obsolete("Use new CudaAccelerator. Refer to http://www.ilgpu.net/Documentation/UpgradeGuide020")]
        [SuppressMessage("Microsoft.Reliability", "CA2000:Dispose objects before losing scope", Justification = "This reference will be automatically disposed by the LightningContext")]
        public static LightningContext CreateCudaContext(Context context, IntPtr d3d11Device, CudaAcceleratorFlags acceleratorFlags, CompileUnitFlags flags)
        {
            return new LightningContext(new CudaAccelerator(context, d3d11Device, acceleratorFlags), flags, true);
        }

        #endregion
    }
}
