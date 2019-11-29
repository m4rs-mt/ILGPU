// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                                www.ilgpu.net
//
// File: CLWarpExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Algorithms.ScanReduceOperations;
using ILGPU.Backends.OpenCL;
using ILGPU.IR;

namespace ILGPU.Algorithms.CL
{
    /// <summary>
    /// Custom OpenCL-specific implementations.
    /// </summary>
    static class CLWarpExtensions
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
                "sub_group_reduce_");

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
                "sub_group_scan_exclusive_");

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
                "sub_group_scan_inclusive_");

        #endregion
    }
}
