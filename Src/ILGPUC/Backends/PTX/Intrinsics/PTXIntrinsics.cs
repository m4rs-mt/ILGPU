// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PTXIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.RadixSort;
using ILGPU.ScanReduce;
using ILGPUC.Intrinsic;

namespace ILGPUC.Backends.PTX.Intrinsics;

/// <summary>
/// Intrinsic wrapper implementations.
/// </summary>
static class PTXIntrinsics
{
    /// <summary cref="Group.ExclusiveScan{T, TScanOperation}(T)"/>
    public static T Group_ScanExclusive<T, TScanOperation>(T value)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T> =>
        GenericGroup.ExclusiveScan<T, TScanOperation>(value);

    /// <summary cref="Group.ExclusiveScan{T, TScan}(T, out ScanBoundaries{T})" />
    public static T Group_ScanExclusiveWithBoundaries<T, TScanOperation>(
        T value,
        out ScanBoundaries<T> boundaries)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T> =>
        GenericGroup.ExclusiveScan<T, TScanOperation>(value, out boundaries);

    /// <summary cref="Group.InclusiveScan{T, TScanOperation}(T)"/>
    public static T Group_ScanInclusive<T, TScanOperation>(T value)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T> =>
        GenericGroup.InclusiveScan<T, TScanOperation>(value);

    /// <summary cref="Group.InclusiveScan{T, TScan}(T, out ScanBoundaries{T})" />
    public static T Group_ScanInclusiveWithBoundaries<T, TScanOperation>(
        T value,
        out ScanBoundaries<T> boundaries)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T> =>
        GenericGroup.InclusiveScan<T, TScanOperation>(value, out boundaries);

    /// <summary cref="Group.Reduce{T, TReduction}(T)"/>
    public static T Group_ReduceFirstLane<T, TReduction>(T value)
        where T : unmanaged
        where TReduction : struct, IScanReduceOperation<T> =>
        GenericGroup.Reduce<T, TReduction>(value);

    /// <summary cref="Group.AllReduce{T, TReduction}(T)"/>
    public static T Group_ReduceAllLanes<T, TReduction>(T value)
        where T : unmanaged
        where TReduction : struct, IScanReduceOperation<T> =>
        GenericGroup.AllReduce<T, TReduction>(value);

    /// <summary cref="Group.RadixSort{T, TRadixSortOperation}(T)"/>
    public static T Group_RadixSort<T, TRadixSortOperation>(T value)
        where T : unmanaged
        where TRadixSortOperation : struct, IRadixSortOperation<T> =>
        GenericGroup.RadixSort<T, TRadixSortOperation>(value);

    /// <summary cref="Warp.ExclusiveScan{T, TScanOperation}(T)"/>
    public static T Warp_ScanExclusive<T, TScanOperation>(T value)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T> =>
        GenericWarp.ExclusiveScan<T, TScanOperation>(value);

    /// <summary cref="Warp.InclusiveScan{T, TScanOperation}(T)"/>
    public static T Warp_ScanInclusive<T, TScanOperation>(T value)
        where T : unmanaged
        where TScanOperation : struct, IScanReduceOperation<T> =>
        GenericWarp.InclusiveScan<T, TScanOperation>(value);

    /// <summary cref="Warp.Reduce{T, TReduction}(T)"/>
    public static T Warp_ReduceFirstLane<T, TReduction>(T value)
        where T : unmanaged
        where TReduction : struct, IScanReduceOperation<T> =>
        GenericWarp.Reduce<T, TReduction>(value);

    /// <summary cref="Warp.AllReduce{T, TReduction}(T)"/>
    public static T Warp_ReduceAllLanes<T, TReduction>(T value)
        where T : unmanaged
        where TReduction : struct, IScanReduceOperation<T> =>
        GenericWarp.AllReduce<T, TReduction>(value);

    /// <summary cref="Warp.RadixSort{T, TRadixSortOperation}(T)"/>
    public static T Warp_RadixSort<T, TRadixSortOperation>(T value)
        where T : unmanaged
        where TRadixSortOperation : struct, IRadixSortOperation<T> =>
        GenericWarp.RadixSort<T, TRadixSortOperation>(value);
}
