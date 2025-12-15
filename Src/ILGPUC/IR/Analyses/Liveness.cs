// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Liveness.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPU.Util;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Analyses;

/// <summary>
/// Represents a live interval for an allocation value.
/// </summary>
/// <param name="Start">The start position (first definition or use).</param>
/// <param name="End">The end position (last use).</param>
readonly record struct LiveInterval(int Start, int End)
{
    /// <summary>
    /// Returns an empty interval.
    /// </summary>
    public static readonly LiveInterval Empty = new(int.MaxValue, int.MinValue);

    /// <summary>
    /// Returns true if this interval is empty.
    /// </summary>
    public bool IsEmpty => Start > End;

    /// <summary>
    /// Returns true if the given position is within this interval.
    /// </summary>
    public bool IsLive(int position) => position >= Start && position <= End;

    /// <summary>
    /// Returns true if this interval overlaps with another interval.
    /// </summary>
    public bool Overlaps(LiveInterval other) =>
        !IsEmpty && !other.IsEmpty &&
        Start <= other.End && other.Start <= End;

    /// <summary>
    /// Extends this interval to include the given position.
    /// </summary>
    public LiveInterval Extend(int position)
    {
        if (IsEmpty)
            return new LiveInterval(position, position);
        return new LiveInterval(Math.Min(Start, position), Math.Max(End, position));
    }

    /// <summary>
    /// Merges this interval with another interval.
    /// </summary>
    public LiveInterval Merge(LiveInterval other)
    {
        if (IsEmpty) return other;
        if (other.IsEmpty) return this;
        return new LiveInterval(Math.Min(Start, other.Start), Math.Max(End, other.End));
    }

    /// <inheritdoc/>
    public override string ToString() =>
        IsEmpty ? "[empty]" : $"[{Start}, {End}]";
}

/// <summary>
/// Stores per-method live intervals for an allocation.
/// </summary>
readonly struct AllocationLiveness(Module module)
{
    private readonly ValueMap<Module, Method, LiveInterval> _methodIntervals =
        module.CreateMap<Method, LiveInterval>();

    /// <summary>
    /// Gets or sets the interval for a specific method.
    /// </summary>
    public LiveInterval this[Method method]
    {
        get => _methodIntervals.TryGetValue(method, out var interval)
            ? interval
            : LiveInterval.Empty;
        set => _methodIntervals[method] = value;
    }

    /// <summary>
    /// Returns true if this allocation has any intervals.
    /// </summary>
    public bool HasIntervals => _methodIntervals.Count > 0;

    /// <summary>
    /// Returns true if this allocation is live in the given method at the given position.
    /// </summary>
    public bool IsLive(Method method, int position)
    {
        if (_methodIntervals.TryGetValue(method, out var interval))
            return interval.IsLive(position);
        return false;
    }

    /// <summary>
    /// Returns true if this allocation overlaps with another in any method.
    /// </summary>
    public bool Overlaps(AllocationLiveness other)
    {
        foreach (var (method, interval) in _methodIntervals)
        {
            if (other._methodIntervals.TryGetValue(method, out var otherInterval))
            {
                if (interval.Overlaps(otherInterval))
                    return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Extends the interval in the given method to include the position.
    /// </summary>
    public void Extend(Method method, int position)
    {
        var current = this[method];
        this[method] = current.Extend(position);
    }

    /// <summary>
    /// Merges another allocation's intervals into this one.
    /// </summary>
    public void Merge(AllocationLiveness other)
    {
        foreach (var (method, otherInterval) in other._methodIntervals)
        {
            var current = this[method];
            this[method] = current.Merge(otherInterval);
        }
    }

    /// <summary>
    /// Enumerates all (method, interval) pairs.
    /// </summary>
    public IEnumerable<(Method Method, LiveInterval Interval)> GetIntervals()
    {
        foreach (var (method, interval) in _methodIntervals)
            yield return (method, interval);
    }
}

/// <summary>
/// Stores liveness information for allocations in a module.
/// </summary>
/// <param name="intervals">Per-method liveness intervals for all allocations.</param>
/// <param name="aliases">Pointer alias information.</param>
readonly struct LivenessInfo(
    GlobalValueMap<AllocationLiveness>? intervals,
    PointerAliases aliases)
{
    /// <summary>
    /// Empty liveness information.
    /// </summary>
    public static readonly LivenessInfo Empty = new(null, PointerAliases.Empty);

    /// <summary>
    /// Returns the per-method liveness for a given allocation.
    /// </summary>
    /// <param name="allocation">The allocation value (Alloca or Global).</param>
    /// <returns>The allocation liveness, or null if not found.</returns>
    public AllocationLiveness? Get<TValue>(TValue allocation)
        where TValue : Value, IAllocationValue
    {
        if (allocation is not Value value || !intervals.HasValue)
            return null;

        return intervals.Value.TryGetValue(value, out var liveness) ? liveness : null;
    }

    /// <summary>
    /// Returns true if the allocation is live in the given method at the given position.
    /// </summary>
    public bool IsLive<TValue>(TValue allocation, Method method, int position)
        where TValue : Value, IAllocationValue
    {
        var liveness = Get(allocation);
        return liveness?.IsLive(method, position) ?? false;
    }

    /// <summary>
    /// Returns true if two allocations have overlapping live ranges in any method.
    /// </summary>
    public bool Overlaps<TFirst, TSecond>(TFirst first, TSecond second)
        where TFirst : Value, IAllocationValue
        where TSecond : Value, IAllocationValue
    {
        var firstLiveness = Get(first);
        var secondLiveness = Get(second);
        if (!firstLiveness.HasValue || !secondLiveness.HasValue)
            return false;
        return firstLiveness.Value.Overlaps(secondLiveness.Value);
    }

    /// <summary>
    /// Returns true if this liveness information is empty.
    /// </summary>
    public bool IsEmpty => intervals is null;

    /// <summary>
    /// Returns the pointer alias information.
    /// </summary>
    public PointerAliases Aliases => aliases;
}

/// <summary>
/// Analyzes the liveness of local and global allocations using per-method interval tracking.
/// </summary>
/// <remarks>
/// This analysis computes per-method live ranges for both local allocations (Alloca) and
/// global allocations (Global). For each allocation, we track an interval per method where
/// it's used. This enables:
/// - Accurate buffer packing: allocations can be packed if they don't overlap in ANY method
/// - Cross-method tracking: local allocations that escape to other methods are tracked
/// - Alias-aware: intervals are extended based on pointer aliases across methods
/// </remarks>
sealed class Liveness
{
    #region Nested Types

    /// <summary>
    /// Represents a numbering of values within a method for interval computation.
    /// </summary>
    private sealed class MethodNumbering
    {
        private readonly ValueMap<Method, BasicBlockValue, int> _valuePositions;

        public MethodNumbering(Method method)
        {
            _valuePositions = method.CreateMap<BasicBlockValue, int>();
            Count = 0;

            // Number all values in reverse post order (blocks and their values)
            foreach (var block in method.Blocks)
            {
                foreach (BasicBlockValue value in block)
                    _valuePositions.Add(value, Count++);
            }
        }

        /// <summary>
        /// Returns the position of a value within the method.
        /// </summary>
        public int GetPosition(BasicBlockValue value) => _valuePositions[value];

        /// <summary>
        /// Returns the total number of positions.
        /// </summary>
        public int Count { get; }
    }

    /// <summary>
    /// Tracks liveness state during analysis of a single method.
    /// </summary>
    private sealed class MethodLivenessState(Method method, LocalAllocations allocations)
    {
        public Method Method => method;
        public LocalAllocations Allocations => allocations;
        public MethodNumbering Numbering { get; } = new(method);
        public ValueMap<Method, BasicBlock, ValueSet<Method, Alloca>> LiveIn { get; } =
            method.CreateMap<BasicBlock, ValueSet<Method, Alloca>>(
                _ => method.CreateSet<Alloca>());
        public ValueMap<Method, BasicBlock, ValueSet<Method, Alloca>> LiveOut { get; } =
            method.CreateMap<BasicBlock, ValueSet<Method, Alloca>>(
                _ => method.CreateSet<Alloca>());
    }

    #endregion

    #region Instance

    private readonly Module _module;
    private readonly GlobalValueMap<AllocationLiveness> _intervals;
    private PointerAliases _aliases;

    /// <summary>
    /// Constructs a new liveness analysis.
    /// </summary>
    private Liveness(Module module)
    {
        _module = module;
        _intervals = module.CreateGlobalMap<AllocationLiveness>();
        _aliases = PointerAliases.Empty;
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the analyzed module.
    /// </summary>
    public Module Module => _module;

    /// <summary>
    /// Returns liveness information for all allocations.
    /// </summary>
    public LivenessInfo Info => new(_intervals, _aliases);

    #endregion

    #region Methods

    /// <summary>
    /// Creates and runs a liveness analysis on the given module.
    /// </summary>
    /// <param name="module">The module to analyze.</param>
    /// <returns>Liveness information for all allocations in the module.</returns>
    public static LivenessInfo Create(Module module)
    {
        var analysis = new Liveness(module);
        analysis.Analyze();
        return analysis.Info;
    }

    /// <summary>
    /// Performs the complete liveness analysis.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void Analyze()
    {
        // Step 1: Compute pointer aliases (includes interprocedural connections)
        _aliases = PointerAliases.Create(_module);

        // Step 2: Analyze all allocations per-method
        AnalyzeAllocations();

        // Step 3: Extend live ranges based on aliases across methods
        ExtendLivenessWithAliases();
    }

    /// <summary>
    /// Analyzes liveness of all allocations (both local Alloca and Global) per-method.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void AnalyzeAllocations()
    {
        // Analyze each method independently
        foreach (var method in _module.Methods)
        {
            if (!method.HasImplementation)
                continue;

            // Get local allocations for this method
            var allocations = LocalAllocations.Create(method.Blocks);
            var state = new MethodLivenessState(method, allocations);
            var numbering = state.Numbering;

            // Analyze local allocations using backwards dataflow
            if (allocations.Length > 0)
                ComputeMethodLiveness(state);

            // Record intervals for local allocations in this method
            foreach (var allocInfo in allocations)
            {
                var alloca = allocInfo.Alloca;
                var liveness = GetOrCreateLiveness(alloca);

                // Find definition point
                int defPosition = numbering.GetPosition(alloca);
                liveness.Extend(method, defPosition);

                // Find all use positions
                foreach (var use in alloca.Uses)
                {
                    if (use.Target is BasicBlockValue bbValue &&
                        bbValue.BasicBlock.Method == method)
                    {
                        int usePosition = numbering.GetPosition(bbValue);
                        liveness.Extend(method, usePosition);
                    }
                }
            }

            // Track globals and ANY allocation used in this method (including from other methods)
            method.ForEachValue<Value<Method>>(value =>
            {
                // Check all operands for allocation references
                foreach (var operand in value.Values)
                {
                    if (operand is IAllocationValue allocation)
                    {
                        var liveness = GetOrCreateLiveness(allocation);

                        // The value using this allocation determines the position
                        if (value is BasicBlockValue bbValue)
                        {
                            int usePosition = numbering.GetPosition(bbValue);
                            liveness.Extend(method, usePosition);
                        }
                    }
                }
            });
        }
    }

    /// <summary>
    /// Computes liveness for local allocations in a single method using backwards dataflow.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static void ComputeMethodLiveness(MethodLivenessState state)
    {
        var method = state.Method;
        var blocks = method.Blocks;

        // Work-list algorithm for backwards dataflow
        var workList = new Queue<BasicBlock>(blocks.Count);
        var onWorkList = method.CreateSet<BasicBlock>();

        // Initialize with all blocks in reverse post order
        foreach (var block in blocks)
        {
            workList.Enqueue(block);
            onWorkList.Add(block);
        }

        // Iterate until fixpoint
        while (workList.Count > 0)
        {
            var block = workList.Dequeue();
            onWorkList.Remove(block);

            if (ProcessBlock(state, block))
            {
                // Live-in changed, add predecessors to work list
                foreach (var pred in block.Predecessors)
                {
                    if (onWorkList.Contains(pred))
                        continue;

                    workList.Enqueue(pred);
                    onWorkList.Add(pred);
                }
            }
        }
    }

    /// <summary>
    /// Processes a single basic block for liveness analysis.
    /// Returns true if live-in set changed.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private static bool ProcessBlock(MethodLivenessState state, BasicBlock block)
    {
        var liveIn = state.LiveIn[block];
        var liveOut = state.LiveOut[block];

        // Remember which allocas were live-in before
        var oldLiveIn = liveIn.Clone();

        // Compute live-out = union of successors' live-in
        liveOut.Clear();
        foreach (var succ in block.Successors)
        {
            var succLiveIn = state.LiveIn[succ];
            foreach (var allocInfo in state.Allocations)
            {
                if (succLiveIn.Contains(allocInfo.Alloca))
                    liveOut.Add(allocInfo.Alloca);
            }
        }

        // Compute live-in starting from live-out
        liveIn.Clear();
        foreach (var allocInfo in state.Allocations)
        {
            if (liveOut.Contains(allocInfo.Alloca))
                liveIn.Add(allocInfo.Alloca);
        }

        // Process block to add uses
        foreach (var allocInfo in state.Allocations)
        {
            var alloca = allocInfo.Alloca;
            // Check if any use of this alloca is in this block
            foreach (var use in alloca.Uses)
            {
                if (use.Target is BasicBlockValue bbValue && bbValue.BasicBlock == block)
                {
                    liveIn.Add(alloca);
                    break;
                }
            }
        }

        // Check if live-in changed
        foreach (var allocInfo in state.Allocations)
        {
            bool wasLive = oldLiveIn.Contains(allocInfo.Alloca);
            bool isLive = liveIn.Contains(allocInfo.Alloca);
            if (wasLive != isLive)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Extends live intervals based on pointer alias information across methods.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private void ExtendLivenessWithAliases()
    {
        if (_aliases.IsEmpty)
            return;

        // For each method, extend intervals based on alias uses
        foreach (var method in _module.Methods)
        {
            if (!method.HasImplementation)
                continue;

            var numbering = new MethodNumbering(method);
            var localAllocs = LocalAllocations.Create(method.Blocks);

            // Get all allocations tracked in this method
            var methodAllocations = InlineList<
                (Value Allocation, AllocationLiveness Liveness)>.Create(
                localAllocs.Length + _module.NumGlobals);

            // Collect local allocations from this method
            foreach (var allocInfo in localAllocs)
            {
                if (_intervals.TryGetValue(allocInfo.Alloca, out var liveness))
                    methodAllocations.Add((allocInfo.Alloca, liveness));
            }

            // Collect global allocations
            foreach (var global in _module.Globals)
            {
                if (_intervals.TryGetValue(global, out var liveness))
                    methodAllocations.Add((global, liveness));
            }

            // Process all allocations that have intervals
            foreach (var (allocation, liveness) in methodAllocations)
            {
                if (allocation is not IAllocationValue allocValue)
                    continue;

                // Get all aliases for this allocation
                var aliases = _aliases[allocValue];
                if (!aliases.HasValue)
                    continue;

                // Extend interval to cover all uses of aliases in this method
                foreach (var alias in aliases)
                {
                    alias.ForEachUseOf<BasicBlockValue>(bbValue =>
                    {
                        if (bbValue.BasicBlock.Method == method)
                        {
                            int usePosition = numbering.GetPosition(bbValue);
                            liveness.Extend(method, usePosition);
                        }
                    });
                }
            }
        }
    }

    /// <summary>
    /// Gets or creates an AllocationLiveness for the given allocation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private AllocationLiveness GetOrCreateLiveness(IAllocationValue allocation)
    {
        var value = allocation as Value;
        if (value is null)
            throw new ArgumentException("Allocation must be a Value", nameof(allocation));

        if (!_intervals.TryGetValue(value, out var liveness))
        {
            liveness = new AllocationLiveness();
            _intervals.Add(value, liveness);
        }

        return liveness;
    }

    #endregion
}
