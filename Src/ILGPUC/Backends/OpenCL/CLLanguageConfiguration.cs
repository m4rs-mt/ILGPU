// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLLanguageConfiguration.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------


// disable: max_line_length
using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
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
    public override bool UsesCStyleStructLiterals => true;

    /// <inheritdoc/>
    /// <remarks>get_local_id and friends return size_t.</remarks>
    public override bool HasUnsignedThreadBuiltIns => true;

    /// <inheritdoc/>
    public override string? FormatFieldAlignment(int alignment) =>
        $" __attribute__((aligned({alignment})))";

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
            // Generic pointers need OpenCL C 2.0 and a device that
            // supports them; ILGPU emits explicit address spaces only.
            LanguageFeature.GenericAddressSpace => false,
            LanguageFeature.ConstantAddressSpace => true,
            LanguageFeature.LocalAddressSpace => true,
            LanguageFeature.Atomics => true,
            LanguageFeature.Barriers => true,
            LanguageFeature.InlineFunctions => true,
            LanguageFeature.BroadcastGroupLevel => true,
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

    /// <summary>
    /// The suffix of the byte-offset parameter that follows every buffer
    /// pointer parameter of a kernel entry point.
    /// </summary>
    public const string BufferOffsetSuffix = "_offset";

    /// <inheritdoc/>
    /// <remarks>
    /// A buffer argument is a <c>cl_mem</c> handle, which cannot point into
    /// the middle of a buffer, so every buffer pointer is followed by the
    /// byte offset of the view within its buffer.
    /// </remarks>
    /// <remarks>
    /// OpenCL C forbids bool kernel arguments: bools are passed as one byte
    /// and converted in the prologue.
    /// </remarks>
    public override string EmitNonFlattenedKernelParam(
        TypeValue type,
        string typeName,
        string paramName,
        int bufferIndex) =>
        type switch
        {
            AddressSpaceType =>
                $"{typeName} {paramName}, long {paramName}{BufferOffsetSuffix}",
            PrimitiveType { BasicValueType: BasicValueType.Int1 } =>
                $"uchar {paramName}{BoolValueSuffix}",
            _ => $"{typeName} {paramName}",
        };

    /// <inheritdoc/>
    public override string? EmitKernelParamPrologue(
        TypeValue type,
        string typeName,
        string paramName) =>
        type switch
        {
            AddressSpaceType =>
                $"  {paramName} = ({typeName})" +
                $"((global char*){paramName} + {paramName}{BufferOffsetSuffix});",
            PrimitiveType { BasicValueType: BasicValueType.Int1 } =>
                $"  bool {paramName} = {paramName}{BoolValueSuffix} != 0;",
            _ => null,
        };

    /// <summary>
    /// The suffix of the one-byte kernel parameter carrying a bool argument.
    /// </summary>
    public const string BoolValueSuffix = "_value";

    /// <inheritdoc/>
    public override string? BoolStorageTypeName => "uchar";

    /// <inheritdoc/>
    /// <remarks>
    /// The launcher only passes the kernel's own arguments; the thread index
    /// is computed from the work-item built-ins.
    /// </remarks>
    public override bool SkipsIndexParameter => true;

    /// <inheritdoc/>
    /// <remarks>
    /// The NDRange is rounded up to whole work-groups; the extra work-items
    /// are skipped by the guard against the hidden extent parameters.
    /// </remarks>
    public override string? LaunchExtentTypeName => "long";

    /// <inheritdoc/>
    public override string? EmitIndexComputation(
        string typeName,
        string paramName,
        TypeValue indexType,
        EntryPointLaunch launch)
    {
        // Grouped launch: KernelIndex {long GridIndex, int GroupIndex} is
        // the work-group id and the work-item id within the group.
        if (launch.HasKernelIndex)
        {
            var kernelIndex = new System.Text.StringBuilder();
            kernelIndex.AppendLine($"  {typeName} {paramName};");
            kernelIndex.AppendLine($"  {paramName}.Field0 = (long)get_group_id(0);");
            kernelIndex.AppendLine($"  {paramName}.Field1 = (int)get_local_id(0);");
            return kernelIndex.ToString().TrimEnd();
        }

        if (indexType is StructureType structType)
        {
            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"  {typeName} {paramName};");
            for (int i = 0; i < structType.NumFields && i < 3; i++)
                lines.AppendLine($"  {paramName}.Field{i} = (int)get_global_id({i});");
            return lines.ToString().TrimEnd();
        }

        return $"  {typeName} {paramName} = ({typeName})get_global_id(0);";
    }

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
        // 64-bit atomics (atom_* built-ins); like cl_khr_fp64 above, kernels
        // without 64-bit atomics are unaffected on devices lacking them.
        yield return "#pragma OPENCL EXTENSION cl_khr_int64_base_atomics : enable";
        yield return
            "#pragma OPENCL EXTENSION cl_khr_int64_extended_atomics : enable";
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
