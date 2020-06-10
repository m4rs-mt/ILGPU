// -----------------------------------------------------------------------------
//                             ILGPU.Algorithms
//                  Copyright (c) 2019 ILGPU Algorithms Project
//                                www.ilgpu.net
//
// File: ILWarpExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details.
// -----------------------------------------------------------------------------

using ILGPU.Algorithms.ScanReduceOperations;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms.IL
{
    /// <summary>
    /// Custom IL-specific implementations.
    /// </summary>
    static class ILWarpExtensions
    {
        #region Reduce

        /// <summary cref="WarpExtensions.Reduce{T, TReduction}"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Reduce<T, TReduction>(T value)
            where T : struct
            where TReduction : IScanReduceOperation<T> => value;

        /// <summary cref="WarpExtensions.AllReduce{T, TReduction}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AllReduce<T, TReduction>(T value)
            where T : struct
            where TReduction : IScanReduceOperation<T> => value;

        #endregion

        #region Scan

        /// <summary cref="WarpExtensions.ExclusiveScan{T, TScanOperation}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ExclusiveScan<T, TScanOperation>(T value)
            where T : struct
            where TScanOperation : struct, IScanReduceOperation<T> => default;

        /// <summary cref="WarpExtensions.InclusiveScan{T, TScanOperation}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T InclusiveScan<T, TScanOperation>(T value)
            where T : struct
            where TScanOperation : struct, IScanReduceOperation<T> => value;

        #endregion
    }
}
