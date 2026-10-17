// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LowerGCInit.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPUC.IR.Analyses;
using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.Roslyn.Analysis;
using ILGPUC.Util;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Validates and lowers all <see cref="GCInit"/> nodes in the module.
/// </summary>
/// <remarks>
/// <para>
/// This pass does two things in one sweep per method:
/// </para>
/// <list type="number">
/// <item>
/// <description>
/// <b>Validate</b> — every <see cref="GCInit"/> must not be inside a loop.
/// An in-loop GCInit would imply per-iteration managed-object allocations in
/// Local (per-thread) memory, which the GPU execution model cannot support.
/// A <see cref="KernelCompilationException"/> (ILGPU1208) is thrown at the
/// location of the first violation.
/// </description>
/// </item>
/// <item>
/// <description>
/// <b>Lower</b> — replace every <see cref="GCInit"/> with its underlying
/// <see cref="Global"/>.  After this pass <see cref="GCInit"/> nodes are gone from
/// the IR and backends never see them.
/// </description>
/// </item>
/// </list>
/// <para>
/// This pass is registered in <see cref="Optimizer.AddAcceleratorSpecializer"/>
/// directly after <see cref="LowerViews"/> — so the preceding lowering passes
/// (Inliner, LowerArrays, LowerViews, …) have already run — and before
/// the subsequent <see cref="SSAConstruction"/>, which promotes the remaining
/// closure state.
/// </para>
/// </remarks>
sealed class LowerGCInit(TransformationArgs args) :
    Transformation<Loops<ReversePostOrder<BasicBlock>, Forwards>>(args)
{
    /// <inheritdoc/>
    protected override Loops<ReversePostOrder<BasicBlock>, Forwards> CreateIntermediate(
        ModuleTransform transform,
        Method method) =>
        method.CreateLoops();

    /// <inheritdoc/>
    protected override void OnMap(ModuleTransform transform)
    {
        base.OnMap(transform);

        MapBasicBlockValue<GCInit>((bbTransform, gcInit) =>
        {
            var loops = GetIntermediate(bbTransform);
            if (loops.TryGetLoop(gcInit.BasicBlock, out _))
            {
                var method = gcInit.BasicBlock.Method;
                throw new KernelCompilationException(
                    KernelDiagnostic.ClosureInLoopId,
                    string.Format(
                        ErrorMessages.KernelDiagnosticClosureInLoop,
                        method.Source?.GetDisplayName() ?? method.Name),
                    gcInit.Location);
            }

            // Lower: replace the GCInit node with its backing Global
            return gcInit.Global;
        });
    }
}
