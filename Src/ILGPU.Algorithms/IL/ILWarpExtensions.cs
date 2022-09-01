// ---------------------------------------------------------------------------------------
//                                   ILGPU Algorithms
//                        Copyright (c) 2019-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: ILWarpExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.PTX;
using ILGPU.Algorithms.ScanReduceOperations;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms.IL
{
    /// <summary>
    /// Custom IL-specific implementations that fall back to PTX-specific implementations
    /// as the CPU runtime is fully compatible with the PTX runtime.
    /// </summary>
    static class ILWarpExtensions
    {
        #region Reduce

        /// <summary cref="WarpExtensions.Reduce{T, TReduction}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Reduce<T, TReduction>(T value)
            where T : unmanaged
            where TReduction : IScanReduceOperation<T> =>
            PTXWarpExtensions.Reduce<T, TReduction>(value);

        /// <summary cref="WarpExtensions.AllReduce{T, TReduction}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AllReduce<T, TReduction>(T value)
            where T : unmanaged
            where TReduction : IScanReduceOperation<T> =>
            PTXWarpExtensions.AllReduce<T, TReduction>(value);

        #endregion

        #region Scan

        /// <summary cref="WarpExtensions.ExclusiveScan{T, TScanOperation}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ExclusiveScan<T, TScanOperation>(T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            PTXWarpExtensions.ExclusiveScan<T, TScanOperation>(value);

        /// <summary cref="WarpExtensions.InclusiveScan{T, TScanOperation}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T InclusiveScan<T, TScanOperation>(T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T> =>
            PTXWarpExtensions.InclusiveScan<T, TScanOperation>(value);

        #endregion
    }
}
