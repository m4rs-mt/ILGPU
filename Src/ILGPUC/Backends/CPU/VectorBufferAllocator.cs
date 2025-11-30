// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2017-2025 ILGPU Project
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
    Dictionary<string, string> BroadcastAssignments);

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

    /// <summary>
    /// Constructs a new vector buffer allocator.
    /// </summary>
    public VectorBufferAllocator(
        Method method,
        CodePlacement codePlacement,
        VectorizationAnalysis analysis,
        CPULanguageConfiguration config)
    {
        _method = method;
        _codePlacement = codePlacement;
        _analysis = analysis;
        _config = config;
    }

    /// <summary>
    /// Runs liveness analysis and linear-scan allocation, returning pool metadata.
    /// </summary>
    public BufferPoolMetadata Allocate()
    {
        // Step 1: Build a linear position map for all values
        var positionMap = BuildPositionMap();

        // Step 2: Compute live intervals for vector buffer values
        int maxPosition = positionMap.Count > 0
            ? positionMap.Values.Max() : 0;
        var intervals = ComputeLiveIntervals(positionMap, maxPosition);

        // Step 3: Group by element type and run linear-scan allocation
        return LinearScanAllocate(intervals);
    }

    /// <summary>
    /// Assigns a sequential position number to every value by iterating blocks
    /// in reverse post order.
    /// </summary>
    private Dictionary<Value, int> BuildPositionMap()
    {
        var map = new Dictionary<Value, int>();
        int pos = 0;

        foreach (var block in _method.Blocks)
        {
            if (!_codePlacement.TryGetPlacedBlock(block, out var placedBlock))
                continue;

            for (int j = 0; j < placedBlock.Count; j++)
            {
                var value = placedBlock[j];
                map[value] = pos++;
            }
        }

        return map;
    }

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
    /// Computes live intervals for all non-scalar vector values that would
    /// normally be allocated as <c>new T[SIMDWidth]</c>.
    /// </summary>
    private List<LiveInterval> ComputeLiveIntervals(
        Dictionary<Value, int> positionMap,
        int maxPosition)
    {
        var intervals = new List<LiveInterval>();

        foreach (var block in _method.Blocks)
        {
            if (!_codePlacement.TryGetPlacedBlock(block, out var placedBlock))
                continue;

            for (int j = 0; j < placedBlock.Count; j++)
            {
                var value = placedBlock[j];

                // Apply the same skip logic as EmitVariableDeclarations
                if (!NeedsVectorBuffer(value))
                    continue;

                var elemType = GetElementType(value);
                if (elemType == null)
                    continue;

                int defPos = positionMap[value];
                int lastUse = ComputeLastUse(
                    value, positionMap, defPos, maxPosition);

                // For loop-carried phi values (multi-argument phis): extend
                // both start and end to cover the entire method. The
                // linear-scan allocator treats code as a single linear
                // sequence but loops re-execute header block values on
                // every iteration. Without extending the phi's range to
                // [0, maxPosition], header intermediates can reuse the
                // phi's buffer and clobber it mid-iteration.
                int effectiveStart = defPos;
                if (value is PhiValue phi2 && phi2.NumArguments > 1)
                {
                    effectiveStart = 0;
                    lastUse = maxPosition;
                }

                intervals.Add(new LiveInterval(
                    elemType, effectiveStart, lastUse, value, null));

                // Check for broadcast temporaries
                var bcastName = GetBroadcastTempName(value);
                if (bcastName != null)
                {
                    var bcastElemType = GetBroadcastElementType(value);
                    if (bcastElemType != null)
                    {
                        // Broadcast temp is only used at the definition point
                        // (filled then immediately consumed by the operation)
                        intervals.Add(new LiveInterval(
                            bcastElemType, defPos, defPos, null, bcastName));
                    }
                }
            }
        }

        return intervals;
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
    /// Computes the last use position for a value by scanning its uses.
    /// For phi values (loop-carried variables), also includes the positions
    /// of the phi's source values so the live range covers the full loop body
    /// including back-edge updates.
    /// </summary>
    private static int ComputeLastUse(
        Value value,
        Dictionary<Value, int> positionMap,
        int defPos,
        int maxPosition)
    {
        int lastUse = defPos;

        foreach (var use in value.Uses)
        {
            if (positionMap.TryGetValue(use.Target, out int usePos))
            {
                if (usePos > lastUse)
                    lastUse = usePos;
            }

            // Fusable LEAs are inlined into their consuming Load/Store.
            // The value feeding into the LEA is needed until that
            // Load/Store executes — follow through to LEA consumers.
            if (use.Target is LoadElementAddress lea)
            {
                foreach (var leaUse in lea.Uses)
                {
                    if (positionMap.TryGetValue(leaUse.Target, out int leaUsePos)
                        && leaUsePos > lastUse)
                    {
                        lastUse = leaUsePos;
                    }
                }
            }

            // PureValues that are not in the position map (scalar single-use
            // values skipped by NeedsVectorBuffer) still consume their operands.
            // Follow through transitive PureValue uses to find the ultimate
            // positioned consumer so the operand's pool slot isn't freed early.
            if (!positionMap.ContainsKey(use.Target) && use.Target is PureValue pv)
            {
                int transitive = ComputeTransitiveLastUse(
                    pv, positionMap, lastUse);
                if (transitive > lastUse)
                    lastUse = transitive;
            }

            // BasicBlock termination values (branch conditions, return values)
            // are children of the BasicBlock, which isn't in the position map.
            // Extend the live range to the block's maximum positioned value.
            if (use.Target is BasicBlock bb)
            {
                // Find the last positioned value in this block
                foreach (var bbUse in bb.Uses)
                {
                    if (positionMap.TryGetValue(bbUse.Target, out int bbUsePos)
                        && bbUsePos > lastUse)
                    {
                        lastUse = bbUsePos;
                    }
                }
                // If no positioned uses found but the block has values,
                // use maxPosition as a safe upper bound
                if (lastUse == defPos)
                    lastUse = maxPosition;
            }
        }

        // For PhiValue: extend the live range to include the positions of
        // all phi source values. In loops, the back-edge source (e.g.
        // i+step) is at a later position in the loop body. Without this,
        // the allocator may reuse the phi's buffer for intermediate header
        // values that execute before the back-edge update.
        if (value is PhiValue phi)
        {
            for (int i = 0; i < phi.NumArguments; i++)
            {
                var src = phi.Arguments[i];
                if (positionMap.TryGetValue(src, out int srcPos))
                {
                    if (srcPos > lastUse)
                        lastUse = srcPos;
                }
            }
        }

        // If this value is a source argument of a multi-argument phi
        // (loop-carried), extend the live range to the maximum position.
        // The loop handler emits back-edge Selects that read this value
        // AFTER all header block values have been emitted, but those
        // Selects are not in the position map. Extending to maxPosition
        // ensures the buffer isn't reused by later header values (like
        // comparison broadcasts) before the Select consumes it.
        foreach (var use in value.Uses)
        {
            if (use.Target is PhiValue usePhi && usePhi.NumArguments > 1)
            {
                lastUse = maxPosition;
                break;
            }
        }

        // If this value is used by a value in a loop header block
        // (a block that has a back-edge: one of its successors'
        // successors points back to itself), the header is re-emitted
        // on each loop iteration. The value must survive the entire
        // loop to remain valid across re-emissions.
        if (lastUse < maxPosition)
        {
            foreach (var use in value.Uses)
            {
                // Find the block containing the use by walking
                // transitive users until we find a BasicBlockValue.
                BasicBlock? useBlock = FindUseBlock(use.Target, 4);
                if (useBlock is null) continue;

                // Check if useBlock is a loop header (has back-edge)
                foreach (var succ in useBlock.Successors)
                {
                    foreach (var succSucc in succ.Successors)
                    {
                        if (succSucc == useBlock)
                        {
                            lastUse = maxPosition;
                            goto doneLoopCheck;
                        }
                    }
                }
            }
            doneLoopCheck:;
        }

        return lastUse;
    }

    /// <summary>
    /// Follows PureValue use chains to find the maximum position of the
    /// ultimate positioned consumer. PureValues that are not in the position
    /// map (e.g., scalar single-use values) create invisible gaps in the use
    /// chain — without this follow-through, the operand's live range ends
    /// too early and its pool slot is reused prematurely.
    /// </summary>
    private static int ComputeTransitiveLastUse(
        PureValue pv,
        Dictionary<Value, int> positionMap,
        int fallback,
        int depth = 8)
    {
        if (depth <= 0)
            return fallback;

        int result = fallback;
        foreach (var use in pv.Uses)
        {
            if (positionMap.TryGetValue(use.Target, out int pos))
            {
                if (pos > result)
                    result = pos;
            }
            else if (use.Target is PureValue childPv)
            {
                int childPos = ComputeTransitiveLastUse(
                    childPv, positionMap, fallback, depth - 1);
                if (childPos > result)
                    result = childPos;
            }
        }
        return result;
    }

    /// <summary>
    /// Walks transitive users to find the BasicBlock containing a value.
    /// </summary>
    private static BasicBlock? FindUseBlock(Value value, int depth)
    {
        if (depth <= 0) return null;
        if (value is BasicBlockValue bbv) return bbv.BasicBlock;
        foreach (var u in value.Uses)
        {
            var block = FindUseBlock(u.Target, depth - 1);
            if (block != null) return block;
        }
        return null;
    }

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

                var poolName = $"pool_{elemType}_{assignedSlot}";
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
