// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: VectorBufferAllocator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System.Collections.Generic;
using System.Linq;

namespace ILGPUC.Backends.CPU;

/// <summary>
/// Metadata describing pre-allocated buffer pool slots and their assignments.
/// </summary>
/// <param name="Slots">
/// Distinct pool slots: (elementType, slotIndex) pairs used by the launcher
/// to emit pool declarations (e.g., <c>var pool_float_0 = new float[simdWidth]</c>).
/// </param>
/// <param name="ValueAssignments">
/// Maps IR values to their pool parameter names (e.g., <c>"pool_float_0"</c>).
/// </param>
/// <param name="BroadcastAssignments">
/// Maps broadcast temp variable names (e.g., <c>"_bcast_tmp_3"</c>)
/// to their pool parameter names.
/// </param>
sealed record BufferPoolMetadata(
    List<(string ElementType, int SlotIndex)> Slots,
    Dictionary<Value, string> ValueAssignments,
    Dictionary<string, string> BroadcastAssignments)
{
    /// <summary>
    /// Returns the field name of a pool slot. Qualified element type names
    /// (e.g. <c>System.Half</c>) are turned into valid identifiers.
    /// </summary>
    public static string GetSlotName(string elementType, int slotIndex) =>
        $"pool_{elementType.Replace('.', '_')}_{slotIndex}";
}

/// <summary>
/// Performs SSA value liveness analysis and linear-scan register allocation
/// to assign vector buffer pool slots, minimizing total buffer count by
/// sharing storage for values with non-overlapping live ranges.
/// </summary>
sealed class VectorBufferAllocator
{
    private readonly Method _method;
    private readonly CodePlacement _codePlacement;
    private readonly VectorizationAnalysis _analysis;
    private readonly CPULanguageConfiguration _config;
    private readonly CPUBlockSchedule _schedule;

    /// <summary>
    /// Constructs a new vector buffer allocator.
    /// </summary>
    /// <param name="method">The method to allocate buffers for.</param>
    /// <param name="codePlacement">The placement of values in blocks.</param>
    /// <param name="analysis">The vectorization analysis.</param>
    /// <param name="config">The language configuration.</param>
    /// <param name="schedule">The block schedule of the method.</param>
    public VectorBufferAllocator(
        Method method,
        CodePlacement codePlacement,
        VectorizationAnalysis analysis,
        CPULanguageConfiguration config,
        CPUBlockSchedule schedule)
    {
        _method = method;
        _codePlacement = codePlacement;
        _analysis = analysis;
        _config = config;
        _schedule = schedule;
    }

    /// <summary>
    /// Runs liveness analysis and linear-scan allocation, returning pool metadata.
    /// </summary>
    public BufferPoolMetadata Allocate() =>
        LinearScanAllocate(ComputeScheduledIntervals(_schedule));

    /// <summary>
    /// Represents a live interval for a buffer value or broadcast temporary.
    /// </summary>
    private readonly record struct LiveInterval(
        string ElementType,
        int Start,
        int End,
        Value? Value,
        string? BroadcastName);

    /// <summary>
    /// Computes live intervals over the linear emission order of a block
    /// schedule. Every block of the schedule runs once per iteration of its
    /// enclosing loops, so an interval that is live across the boundary of a
    /// loop is extended to cover the whole loop. Phis are written on the
    /// edges into their block (at the terminators of the predecessors) and
    /// phi arguments are read there.
    /// </summary>
    private List<LiveInterval> ComputeScheduledIntervals(CPUBlockSchedule schedule)
    {
        // Positions of values and block terminators in emission order
        var positions = new Dictionary<Value, int>();
        var terminators = new Dictionary<BasicBlock, int>();
        var blockStarts = new Dictionary<BasicBlock, int>();
        int pos = 0;
        foreach (var block in schedule.LinearOrder)
        {
            blockStarts[block] = pos;
            if (_codePlacement.TryGetPlacedBlock(block, out var placedBlock))
            {
                foreach (var value in placedBlock)
                    positions[value] = pos++;
            }
            terminators[block] = pos++;
        }

        // Ranges of all loops, innermost loops first
        var loopRanges = new List<(int Start, int End)>();
        schedule.Loops.ProcessLoops(loop =>
        {
            int start = int.MaxValue, end = -1;
            foreach (var member in loop.AllMembers)
            {
                if (!blockStarts.TryGetValue(member, out int memberStart))
                    continue;
                start = System.Math.Min(start, memberStart);
                end = System.Math.Max(end, terminators[member]);
            }
            if (end >= 0)
                loopRanges.Add((start, end));
        });

        var intervals = new List<LiveInterval>();
        foreach (var block in schedule.LinearOrder)
        {
            if (!_codePlacement.TryGetPlacedBlock(block, out var placedBlock))
                continue;
            foreach (var value in placedBlock)
            {
                if (!NeedsVectorBuffer(value))
                    continue;
                var elemType = GetElementType(value);
                if (elemType == null)
                    continue;

                int start = positions[value];
                int end = start;
                if (value is PhiValue phi)
                {
                    // Written on every incoming edge
                    start = int.MaxValue;
                    for (int i = 0; i < phi.NumArguments; i++)
                    {
                        if (!terminators.TryGetValue(phi.Sources[i], out int t))
                            continue;
                        start = System.Math.Min(start, t);
                        end = System.Math.Max(end, t);
                    }
                    if (start == int.MaxValue)
                        start = positions[value];
                }
                end = System.Math.Max(
                    end, GetLastScheduledUse(value, positions, terminators, 0));

                foreach (var (loopStart, loopEnd) in loopRanges)
                {
                    bool overlaps = start <= loopEnd && end >= loopStart;
                    if (overlaps && (start < loopStart || end > loopEnd))
                    {
                        start = System.Math.Min(start, loopStart);
                        end = System.Math.Max(end, loopEnd);
                    }
                }

                intervals.Add(new LiveInterval(elemType, start, end, value, null));

                var bcastName = GetBroadcastTempName(value);
                var bcastElemType = bcastName != null
                    ? GetBroadcastElementType(value)
                    : null;
                if (bcastName != null && bcastElemType != null)
                {
                    int defPos = positions[value];
                    intervals.Add(new LiveInterval(
                        bcastElemType, defPos, defPos, null, bcastName));
                }
            }
        }
        return intervals;
    }

    /// <summary>
    /// Returns the last position in emission order at which a value is read.
    /// Operands of values that are inlined into their consumers (fused
    /// element addresses and single-use scalar values) are read wherever
    /// those consumers are read.
    /// </summary>
    private int GetLastScheduledUse(
        Value value,
        Dictionary<Value, int> positions,
        Dictionary<BasicBlock, int> terminators,
        int depth)
    {
        int last = -1;
        foreach (var use in value.Uses)
        {
            var target = use.Target;
            switch (target)
            {
                case PhiValue phi:
                    for (int i = 0; i < phi.NumArguments; i++)
                    {
                        if (phi.Arguments[i] == value &&
                            terminators.TryGetValue(phi.Sources[i], out int t))
                        {
                            last = System.Math.Max(last, t);
                        }
                    }
                    break;
                case BasicBlock block:
                    if (terminators.TryGetValue(block, out int term))
                        last = System.Math.Max(last, term);
                    break;
                default:
                    if (positions.TryGetValue(target, out int usePos))
                        last = System.Math.Max(last, usePos);
                    bool inlined = target is LoadElementAddress lea
                        ? IsLeaFusable(lea)
                        : target is PureValue && target.Uses.HasExactlyOne
                            && _analysis.IsScalar(target);
                    if (inlined && depth < 16)
                    {
                        last = System.Math.Max(
                            last,
                            GetLastScheduledUse(
                                target, positions, terminators, depth + 1));
                    }
                    break;
            }
        }
        return last;
    }

    /// <summary>
    /// Determines if a value needs a vector buffer (matches the logic in
    /// <see cref="CPUMethodEmitter.EmitVariableDeclarations"/>).
    /// </summary>
    private bool NeedsVectorBuffer(Value value)
    {
        if (value.Type is VoidType or KindType)
            return false;
        if (value is Parameter)
            return false;
        if (value is PrimitiveValue)
            return false;
        if (value is LoadElementAddress lea && IsLeaFusable(lea))
            return false;
        if (value is GroupIndexValue or SubGroupIndexValue
            or SubGroupLaneIndexValue)
            return false;
        if (value is Store)
            return false;
        if (value is PureValue && value.Uses.HasExactlyOne
            && _analysis.IsScalar(value))
            return false;
        if (_analysis.IsScalar(value) && value is
            (Load or BinaryArithmeticValue or CompareValue
             or GenericAtomic or ConvertValue))
            return false;

        // Only vector values with array allocations
        if (_analysis.IsScalar(value))
            return false;

        // Must be a type that gets allocated as T[]
        return value.Type is PrimitiveType
            || value is CompareValue
            || value.Type is PointerType;
    }

    /// <summary>
    /// Gets the element type name for a vector buffer value.
    /// </summary>
    private string? GetElementType(Value value)
    {
        if (value is CompareValue)
            return "bool";
        if (value.Type is PointerType)
            return "nint";
        if (value.Type is PrimitiveType pt)
            return _config.GetPrimitiveTypeName(pt.BasicValueType);
        return null;
    }

    /// <summary>
    /// Gets the broadcast temp variable name if this value needs one,
    /// or null if not.
    /// </summary>
    private string? GetBroadcastTempName(Value value)
    {
        if (value is BinaryArithmeticValue bin && !_analysis.IsScalar(bin))
        {
            bool lScalar = _analysis.IsScalar(bin.Left);
            bool rScalar = _analysis.IsScalar(bin.Right);
            if (lScalar != rScalar)
                return $"_bcast_{GetVarNameForValue(value)}";
        }
        else if (value is CompareValue cmp && !_analysis.IsScalar(cmp))
        {
            bool lScalar = _analysis.IsScalar(cmp.Left);
            bool rScalar = _analysis.IsScalar(cmp.Right);
            if (lScalar != rScalar)
                return $"_bcast_{GetVarNameForValue(value)}";
        }

        return null;
    }

    /// <summary>
    /// Gets the element type for a broadcast temporary.
    /// </summary>
    private string? GetBroadcastElementType(Value value)
    {
        if (value is BinaryArithmeticValue bin)
        {
            if (bin.Type is PrimitiveType pt)
                return _config.GetPrimitiveTypeName(pt.BasicValueType);
        }
        else if (value is CompareValue cmp)
        {
            // Broadcast temp uses the operand type, not bool
            if (cmp.Left.Type is PrimitiveType cpt)
                return _config.GetPrimitiveTypeName(cpt.BasicValueType);
            return "int";
        }

        return null;
    }

    /// <summary>
    /// Generates a stable variable name for a value (matches GenerationContext logic).
    /// </summary>
    private static string GetVarNameForValue(Value value) =>
        $"tmp_{value.Id}";

    /// <summary>
    /// Runs linear-scan allocation: groups intervals by element type,
    /// sorts by start position, and greedily assigns to the first
    /// non-overlapping slot.
    /// </summary>
    private static BufferPoolMetadata LinearScanAllocate(
        List<LiveInterval> intervals)
    {
        var slotSet = new HashSet<(string, int)>();
        var valueAssignments = new Dictionary<Value, string>();
        var broadcastAssignments = new Dictionary<string, string>();

        // Group by element type
        var groups = intervals
            .GroupBy(i => i.ElementType)
            .ToDictionary(g => g.Key, g => g.OrderBy(i => i.Start).ToList());

        foreach (var (elemType, sortedIntervals) in groups)
        {
            // Each slot tracks its last end position
            var slotEnds = new List<int>();

            foreach (var interval in sortedIntervals)
            {
                // Find first slot whose last interval ended strictly before
                // this one starts. Strict inequality is required because
                // broadcast temps (which write to the slot) are emitted at
                // the same position as the operation that uses the previous
                // slot value. Equality would allow the broadcast to
                // overwrite data that hasn't been consumed yet.
                int assignedSlot = -1;
                for (int s = 0; s < slotEnds.Count; s++)
                {
                    if (slotEnds[s] < interval.Start)
                    {
                        assignedSlot = s;
                        slotEnds[s] = interval.End;
                        break;
                    }
                }

                if (assignedSlot < 0)
                {
                    assignedSlot = slotEnds.Count;
                    slotEnds.Add(interval.End);
                }

                var poolName = BufferPoolMetadata.GetSlotName(elemType, assignedSlot);
                slotSet.Add((elemType, assignedSlot));

                if (interval.Value != null)
                    valueAssignments[interval.Value] = poolName;
                if (interval.BroadcastName != null)
                    broadcastAssignments[interval.BroadcastName] = poolName;
            }
        }

        var slots = slotSet
            .OrderBy(s => s.Item1)
            .ThenBy(s => s.Item2)
            .ToList();

        return new BufferPoolMetadata(slots, valueAssignments, broadcastAssignments);
    }

    /// <summary>
    /// Returns true if a LoadElementAddress is only consumed by Load/Store.
    /// </summary>
    private static bool IsLeaFusable(LoadElementAddress lea)
    {
        foreach (var use in lea.Uses)
        {
            if (use.Target is not (Load or Store))
                return false;
        }
        return true;
    }
}
