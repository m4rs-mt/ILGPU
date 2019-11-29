// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                                www.ilgpu.net
//
// File: CLGroupExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.Backends.OpenCL;
using ILGPU.IR;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms.CL
{
    /// <summary>
    /// Custom OpenCL-specific implementations.
    /// </summary>
    static class CLGroupExtensions
    {
        #region Reduce

        /// <summary>
        /// Generates an intrinsic reduce.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="backend">The current backend.</param>
        /// <param name="codeGenerator">The code generator.</param>
        /// <param name="value">The value to generate code for.</param>
        public static void GenerateReduce<T, TReduction>(
            CLBackend backend,
            CLCodeGenerator codeGenerator,
            Value value)
            where T : struct
            where TReduction : struct, IScanReduceOperation<T> =>
            GenerateAllReduce<T, TReduction>(
                backend,
                codeGenerator,
                value);

        /// <summary>
        /// Generates an intrinsic reduce.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TReduction">The type of the reduction logic.</typeparam>
        /// <param name="backend">The current backend.</param>
        /// <param name="codeGenerator">The code generator.</param>
        /// <param name="value">The value to generate code for.</param>
        public static void GenerateAllReduce<T, TReduction>(
            CLBackend backend,
            CLCodeGenerator codeGenerator,
            Value value)
            where T : struct
            where TReduction : struct, IScanReduceOperation<T> =>
            CLContext.GenerateScanReduce<T, TReduction>(
                backend,
                codeGenerator,
                value,
                "work_group_reduce_");

        #endregion

        #region Scan

        /// <summary>
        /// Generates an intrinsic scan.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
        /// <param name="backend">The current backend.</param>
        /// <param name="codeGenerator">The code generator.</param>
        /// <param name="value">The value to generate code for.</param>
        public static void GenerateExclusiveScan<T, TScanOperation>(
            CLBackend backend,
            CLCodeGenerator codeGenerator,
            Value value)
            where T : struct
            where TScanOperation : struct, IScanReduceOperation<T> =>
            CLContext.GenerateScanReduce<T, TScanOperation>(
                backend,
                codeGenerator,
                value,
                "work_group_scan_exclusive_");

        /// <summary>
        /// Generates an intrinsic scan.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
        /// <param name="backend">The current backend.</param>
        /// <param name="codeGenerator">The code generator.</param>
        /// <param name="value">The value to generate code for.</param>
        public static void GenerateInclusiveScan<T, TScanOperation>(
            CLBackend backend,
            CLCodeGenerator codeGenerator,
            Value value)
            where T : struct
            where TScanOperation : struct, IScanReduceOperation<T> =>
            CLContext.GenerateScanReduce<T, TScanOperation>(
                backend,
                codeGenerator,
                value,
                "work_group_scan_inclusive_");

        /// <summary cref="GroupExtensions.ExclusiveScanWithBoundaries{T, TScanOperation}(T, out ScanBoundaries{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ExclusiveScanWithBoundaries<T, TScanOperation>(T value, out ScanBoundaries<T> boundaries)
            where T : struct
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            var scanned = GroupExtensions.ExclusiveScan<T, TScanOperation>(value);
            boundaries = new ScanBoundaries<T>(
                Group.Broadcast(scanned, 0),
                Group.Broadcast(scanned, Group.DimensionX - 1));
            return scanned;
        }

        /// <summary cref="GroupExtensions.InclusiveScanWithBoundaries{T, TScanOperation}(T, out ScanBoundaries{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T InclusiveScanWithBoundaries<T, TScanOperation>(T value, out ScanBoundaries<T> boundaries)
            where T : struct
            where TScanOperation : struct, IScanReduceOperation<T>
        {
            var scanned = GroupExtensions.InclusiveScan<T, TScanOperation>(value);
            boundaries = new ScanBoundaries<T>(
                Group.Broadcast(scanned, 0),
                Group.Broadcast(scanned, Group.DimensionX - 1));
            return scanned;
        }

        #endregion
    }
}
