// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: CUDALanguageConfiguration.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
using System.Collections.Generic;
using System.Text;

namespace ILGPUC.Backends.Cuda;

/// <summary>
/// CUDA-specific language configuration.
/// </summary>
class CudaLanguageConfiguration : LanguageConfiguration
{
    /// <inheritdoc/>
    /// <remarks>
    /// CUDA pointer types do NOT carry an address-space qualifier — only
    /// variable declarations do, and even then only for shared/constant
    /// (global memory uses <c>__device__</c> at file scope but plain
    /// pointers in kernel arguments and struct fields are unqualified).
    /// Returning <c>__global__</c> for <see cref="MemoryAddressSpace.Global"/>
    /// here is wrong: <c>__global__</c> in CUDA is the kernel-function
    /// attribute, not a storage class. nvcc translates it to
    /// <c>__attribute__((global))</c> and warns when it appears on a
    /// type/field. Local has no qualifier — it lowers to per-thread
    /// stack memory inside the kernel via
    /// <see cref="IsFileScopeAddressSpace"/>.
    /// </remarks>
    public override string GetAddressSpaceKeyword(MemoryAddressSpace addressSpace) =>
        addressSpace switch
        {
            MemoryAddressSpace.Shared => "__shared__",
            MemoryAddressSpace.Constant => "__constant__",
            _ => ""
        };

    /// <inheritdoc/>
    /// <remarks>
    /// Per-thread <see cref="MemoryAddressSpace.Local"/> globals (used by
    /// <c>LowerArrays</c> for stack-allocated <c>new T[N]</c>) must be
    /// emitted inside the kernel function body, not at file scope. nvcc
    /// treats unqualified file-scope variables as host variables and
    /// rejects any device-side reference to them with
    /// "identifier ... is undefined in device code". Constant and shared
    /// remain file-scope (file-scope <c>__shared__</c> is accepted by
    /// nvcc and shared by all blocks).
    /// </remarks>
    public override bool IsFileScopeAddressSpace(MemoryAddressSpace addressSpace) =>
        addressSpace != MemoryAddressSpace.Local;

    /// <inheritdoc/>
    public override string GetPrimitiveTypeName(BasicValueType basicType) =>
        basicType switch
        {
            BasicValueType.Int1 => "bool",
            BasicValueType.Int8 => "signed char",
            BasicValueType.Int16 => "short",
            BasicValueType.Int32 => "int",
            BasicValueType.Int64 => "long long",
            BasicValueType.Float16 => "__half",
            BasicValueType.Float32 => "float",
            BasicValueType.Float64 => "double",
            _ => "void"
        };

    /// <inheritdoc/>
    public override string GetPrimitiveTypeName(ArithmeticBasicValueType basicType) =>
        basicType switch
        {
            ArithmeticBasicValueType.UInt1 => "bool",
            ArithmeticBasicValueType.Int8 => "signed char",
            ArithmeticBasicValueType.Int16 => "short",
            ArithmeticBasicValueType.Int32 => "int",
            ArithmeticBasicValueType.Int64 => "long long",
            ArithmeticBasicValueType.Float16 => "__half",
            ArithmeticBasicValueType.Float32 => "float",
            ArithmeticBasicValueType.Float64 => "double",
            ArithmeticBasicValueType.UInt8 => "unsigned char",
            ArithmeticBasicValueType.UInt16 => "unsigned short",
            ArithmeticBasicValueType.UInt32 => "unsigned int",
            ArithmeticBasicValueType.UInt64 => "unsigned long long",
            _ => "void"
        };

    /// <inheritdoc/>
    /// <remarks>
    /// CUDA / HIP pointer types do not take a pointee address-space
    /// qualifier (unlike Metal's <c>threadgroup T*</c> or OpenCL's
    /// <c>__local T*</c>). Prepending <c>__shared__</c> to a pointer
    /// variable declaration places the variable itself in shared memory,
    /// turning a per-thread pointer into a single shared cell that races
    /// on assignment from every thread. Always emit pointer types as the
    /// bare element-type + <c>*</c>; the address space is carried by
    /// the storage of the pointee (file-scope <c>__shared__</c> globals,
    /// kernel arguments, etc.) and tracked by nvcc automatically.
    /// </remarks>
    public override string GetPointerTypeName(
        string elementTypeName,
        string addressSpaceKeyword) =>
        elementTypeName + "*";

    /// <inheritdoc/>
    /// <remarks>
    /// Same rationale as <see cref="GetPointerTypeName"/>: view types are
    /// lowered to plain pointers on CUDA / HIP, so the local view variable
    /// must NOT inherit the pointee's address-space qualifier — that
    /// keyword would attach to the variable itself and turn the local
    /// pointer into a single shared cell. The pointee's storage already
    /// carries the address space; the local just holds an address.
    /// </remarks>
    public override string GetViewTypeName(
        string elementTypeName,
        string addressSpaceKeyword) =>
        elementTypeName + "*";

    /// <inheritdoc/>
    public override bool UsesCStyleStructLiterals => true;

    /// <inheritdoc/>
    /// <remarks>threadIdx, blockIdx and blockDim are unsigned int.</remarks>
    public override bool HasUnsignedThreadBuiltIns => true;

    /// <inheritdoc/>
    public override string? FormatFieldAlignment(int alignment) =>
        $" __attribute__((aligned({alignment})))";

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
            LanguageFeature.LabeledStatements => true, // nvcc/hipcc are C++ compilers
            _ => false
        };

    /// <inheritdoc/>
    public override IEnumerable<string> GetHeaderIncludes()
    {
        // nvcc includes the CUDA runtime implicitly, but not __half and its
        // conversions and intrinsics
        yield return "#include <cuda_fp16.h>";
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Apply <c>extern "C"</c> so the entry point keeps its plain C
    /// identifier. Without it the C++ front-end of <c>nvcc</c> mangles the
    /// symbol (e.g. <c>_Z29method_…i</c>), and the runtime kernel loader
    /// looks up the unmangled name passed via <c>KernelName</c> and fails
    /// with <c>named symbol not found</c>.
    /// </remarks>
    public override string KernelAttribute => "extern \"C\" __global__";

    /// <inheritdoc/>
    /// <remarks>
    /// CUDA / HIP non-entry methods need <c>__device__</c> so they are
    /// callable from <c>__global__</c> kernels. Without this qualifier,
    /// nvcc treats them as host-only and rejects the call site.
    /// </remarks>
    public override string DeviceFunctionAttribute => "__device__";

    /// <summary>
    /// Returns the CUDA / HIP built-in identifier for the block index
    /// along the given dimension axis (e.g. <c>blockIdx.x</c>).
    /// </summary>
    /// <remarks>
    /// Overridden by <c>ROCmLanguageConfiguration</c> to emit
    /// <c>hipBlockIdx_x</c> / <c>hipBlockIdx_y</c> / <c>hipBlockIdx_z</c>
    /// instead, matching the per-axis form HIP exposes.
    /// </remarks>
    protected virtual string GetBlockIndexExpr(char axis) =>
        $"blockIdx.{axis}";

    /// <summary>
    /// Returns the CUDA / HIP built-in identifier for the block dimension
    /// (group size) along the given dimension axis.
    /// </summary>
    protected virtual string GetBlockDimExpr(char axis) =>
        $"blockDim.{axis}";

    /// <summary>
    /// Returns the CUDA / HIP built-in identifier for the thread index
    /// within the block along the given dimension axis.
    /// </summary>
    protected virtual string GetThreadIndexExpr(char axis) =>
        $"threadIdx.{axis}";

    /// <inheritdoc/>
    /// <remarks>
    /// The IL frontend declares an explicit thread-index parameter on every
    /// kernel entry point (e.g. <c>Index1D index</c>). CUDA / HIP kernels
    /// receive the index via the <c>blockIdx</c> / <c>blockDim</c> /
    /// <c>threadIdx</c> built-ins instead, so we strip that parameter
    /// from the emitted C++ signature and reconstruct it at the top of
    /// the kernel body via <see cref="EmitIndexComputation"/>. Without
    /// this the kernel signature has one more parameter than the
    /// launcher passes, the kernel reads the views from garbage stack
    /// space, and the first device access produces
    /// <c>an illegal memory access was encountered</c>.
    /// </remarks>
    public override bool SkipsIndexParameter => true;

    /// <inheritdoc/>
    /// <remarks>
    /// Implicitly grouped launches round the thread count up to whole
    /// blocks; the extra threads are skipped by the guard against the hidden
    /// extent parameters.
    /// </remarks>
    public override string? LaunchExtentTypeName => "long long";

    /// <inheritdoc/>
    public override string? EmitIndexComputation(
        string typeName,
        string paramName,
        TypeValue indexType,
        EntryPointLaunch launch)
    {
        const char xAxis = 'x';

        // Grouped launch: KernelIndex {long GridIndex, int GroupIndex} is
        // the block id and the thread id within the block.
        if (launch.HasKernelIndex)
        {
            var kernelIndex = new System.Text.StringBuilder();
            kernelIndex.AppendLine($"  {typeName} {paramName};");
            kernelIndex.AppendLine(
                $"  {paramName}.Field0 = (long long){GetBlockIndexExpr(xAxis)};");
            kernelIndex.AppendLine(
                $"  {paramName}.Field1 = (int){GetThreadIndexExpr(xAxis)};");
            return kernelIndex.ToString().TrimEnd();
        }

        // Multi-dim index (Index2D / Index3D) — derive each field from the
        // corresponding axis built-ins.
        if (indexType is StructureType structType)
        {
            string[] axes = ["x", "y", "z"];
            var lines = new System.Text.StringBuilder();
            lines.AppendLine($"  {typeName} {paramName};");
            for (int i = 0; i < structType.NumFields && i < 3; i++)
            {
                char a = axes[i][0];
                lines.AppendLine(
                    $"  {paramName}.Field{i} = (int)(" +
                    $"{GetBlockIndexExpr(a)} * {GetBlockDimExpr(a)} + " +
                    $"{GetThreadIndexExpr(a)});");
            }
            return lines.ToString().TrimEnd();
        }

        // 1D scalar index — compute a linear thread id on the x axis in 64
        // bits so long indices do not wrap.
        return $"  {typeName} {paramName} = ({typeName})(" +
            $"(long long){GetBlockIndexExpr(xAxis)} * {GetBlockDimExpr(xAxis)} + " +
            $"{GetThreadIndexExpr(xAxis)});";
    }

    /// <inheritdoc/>
    public override IntrinsicEmitter CreateIntrinsicEmitter() => new
        CudaIntrinsicEmitter();
}
