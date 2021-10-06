// ---------------------------------------------------------------------------------------
//                                   ILGPU.Algorithms
//                      Copyright (c) 2019 ILGPU Algorithms Project
//                                    www.ilgpu.net
//
// File: ILFunctions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Algorithms.ScanReduceOperations;
using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.Algorithms.IL
{
    /// <summary>
    /// Custom IL-specific implementations.
    /// </summary>
    static class ILFunctions
    {
        #region Nested Types

        public interface IILFunctionImplementation
        {
            /// <summary>
            /// The maximum number of supported thread per context on the CPU accelerator
            /// for the implementation of specific algorithms.
            /// </summary>
            int MaxNumThreads { get; }

            /// <summary>
            /// Returns true if the current thread is the first thread.
            /// </summary>
            bool IsFirstThread { get; }

            /// <summary>
            /// Returns the current linear thread index.
            /// </summary>
            int ThreadIndex { get; }

            /// <summary>
            /// Returns the linear thread dimension.
            /// </summary>
            int ThreadDimension { get; }

            /// <summary>
            /// The number of segments for reduce operations.
            /// </summary>
            int ReduceSegments { get; }

            /// <summary>
            /// The reduction segment index to write to and read from.
            /// </summary>
            int ReduceSegmentIndex { get; }

            /// <summary>
            /// Executes a barrier in the current context.
            /// </summary>
            void Barrier();
        }

        #endregion

        #region Reduce

        /// <summary cref="GroupExtensions.Reduce{T, TReduction}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T Reduce<T, TReduction, TImpl>(T value)
            where T : unmanaged
            where TReduction : IScanReduceOperation<T>
            where TImpl : struct, IILFunctionImplementation =>
            AllReduce<T, TReduction, TImpl>(value);

        /// <summary cref="GroupExtensions.AllReduce{T, TReduction}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T AllReduce<T, TReduction, TImpl>(T value)
            where T : unmanaged
            where TReduction : IScanReduceOperation<T>
            where TImpl : struct, IILFunctionImplementation
        {
            TImpl impl = default;
            var sharedMemory = SharedMemory.Allocate<T>(impl.ReduceSegments);

            TReduction reduction = default;
            if (impl.IsFirstThread)
                sharedMemory[impl.ReduceSegmentIndex] = reduction.Identity;
            impl.Barrier();

            reduction.AtomicApply(ref sharedMemory[impl.ReduceSegmentIndex], value);

            impl.Barrier();
            return sharedMemory[impl.ReduceSegmentIndex];
        }

        #endregion

        #region Scan

        /// <summary cref="GroupExtensions.ExclusiveScan{T, TScanOperation}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ExclusiveScan<T, TScanOperation, TImpl>(T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
            where TImpl : struct, IILFunctionImplementation =>
            ExclusiveScanWithBoundaries<T, TScanOperation, TImpl>(value, out var _);

        /// <summary cref="GroupExtensions.InclusiveScan{T, TScanOperation}(T)"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T InclusiveScan<T, TScanOperation, TImpl>(T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
            where TImpl : struct, IILFunctionImplementation =>
            InclusiveScanWithBoundaries<T, TScanOperation, TImpl>(value, out var _);

        /// <summary cref="GroupExtensions.ExclusiveScanWithBoundaries{T, TScanOperation}(
        /// T, out ScanBoundaries{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T ExclusiveScanWithBoundaries<T, TScanOperation, TImpl>(
            T value,
            out ScanBoundaries<T> boundaries)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
            where TImpl : struct, IILFunctionImplementation
        {
            TImpl impl = default;
            var sharedMemory = InclusiveScanImplementation<T, TScanOperation, TImpl>(
                value);
            boundaries = new ScanBoundaries<T>(
                sharedMemory[0],
                sharedMemory[Math.Max(0, impl.ThreadDimension - 2)]);
            return impl.IsFirstThread
                ? default(TScanOperation).Identity
                : sharedMemory[impl.ThreadIndex - 1];
        }

        /// <summary cref="GroupExtensions.InclusiveScanWithBoundaries{T, TScanOperation}(
        /// T, out ScanBoundaries{T})"/>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static T InclusiveScanWithBoundaries<T, TScanOperation, TImpl>(
            T value,
            out ScanBoundaries<T> boundaries)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
            where TImpl : struct, IILFunctionImplementation
        {
            TImpl impl = default;
            var sharedMemory = InclusiveScanImplementation<T, TScanOperation, TImpl>(
                value);
            boundaries = new ScanBoundaries<T>(
                sharedMemory[0],
                sharedMemory[impl.ThreadDimension - 1]);
            return sharedMemory[impl.ThreadIndex];
        }

        /// <summary>
        /// Performs a group-wide inclusive scan.
        /// </summary>
        /// <typeparam name="T">The element type.</typeparam>
        /// <typeparam name="TScanOperation">The type of the warp scan logic.</typeparam>
        /// <typeparam name="TImpl">The internal implementation type.</typeparam>
        /// <param name="value">The value to scan.</param>
        /// <returns>The resulting value for the current lane.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ArrayView<T> InclusiveScanImplementation<
            T,
            TScanOperation,
            TImpl>(
            T value)
            where T : unmanaged
            where TScanOperation : struct, IScanReduceOperation<T>
            where TImpl : struct, IILFunctionImplementation
        {
            TImpl impl = default;

            // Load values into shared memory
            var sharedMemory = SharedMemory.Allocate<T>(impl.MaxNumThreads);
            Debug.Assert(
                impl.ThreadDimension <= impl.MaxNumThreads,
                "Invalid group/warp size");
            sharedMemory[impl.ThreadIndex] = value;
            impl.Barrier();

            // First thread performs all operations
            if (impl.IsFirstThread)
            {
                TScanOperation scanOperation = default;
                for (int i = 1; i < impl.ThreadDimension; ++i)
                {
                    sharedMemory[i] = scanOperation.Apply(
                        sharedMemory[i - 1],
                        sharedMemory[i]);
                }
            }
            impl.Barrier();

            return sharedMemory;
        }

        #endregion
    }
}
