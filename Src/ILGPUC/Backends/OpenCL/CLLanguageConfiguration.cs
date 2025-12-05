// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLLanguageConfiguration.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using System.Collections.Generic;

namespace ILGPUC.Backends.OpenCL;

/// <summary>
/// Identifies the OpenCL vendor for intrinsic selection.
/// </summary>
enum CLVendor
{
    /// <summary>Intel GPU (uses cl_intel_subgroups).</summary>
    Intel,
    /// <summary>AMD GPU (uses cl_khr_subgroup_shuffle).</summary>
    Amd,
}

/// <summary>
/// OpenCL-specific language configuration.
/// </summary>
sealed class CLLanguageConfiguration(CLVendor vendor = CLVendor.Intel) : LanguageConfiguration
{
    /// <inheritdoc/>
    public override string GetAddressSpaceKeyword(MemoryAddressSpace addressSpace) =>
        addressSpace switch
        {
            MemoryAddressSpace.Generic => "global",
            MemoryAddressSpace.Global => "global",
            MemoryAddressSpace.Shared => "local",
            MemoryAddressSpace.Local => "private",
            MemoryAddressSpace.Constant => "constant",
            _ => "global"
        };

    /// <inheritdoc/>
    public override string GetPrimitiveTypeName(BasicValueType basicType) =>
        basicType switch
        {
            BasicValueType.Int1 => "bool",
            BasicValueType.Int8 => "char",
            BasicValueType.Int16 => "short",
            BasicValueType.Int32 => "int",
            BasicValueType.Int64 => "long",
            BasicValueType.Float16 => "half",
            BasicValueType.Float32 => "float",
            BasicValueType.Float64 => "double",
            _ => "void"
        };

    /// <inheritdoc/>
    public override string GetPrimitiveTypeName(ArithmeticBasicValueType basicType) =>
        basicType switch
        {
            ArithmeticBasicValueType.UInt1 => "bool",
            ArithmeticBasicValueType.Int8 => "char",
            ArithmeticBasicValueType.Int16 => "short",
            ArithmeticBasicValueType.Int32 => "int",
            ArithmeticBasicValueType.Int64 => "long",
            ArithmeticBasicValueType.Float16 => "half",
            ArithmeticBasicValueType.Float32 => "float",
            ArithmeticBasicValueType.Float64 => "double",
            ArithmeticBasicValueType.UInt8 => "uchar",
            ArithmeticBasicValueType.UInt16 => "ushort",
            ArithmeticBasicValueType.UInt32 => "uint",
            ArithmeticBasicValueType.UInt64 => "ulong",
            _ => "void"
        };

    /// <inheritdoc/>
    public override string PointerSyntax => "*";

    /// <inheritdoc/>
    public override bool UsesCStyleStructLiterals => true;

    /// <inheritdoc/>
    /// <remarks>
    /// OpenCL C is C99-based and requires the <c>struct</c> tag at every
    /// reference site (the codegen always uses the bare name). Emit a
    /// matching <c>typedef struct &lt;name&gt; &lt;name&gt;;</c> alongside
    /// every struct declaration so the bare name resolves.
    /// </remarks>
    public override bool EmitStructTypedefAlias => true;

    /// <inheritdoc/>
    public override bool SupportsFeature(LanguageFeature feature) =>
        feature switch
        {
            LanguageFeature.GenericAddressSpace => true,
            LanguageFeature.ConstantAddressSpace => true,
            LanguageFeature.LocalAddressSpace => true,
            LanguageFeature.Atomics => true,
            LanguageFeature.Barriers => true,
            LanguageFeature.InlineFunctions => true,
            LanguageFeature.BroadcastGroupLevel => true,
            // OpenCL C is C99-based, and C99 forbids a label at the end
            // of a compound statement (the label must be followed by an
            // actual statement). The IR's basic-block labels are dead
            // code anyway — the codegen never emits a `goto` — so
            // suppressing them entirely matches what Metal does and
            // unblocks every kernel with nested control flow that ends
            // a block on a label.
            LanguageFeature.LabeledStatements => false,
            _ => false
        };

    /// <inheritdoc/>
    public override IEnumerable<string> GetHeaderIncludes()
    {
        // OpenCL doesn't require explicit includes
        yield break;
    }

    /// <inheritdoc/>
    /// <remarks>
    /// OpenCL C disallows program-scope variables in any address space
    /// other than <c>constant</c>. <c>private</c> (per-thread / Local in
    /// the IR) and <c>local</c> (work-group shared / Shared in the IR)
    /// must live inside the kernel function body — ocloc rejects
    /// file-scope ones with: <c>error: program scope variable must
    /// reside in global or constant address space</c>. The IR pass
    /// <c>LowerArrays</c> creates <c>MemoryAddressSpace.Local</c>
    /// globals for stack-allocated <c>new T[N]</c>, and shared-memory
    /// intrinsics create <c>MemoryAddressSpace.Shared</c> globals; both
    /// need to be hoisted to function scope by
    /// <c>MethodEmitter.EmitFunctionScopedGlobals</c>.
    /// </remarks>
    public override bool IsFileScopeAddressSpace(MemoryAddressSpace addressSpace) =>
        addressSpace == MemoryAddressSpace.Constant
        || addressSpace == MemoryAddressSpace.Global;

    /// <inheritdoc/>
    public override string KernelAttribute => "__kernel";

    /// <inheritdoc/>
    public override IEnumerable<string> GetPragmas()
    {
        // cl_khr_fp64 is required for any kernel that uses `double` —
        // OpenCL C disallows it without an explicit enable pragma.
        // Emitting unconditionally is safe: implementations without
        // FP64 support produce a warning (not an error) and FP64-using
        // kernels are already gated by the BackendCapability.Float64
        // skip path on devices that lack the extension.
        yield return "#pragma OPENCL EXTENSION cl_khr_fp64 : enable";
        yield return "#pragma OPENCL EXTENSION cl_khr_subgroups : enable";
        if (vendor == CLVendor.Intel)
            yield return "#pragma OPENCL EXTENSION cl_intel_subgroups : enable";
        else
            yield return "#pragma OPENCL EXTENSION cl_khr_subgroup_shuffle : enable";
    }

    /// <inheritdoc/>
    public override IntrinsicEmitter CreateIntrinsicEmitter() =>
        new CLIntrinsicEmitter(vendor);

    // Default EmitFlattenedKernelField works for OpenCL: just "type name" (e.g.,
    // "__global float* param_Field0", "long param_Field1") — no attributes needed.
}
