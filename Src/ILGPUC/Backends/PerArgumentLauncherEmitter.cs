// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: PerArgumentLauncherEmitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPUC.IR.ModuleValues;
using System.Collections.Generic;

namespace ILGPUC.Backends;

/// <summary>
/// Shared launcher emitter base for OpenCL and Metal backends that set
/// kernel arguments one at a time (per-argument API calls).
/// </summary>
abstract class PerArgumentLauncherEmitter : LauncherEmitter
{
    /// <inheritdoc/>
    public sealed override bool NeedsKernelArgsStruct => false;

    /// <summary>
    /// Emits setup code at the start of the launch body (kernel/stream casting, etc.).
    /// </summary>
    protected abstract void EmitPreamble(LauncherEmissionContext ctx);

    /// <summary>
    /// Returns the buffer index at which user kernel arguments start.
    /// The kernel's implicit index parameter occupies buffer(0), so GPU
    /// backends return 1. Override if the backend uses a different layout.
    /// </summary>
    protected virtual int FirstUserArgIndex => 0;

    /// <summary>
    /// Emits code to set a primitive or plain-struct argument at the given index.
    /// </summary>
    protected abstract void EmitPrimitiveArg(
        LauncherEmissionContext ctx,
        string expression,
        string marshaledTypeName,
        int argIndex);

    /// <summary>
    /// Emits code to set a view argument (expanded to pointer + length) starting
    /// at the given index.
    /// </summary>
    /// <param name="ctx">The emission context.</param>
    /// <param name="viewImplVar">
    /// Name of the <c>ViewImplementation&lt;T&gt;</c> local variable.
    /// </param>
    /// <param name="viewSourceExpr">
    /// The original <c>ArrayView&lt;T&gt;</c> expression (parameter name or
    /// struct field access) that the view was created from. Backends that need
    /// the underlying <see cref="IArrayView.Buffer"/> can use this expression.
    /// </param>
    /// <param name="argIndex">The current kernel argument index.</param>
    /// <returns>The next argument index after the view args.</returns>
    protected abstract int EmitViewArg(
        LauncherEmissionContext ctx,
        string viewImplVar,
        string viewSourceExpr,
        int argIndex);

    /// <summary>
    /// Emits code to set a pointer argument at the given index.
    /// </summary>
    protected abstract void EmitPointerArg(
        LauncherEmissionContext ctx,
        string expression,
        int argIndex);

    /// <summary>
    /// Emits finalization code after all arguments (dispatch, shared memory, etc.).
    /// </summary>
    protected abstract void EmitPostamble(
        LauncherEmissionContext ctx,
        int nextArgIndex);

    public sealed override void EmitLaunchBody(
        LauncherEmissionContext ctx,
        List<ParameterInfo> parameters)
    {
        EmitPreamble(ctx);

        // The IR backend transform (SplitViewLowering) flattens struct
        // parameters with pointer fields. Flattened fields are appended
        // after all non-struct params, so the kernel expects: scalars
        // first, then flattened view/struct fields. Emit in that order.
        int argIndex = FirstUserArgIndex;

        // Pass 1: non-view, non-struct-with-views parameters
        foreach (var param in parameters)
        {
            if (param.Kind is ParameterKind.View
                or ParameterKind.StructWithViews)
                continue;

            argIndex = EmitSingleArg(ctx, param, argIndex);
        }

        // Pass 2: view and struct-with-views parameters
        foreach (var param in parameters)
        {
            if (param.Kind is not ParameterKind.View
                and not ParameterKind.StructWithViews)
                continue;

            argIndex = EmitSingleArg(ctx, param, argIndex);
        }

        EmitPostamble(ctx, argIndex);
    }

    /// <summary>
    /// Emits a single parameter's arguments.
    /// </summary>
    private int EmitSingleArg(
        LauncherEmissionContext ctx,
        ParameterInfo param,
        int argIndex)
    {
        switch (param.Kind)
        {
            case ParameterKind.Primitive:
            case ParameterKind.StructPlain:
                var sizeofType = param.LaunchTypeName
                    ?? param.MarshaledTypeName;
                EmitPrimitiveArg(
                    ctx, param.Name, sizeofType, argIndex);
                return argIndex + 1;

            case ParameterKind.View:
            {
                var viewImpl = $"viewImpl_{argIndex}";
                var baseView = $"{param.Name}.BaseView";
                ctx.WriteLine(
                    $"var {viewImpl} = {baseView}.IsValid" +
                    $" ? new ViewImplementation<byte>(" +
                    $"Unsafe.AsPointer(ref {baseView}" +
                    $".LoadEffectiveAddress()), " +
                    $"{baseView}.Length)" +
                    $" : default(ViewImplementation<byte>);");
                return EmitViewArg(
                    ctx, viewImpl, param.Name, argIndex);
            }

            case ParameterKind.StructWithViews:
                return EmitStructArgs(
                    ctx, (StructureType)param.Type, param.Name, argIndex,
                    param.UserFieldAccessors);

            case ParameterKind.Pointer:
                EmitPointerArg(ctx, param.Name, argIndex);
                return argIndex + 1;

            default:
                return argIndex;
        }
    }

    /// <summary>
    /// Recursively flattens a struct with views into individual arguments.
    /// </summary>
    private int EmitStructArgs(
        LauncherEmissionContext ctx,
        StructureType st,
        string accessPrefix,
        int argIndex,
        string[]? userFieldAccessors = null)
    {
        for (int i = 0; i < st.NumFields; i++)
        {
            var fieldType = st.Fields[i];
            var fieldName = userFieldAccessors is not null
                && i < userFieldAccessors.Length
                ? userFieldAccessors[i]
                : StructureType.GetFieldName(i);
            var fieldAccess = $"{accessPrefix}.{fieldName}";

            switch (fieldType)
            {
                case ViewType vt:
                    {
                        var viewImpl = $"viewImpl_{argIndex}";
                        // Use byte + raw constructor to avoid type mismatch
                        ctx.WriteLine(
                            $"var {viewImpl} = {fieldAccess}.IsValid" +
                            $" ? new ViewImplementation<byte>(" +
                            $"Unsafe.AsPointer(ref {fieldAccess}" +
                            $".LoadEffectiveAddress()), " +
                            $"{fieldAccess}.Length)" +
                            $" : default(ViewImplementation<byte>);");
                        argIndex = EmitViewArg(
                            ctx, viewImpl, fieldAccess, argIndex);
                        break;
                    }

                case StructureType nested when
                    nested.HasFlags(TypeFlags.ViewDependent):
                    argIndex = EmitStructArgs(
                        ctx, nested, fieldAccess, argIndex);
                    break;

                default:
                    EmitPrimitiveArg(
                        ctx,
                        fieldAccess,
                        ctx.GetPrimitiveTypeName(fieldType),
                        argIndex);
                    argIndex++;
                    break;
            }
        }

        return argIndex;
    }
}
