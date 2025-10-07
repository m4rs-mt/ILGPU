// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PhiRewriter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Util;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.BasicBlockValues.Construction;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Rewriting;

/// <summary>
/// Represents a method-based rewriter.
/// </summary>
interface IMethodPhiRewriter : IRewriter
{
    /// <summary>
    /// Maps an old phi value to a new one.
    /// </summary>
    /// <param name="oldPhi">The old value to map to a new one.</param>
    /// <param name="newValue">The new value to map to.</param>
    void Map(PhiValue oldPhi, Value? newValue);

    /// <summary>
    /// Called before resolving a phi's arguments. Sets the demand context
    /// to the phi's block so that side-effect values created during
    /// argument resolution are not orphaned by nested isolation.
    /// </summary>
    /// <param name="phiBlock">The phi's owning block.</param>
    void BeginPhiWiring(BasicBlock phiBlock);

    /// <summary>
    /// Called after a phi's arguments are resolved and sealed.
    /// </summary>
    void EndPhiWiring();
}

/// <summary>
/// A nested phi rewriter in the scope of a method.
/// </summary>
/// <param name="capacity">The phi capacity.</param>
struct PhiRewriter(int capacity)
{
    private InlineList<(PhiValue Value, PhiValue.Builder Builder)> _phiValues =
        InlineList<(PhiValue, PhiValue.Builder)>.Create(capacity);

    /// <summary>
    /// Processes the given block.
    /// </summary>
    /// <typeparam name="TRewriter">The rewriter type.</typeparam>
    /// <param name="rewriter">The method rewriter to use.</param>
    /// <param name="builder">The new block builder.</param>
    /// <param name="basicBlock">The old block.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public void ProcessBlock<TRewriter>(
        in TRewriter rewriter,
        BasicBlockBuilder builder,
        BasicBlock basicBlock)
        where TRewriter : IMethodPhiRewriter
    {
        rewriter.Generation.ValidateGeneration(builder);
        rewriter.Generation.ValidateCurrentOrPreviousGeneration(basicBlock);

        foreach (BasicBlockValue value in basicBlock)
        {
            if (value is not PhiValue phiValue ||
                rewriter.IsReplacedOrRemoved(value))
            {
                continue;
            }

            // Create a new remapped phi value-builder to ensure proper wiring
            var newPhi = builder.CreatePhi(
                phiValue.Location,
                rewriter.RewriteAs<TypeValue>(phiValue.Type),
                phiValue.NumArguments);
            rewriter.Map(phiValue, newPhi.PhiValue);
            _phiValues.Add((phiValue, newPhi));
        }
    }

    /// <summary>
    /// Finishes building all phi values.
    /// </summary>
    /// <typeparam name="TRewriter">The rewriter type.</typeparam>
    /// <param name="rewriter">The method rewriter to use.</param>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public readonly void Finish<TRewriter>(TRewriter rewriter)
        where TRewriter : IMethodPhiRewriter
    {
        foreach (var (phiValue, phiBuilder) in _phiValues)
        {
            // Check if the old phi was superseded by a later transformation
            // (e.g., IfConversion.ConvertPhi replaces phi -> predicate after
            // ProcessBlock already registered phi -> phiBuilder.PhiValue).
            // In that case the phi placeholder is orphaned: we must NOT add
            // source blocks (which may be unreachable and therefore unsealed),
            // and we must forward any references that already point to the
            // placeholder (e.g., a TerminationValue set during init) to the
            // real replacement so that FinishRewrite resolves them correctly.
            bool isReplaced = rewriter.TryGetReplaced(
                phiValue,
                out var actualReplacement);
            if (isReplaced
                && actualReplacement is not null
                && actualReplacement != phiBuilder.PhiValue)
            {
                rewriter.Map(phiBuilder.PhiValue, actualReplacement);
                phiBuilder.Seal();
                continue;
            }

            // Remap targets — set demand context to the phi's block
            // so side-effect values aren't orphaned by nested isolation.
            rewriter.BeginPhiWiring(phiBuilder.PhiValue.BasicBlock);
            for (int i = 0; i < phiValue.Sources.Count; ++i)
            {
                var source = rewriter.Rewrite(phiValue.Sources[i]);
                var value = rewriter.Rewrite(phiValue.Arguments[i]);

                if (source is BasicBlock newSource &&
                    value is Value newValue)
                {
                    phiBuilder.AddArgument(newSource, newValue);
                }
            }
            rewriter.EndPhiWiring();

            phiBuilder.Seal();
        }
    }
}
