// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CPUAcceleratorTask.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Util;
using System;
using System.Diagnostics;
using System.Reflection;

namespace ILGPU.Runtime.CPU
{
    /// <summary>
    /// Execution delegate for CPU kernels inside the runtime system.
    /// </summary>
    /// <param name="task">The referenced task.</param>
    /// <param name="globalIndex">The global thread index.</param>
    public delegate void CPUKernelExecutionHandler(
        CPUAcceleratorTask task,
        int globalIndex);

    /// <summary>
    /// Represents a single CPU-accelerator task.
    /// </summary>
    public class CPUAcceleratorTask
    {
        #region Static

        internal const int LinearIndex = 1;

        /// <summary>
        /// Contains the required parameter types of the default task constructor.
        /// </summary>
        internal static readonly Type[] ConstructorParameterTypes =
        {
            typeof(CPUKernelExecutionHandler),
            typeof(KernelConfig),
            typeof(RuntimeKernelConfig)
        };

        /// <summary>
        /// Contains the required parameter types of the task-execution method.
        /// </summary>
        internal static readonly Type[] ExecuteParameterTypes =
        {
            typeof(CPUAcceleratorTask), // task
            typeof(int)                 // linear index
        };

        /// <summary>
        /// Gets a task-specific constructor.
        /// </summary>
        /// <param name="taskType">The task type.</param>
        /// <returns>The constructor to create a new task instance.</returns>
        public static ConstructorInfo GetTaskConstructor(Type taskType) =>
            taskType.GetConstructor(
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance,
                null,
                ConstructorParameterTypes,
                null)
            .AsNotNull();

        /// <summary>
        /// Returns the getter for the <see cref="TotalUserDim"/> of a specific task
        /// type.
        /// </summary>
        /// <param name="taskType">The task type.</param>
        /// <returns>The getter method.</returns>
        public static MethodInfo GetTotalUserDimGetter(Type taskType) =>
            taskType.GetProperty(
                nameof(TotalUserDim),
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            .AsNotNull()
            .GetGetMethod(true)
            .AsNotNull();

        /// <summary>
        /// Returns the getter for the <see cref="TotalUserDimXY"/> of a specific task
        /// type.
        /// </summary>
        /// <param name="taskType">The task type.</param>
        /// <returns>The getter method.</returns>
        public static MethodInfo GetTotalUserDimXYGetter(Type taskType) =>
            taskType.GetProperty(
                nameof(TotalUserDimXY),
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)
            .AsNotNull()
            .GetGetMethod(true)
            .AsNotNull();

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new accelerator task.
        /// </summary>
        /// <param name="kernelExecutionDelegate">The execution method.</param>
        /// <param name="userConfig">The user-defined grid configuration.</param>
        /// <param name="config">The global task configuration.</param>
        public CPUAcceleratorTask(
            CPUKernelExecutionHandler kernelExecutionDelegate,
            KernelConfig userConfig,
            RuntimeKernelConfig config)
        {
            Debug.Assert(
                kernelExecutionDelegate != null,
                "Invalid execution delegate");
            if (!userConfig.IsValid)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(userConfig),
                    RuntimeErrorMessages.InvalidGridDimension);
            }

            if (!config.IsValid)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(config),
                    RuntimeErrorMessages.InvalidGridDimension);
            }

            KernelExecutionDelegate = kernelExecutionDelegate;
            TotalUserDim = userConfig.GridDim * userConfig.GroupDim;
            GridDim = config.GridDim;
            GroupDim = config.GroupDim;
            DynamicSharedMemoryConfig = config.SharedMemoryConfig.DynamicConfig;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the total dimension that was specified by the user.
        /// </summary>
        public Index3D TotalUserDim { get; }

        /// <summary>
        /// Extracts the upper XY part from the <see cref="TotalUserDim"/>.
        /// </summary>
        internal Index2D TotalUserDimXY => TotalUserDim.XY;

        /// <summary>
        /// Returns the current grid dimension.
        /// </summary>
        public Index3D GridDim { get; }

        /// <summary>
        /// Returns the current group dimension.
        /// </summary>
        public Index3D GroupDim { get; }

        /// <summary>
        /// Returns the shared memory config to use.
        /// </summary>
        public SharedMemoryConfig DynamicSharedMemoryConfig { get; }

        /// <summary>
        /// Returns the associated kernel-execution delegate.
        /// </summary>
        public CPUKernelExecutionHandler KernelExecutionDelegate { get; }

        #endregion
    }
}
