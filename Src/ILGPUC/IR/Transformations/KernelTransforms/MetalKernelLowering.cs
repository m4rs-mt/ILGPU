// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: MetalKernelLowering.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.BasicBlockValues;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.Transformations.KernelTransform;

/// <summary>
/// IR-level preparation pass for Metal kernels.
/// Runs after <see cref="SplitViewLowering"/> and before
/// <see cref="AcceleratorSpecializer"/>.
/// </summary>
/// <remarks>
/// <b>Thread index parameter threading</b><br/>
/// Metal MSL does not have implicit global thread-index built-ins like
/// <c>get_local_id()</c>. Instead, thread-position values must be declared as kernel
/// entry-point parameters with <c>[[attribute]]</c> qualifiers. When a
/// <c>[NoInline]</c> helper function reads <c>Group.Index</c> or <c>Grid.Index</c>
/// (represented by <see cref="GroupIndexValue"/> / <see cref="GridIndexValue"/> in the
/// IR), those identifiers are undeclared inside the helper's scope and cause a Metal
/// compile error. This pass threads the required thread-index values through helper
/// call chains as explicit <c>Int32</c> parameters.
/// </remarks>
sealed class MetalKernelLowering(TransformationArgs args) :
    Transformation<MetalKernelLowering.Analysis>(args)
{
    /// <summary>
    /// An internal lowering analysis.
    /// </summary>
    internal sealed class Analysis
    {
        /// <summary>
        /// Creates a new analysis instance accepting the parent module and current
        /// method.
        /// </summary>
        /// <param name="module">The parent module.</param>
        /// <param name="method">The current method.</param>
        public Analysis(Module module, Method method)
        {
            bool needsGroup = false;
            bool needsGrid = false;
            bool needsGroupDim = false;
            bool needsGridDim = false;

            if (module.EntryPoint != method)
            {
                method.ForEachValue<DeviceConstantValue>(deviceConstant =>
                {
                    needsGroup    |= deviceConstant is GroupIndexValue;
                    needsGrid     |= deviceConstant is GridIndexValue;
                    needsGroupDim |= deviceConstant is GroupDimensionValue;
                    needsGridDim  |= deviceConstant is GridDimensionValue;
                });
            }

            NeedsGroup    = needsGroup;
            NeedsGrid     = needsGrid;
            NeedsGroupDim = needsGroupDim;
            NeedsGridDim  = needsGridDim;
        }

        /// <summary>
        /// Returns true if the underlying method needs group-index information.
        /// </summary>
        public bool NeedsGroup { get; }

        /// <summary>
        /// Returns true if the underlying method needs grid-index information.
        /// </summary>
        public bool NeedsGrid { get; }

        /// <summary>
        /// Returns true if the underlying method needs group-dimension information.
        /// </summary>
        public bool NeedsGroupDim { get; }

        /// <summary>
        /// Returns true if the underlying method needs grid-dimension information.
        /// </summary>
        public bool NeedsGridDim { get; }

        /// <summary>
        /// Returns true if the underlying method needs any group or grid information.
        /// </summary>
        public bool NeedsGroupOrGridInformation =>
            NeedsGroup || NeedsGrid || NeedsGroupDim || NeedsGridDim;

        /// <summary>
        /// Gets or set the underlying group-index parameter.
        /// </summary>
        public Parameter? Group { get; set; }

        /// <summary>
        /// Gets or set the underlying grid-index parameter.
        /// </summary>
        public Parameter? Grid { get; set; }

        /// <summary>
        /// Gets or set the underlying group-dimension parameter.
        /// </summary>
        public Parameter? GroupDim { get; set; }

        /// <summary>
        /// Gets or set the underlying grid-dimension parameter.
        /// </summary>
        public Parameter? GridDim { get; set; }
    }

    private readonly Method _entryPoint = args.Module.EntryPoint;

    /// <inheritdoc/>
    protected override Analysis CreateIntermediate(
        ModuleTransform transform,
        Method method) => new(transform.OldModule, method);

    /// <inheritdoc/>
    protected override void OnMap(ModuleTransform transform)
    {
        base.OnMap(transform);

        MapPureValue<GroupIndexValue>(RewriteGroupIndex);
        MapPureValue<GridIndexValue>(RewriteGridIndex);
        MapPureValue<GroupDimensionValue>(RewriteGroupDimension);
        MapPureValue<GridDimensionValue>(RewriteGridDimension);

        MapBasicBlockValue<MethodCall>(RewriteCallSite);
    }

    /// <inheritdoc/>
    protected override void OnTransform(MethodTransform transform)
    {
        // Check whether we need to create parameters
        if (transform.OldMethod != _entryPoint)
        {
            // Get intermediate and prepare parameters
            var intermediate = GetIntermediate(transform);

            transform.Assert(
                intermediate.Group is null &&
                intermediate.Grid is null &&
                intermediate.GroupDim is null &&
                intermediate.GridDim is null);

            var intType = transform.ModuleBuilder.GetPrimitiveType(BasicValueType.Int32);
            intermediate.Group = intermediate.NeedsGroup
                ? transform.CreateParameter(intType, "groupIdx")
                : null;
            intermediate.Grid = intermediate.NeedsGrid
                ? transform.CreateParameter(intType, "gridIdx")
                : null;
            intermediate.GroupDim = intermediate.NeedsGroupDim
                ? transform.CreateParameter(intType, "groupDim")
                : null;
            intermediate.GridDim = intermediate.NeedsGridDim
                ? transform.CreateParameter(intType, "gridDim")
                : null;
        }

        base.OnTransform(transform);
    }

    /// <summary>
    /// Converter for <see cref="GroupIndexValue"/>.
    /// In helper methods, replaces the device constant with the helper's
    /// dedicated <c>groupIdx</c> parameter.
    /// In the entry point, leaves the value unchanged (codegen maps it to
    /// <c>thread_position_in_threadgroup.x</c>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? RewriteGroupIndex(
        PureValueTransform transform,
        GroupIndexValue groupIdx)
    {
        var oldMethod = transform.MethodTransform.OldMethod;

        // Entry point: keep as GroupIndexValue (codegen handles it correctly)
        if (oldMethod == _entryPoint)
            return groupIdx;

        // Helper: substitute the dedicated thread-index parameter
        var intermediate = GetIntermediate(transform);
        if (intermediate.Group is not null)
            return intermediate.Group;

        return groupIdx;
    }

    /// <summary>
    /// Converter for <see cref="GridIndexValue"/>.
    /// Same logic as <see cref="RewriteGroupIndex"/> but for the grid index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? RewriteGridIndex(
        PureValueTransform transform,
        GridIndexValue gridIdx)
    {
        var oldMethod = transform.MethodTransform.OldMethod;

        // Entry point: keep as GridIndexValue (codegen handles it correctly)
        if (oldMethod == _entryPoint)
            return gridIdx;

        var intermediate = GetIntermediate(transform);
        if (intermediate.Grid is not null)
            return intermediate.Grid;

        return gridIdx;
    }

    /// <summary>
    /// Converter for <see cref="GroupDimensionValue"/>.
    /// In helper methods, replaces the device constant with the helper's dedicated
    /// <c>groupDim</c> parameter. In the entry point, leaves the value unchanged
    /// (codegen maps it to <c>threads_per_threadgroup.x</c>).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? RewriteGroupDimension(
        PureValueTransform transform,
        GroupDimensionValue groupDim)
    {
        if (transform.MethodTransform.OldMethod == _entryPoint)
            return groupDim;

        var intermediate = GetIntermediate(transform);
        return intermediate.GroupDim ?? (Value)groupDim;
    }

    /// <summary>
    /// Converter for <see cref="GridDimensionValue"/>.
    /// Same logic as <see cref="RewriteGroupDimension"/> but for the grid dimension.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? RewriteGridDimension(
        PureValueTransform transform,
        GridDimensionValue gridDim)
    {
        if (transform.MethodTransform.OldMethod == _entryPoint)
            return gridDim;

        var intermediate = GetIntermediate(transform);
        return intermediate.GridDim ?? (Value)gridDim;
    }

    /// <summary>
    /// Converter for <see cref="MethodCall"/> values.
    /// When the callee is a helper that needs thread-index parameters, injects
    /// the appropriate thread-index values (group index and/or grid index) as
    /// extra trailing arguments.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    private Value? RewriteCallSite(BasicBlockTransform transform, MethodCall call)
    {
        // Only intercept calls to helpers that need thread-index params.
        // External methods (runtime intrinsics, etc.) have no analysis
        // entry and cannot reference device constants — skip them.
        if (!TryGetIntermediate(call.Target, out var calleeIntermediate))
            return call;
        if (!calleeIntermediate.NeedsGroupOrGridInformation)
            return call;

        var location = call.Location;

        // Manually rewrite the call target (the standard MethodCall.Rewrite path
        // is bypassed when a MapBasicBlockValue converter returns a new value).
        var newTarget = transform.Rewrite(call.Target) as Method ?? call.Target;
        var callBuilder = transform.CreateCall(location, newTarget);

        // Rewrite and re-add the original arguments (Arguments = Values[1..]).
        foreach (var arg in call.Arguments)
            callBuilder.Add(transform.Rewrite(arg));

        // Inject thread-index arguments after the original ones.
        var callerMethod = transform.OldMethod;
        if (callerMethod == _entryPoint)
        {
            // Caller is the entry point: inject device-constant values that codegen
            // maps to the appropriate kernel built-in identifiers.
            if (calleeIntermediate.NeedsGroup)
                callBuilder.Add(transform.CreateGroupIndexValue(location));
            if (calleeIntermediate.NeedsGrid)
                callBuilder.Add(transform.CreateGridIndexValue(location));
            if (calleeIntermediate.NeedsGroupDim)
                callBuilder.Add(transform.CreateGroupDimensionValue(location));
            if (calleeIntermediate.NeedsGridDim)
                callBuilder.Add(transform.CreateGridDimensionValue(location));
        }
        else
        {
            // Caller is a helper: pass our own parameters through.
            var intermediate = GetIntermediate(call.Scope);
            if (intermediate.NeedsGroup && intermediate.Group is Parameter gp)
                callBuilder.Add(gp);
            if (intermediate.NeedsGrid && intermediate.Grid is Parameter gdp)
                callBuilder.Add(gdp);
            if (intermediate.NeedsGroupDim && intermediate.GroupDim is Parameter gdimP)
                callBuilder.Add(gdimP);
            if (intermediate.NeedsGridDim && intermediate.GridDim is Parameter griddimP)
                callBuilder.Add(griddimP);
        }

        return callBuilder.Seal();
    }
}
