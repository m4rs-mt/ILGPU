// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: SplitViewLowering.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Runtime;
using ILGPUC.IR.MethodValues;
using ILGPUC.IR.ModuleValues;
using ILGPUC.IR.PureValues;

namespace ILGPUC.IR.Transformations.KernelTransform;

/// <summary>
/// IR-level preparation pass for Metal and OpenCL kernel entry points.
/// Runs after <see cref="LowerViews"/> and before
/// <see cref="AcceleratorSpecializer"/>.
/// </summary>
/// <remarks>
/// <b>(Metal + OpenCL) — entry-point view struct flattening</b><br/>
/// After <c>LowerViews</c>, view-type entry-point parameters have been converted to
/// <c>StructureType{PointerType, Int64}</c>. Both Metal and OpenCL require each field
/// of such a struct to be a separate kernel argument (buffer slot). This pass replaces
/// every such entry-point parameter with two flat parameters (one per field) and
/// rewrites all <c>GetField</c> uses so that they constant-fold to the appropriate flat
/// parameter.  The codegen layer then sees only primitive/pointer params and emits the
/// correct per-slot declarations without any special casing.<br/>
/// </remarks>
sealed class SplitViewLowering(TransformationArgs args) : Transformation(args)
{
    /// <summary>
    /// Tries to create a new split-view lowering.
    /// </summary>
    /// <param name="acceleratorType">The accelerator type to use.</param>
    /// <param name="args">The transformation args.</param>
    /// <returns>
    /// A split view lowering for the specific accelerator type if required.
    /// </returns>
    public static SplitViewLowering? TryCreate(
        AcceleratorType acceleratorType,
        TransformationArgs args) =>
        acceleratorType switch
        {
            AcceleratorType.OpenCL or AcceleratorType.Metal => new(args),
            _ => null
        };

    /// <inheritdoc/>
    protected override void OnMap(ModuleTransform transform)
    {
        base.OnMap(transform);

        MapMethodValue<Parameter>(RewriteEntryPointStructParam);
    }

    /// <summary>
    /// Converter for <see cref="Parameter"/> values.
    /// When the parameter belongs to the entry-point method and has a
    /// <see cref="StructureType"/> with pointer fields, the original parameter
    /// is replaced by one flat parameter per field and a temporary
    /// <see cref="StructureValue"/> that folds all downstream
    /// <c>GetField</c> accesses to the corresponding flat parameter.
    /// </summary>
    private Value? RewriteEntryPointStructParam(
        MethodTransform transform,
        Parameter param)
    {
        // Only apply to the kernel entry point.
        if (transform.OldMethod != transform.OldMethod.Module.EntryPoint)
            return param;

        // Only flatten structs that contain pointer fields (view structs).
        if (param.Type is not StructureType structType
            || !structType.HasAnyPointerField())
            return param;

        // Mark the pre-defined parameter copy for removal.
        // MethodTransform's constructor created a copy and registered
        // oldParam → preDefinedCopy. The chain-following protection in
        // ModuleTransform.Replace prevents the framework from marking
        // preDefinedCopy when it later calls Replace(oldParam, StructureValue).
        // Use ReplaceDirect to bypass chain-following so SealInternal's
        // predicate excludes the pre-defined copy from the final parameter list.
        if (transform.TryGetReplaced(param, out var preDefinedCopy)
            && preDefinedCopy is Parameter)
        {
            transform.ModuleTransform.ReplaceDirect(preDefinedCopy, null);
        }

        // Create one flat parameter per struct field.
        var flatParams = new Value[structType.NumFields];
        for (int i = 0; i < structType.NumFields; i++)
        {
            flatParams[i] = transform.CreateParameter(
                transform.Rewrite(structType.Fields[i]),
                $"p{param.Index}_f{i}");
        }

        // Return a StructureValue wrapping the flat params.
        // CreateGetField constant-folds GetField(StructureValue{p0, p1}, i) -> pi,
        // so all downstream field accesses automatically resolve to the flat params.
        return transform.CreateDynamicStructure(param.Location, flatParams);
    }
}
