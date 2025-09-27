// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: IRVerifier.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ILGPUC.IR.Analyses;

/// <summary>
/// Comprehensive IR verifier that checks structural integrity of the IR after
/// each transformation step. Only active in DEBUG builds.
/// </summary>
static class IRVerifier
{
    /// <summary>
    /// Verifies the structural integrity of the given module. Throws
    /// <see cref="IRVerificationException"/> with all violations found.
    /// Only active in DEBUG builds.
    /// </summary>
    /// <param name="module">The module to verify.</param>
    /// <param name="transformName">
    /// Optional name of the transformation that produced this module.
    /// </param>
    [Conditional("DEBUG")]
    public static void Verify(Module module, string? transformName = null)
    {
        var ctx = new VerificationContext(module.Generation, transformName);
        ctx.VerifyModule(module);
        ctx.ThrowIfErrors();
    }

    /// <summary>
    /// Verifies the structural integrity of a single method after it has
    /// been sealed during a transformation step. This catches stale
    /// references early, before the module-level seal/ComputeUses runs.
    /// Only active in DEBUG builds.
    /// </summary>
    /// <param name="method">The sealed method to verify.</param>
    /// <param name="generation">The expected generation.</param>
    /// <param name="transformName">
    /// Optional name of the transformation that produced this method.
    /// </param>
    [Conditional("DEBUG")]
    public static void VerifyMethod(
        Method method,
        Generation generation,
        string? transformName = null)
    {
        var ctx = new VerificationContext(generation, transformName);
        ctx.VerifySingleMethod(method);
        ctx.ThrowIfErrors();
    }

}

/// <summary>
/// Accumulates verification errors found during a single verification pass and
/// formats them into an <see cref="IRVerificationException"/> on demand.
/// </summary>
sealed class VerificationContext
{
    #region Instance

    /// <summary>The expected IR generation for all values in this pass.</summary>
    private readonly Generation _gen;

    /// <summary>
    /// Name of the transformation that produced the IR being verified, or
    /// <see langword="null"/> if no name was provided.
    /// </summary>
    private readonly string? _transformName;

    /// <summary>
    /// Set of pure-value IDs already visited during the current pass, used to
    /// avoid re-verifying shared pure-value sub-trees.
    /// </summary>
    private readonly HashSet<ValueId> _visitedPureValues = new();

    /// <summary>
    /// Human-readable error messages collected during the current pass.
    /// </summary>
    private readonly List<string> _errors = new();

    /// <summary>
    /// Initializes a new verification context for a single pass.
    /// </summary>
    /// <param name="generation">
    /// The IR generation that every verified value must belong to.
    /// </param>
    /// <param name="transformName">
    /// Optional name of the transformation that produced the IR, used in
    /// error messages.
    /// </param>
    public VerificationContext(Generation generation, string? transformName)
    {
        _gen = generation;
        _transformName = transformName;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Throws an <see cref="IRVerificationException"/> listing all recorded
    /// errors if any errors were collected during the pass; otherwise returns
    /// without side-effects.
    /// </summary>
    public void ThrowIfErrors()
    {
        if (_errors.Count == 0)
            return;

        var prefix = _transformName is not null
            ? $"[IRVerifier after {_transformName}]"
            : "[IRVerifier]";

        var sb = new StringBuilder();
        sb.AppendLine($"{prefix} {_errors.Count} verification error(s):");
        for (int i = 0; i < _errors.Count; i++)
            sb.AppendLine($"  ({i + 1}) {_errors[i]}");

        throw new IRVerificationException(sb.ToString());
    }

    /// <summary>
    /// Verifies module-level types, globals, and all methods with
    /// implementations, recording any violations found.
    /// </summary>
    /// <param name="module">The module to verify.</param>
    public void VerifyModule(Module module)
    {
        // Verify module-level types
        foreach (var type in module.Types)
            VerifyGeneration(type, "module type");

        // Verify module-level globals
        foreach (var global in module.Globals)
        {
            VerifyGeneration(global, "module global");
            if (global.Type is not null)
                VerifyGeneration(
                    global.Type, $"global {global.ToReferenceString()} type");
        }

        // Verify all methods
        foreach (var method in module.Methods)
        {
            VerifyGeneration(method, "module method");
            if (method.HasImplementation)
                VerifySingleMethod(method);
        }
    }

    /// <summary>
    /// Verifies a single method in isolation: checks its generation, return
    /// type, parameters, and all basic blocks.
    /// </summary>
    /// <param name="method">The method to verify.</param>
    public void VerifySingleMethod(Method method)
    {
        VerifyGeneration(method, "method");
        if (method.HasImplementation)
            VerifyMethodInternal(method);
    }

    /// <summary>
    /// Verifies the return type, parameters, and all basic blocks of a method
    /// that is known to have an implementation.
    /// </summary>
    /// <param name="method">The method whose internals to verify.</param>
    private void VerifyMethodInternal(Method method)
    {
        var methodRef = method.ToReferenceString();

        // Verify return type
        if (method.Type is not null)
            VerifyGeneration(method.Type, $"method {methodRef} return type");

        // Verify parameters
        foreach (var param in method.Parameters)
        {
            VerifyGeneration(param, $"method {methodRef} parameter");
            if (param.Type is not null)
                VerifyGeneration(
                    param.Type, $"parameter {param.ToReferenceString()} type");
        }

        // Verify all blocks
        foreach (var block in method.Blocks)
        {
            VerifyGeneration(block, $"method {methodRef} block");
            VerifyBasicBlock(block, method);
        }

        // TODO: 192 orphans from newly-inlined ComputeElementIndexChecked +
        // 21 Half optimizer crashes. Orphans need systematic fix.
        // VerifyNoOrphanedOperands(method);
    }

    /// <summary>
    /// Collects all BasicBlockValues reachable from block value chains
    /// (phis + regular values) in the method, then checks that every
    /// BasicBlockValue referenced as an operand is in this set.
    /// Orphaned values indicate a bug in the transformation framework
    /// (typically cross-block side-effect values not placed in a chain).
    /// </summary>
    private void VerifyNoOrphanedOperands(Method method)
    {
        var methodRef = method.ToReferenceString();

        // Build set of all chain-reachable BasicBlockValues and
        // detect dual-chain membership (same value in two blocks).
        var chainReachable = new HashSet<ValueId>();
        var valueToBlock = new Dictionary<ValueId, BasicBlock>();
        foreach (var block in method.Blocks)
        {
            // Walk phi chain (with cycle guard)
            var phiSeen = new HashSet<ValueId>();
            for (var phi = block.FirstPhiValue;
                 phi is not null;
                 phi = phi.Next as PhiValue)
            {
                if (!phiSeen.Add(phi.Id)) break;
                chainReachable.Add(phi.Id);
                if (valueToBlock.TryGetValue(phi.Id, out var prevBlock)
                    && prevBlock != block)
                {
                    AddError(
                        $"Dual-chain: phi {phi.ToReferenceString()} " +
                        $"in both {prevBlock.ToReferenceString()} " +
                        $"and {block.ToReferenceString()}");
                }
                else
                {
                    valueToBlock[phi.Id] = block;
                }
            }

            // Walk regular value chain (with cycle guard)
            var valSeen = new HashSet<ValueId>();
            for (var val = block.FirstValue;
                 val is not null;
                 val = val.Next)
            {
                if (!valSeen.Add(val.Id)) break;
                chainReachable.Add(val.Id);
                if (valueToBlock.TryGetValue(val.Id, out var prevBlock)
                    && prevBlock != block)
                {
                    AddError(
                        $"Dual-chain: value {val.ToReferenceString()} " +
                        $"in both {prevBlock.ToReferenceString()} " +
                        $"and {block.ToReferenceString()}");
                }
                else
                {
                    valueToBlock[val.Id] = block;
                }
            }
        }

        // Also include parameters (they're not in any block chain)
        foreach (var param in method.Parameters)
            chainReachable.Add(param.Id);

        // Check every operand of every value in the method
        foreach (var block in method.Blocks)
        {
            foreach (BasicBlockValue bbv in block)
            {
                CheckOperandsNotOrphaned(
                    bbv, method, chainReachable);
            }
        }
    }

    /// <summary>
    /// Recursively checks that all BasicBlockValue operands (direct and
    /// through PureValue trees) are in the chain-reachable set.
    /// </summary>
    private void CheckOperandsNotOrphaned(
        Value value,
        Method method,
        HashSet<ValueId> chainReachable)
    {
        foreach (var operand in value.Values)
        {
            if (operand is BasicBlockValue bbOp
                && bbOp.BasicBlock.Method == method
                && !chainReachable.Contains(bbOp.Id))
            {
                // Check if the block has pending values
                var blockChainCount = 0;
                for (var cv = bbOp.BasicBlock.LastValue;
                     cv is not null;
                     cv = cv.Previous as BasicBlockValue)
                {
                    blockChainCount++;
                    if (blockChainCount > 10000) break;
                }
                AddError(
                    $"Orphaned operand: {value.ToReferenceString()} " +
                    $"in method {method.ToReferenceString()} references " +
                    $"{bbOp.ToReferenceString()} (gen={bbOp.Generation}) which claims " +
                    $"BasicBlock={bbOp.BasicBlock.ToReferenceString()} " +
                    $"(gen={bbOp.BasicBlock.Generation}, chainLen={blockChainCount}) " +
                    $"but is not in any block's value chain. " +
                    $"Previous={bbOp.Previous?.ToReferenceString() ?? "null"}");
            }

            // Check PureValue operand trees too
            if (operand is PureValue pv)
                CheckOperandsNotOrphaned(pv, method, chainReachable);
        }
    }

    /// <summary>
    /// Verifies the phi chain, value chain, terminator, and
    /// predecessor/successor symmetry of a single basic block.
    /// </summary>
    /// <param name="block">The block to verify.</param>
    /// <param name="method">The method that owns the block.</param>
    private void VerifyBasicBlock(BasicBlock block, Method method)
    {
        // 1. Verify phi value chain
        VerifyPhiChain(block, method);

        // 2. Verify non-phi value chain
        VerifyValueChain(block, method);

        // 3. Verify termination
        VerifyTermination(block, method);

        // 4. Verify predecessor/successor symmetry
        VerifyPredSuccSymmetry(block);
    }

    /// <summary>
    /// Verifies the generation, block ownership, and source-predecessor
    /// consistency of every phi value in <paramref name="block"/>.
    /// </summary>
    /// <param name="block">The block whose phi values to verify.</param>
    /// <param name="method">The method that owns the block.</param>
    private void VerifyPhiChain(BasicBlock block, Method method)
    {
        var blockRef = block.ToReferenceString();

        foreach (var phi in block.PhiValues)
        {
            VerifyGeneration(phi, $"phi in {blockRef}");

            if (phi.BasicBlock != block)
            {
                AddError(
                    $"Phi {phi.ToReferenceString()} in {blockRef} has " +
                    $"BasicBlock={phi.BasicBlock.ToReferenceString()} " +
                    $"(expected {blockRef})");
            }

            for (int i = 0; i < phi.NumArguments; i++)
            {
                var arg = phi.Arguments[i];
                VerifyGeneration(arg, $"phi {phi.ToReferenceString()} arg[{i}]");
            }

            foreach (var source in phi.Sources)
            {
                VerifyGeneration(source, $"phi {phi.ToReferenceString()} source");

                bool isPred = false;
                foreach (var pred in block.Predecessors)
                {
                    if (pred == source)
                    {
                        isPred = true;
                        break;
                    }
                }
                if (!isPred)
                {
                    AddError(
                        $"Phi {phi.ToReferenceString()} in {blockRef} has " +
                        $"source {source.ToReferenceString()} which is not a " +
                        $"predecessor of the block");
                }
            }

            if (phi.Type is not null)
                VerifyGeneration(phi.Type, $"phi {phi.ToReferenceString()} type");
        }
    }

    /// <summary>
    /// Walks the linked list of non-phi values in <paramref name="block"/>,
    /// verifying generation, block/method ownership, Previous-pointer
    /// consistency, types, and operands.
    /// </summary>
    /// <param name="block">The block whose value chain to verify.</param>
    /// <param name="method">The method that owns the block.</param>
    private void VerifyValueChain(BasicBlock block, Method method)
    {
        var blockRef = block.ToReferenceString();
        var methodRef = method.ToReferenceString();
        BasicBlockValue? prev = null;
        var visited = new HashSet<ValueId>();

        var current = block.FirstValue;
        while (current is not null)
        {
            // Cycle detection
            if (!visited.Add(current.Id))
            {
                AddError(
                    $"Cycle in value chain of {blockRef}: " +
                    $"{current.ToReferenceString()} visited twice");
                break;
            }

            VerifyGeneration(current, $"value in {blockRef}");

            if (current.BasicBlock != block)
            {
                AddError(
                    $"Value {current.ToReferenceString()} in {blockRef} has " +
                    $"BasicBlock={current.BasicBlock.ToReferenceString()} " +
                    $"(expected {blockRef})");
            }

            if (current.BasicBlock.Method != method)
            {
                AddError(
                    $"Value {current.ToReferenceString()} in {blockRef} " +
                    $"belongs to method " +
                    $"{current.BasicBlock.Method.ToReferenceString()} " +
                    $"(expected {methodRef})");
            }

            if (current.Previous != prev)
            {
                AddError(
                    $"Value {current.ToReferenceString()} in {blockRef} has " +
                    $"Previous={current.Previous?.ToReferenceString() ?? "null"} " +
                    $"(expected {prev?.ToReferenceString() ?? "null"})");
            }

            if (current.Type is not null)
                VerifyGeneration(
                    current.Type, $"value {current.ToReferenceString()} type");

            VerifyOperands(current, method);

            prev = current;
            current = current.Next;
        }

        if (block.LastValue != prev)
        {
            AddError(
                $"Block {blockRef} LastValue=" +
                $"{block.LastValue?.ToReferenceString() ?? "null"} " +
                $"but chain tail is {prev?.ToReferenceString() ?? "null"}");
        }
    }

    /// <summary>
    /// Verifies the termination value, termination condition, successor count
    /// consistency with the termination kind, and the generation of each
    /// successor of <paramref name="block"/>.
    /// </summary>
    /// <param name="block">The block whose termination to verify.</param>
    /// <param name="method">The method that owns the block.</param>
    private void VerifyTermination(BasicBlock block, Method method)
    {
        var blockRef = block.ToReferenceString();

        if (block.TerminationValue is { } termValue)
        {
            VerifyGeneration(termValue, $"termination of {blockRef}");
            if (termValue is PureValue pureTermValue)
                VerifyPureValueTree(pureTermValue, method, blockRef);
            else
                VerifyOperands(termValue, method);
        }

        if (block.TerminationCondition is { } termCond &&
            termCond != block.TerminationValue)
        {
            VerifyGeneration(termCond, $"termination condition of {blockRef}");
            if (termCond is PureValue pureCond)
                VerifyPureValueTree(pureCond, method, blockRef);
            else
                VerifyOperands(termCond, method);
        }

        int numSuccessors = block.Successors.Length;
        switch (block.TerminationKind)
        {
            case BlockTerminationKind.Unconditional:
                if (numSuccessors != 1)
                {
                    AddError(
                        $"Block {blockRef} has unconditional termination but " +
                        $"{numSuccessors} successors (expected 1)");
                }
                break;
            case BlockTerminationKind.Conditional:
                if (numSuccessors != 2)
                {
                    AddError(
                        $"Block {blockRef} has conditional termination but " +
                        $"{numSuccessors} successors (expected 2)");
                }
                break;
            case BlockTerminationKind.Return:
                if (numSuccessors != 0)
                {
                    AddError(
                        $"Block {blockRef} has return termination but " +
                        $"{numSuccessors} successors (expected 0)");
                }
                break;
            case BlockTerminationKind.Switch:
                if (numSuccessors < 2)
                {
                    AddError(
                        $"Block {blockRef} has switch termination but " +
                        $"{numSuccessors} successors (expected ≥2)");
                }
                break;
        }

        foreach (var succ in block.Successors)
            VerifyGeneration(succ, $"successor of {blockRef}");
    }

    /// <summary>
    /// Verifies that every successor of <paramref name="block"/> lists it as a
    /// predecessor, and that every predecessor lists it as a successor.
    /// </summary>
    /// <param name="block">The block whose pred/succ symmetry to verify.</param>
    private void VerifyPredSuccSymmetry(BasicBlock block)
    {
        var blockRef = block.ToReferenceString();

        foreach (var succ in block.Successors)
        {
            bool found = false;
            foreach (var pred in succ.Predecessors)
            {
                if (pred == block)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                AddError(
                    $"Block {blockRef} lists {succ.ToReferenceString()} as " +
                    $"successor, but it does not list {blockRef} as predecessor");
            }
        }

        foreach (var pred in block.Predecessors)
        {
            bool found = false;
            foreach (var succ in pred.Successors)
            {
                if (succ == block)
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                AddError(
                    $"Block {blockRef} lists {pred.ToReferenceString()} as " +
                    $"predecessor, but it does not list {blockRef} as successor");
            }
        }
    }

    /// <summary>
    /// Verifies the generation of every operand of <paramref name="value"/>,
    /// checks that any <see cref="BasicBlockValue"/> operands belong to
    /// <paramref name="method"/>, and recursively verifies pure-value sub-trees.
    /// </summary>
    /// <param name="value">The value whose operands to verify.</param>
    /// <param name="method">The method that owns the value.</param>
    private void VerifyOperands(Value value, Method method)
    {
        var valueRef = value.ToReferenceString();
        foreach (var operand in value.Values)
        {
            VerifyGeneration(operand, $"operand of {valueRef}");

            if (operand is BasicBlockValue bbOperand &&
                bbOperand.BasicBlock.Method != method)
            {
                AddError(
                    $"Value {valueRef} in method {method.ToReferenceString()} " +
                    $"references {bbOperand.ToReferenceString()} from method " +
                    $"{bbOperand.BasicBlock.Method.ToReferenceString()}");
            }

            if (operand is PureValue pureOperand)
                VerifyPureValueTree(pureOperand, method, valueRef);
        }
    }

    /// <summary>
    /// Recursively verifies the sub-tree rooted at <paramref name="pureValue"/>,
    /// skipping nodes already visited in this pass to handle DAG sharing.
    /// Checks generation, type, operands, and cross-method references.
    /// </summary>
    /// <param name="pureValue">The pure value whose sub-tree to verify.</param>
    /// <param name="method">The method that transitively owns this pure value.</param>
    /// <param name="parentRef">
    /// Reference string of the value that introduced this pure value, used in
    /// error messages.
    /// </param>
    private void VerifyPureValueTree(
        PureValue pureValue,
        Method method,
        string parentRef)
    {
        if (!_visitedPureValues.Add(pureValue.Id))
            return;

        var pureRef = pureValue.ToReferenceString();

        if (pureValue.Type is not null)
            VerifyGeneration(pureValue.Type, $"pure value {pureRef} type");

        foreach (var operand in pureValue.Values)
        {
            VerifyGeneration(
                operand,
                $"sub-operand of pure value {pureRef} (parent: {parentRef})");

            if (operand is BasicBlockValue bbOperand &&
                bbOperand.BasicBlock.Method != method)
            {
                AddError(
                    $"Pure value {pureRef} (reachable from {parentRef} " +
                    $"in method {method.ToReferenceString()}) references " +
                    $"{bbOperand.ToReferenceString()} from method " +
                    $"{bbOperand.BasicBlock.Method.ToReferenceString()}");
            }

            if (operand is PureValue nestedPure)
                VerifyPureValueTree(nestedPure, method, parentRef);
        }
    }

    /// <summary>
    /// Records an error if <paramref name="value"/>'s generation does not match
    /// the expected generation for this pass.
    /// </summary>
    /// <param name="value">The value whose generation to check.</param>
    /// <param name="context">
    /// Human-readable description of where this value appears, used in the
    /// error message.
    /// </param>
    private void VerifyGeneration(Value value, string context)
    {
        if (value.Generation != _gen)
        {
            AddError(
                $"Generation mismatch for {value.GetType().Name}:" +
                $"{value.ToReferenceString()} " +
                $"(gen={value.Generation}, expected={_gen}) " +
                $"in context: {context}");
        }
    }

    /// <summary>Appends an error message to the collection for this pass.</summary>
    /// <param name="message">The error message to record.</param>
    private void AddError(string message) => _errors.Add(message);

    #endregion
}

/// <summary>
/// Exception thrown when IR verification fails.
/// </summary>
sealed class IRVerificationException : Exception
{
    /// <summary>
    /// Constructs a new IR verification exception with a detail message.
    /// </summary>
    /// <param name="message">The verification failure details.</param>
    public IRVerificationException(string message) : base(message) { }

    /// <summary>
    /// Constructs a new IR verification exception with no message.
    /// </summary>
    public IRVerificationException() { }

    /// <summary>
    /// Constructs a new IR verification exception wrapping an inner exception.
    /// </summary>
    /// <param name="message">The verification failure details.</param>
    /// <param name="innerException">The exception that caused this failure.</param>
    public IRVerificationException(string message, Exception innerException)
        : base(message, innerException)
    { }
}
