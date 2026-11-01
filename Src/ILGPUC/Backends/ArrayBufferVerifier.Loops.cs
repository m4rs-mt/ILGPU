// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: ArrayBufferVerifier.Loops.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPUC.IR;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using ILGPUC.Roslyn.Analysis;
using System.Collections.Generic;
using System.Globalization;

namespace ILGPUC.Backends;

/// <remarks>
/// An array allocated in a loop, directly or by a method called in it, uses the
/// same buffer in every iteration. The array of an iteration becomes stale when
/// its allocation runs again, and every use of a value that can refer to a stale
/// array is rejected. Arrays reach later iterations through phis, and pure
/// values refer to the arrays of their operands. An array stored to memory
/// before its allocation can run again is rejected, as a later iteration could
/// load it. An allocation runs where the frontend clears the elements of an
/// array, which it does in loops and in all methods but kernels, or where a
/// method returning or storing its arrays is called. Otherwise, it can run
/// anywhere in the blocks dominating the uses of the array.
/// </remarks>
static partial class ArrayBufferVerifier
{
    /// <summary>
    /// A position in a method: the phis of a block (-1), the index of a value in
    /// its block, or the number of its values for its termination.
    /// </summary>
    private readonly record struct Position(BasicBlock Block, int Index);

    /// <summary>
    /// How the arrays that a method allocates during a call, directly or through
    /// the methods it calls, escape it, and the buffer of an escaping array.
    /// </summary>
    private readonly record struct Allocation(Escape Escape, Global? Global);

    /// <summary>
    /// The arrays of a buffer in a method, or of the calls of a method returning
    /// or storing the arrays it allocates.
    /// </summary>
    private sealed class Arrays(Global global, Method? callee)
    {
        public Global Global { get; } = global;
        public Method? Callee { get; } = callee;

        /// <summary>
        /// The positions at which the allocation can run, per block.
        /// </summary>
        public Dictionary<BasicBlock, List<int>> Allocations { get; } = [];

        /// <summary>
        /// The values that are no pure values and can refer to an array, and
        /// whether they can refer to a stale array where they are defined.
        /// </summary>
        public Dictionary<Value, bool> Bases { get; } = [];

        /// <summary>
        /// The blocks entered after the definitions of bases, without and after
        /// an allocation.
        /// </summary>
        public Dictionary<Value, (HashSet<BasicBlock>, HashSet<BasicBlock>)>
            Reaches { get; } = [];

        public void Allocate(Position position)
        {
            if (!Allocations.TryGetValue(position.Block, out var indices))
                Allocations.Add(position.Block, indices = []);
            indices.Add(position.Index);
        }

        /// <summary>
        /// Returns true if the allocation can run in a block at an index from the
        /// start to the end, both inclusive.
        /// </summary>
        public bool Allocates(BasicBlock block, int start, int end) =>
            Allocations.TryGetValue(block, out var indices) &&
            indices.Exists(index => index >= start && index <= end);
    }

    /// <summary>
    /// Returns how the arrays that the given method allocates during a call
    /// escape it. A method whose analysis is in progress, as in a call cycle, has
    /// no escaping arrays: a method of a cycle is rejected if its arrays escape.
    /// </summary>
    private static Allocation GetAllocation(Context context, Method method)
    {
        if (context.Allocations.TryGetValue(method, out var allocation))
            return allocation;
        context.Allocations.Add(method, default);

        Escape escape = default;
        Global? global = null;
        void Add(Escape other, Global? source)
        {
            escape = new(escape.Stored | other.Stored, escape.Returned | other.Returned);
            if (other.Stored || other.Returned)
                global ??= source;
        }
        foreach (var value in GetValues(context, method))
        {
            if (value is Global { IsAllocatedPerCopy: true } buffer)
            {
                Add(Trace(context, method, [buffer]), buffer);
            }
            else if (value is MethodCall { Target.HasImplementation: true } call)
            {
                var inner = GetAllocation(context, call.Target);
                Add(new(inner.Escape.Stored, false), inner.Global);
                if (inner.Escape.Returned && HoldsAddress(call.Type))
                    Add(Trace(context, method, [call]), inner.Global);
            }
        }
        context.Allocations[method] = allocation = new(escape, global);
        return allocation;
    }

    /// <summary>
    /// Throws if the given method uses an array allocated in a loop after its
    /// allocation has run again.
    /// </summary>
    private static void VerifyLoops(Context context, Method method)
    {
        var loops = method.CreateLoops();
        if (loops.TopLevelLoops.Length < 1)
            return;

        var analysis = new LoopAnalysis(context, method);
        foreach (var arrays in analysis.GetArrays())
        {
            bool inLoop = false;
            foreach (var block in arrays.Allocations.Keys)
                inLoop |= loops.GetLoop(block) is not null;
            if (!inLoop || !analysis.UsesStaleArray(arrays))
                continue;
            throw new KernelCompilationException(
                KernelDiagnostic.UnsupportedConstructId,
                string.Format(
                    CultureInfo.InvariantCulture,
                    ErrorMessages.KernelDiagnosticArrayReusedInLoop,
                    GetDisplayName(method)),
                arrays.Global.Location);
        }
    }

    /// <summary>
    /// Finds the uses of stale arrays in a method.
    /// </summary>
    private sealed class LoopAnalysis
    {
        private readonly Context _context;
        private readonly Method _method;
        private readonly Dictionary<Value, Position> _positions = [];
        private readonly Dictionary<BasicBlock, int> _ends = [];
        private readonly Dictionary<Value, HashSet<Value>> _leaves = [];

        public LoopAnalysis(Context context, Method method)
        {
            _context = context;
            _method = method;
            foreach (var block in method.Blocks)
            {
                int index = 0;
                foreach (var (value, _) in block)
                    _positions[value] = new(block, value is PhiValue ? -1 : index++);
                _ends[block] = index;
            }
        }

        private Position End(BasicBlock block) => new(block, _ends[block]);

        /// <summary>
        /// Returns the arrays of all allocations of the method with the positions
        /// at which they can run.
        /// </summary>
        public List<Arrays> GetArrays()
        {
            var result = new List<Arrays>();
            var callees = new Dictionary<Method, Arrays>();
            foreach (var block in _method.Blocks)
            {
                foreach (var (value, _) in block)
                {
                    if (value is not MethodCall { Target.HasImplementation: true } call ||
                        GetAllocation(_context, call.Target).Global is not { } global)
                    {
                        continue;
                    }
                    if (!callees.TryGetValue(call.Target, out var arrays))
                    {
                        callees.Add(call.Target, arrays = new(global, call.Target));
                        result.Add(arrays);
                    }
                    arrays.Allocate(_positions[call]);
                }
            }
            foreach (var value in GetValues(_context, _method))
            {
                if (value is not Global { IsAllocatedPerCopy: true } global)
                    continue;
                var arrays = new Arrays(global, null);
                if (!TryAllocateAtClearings(arrays))
                    AllocateInDominators(arrays);
                result.Add(arrays);
            }
            return result;
        }

        /// <summary>
        /// Lets the allocation of a buffer run at the stores of the blocks that
        /// clear all its elements, which the frontend emits for arrays of up to a
        /// fixed number of elements where it allocates them.
        /// </summary>
        /// <returns>False if no block clears the buffer.</returns>
        private bool TryAllocateAtClearings(Arrays arrays)
        {
            var global = arrays.Global;
            if (global.ArrayLength is not { Int32Value: >= 1 and var length } ||
                length > Frontend.CodeGenerator.MaxNumArrayElementsToReinitialize)
            {
                return false;
            }

            bool found = false;
            foreach (var block in _method.Blocks)
            {
                var elements = new HashSet<int>();
                var stores = new List<Position>();
                foreach (var (value, _) in block)
                {
                    if (value is Store store &&
                        (store.Value is NullValue || store.Value.IsPrimitiveValue(0)) &&
                        TryGetElement(store.Target, global, out int element))
                    {
                        elements.Add(element);
                        stores.Add(_positions[store]);
                    }
                }
                if (elements.Count >= length)
                {
                    found = true;
                    stores.ForEach(arrays.Allocate);
                }
            }
            return found;
        }

        /// <summary>
        /// Returns the element of the given buffer that an address refers to.
        /// </summary>
        private static bool TryGetElement(Value address, Global global, out int element)
        {
            element = 0;
            bool hasElement = false;
            for (var value = address; ;)
            {
                switch (value)
                {
                    case Global target:
                        return target == global;
                    case LoadElementAddress lea when
                        !hasElement && lea.Offset is PrimitiveValue offset:
                        element = offset.Int32Value;
                        hasElement = true;
                        value = lea.Source;
                        break;
                    case LoadFieldAddress lfa when !hasElement:
                        value = lfa.Source;
                        break;
                    case AddressSpaceCast cast:
                        value = cast.Source;
                        break;
                    case NewView view:
                        value = view.Pointer;
                        break;
                    default:
                        return false;
                }
            }
        }

        /// <summary>
        /// Lets the allocation of a buffer run anywhere in the blocks dominating
        /// all its uses, up to its first use in the nearest of them.
        /// </summary>
        private void AllocateInDominators(Arrays arrays)
        {
            var uses = new Dictionary<BasicBlock, int>();
            void Use(Value value, Position position)
            {
                if (GetLeaves(value).Contains(arrays.Global) &&
                    (!uses.TryGetValue(position.Block, out int index) ||
                    position.Index < index))
                {
                    uses[position.Block] = position.Index;
                }
            }
            foreach (var block in _method.Blocks)
            {
                foreach (var (value, _) in block)
                {
                    if (value is PhiValue phi)
                    {
                        for (int i = 0; i < phi.NumArguments; ++i)
                            Use(phi.Arguments[i], End(phi.Sources[i]));
                        continue;
                    }
                    foreach (var operand in value.Values)
                        Use(operand, _positions[value]);
                }
                if (block.TerminationValue is { } terminationValue)
                    Use(terminationValue, End(block));
            }
            if (uses.Count < 1)
                return;

            var dominators = _method.Blocks.CreateDominators();
            var dominator = dominators.GetImmediateCommonDominator([.. uses.Keys]);
            int end = uses.TryGetValue(dominator, out int first)
                ? first
                : _ends[dominator];
            while (true)
            {
                for (int i = 0; i <= end; ++i)
                    arrays.Allocate(new(dominator, i));
                var next = dominators.GetImmediateDominator(dominator);
                if (next == dominator)
                    break;
                dominator = next;
                end = _ends[dominator];
            }
        }

        /// <summary>
        /// Returns the values of blocks, parameters and globals from which the
        /// given value is computed through pure values, without recursion for
        /// long chains of pure values.
        /// </summary>
        private HashSet<Value> GetLeaves(Value value)
        {
            var worklist = new Stack<(Value Value, bool Expanded)>();
            worklist.Push((value, false));
            while (worklist.TryPop(out var entry))
            {
                var current = entry.Value;
                if (_leaves.ContainsKey(current))
                    continue;
                if (current is not PureValue)
                {
                    _leaves.Add(current, current is BasicBlockValue or Parameter or Global
                        ? [current]
                        : []);
                }
                else if (!entry.Expanded)
                {
                    worklist.Push((current, true));
                    foreach (var operand in current.Values)
                        worklist.Push((operand, false));
                }
                else
                {
                    var leaves = new HashSet<Value>();
                    foreach (var operand in current.Values)
                        leaves.UnionWith(_leaves[operand]);
                    _leaves.Add(current, leaves);
                }
            }
            return _leaves[value];
        }

        /// <summary>
        /// Returns true if the given value can refer to an array.
        /// </summary>
        private bool RefersTo(Arrays arrays, Value value)
        {
            foreach (var leaf in GetLeaves(value))
            {
                if (leaf == arrays.Global && arrays.Callee is null ||
                    arrays.Bases.ContainsKey(leaf))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns true if the given value can refer to a stale array at the given
        /// position: if a value it is computed from can, where it is defined, or if
        /// the allocation can run in between. The buffer itself is never stale.
        /// </summary>
        private bool IsStale(Arrays arrays, Value value, Position position)
        {
            foreach (var leaf in GetLeaves(value))
            {
                if (!arrays.Bases.TryGetValue(leaf, out bool stale))
                    continue;
                var definition = _positions[leaf];
                if (definition.Block == position.Block &&
                    definition.Index < position.Index)
                {
                    stale |= arrays.Allocates(position.Block, definition.Index + 1,
                        position.Index);
                }
                else
                {
                    // A value is defined again when its block is entered again
                    var (clean, allocated) = GetReach(arrays, definition, leaf);
                    stale |= allocated.Contains(position.Block) ||
                        clean.Contains(position.Block) &&
                        arrays.Allocates(position.Block, 0, position.Index);
                }
                if (stale)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the blocks entered after the given position without and after an
        /// allocation, stopping at the block of the given value.
        /// </summary>
        private (HashSet<BasicBlock>, HashSet<BasicBlock>) GetReach(
            Arrays arrays,
            Position position,
            Value? value)
        {
            if (value is not null && arrays.Reaches.TryGetValue(value, out var reach))
                return reach;
            reach = ([], []);
            var worklist = new Stack<(BasicBlock Block, bool Allocated)>();
            bool allocated = arrays.Allocates(position.Block, position.Index + 1,
                _ends[position.Block]);
            foreach (var successor in position.Block.Successors)
                worklist.Push((successor, allocated));
            while (worklist.TryPop(out var entry))
            {
                if (!(entry.Allocated ? reach.Item2 : reach.Item1).Add(entry.Block) ||
                    value is not null && entry.Block == position.Block)
                {
                    continue;
                }
                allocated = entry.Allocated ||
                    arrays.Allocates(entry.Block, 0, _ends[entry.Block]);
                foreach (var successor in entry.Block.Successors)
                    worklist.Push((successor, allocated));
            }
            if (value is not null)
                arrays.Reaches.Add(value, reach);
            return reach;
        }

        /// <summary>
        /// Returns true if the method uses a value that can refer to a stale array,
        /// or stores an array to memory before the allocation can run again: a
        /// later iteration could load it. A value used at the termination of a
        /// block or by a phi without an address is used at the end of its block.
        /// </summary>
        public bool UsesStaleArray(Arrays arrays)
        {
            for (bool changed = true; changed;)
            {
                changed = false;
                foreach (var block in _method.Blocks)
                {
                    foreach (var (value, _) in block)
                    {
                        var (refers, stale, _) = Trace(arrays, value);
                        if (refers && (!arrays.Bases.TryGetValue(value, out bool old) ||
                            stale && !old))
                        {
                            arrays.Bases[value] = stale;
                            changed = true;
                        }
                    }
                }
            }

            foreach (var block in _method.Blocks)
            {
                foreach (var (value, _) in block)
                {
                    var position = _positions[value];
                    if (value is PhiValue phi)
                    {
                        for (int i = 0; i < phi.NumArguments; ++i)
                        {
                            if (!HoldsAddress(phi.Type) &&
                                IsStale(arrays, phi.Arguments[i], End(phi.Sources[i])))
                            {
                                return true;
                            }
                        }
                        continue;
                    }
                    foreach (var operand in value.Values)
                    {
                        if (IsStale(arrays, operand, position))
                            return true;
                    }
                    if (Trace(arrays, value).Stores)
                    {
                        var (clean, allocated) = GetReach(arrays, position, null);
                        if (allocated.Count > 0 ||
                            clean.Overlaps(arrays.Allocations.Keys) ||
                            arrays.Allocates(block, position.Index + 1, _ends[block]))
                        {
                            return true;
                        }
                    }
                }
                if (block.TerminationValue is { } terminationValue &&
                    IsStale(arrays, terminationValue, End(block)))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Returns whether the given value of a block can refer to an array, also
        /// to a stale array where it is defined, and whether it can store an array.
        /// A phi refers to the arrays of its arguments. A call allocates the
        /// arrays of its target, refers to arrays passed to parameters that its
        /// target returns, and stores arrays passed to parameters that its target
        /// stores. Other values holding addresses refer to the arrays of their
        /// operands, and other values can store the addresses passed to them.
        /// </summary>
        private (bool Refers, bool Stale, bool Stores) Trace(Arrays arrays, Value value)
        {
            var position = _positions[value];
            bool refers = false, stale = false, stores = false;
            void Pass(Value operand, Position at, Escape escape)
            {
                if (!RefersTo(arrays, operand))
                    return;
                refers |= escape.Returned;
                stores |= escape.Stored;
                if (escape.Returned)
                    stale |= IsStale(arrays, operand, at);
            }

            switch (value)
            {
                case PhiValue phi:
                    for (int i = 0; i < phi.NumArguments; ++i)
                        Pass(phi.Arguments[i], End(phi.Sources[i]), new(false, true));
                    break;
                case Store store:
                    Pass(store.Value, position,
                        new(HoldsAddress(store.Value.Type), false));
                    break;
                case Load or Alloca:
                    break;
                case MethodCall { Target.HasImplementation: true } call:
                    if (call.Target == arrays.Callee)
                    {
                        var escape = GetAllocation(_context, call.Target).Escape;
                        refers = escape.Returned;
                        stores = escape.Stored;
                    }
                    for (int i = 0; i < call.Arguments.Length; ++i)
                    {
                        Pass(call.Arguments[i], position,
                            GetParameterEscape(_context, call.Target, i));
                    }
                    break;
                default:
                    foreach (var operand in value.Values)
                        Pass(operand, position, new(HoldsAddress(operand.Type), true));
                    break;
            }
            return (refers && HoldsAddress(value.Type), stale, stores);
        }
    }
}
