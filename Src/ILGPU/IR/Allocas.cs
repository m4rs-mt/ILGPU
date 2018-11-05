// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: Allocas.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ILGPU.IR
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
        /// <param name="elementSize">The element size.</param>
        internal AllocaInformation(
            int index,
            Alloca alloca,
            int elementSize)
        {
            Index = index;
            Alloca = alloca;
            ElementSize = elementSize;
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
        /// Returns true iff this is an array.
        /// </summary>
        public bool IsArray => ArraySize > 1;

        /// <summary>
        /// Returns the number 
        /// </summary>
        public int ArraySize
        {
            get
            {
                var arrayLength = Alloca.ArrayLength;
                return arrayLength.ResolveAs<PrimitiveValue>().Int32Value;
            }
        }

        /// <summary>
        /// Returns the element size in bytes of a single element.
        /// </summary>
        public int ElementSize { get; }

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
        public AllocaInformation this[int index] =>
            Allocas[index];

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
        /// <param name="scope">The parent scope.</param>
        /// <param name="abi">The ABI specification.</param>
        public static Allocas Create(Scope scope, ABI abi)
        {
            return new Allocas(
                scope ?? throw new ArgumentNullException(nameof(scope)),
                abi ?? throw new ArgumentNullException(nameof(abi)));
        }

        /// <summary>
        /// Creates an alloca analysis in a separate task.
        /// </summary>
        /// <param name="scope">The parent scope.</param>
        /// <param name="abi">The ABI specification.</param>
        public static Task<Allocas> CreateAsync(Scope scope, ABI abi)
        {
            return Task.Run(() => Create(scope, abi));
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new analysis.
        /// </summary>
        /// <param name="scope">The current scope.</param>
        /// <param name="abi">The ABI specification.</param>
        private Allocas(Scope scope, ABI abi)
        {
            if (!scope.Entry.IsTopLevel)
                throw new NotSupportedException(ErrorMessages.AllocaAnalysisRequiresTopLevelFunction);

            var localAllocations = ImmutableArray.CreateBuilder<AllocaInformation>(20);
            var sharedAllocations = ImmutableArray.CreateBuilder<AllocaInformation>(20);

            int localMemorySize = 0;
            int sharedMemorySize = 0;

            foreach (var node in scope)
            {
                if (node is Alloca alloca)
                {
                    switch (alloca.AddressSpace)
                    {
                        case MemoryAddressSpace.Local:
                            AddAllocation(abi, alloca, localAllocations, ref localMemorySize);
                            break;
                        case MemoryAddressSpace.Shared:
                            AddAllocation(abi, alloca, sharedAllocations, ref sharedMemorySize);
                            break;
                        default:
                            Debug.Assert(false, "Invalid address space");
                            break;
                    }
                }
            }

            LocalAllocations = new AllocaKindInformation(
                localAllocations.ToImmutable(),
                localMemorySize);
            SharedAllocations = new AllocaKindInformation(
                sharedAllocations.ToImmutable(),
                sharedMemorySize);
        }

        /// <summary>
        /// Creates and adds a new allocation to the given list.
        /// </summary>
        /// <param name="abi">The current ABI.</param>
        /// <param name="alloca">The current alloca.</param>
        /// <param name="builder">The target builder.</param>
        /// <param name="memorySize">The current memory size.</param>
        private static void AddAllocation(
            ABI abi,
            Alloca alloca,
            ImmutableArray<AllocaInformation>.Builder builder,
            ref int memorySize)
        {
            var info = new AllocaInformation(
                builder.Count,
                alloca,
                abi.GetSizeOf(alloca.AllocaType));
            memorySize += info.TotalSize;
            builder.Add(info);
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
