// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaLanguageConfiguration.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using System.Collections.Generic;

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
            BasicValueType.Int8 => "char",
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
            ArithmeticBasicValueType.Int8 => "char",
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
    public override string PointerSyntax => "*";

    /// <inheritdoc/>
    public override bool UsesCStyleStructLiterals => true;

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
        // CUDA includes are typically handled by the compiler
        yield break;
    }

    /// <inheritdoc/>
    public override string KernelAttribute => "__global__";

    /// <inheritdoc/>
    /// <remarks>
    /// CUDA / HIP non-entry methods need <c>__device__</c> so they are
    /// callable from <c>__global__</c> kernels. Without this qualifier,
    /// nvcc treats them as host-only and rejects the call site.
    /// </remarks>
    public override string DeviceFunctionAttribute => "__device__";

    /// <inheritdoc/>
    public override IntrinsicEmitter CreateIntrinsicEmitter() => new
        CudaIntrinsicEmitter();
}
