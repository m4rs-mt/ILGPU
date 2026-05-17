// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: EntryPointLaunch.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR;
using ILGPUC.IR.ModuleValues;

namespace ILGPUC.Backends;

/// <summary>
/// Describes how a kernel entry point is launched. The device code generator
/// and the generated launcher both derive the kernel's index parameter, its
/// hidden launch arguments and the launch shape from this, so they always
/// agree.
/// </summary>
/// <param name="IndexDimensions">
/// For implicitly grouped launches the number of index dimensions (1-3). For
/// explicitly grouped launches 1 if the kernel takes a <c>KernelIndex</c>
/// parameter, 0 if it takes no index parameter.
/// </param>
/// <param name="IsGrouped">
/// True if the kernel is launched with an explicit <c>KernelConfig</c>.
/// </param>
readonly record struct EntryPointLaunch(int IndexDimensions, bool IsGrouped)
{
    /// <summary>
    /// Returns an implicitly grouped launch with the given dimensions.
    /// </summary>
    public static EntryPointLaunch Implicit(int dimensions) => new(dimensions, false);

    /// <summary>
    /// Returns an explicitly grouped launch.
    /// </summary>
    /// <param name="hasKernelIndex">
    /// True if the kernel takes a <c>KernelIndex</c> parameter.
    /// </param>
    public static EntryPointLaunch Grouped(bool hasKernelIndex) =>
        new(hasKernelIndex ? 1 : 0, true);

    /// <summary>
    /// Returns true if the entry point's first parameter is the thread index.
    /// </summary>
    public bool HasIndexParameter => !IsGrouped || IndexDimensions > 0;

    /// <summary>
    /// Returns true if the first parameter is a <c>KernelIndex</c>.
    /// </summary>
    public bool HasKernelIndex => IsGrouped && IndexDimensions > 0;

    /// <summary>
    /// Returns true for implicitly grouped launches. Their launch rounds the
    /// thread count up to whole groups, so the kernel must skip the threads
    /// beyond the user's extent.
    /// </summary>
    public bool IsImplicit => !IsGrouped;

    /// <summary>
    /// Returns the name of the hidden kernel parameter that carries the
    /// launch extent of the given dimension for implicitly grouped launches.
    /// </summary>
    public static string GetExtentParameterName(int dimension) =>
        $"_ilgpuc_extent{dimension}";

    /// <summary>
    /// Returns the given launch, or derives one from the entry point's first
    /// parameter when none was provided (paths that compile a kernel without
    /// a Roslyn-analyzed launch site).
    /// </summary>
    /// <param name="module">The module whose entry point is launched.</param>
    /// <param name="launch">The launch described by the launch site, if any.</param>
    public static EntryPointLaunch Resolve(Module module, EntryPointLaunch? launch)
    {
        if (launch is { } known)
            return known;

        var entryPoint = module.EntryPoint;
        if (entryPoint is null || entryPoint.NumParameters < 1)
            return Implicit(1);

        // Index types are pure numeric structs; views and user structs that
        // contain views carry pointer/view flags.
        if (entryPoint.Parameters[0].Type is StructureType st
            && !st.HasFlags(TypeFlags.PointerDependent | TypeFlags.ViewDependent))
        {
            return IsKernelIndexShape(st)
                ? Grouped(hasKernelIndex: true)
                : Implicit(st.NumFields);
        }
        return Implicit(1);
    }

    /// <summary>
    /// Matches the flat field layout of <c>ILGPU.KernelIndex</c>:
    /// <c>{i64 GridIndex, i32 GroupIndex}</c>. Multi-dimensional index types
    /// use the same type for all fields.
    /// </summary>
    public static bool IsKernelIndexShape(StructureType st) =>
        st.NumFields == 2
        && st.Fields[0] is PrimitiveType { BasicValueType: BasicValueType.Int64 }
        && st.Fields[1] is PrimitiveType { BasicValueType: BasicValueType.Int32 };
}
