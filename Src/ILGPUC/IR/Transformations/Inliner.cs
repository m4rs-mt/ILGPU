// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2018-2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: Inliner.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Frontend;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.ModuleValues;
using MethodImplAttributes = System.Reflection.MethodImplAttributes;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Represents a function inliner.
/// </summary>
sealed class Inliner(TransformationArgs args) : Transformation(args)
{
    /// <summary>
    /// The maximum number of IL instructions to inline.
    /// </summary>
    private const int MaxNumILInstructionsToInline = 32;

    /// <summary>
    /// Setups inlining attributes.
    /// </summary>
    /// <param name="properties">Current compilation properties.</param>
    /// <param name="method">The current method.</param>
    /// <param name="disassembledMethod">The disassembled source method.</param>
    public static void SetupInliningAttributes(
        CompilationProperties properties,
        Method method,
        DisassembledMethod disassembledMethod)
    {
        // Check whether we can inline this method
        if (!method.HasImplementation)
            return;

        if (method.Source is not null)
        {
            if ((method.Source.MethodImplementationFlags &
                MethodImplAttributes.NoInlining) ==
                MethodImplAttributes.NoInlining)
            {
                return;
            }

            if ((method.Source.MethodImplementationFlags &
                MethodImplAttributes.AggressiveInlining) ==
                MethodImplAttributes.AggressiveInlining ||
                method.Source.Module.Name == nameof(ILGPU))
            {
                method.AddFlags(MethodFlags.Inline);
            }
        }

        // Evaluate a simple inlining heuristic
        if (properties.InliningMode != InliningMode.Conservative ||
            disassembledMethod.Instructions.Length <= MaxNumILInstructionsToInline)
        {
            method.AddFlags(MethodFlags.Inline);
        }
    }

    /// <summary>
    /// Tries to inline method calls.
    /// </summary>
    protected override void OnTransform(MethodTransform transform)
    {
        base.OnTransform(transform);

        transform.OldMethod.ForEachValue<MethodCall>(call =>
        {
            // Skip methods whose blocks haven't been loaded (e.g.,
            // external method references that couldn't be resolved).
            // Check Count > 0 before accessing EntryBlock to avoid
            // an assertion failure on methods with empty value lists.
            if (call.Target.IsInline
                && call.Target.Count > 0
                && call.Target.EntryBlock is not null)
                transform.SpecializeMethodCall(call);
        });
    }
}

/// <summary>
/// Runs the inliner to a fixed point, applying Inliner + SimplifyControlFlow +
/// SSAConstruction + SSACleanup in a loop until no inline-able method calls remain.
/// This handles arbitrary call chain depths in a single optimizer slot rather than
/// requiring one inliner pass per call depth level.
/// </summary>
/// <param name="args">The transformation args.</param>
sealed class IterativeInliner(TransformationArgs args) : Transformation(args)
{
    /// <summary>
    /// Safety cap to prevent infinite iteration in pathological cases
    /// (e.g., recursive methods that are marked inline).
    /// </summary>
    private const int MaxIterations = 32;

    /// <summary>
    /// Applies inliner + cleanup passes in a loop until no inline-able calls remain.
    /// </summary>
    protected override Module TransformInternal(Module module)
    {
        var current = module;

        for (int i = 0; i < MaxIterations; ++i)
        {
            if (!HasInlineableCalls(current))
                break;

            // SimplifyControlFlow is intentionally omitted from the
            // inner loop. Running it between Inliner and SSA causes
            // RebuildAndMemoize to reconstruct alloca→addrspacecast→load
            // chains with stale operands, leading to SSA promoting
            // allocas with initial-value (zero) reads instead of the
            // computed values. The outer pipeline's SimplifyControlFlow
            // handles block merging after all iterations complete.
            var subTransformer = Transformer.Create(
                a => new Inliner(a),
                a => new DeadLoadElimination(a),
                a => new SSAConstruction(a),
                a => new SSACleanup(a));

            current = subTransformer.Apply(
                Properties,
                TypeInformationManager,
                current);
        }

        return current;
    }

    /// <summary>
    /// Checks whether any method in the module still has calls to inline-able targets
    /// that are within the size limit.
    /// </summary>
    private static bool HasInlineableCalls(Module module)
    {
        foreach (var method in module.Methods)
        {
            bool hasCalls = false;
            method.ForEachValue<MethodCall>(call =>
            {
                hasCalls |= call.Target.IsInline;
            });
            if (hasCalls) return true;
        }
        return false;
    }
}