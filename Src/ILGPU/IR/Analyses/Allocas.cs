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
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Analyses
{
    /// <summary>
    /// Represents information about an alloca node.
    /// </summary>
    public readonly struct AllocaInformation
    {
        #region Instance

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

        #endregion

        #region Properties

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

        #endregion
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
        /// Returns true if the given alloca is contained in this collection.
        /// </summary>
        /// <param name="alloca">The alloca.</param>
        /// <returns>True, if the given alloca is contained in this collection.</returns>
        public bool Contains(Alloca alloca)
        {
            foreach (var allocaInfo in Allocas)
            {
                if (allocaInfo.Alloca == alloca)
                    return true;
            }
            return false;
        }

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

    /// <summary>
    /// Represents a lightweight analysis to determine alignment
    /// </summary>
    public readonly struct AllocaAlignments
    {
        #region Static

        /// <summary>
        /// Determines the initial alloca alignment based on the type of the allocation.
        /// </summary>
        /// <param name="alloca">
        /// The alloca to determine to alignment information for.
        /// </param>
        /// <returns>The initial alignment in bytes.</returns>
        public static int GetInitialAlignment(Alloca alloca) =>
            GetAllocaTypeAlignment(alloca.AllocaType);

        /// <summary>
        /// Determines the allocation alignment information based on the given type.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <returns>The compatible allocation alignment in bytes.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static int GetAllocaTypeAlignment(TypeNode type) =>
            // Assume that we can align the type to an appropriate power of
            // 2 if the type size is compatible
            Utilities.IsPowerOf2(type.Size)
            ? Math.Max(type.Alignment, type.Size)
            : type.Alignment;

        /// <summary>
        /// Tries to determine type information that can be used to compute a compatible
        /// allocation type alignment using
        /// <see cref="GetAllocaTypeAlignment(TypeNode)"/>.
        /// </summary>
        /// <param name="value">The value to get the type information for.</param>
        /// <returns>The type, if the value is supported, null otherwise.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode TryGetAnalysisType(Value value) =>
            value switch
            {
                PhiValue phiValue => phiValue.Type,
                PointerCast cast => cast.TargetElementType,
                AddressSpaceCast cast =>
                    (cast.TargetType as IAddressSpaceType).ElementType,
                NewView newView => newView.ViewElementType,
                ViewCast cast => cast.TargetElementType,
                SubViewValue subView => subView.ElementType,
                LoadElementAddress lea when lea.IsPointerAccess || lea.IsViewAccess =>
                    lea.ElementType,
                // AlignViewTo values cannot be mapped to a supported analysis type since
                // this value specifies the alignment in bytes explicitly
                AlignViewTo _ => null,
                _ => null
            };

        /// <summary>
        /// Creates a new allocation alignments analysis using a default stack processing
        /// capacity of 16 elements.
        /// </summary>
        public static AllocaAlignments Create() => Create(16);

        /// <summary>
        /// Creates a new allocation alignments analysis.
        /// </summary>
        /// <param name="capacity">The initial stack processing capacity.</param>
        public static AllocaAlignments Create(int capacity) =>
            new AllocaAlignments(capacity);

        #endregion

        #region Instance

        private readonly HashSet<Value> visited;
        private readonly Stack<Value> toProcess;

        /// <summary>
        /// Constructs a new alloca allignment analysis.
        /// </summary>
        /// <param name="capacity">The initial stack capacity.</param>
        private AllocaAlignments(int capacity)
        {
            visited = new HashSet<Value>();
            toProcess = new Stack<Value>(capacity);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Computes detailed allocation alignment information using all aliases.
        /// </summary>
        /// <returns>The maximium alignment of the allocation.</returns>
        public readonly int ComputeAllocaAlignment(Alloca alloca)
        {
            alloca.AssertNotNull(alloca);

            // Determine the initial alloca alignment and push all uses
            int alignment = GetInitialAlignment(alloca);
            foreach (Value use in alloca.Uses)
                toProcess.Push(use);

            // Continue our search until we have seen all transitively reachable uses
            while (toProcess.Count > 0)
            {
                var current = toProcess.Pop();
                TypeNode type;

                // Check whether we have already seen this value
                if (!visited.Add(current))
                    continue;

                if (current is AlignViewTo alignTo)
                {
                    // If this is a view alignment value that explicitly specifies
                    // an alignment, you this alignment value instead of an automatically
                    // determined alignment based on type information
                    alignment = Math.Max(alignment, alignTo.GetAlignmentConstant());
                }
                else if ((type = TryGetAnalysisType(current)) != null)
                {
                    // Determine the maximal alignment based on the current alignment and
                    // the allocation-specific alignment of analysis type
                    alignment = Math.Max(alignment, GetAllocaTypeAlignment(type));
                }
                else
                {
                    // Check whether we can skip this value since it will not contribute
                    // to the alloca alignment info
                    continue;
                }

                // Continue the search by pushing all uses onto the stack
                foreach (Value use in current.Uses)
                    toProcess.Push(use);
            }

            // Clear all temporary data structures
            visited.Clear();
            alloca.Assert(toProcess.Count == 0 && visited.Count == 0);

            return alignment;
        }

        #endregion
    }
}
