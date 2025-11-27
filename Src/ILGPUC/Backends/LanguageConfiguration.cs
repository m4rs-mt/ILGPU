// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: LanguageConfiguration.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;
using System.Collections.Generic;

namespace ILGPUC.Backends;

/// <summary>
/// Configuration for language-specific code generation features.
/// Different C-like languages (Metal, OpenCL, CUDA, HIP) have slightly different
/// syntax and capabilities that are configured here.
/// </summary>
/// <remarks>
/// This class abstracts away language-specific differences in:
/// - Address space qualifiers (device/global/__shared__, threadgroup/local, etc.)
/// - Primitive type names (long vs long long, half vs __half)
/// - Built-in function names (threadIdx vs get_local_id)
/// - Kernel entry point attributes (__global__ vs kernel vs __kernel)
/// </remarks>
abstract class LanguageConfiguration
{
    /// <summary>
    /// Returns the keyword for the given address space.
    /// </summary>
    /// <remarks>
    /// Address space keywords are used to qualify pointer types and variable declarations
    /// to specify which memory space they reside in. Each target language has different
    /// syntax for this:
    ///
    /// Metal:
    /// - Global -> "device" (GPU global memory)
    /// - Shared -> "threadgroup" (thread group local shared memory)
    /// - Local -> "thread" (thread-private memory)
    /// - Constant -> "constant" (read-only constant memory)
    /// - Generic -> "" (empty)
    ///
    /// OpenCL:
    /// - Global -> "global" (GPU global memory)
    /// - Shared -> "local" (work group local shared memory)
    /// - Local -> "private" (work item private memory)
    /// - Constant -> "constant" (read-only constant memory)
    /// - Generic -> "" (empty)
    ///
    /// CUDA/HIP:
    /// - Global -> "__global__" (GPU global memory)
    /// - Shared -> "__shared__" (block-shared memory)
    /// - Local -> "" (no qualifier, default is per-thread)
    /// - Constant -> "__constant__" (read-only constant memory)
    /// - Generic -> "" (empty)
    /// </remarks>
    public abstract string GetAddressSpaceKeyword(MemoryAddressSpace addressSpace);

    /// <summary>
    /// Returns the type name for a primitive type.
    /// </summary>
    public abstract string GetPrimitiveTypeName(BasicValueType basicType);

    /// <summary>
    /// Returns the type name for a primitive type.
    /// </summary>
    public abstract string GetPrimitiveTypeName(ArithmeticBasicValueType basicType);

    /// <summary>
    /// Returns the type name for a view type given its element type name
    /// and address-space keyword (possibly empty).
    /// Default implementation returns pointer syntax with an optional
    /// leading address-space qualifier (e.g., <c>threadgroup int*</c>,
    /// <c>__local float*</c>).
    /// </summary>
    public virtual string GetViewTypeName(
        string elementTypeName,
        string addressSpaceKeyword) =>
        string.IsNullOrEmpty(addressSpaceKeyword)
            ? elementTypeName + "*"
            : $"{addressSpaceKeyword} {elementTypeName}*";

    /// <summary>
    /// Returns the keyword for a pointer type.
    /// </summary>
    public abstract string PointerSyntax { get; }

    /// <summary>
    /// Returns true if this language supports a specific feature.
    /// </summary>
    public abstract bool SupportsFeature(LanguageFeature feature);

    /// <summary>
    /// Gets all required header includes/imports.
    /// </summary>
    public abstract IEnumerable<string> GetHeaderIncludes();

    /// <summary>
    /// Gets language-specific pragmas or attributes.
    /// </summary>
    public virtual IEnumerable<string> GetPragmas() => [];

    /// <summary>
    /// Formats a null literal for a given type name.
    /// Default: C-style <c>TypeNULL</c>. CPU override: <c>default</c>.
    /// </summary>
    public virtual string FormatNullLiteral(string typeName) =>
        $"(({typeName}){{}})";  // Zero-initialized struct/value

    /// <summary>
    /// Returns the field name used to access a view's length.
    /// Default: <c>Field1</c> (lowered struct). CPU: <c>Length</c>.
    /// </summary>
    public virtual string ViewLengthFieldName =>
        StructureType.GetFieldName(1);

    /// <summary>
    /// Returns the syntax for declaring a structure.
    /// </summary>
    public virtual string StructKeyword => "struct";

    /// <summary>
    /// Returns the access modifier prefix for struct field declarations.
    /// C# structs require <c>public</c>; C/C++/Metal structs are public by default.
    /// </summary>
    public virtual string StructFieldModifier => "";

    /// <summary>
    /// Formats a default-initialized value for the given type name.
    /// C/C++/Metal: <c>(TypeName){}</c>, C#: <c>default(TypeName)</c>.
    /// </summary>
    public virtual string FormatDefaultValue(string typeName) =>
        $"(({typeName}){{}})";

    /// <summary>
    /// Returns true if a global variable with the given address space can be
    /// declared at file/program scope. When false, the global must be emitted
    /// inside the kernel function body instead.
    /// Metal requires <c>thread</c> and <c>threadgroup</c> variables to be
    /// function-scoped; only <c>constant</c> can be at program scope.
    /// </summary>
    public virtual bool IsFileScopeAddressSpace(MemoryAddressSpace addressSpace) => true;

    /// <summary>
    /// Returns true if pointers use '*' syntax, false if they use special keywords.
    /// </summary>
    public virtual bool UsesPointerStar => true;

    /// <summary>
    /// Returns the attribute/qualifier for the entry point function.
    /// </summary>
    public abstract string KernelAttribute { get; }

    /// <summary>
    /// Returns the attribute/qualifier for non-entry-point device
    /// functions. CUDA / HIP need <c>__device__</c> so the function is
    /// callable from the kernel; without it, nvcc treats unqualified
    /// functions as host-only and rejects calls from <c>__global__</c>.
    /// Metal, OpenCL, and CPU return empty (no per-function qualifier).
    /// </summary>
    public virtual string DeviceFunctionAttribute => string.Empty;

    /// <summary>
    /// Returns true if <c>LoadFieldAddress</c> values must be looked up
    /// by their pre-declared variable name (CPU C# emits an explicit
    /// <c>nint[]</c> via <c>CPUVectorIntrinsics.OffsetPointers</c>),
    /// false to inline as a C-style pointer cast (all GPU backends).
    /// </summary>
    /// <remarks>
    /// Replaces a <c>string.IsNullOrEmpty(addressSpaceKeyword)</c>
    /// heuristic that conflated CPU with CUDA's empty-keyword address
    /// spaces (Local, Generic, and — after the recent CUDA fix — Global)
    /// and caused undeclared <c>tmp_N</c> identifiers in CUDA fixed-buffer
    /// kernels.
    /// </remarks>
    public virtual bool LoadFieldAddressUsesVariable => false;

    /// <summary>
    /// Returns true if struct declarations should be followed by a
    /// matching <c>typedef struct &lt;name&gt; &lt;name&gt;;</c> alias so
    /// the bare struct name can be used to reference the type. C99 and
    /// OpenCL C require either the <c>struct</c> tag or a typedef alias
    /// at every reference site; C++ (CUDA, HIP, Metal, CPU) and C# do
    /// not. Defaults to false; OpenCL overrides to true.
    /// </summary>
    public virtual bool EmitStructTypedefAlias => false;

    /// <summary>
    /// Returns the intrinsic emitter for this language.
    /// </summary>
    public abstract IntrinsicEmitter CreateIntrinsicEmitter();

    /// <summary>
    /// Emits a kernel parameter declaration for the entry point.
    /// Override to append buffer index attributes (e.g., Metal's
    /// <c>[[buffer(n)]]</c>) or to wrap scalar types in <c>constant T&amp;</c>.
    /// </summary>
    /// <param name="type">The IR type of the parameter.</param>
    /// <param name="typeName">The target-language type name.</param>
    /// <param name="paramName">The generated parameter name.</param>
    /// <param name="bufferIndex">The current kernel argument/buffer slot index.</param>
    /// <returns>The full parameter declaration string to emit.</returns>
    public virtual string EmitNonFlattenedKernelParam(
        TypeValue type,
        string typeName,
        string paramName,
        int bufferIndex) =>
        $"{typeName} {paramName}";

    /// <summary>
    /// Returns true if the kernel entry point's first parameter (the thread index)
    /// should NOT be emitted as a buffer parameter. GPU backends return true and
    /// compute the index from built-in thread attributes instead.
    /// </summary>
    public virtual bool SkipsIndexParameter => false;

    /// <summary>
    /// Returns the code to compute the thread index from built-in
    /// thread attributes. Called at the start of the entry point body when
    /// <see cref="SkipsIndexParameter"/> is true.
    /// For 1D indices, computes a linear index. For 2D/3D struct indices,
    /// computes each component separately.
    /// </summary>
    /// <param name="typeName">The target-language type name for the index.</param>
    /// <param name="paramName">The generated parameter name.</param>
    /// <param name="indexType">The IR type of the index parameter.</param>
    /// <returns>Statement(s) to emit, or null if not applicable.</returns>
    public virtual string? EmitIndexComputation(
        string typeName, string paramName, TypeValue indexType) =>
        null;

    /// <summary>
    /// Returns true if struct literals should use C-style compound literal syntax:
    /// <c>(TypeName){ val0, val1, val2 }</c> instead of C# object initializer
    /// syntax: <c>new TypeName { Field0 = val0, ... }</c>.
    /// GPU backends (Metal, CUDA, OpenCL, ROCm) return true; CPU returns false.
    /// </summary>
    public virtual bool UsesCStyleStructLiterals => false;

    /// <summary>
    /// Returns additional built-in parameter declarations to append after all regular
    /// parameters in a kernel entry-point signature. Used by backends (Metal) where
    /// thread position, group index, etc. must be declared as explicit kernel parameters
    /// with attribute qualifiers rather than accessed as pre-declared globals.
    /// </summary>
    public virtual IEnumerable<string> GetKernelBuiltInParameters() => [];
}

/// <summary>
/// Enumeration of language features that may or may not be supported.
/// </summary>
enum LanguageFeature
{
    /// <summary>
    /// Support for generic/private address space.
    /// </summary>
    GenericAddressSpace,

    /// <summary>
    /// Support for constant address space.
    /// </summary>
    ConstantAddressSpace,

    /// <summary>
    /// Support for local/thread-group shared memory.
    /// </summary>
    LocalAddressSpace,

    /// <summary>
    /// Support for atomic operations.
    /// </summary>
    Atomics,

    /// <summary>
    /// Support for barrier/fence operations.
    /// </summary>
    Barriers,

    /// <summary>
    /// Support for inline functions.
    /// </summary>
    InlineFunctions,

    /// <summary>
    /// Support for broadcast operations on the group level.
    /// </summary>
    BroadcastGroupLevel,

    /// <summary>
    /// Support for C-style labeled statements (label:) used with goto.
    /// CUDA/HIP/OpenCL support this; Metal MSL does not.
    /// Note: the compiler currently never emits goto, so labels in structured
    /// control flow are dead code. This flag gates their emission so Metal is
    /// not broken by unreachable labels.
    /// </summary>
    LabeledStatements,
}
