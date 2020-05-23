// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Allocas.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Analyses.ControlFlowDirection;
using ILGPU.IR.Analyses.TraversalOrders;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using System;
using System.Collections.Immutable;
using System.Diagnostics;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// Represents information about an alloca node.
    /// </summary>
    public readonly struct AllocaInformation
    {
        /// <summary>
        /// Constructs new alloca information.
        /// </summary>
        /// <param name="index">The allocation index.</param>
        /// <param name="alloca">The alloca node.</param>
        internal AllocaInformation(int index, Alloca alloca)
        {
            Index = index;
            Alloca = alloca;

            ArraySize = alloca.IsArrayAllocation(out var length)
                ? length.Int32Value
                : alloca.IsSimpleAllocation
                    ? 1
                    : throw new NotSupportedException(
                        ErrorMessages.NotSupportedDynamicAllocation);
        }

        /// <summary>
        /// Returns the allocation index.
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// Returns the alloca node.
        /// </summary>
        public Alloca Alloca { get; }

        /// <summary>
        /// Returns true if this is an array.
        /// </summary>
        public bool IsArray => ArraySize > 1;

        /// <summary>
        /// Returns true if this is an array with dynamic length.
        /// </summary>
        public bool IsDynamicArray => ArraySize < 0;

        /// <summary>
        /// Returns the number 
        /// </summary>
        public int ArraySize { get; }

        /// <summary>
        /// Returns the element size in bytes of a single element.
        /// </summary>
        public int ElementSize => Alloca.AllocaType.Size;

        /// <summary>
        /// Returns the element alignment in bytes of a single element.
        /// </summary>
        public int ElementAlignment => Alloca.AllocaType.Alignment;

        /// <summary>
        /// Returns the total size in bytes.
        /// </summary>
        public int TotalSize => ElementSize * ArraySize;

        /// <summary>
        /// Returns the element type.
        /// </summary>
        public TypeNode ElementType => Alloca.AllocaType;
    }

    /// <summary>
    /// Represents information about a whole category of alloca nodes.
    /// </summary>
    public readonly struct AllocaKindInformation
    {
        /// <summary>
        /// Constructs new alloca information.
        /// </summary>
        /// <param name="allocas">The alloca nodes.</param>
        /// <param name="totalSize">The total size.</param>
        internal AllocaKindInformation(
            ImmutableArray<AllocaInformation> allocas,
            int totalSize)
        {
            Allocas = allocas;
            TotalSize = totalSize;
        }

        /// <summary>
        /// Returns the alloca nodes.
        /// </summary>
        public ImmutableArray<AllocaInformation> Allocas { get; }

        /// <summary>
        /// Returns the i-th allocations.
        /// </summary>
        /// <param name="index">The index.</param>
        /// <returns>The resolved alloca information.</returns>
        public AllocaInformation this[int index] => Allocas[index];

        /// <summary>
        /// Returns the number of allocations.
        /// </summary>
        public int Length => Allocas.Length;

        /// <summary>
        /// Returns the element size in bytes of a single element.
        /// </summary>
        public int TotalSize { get; }

        /// <summary>
        /// Returns an enumerator to enumerate all allocas.
        /// </summary>
        /// <returns>An enumerator to enumerate all allocas.</returns>
        public ImmutableArray<AllocaInformation>.Enumerator GetEnumerator() =>
            Allocas.GetEnumerator();
    }

    /// <summary>
    /// Implements an alloca analysis to resolve information
    /// about alloca nodes.
    /// </summary>
    public sealed class Allocas
    {
        #region Shared

        /// <summary>
        /// Creates an alloca analysis.
        /// </summary>
        /// <typeparam name="TOrder">The traversal order.</typeparam>
        /// <typeparam name="TDirection">The control-flow direction.</typeparam>
        /// <param name="collection">The block collection.</param>
        public static Allocas Create<TOrder, TDirection>(
            in BasicBlockCollection<TOrder, TDirection> collection)
            where TOrder : struct, ITraversalOrder
            where TDirection : struct, IControlFlowDirection
        {
            var localAllocations = ImmutableArray.CreateBuilder<
                AllocaInformation>(20);
            var sharedAllocations = ImmutableArray.CreateBuilder<
                AllocaInformation>(20);
            var dynamicSharedAllocations = ImmutableArray.CreateBuilder<
                AllocaInformation>(20);

            int localMemorySize = 0;
            int sharedMemorySize = 0;

            foreach (Value value in collection.Values)
            {
                if (value is Alloca alloca)
                {
                    switch (alloca.AddressSpace)
                    {
                        case MemoryAddressSpace.Local:
                            AddAllocation(
                                alloca,
                                localAllocations,
                                ref localMemorySize);
                            break;
                        case MemoryAddressSpace.Shared:
                            AddAllocation(
                                alloca,
                                sharedAllocations,
                                ref sharedMemorySize,
                                dynamicSharedAllocations);
                            break;
                        default:
                            Debug.Assert(false, "Invalid address space");
                            break;
                    }
                }
            }

            return new Allocas(
                new AllocaKindInformation(
                    localAllocations.ToImmutable(),
                    localMemorySize),
                new AllocaKindInformation(
                    sharedAllocations.ToImmutable(),
                    sharedMemorySize),
                new AllocaKindInformation(
                    dynamicSharedAllocations.ToImmutable(),
                    0));
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new analysis.
        /// </summary>
        public Allocas(
            in AllocaKindInformation localAllocations,
            in AllocaKindInformation sharedAllocations,
            in AllocaKindInformation dynamicSharedAllocations)
        {
            LocalAllocations = localAllocations;
            SharedAllocations = sharedAllocations;
            DynamicSharedAllocations = dynamicSharedAllocations;
        }

        /// <summary>
        /// Creates and adds a new allocation to the given list.
        /// </summary>
        /// <param name="alloca">The current alloca.</param>
        /// <param name="builder">The target builder.</param>
        /// <param name="memorySize">The current memory size.</param>
        /// <param name="dynamicBuilder">
        /// The target builder for dynamic allocations.
        /// </param>
        private static void AddAllocation(
            Alloca alloca,
            ImmutableArray<AllocaInformation>.Builder builder,
            ref int memorySize,
            ImmutableArray<AllocaInformation>.Builder dynamicBuilder = null)
        {
            var info = new AllocaInformation(builder.Count, alloca);
            if (info.IsDynamicArray)
            {
                alloca.AssertNotNull(dynamicBuilder);
                dynamicBuilder.Add(info);
            }
            else
            {
                builder.Add(info);
                memorySize += info.TotalSize;
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns all location allocations.
        /// </summary>
        public AllocaKindInformation LocalAllocations { get; }

        /// <summary>
        /// Returns all shared allocations.
        /// </summary>
        public AllocaKindInformation SharedAllocations { get; }

        /// <summary>
        /// Returns all dynamic shared allocations.
        /// </summary>
        public AllocaKindInformation DynamicSharedAllocations { get; }

        /// <summary>
        /// Returns the total local memory size in bytes.
        /// </summary>
        public int LocalMemorySize => LocalAllocations.TotalSize;

        /// <summary>
        /// Returns the total shared memory size in bytes.
        /// </summary>
        public int SharedMemorySize => SharedAllocations.TotalSize;

        #endregion
    }
}
