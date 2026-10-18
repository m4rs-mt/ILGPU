// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: LauncherAlignmentTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Xunit;
using Xunit.Abstractions;

namespace ILGPUC.Tests.BackendTests;

/// <summary>
/// Hardware-free launch-contract tests: on every GPU backend, the parameter list of
/// the generated kernel signature must align slot for slot with the arguments the
/// generated launcher supplies, for implicitly grouped launches (index computed from
/// thread built-ins, launch extents as hidden trailing parameters) and explicitly
/// grouped launches with and without a <c>KernelIndex</c>.
/// </summary>
/// <remarks>
/// The CPU backend is not covered: its launcher calls the generated entry point
/// directly, so the C# compiler already rejects any mismatch.
/// </remarks>
public sealed class LauncherAlignmentTests(ITestOutputHelper output)
{
    /// <summary>
    /// Kernels launched with an explicit <c>KernelConfig</c>. Declared outside the
    /// <c>ILGPUC.Tests.Kernels</c> namespace so that <see cref="KernelRegistry"/>
    /// does not discover them as implicitly grouped kernels.
    /// </summary>
    private static class GroupedLaunchKernels
    {
        /// <summary>
        /// Parameter 0 is a user parameter (a view); the thread index is derived
        /// from the grid and group intrinsics.
        /// </summary>
        public static void GroupedStoreKernel(
            ArrayView1D<int, Stride1D.Dense> data,
            int value)
        {
            long idx = Grid.GlobalThreadIndex;
            if (idx < data.Length)
                data[idx] = value;
        }

        /// <summary>
        /// Parameter 0 is a <c>KernelIndex</c> that the kernel computes from the
        /// grid and group intrinsics.
        /// </summary>
        public static void GroupedKernelIndexKernel(
            KernelIndex index,
            ArrayView1D<int, Stride1D.Dense> data,
            int value)
        {
            data[index.GridIndex * ILGPU.Group.Dimension + index.GroupIndex] = value;
        }
    }

    /// <summary>
    /// Implicitly grouped kernels covering the marshaling matrix: 1D to 3D indices,
    /// a view with trailing scalars, a sub-view, a structure parameter and views of
    /// pointers.
    /// </summary>
    private static readonly Dictionary<string, (Type Class, int Dimensions)>
        ImplicitKernelTable = new()
        {
            [nameof(KernelEntryPointKernels.Index1DKernel)] =
                (typeof(KernelEntryPointKernels), 1),
            [nameof(KernelEntryPointKernels.Index2DKernel)] =
                (typeof(KernelEntryPointKernels), 2),
            [nameof(KernelEntryPointKernels.Index3DKernel)] =
                (typeof(KernelEntryPointKernels), 3),
            [nameof(ArrayViewKernels.SubViewKernel)] =
                (typeof(ArrayViewKernels), 1),
            [nameof(StructureKernels.StructFieldAccessKernel)] =
                (typeof(StructureKernels), 1),
            [nameof(AddressSpaceKernels.PointerViewKernel)] =
                (typeof(AddressSpaceKernels), 1),
        };

    /// <summary>
    /// The GPU backends.
    /// </summary>
    private static readonly BackendType[] GpuBackends =
    [
        BackendType.Cuda,
        BackendType.ROCm,
        BackendType.OpenCL,
        BackendType.Metal,
    ];

    /// <summary>
    /// All GPU backends combined with all implicitly grouped kernels.
    /// </summary>
    public static TheoryData<BackendType, string> ImplicitKernels()
    {
        var data = new TheoryData<BackendType, string>();
        foreach (var backend in GpuBackends)
        {
            foreach (var kernel in ImplicitKernelTable.Keys)
                data.Add(backend, kernel);
        }
        return data;
    }

    /// <summary>
    /// All GPU backends.
    /// </summary>
    public static TheoryData<BackendType> Backends()
    {
        var data = new TheoryData<BackendType>();
        foreach (var backend in GpuBackends)
            data.Add(backend);
        return data;
    }

    /// <summary>
    /// Implicitly grouped launches compute the index inside the kernel and pass the
    /// launch extents as hidden trailing parameters, one per dimension, on the
    /// backends whose launch rounds the thread count up to whole groups (Metal
    /// dispatches the exact thread count instead).
    /// </summary>
    [Theory]
    [MemberData(nameof(ImplicitKernels))]
    public void ImplicitLaunch_SignatureMatchesLauncher(
        BackendType backend,
        string kernelName)
    {
        var (kernelClass, dimensions) = ImplicitKernelTable[kernelName];
        var signature = GenerateAndAlign(
            backend,
            GetKernel(kernelClass, kernelName),
            new EntryPointLaunch(dimensions, IsGrouped: false));

        AssertNoIndexParameter(signature);

        var extents = signature
            .Select(ExtractParamName)
            .Where(name => name.StartsWith("_ilgpuc_extent", StringComparison.Ordinal))
            .ToList();
        var expectedExtents = backend == BackendType.Metal
            ? []
            : Enumerable.Range(0, dimensions)
                .Select(EntryPointLaunch.GetExtentParameterName)
                .ToList();
        Assert.Equal(expectedExtents, extents);

        // The extents trail the user parameters
        Assert.Equal(
            expectedExtents,
            signature.TakeLast(expectedExtents.Count).Select(ExtractParamName));
    }

    /// <summary>
    /// Explicitly grouped kernels without an index parameter: parameter 0 is a user
    /// parameter that must survive in the signature, and no extents are passed.
    /// </summary>
    [Theory]
    [MemberData(nameof(Backends))]
    public void GroupedLaunch_KeepsUserParameterZero(BackendType backend)
    {
        var signature = GenerateAndAlign(
            backend,
            GetKernel(
                typeof(GroupedLaunchKernels),
                nameof(GroupedLaunchKernels.GroupedStoreKernel)),
            EntryPointLaunch.Grouped(hasKernelIndex: false));

        // On CUDA and ROCm the view keeps its name; on Metal and OpenCL it is
        // flattened into fields named "p0_f{n}" after original parameter 0. View
        // lowering may reorder parameters, so only membership is asserted.
        Assert.Contains(
            signature,
            p => ExtractParamName(p).EndsWith("_data", StringComparison.Ordinal)
                || p.Contains("_p0_f", StringComparison.Ordinal));
        Assert.Contains(
            signature,
            p => ExtractParamName(p).EndsWith("_value", StringComparison.Ordinal));
        AssertNoExtents(signature);
    }

    /// <summary>
    /// Explicitly grouped kernels taking a <c>KernelIndex</c>: the index is computed
    /// from thread built-ins and the remaining user parameters align with the
    /// launcher.
    /// </summary>
    [Theory]
    [MemberData(nameof(Backends))]
    public void GroupedKernelIndexLaunch_SignatureMatchesLauncher(BackendType backend)
    {
        var signature = GenerateAndAlign(
            backend,
            GetKernel(
                typeof(GroupedLaunchKernels),
                nameof(GroupedLaunchKernels.GroupedKernelIndexKernel)),
            EntryPointLaunch.Grouped(hasKernelIndex: true));

        AssertNoIndexParameter(signature);
        AssertNoExtents(signature);
    }

    #region Alignment

    /// <summary>
    /// Generates the kernel and its launcher, asserts that both agree on all
    /// argument slots and returns the parameter declarations of the signature that
    /// receive launch arguments.
    /// </summary>
    private List<string> GenerateAndAlign(
        BackendType backend,
        MethodInfo kernel,
        EntryPointLaunch launch)
    {
        using var helper = new CompilationHelper();
        var (compiled, wrapper) = helper.GenerateLauncher(kernel, backend, launch);
        var source = compiled.SourceCode;
        var entryPoint = compiled.EntryPointName;

        output.WriteLine($"// Entry point: {entryPoint}");
        output.WriteLine("// ----- Kernel source -----");
        output.WriteLine(source);
        output.WriteLine("// ----- Launcher wrapper -----");
        output.WriteLine(wrapper);

        var signature = backend switch
        {
            BackendType.Cuda or BackendType.ROCm => AssertFlatStructAlignment(
                ExtractSignature(source, "__global__", entryPoint),
                wrapper),
            BackendType.OpenCL => AssertPerArgumentAlignment(
                ExtractSignature(source, "__kernel", entryPoint),
                wrapper,
                @"CLAPI\.CurrentAPI\.SetKernelArgument\(" +
                @"\s*clKernel\.KernelPtr,\s*(\d+),"),
            BackendType.Metal => AssertPerArgumentAlignment(
                // Only [[buffer(n)]] parameters are launch arguments; the
                // thread-position built-ins follow them.
                [.. ExtractSignature(source, "kernel", entryPoint)
                    .Where(p => p.Contains("[[buffer(", StringComparison.Ordinal))],
                wrapper,
                @"MetalAPI\.Set(?:Bytes|Buffer)\([^;]*,\s*(\d+)\);"),
            _ => throw new ArgumentOutOfRangeException(nameof(backend)),
        };
        Assert.NotEmpty(signature);
        return signature;
    }

    /// <summary>
    /// CUDA and ROCm marshal all arguments into one flat <c>KernelArgs</c> struct
    /// that the driver reads positionally, so the signature must declare exactly its
    /// fields in the same order.
    /// </summary>
    private static List<string> AssertFlatStructAlignment(
        List<string> signature,
        string wrapper)
    {
        var fields = ExtractKernelArgsFields(wrapper);
        Assert.Equal(fields.Count, signature.Count);

        // Both sides derive user parameter names from the IR parameter name: the
        // wrapper as "param_{name}", the kernel as "param_{index}_{name}". Hidden
        // launch parameters have the same name on both sides.
        for (int i = 0; i < fields.Count; ++i)
        {
            var field = fields[i];
            var name = ExtractParamName(signature[i]);
            if (field.StartsWith("param_", StringComparison.Ordinal))
                Assert.EndsWith("_" + field["param_".Length..], name);
            else
                Assert.Equal(field, name);
        }
        return signature;
    }

    /// <summary>
    /// OpenCL and Metal set arguments one at a time with explicit slot indices,
    /// which must be exactly 0..N-1 for the N launch parameters of the signature.
    /// </summary>
    private static List<string> AssertPerArgumentAlignment(
        List<string> signature,
        string wrapper,
        string setArgumentPattern)
    {
        var slots = Regex.Matches(wrapper, setArgumentPattern)
            .Select(m => int.Parse(m.Groups[1].Value))
            .Order()
            .ToList();
        Assert.Equal(Enumerable.Range(0, signature.Count), slots);
        return signature;
    }

    /// <summary>
    /// The thread index is never a kernel parameter: the kernel body computes it
    /// from thread built-ins instead.
    /// </summary>
    private static void AssertNoIndexParameter(List<string> signature) =>
        Assert.DoesNotContain(
            signature,
            p => ExtractParamName(p).EndsWith("_index", StringComparison.Ordinal));

    /// <summary>
    /// Explicitly grouped launches pass no launch extents.
    /// </summary>
    private static void AssertNoExtents(List<string> signature) =>
        Assert.DoesNotContain(
            signature,
            p => p.Contains("_ilgpuc_extent", StringComparison.Ordinal));

    #endregion

    #region Source Parsing

    /// <summary>
    /// Returns the parameter declarations of the kernel entry point signature.
    /// </summary>
    private static List<string> ExtractSignature(
        string source,
        string kernelAttribute,
        string entryPoint)
    {
        var marker = $"{kernelAttribute} void {entryPoint}(";
        int start = source.IndexOf(marker, StringComparison.Ordinal);
        Assert.True(start >= 0, $"Kernel signature '{marker}' not found.");
        return ExtractParenthesizedList(source, start + marker.Length - 1);
    }

    /// <summary>
    /// Returns the field names of the generated <c>KernelArgs</c> struct in
    /// declaration order.
    /// </summary>
    private static List<string> ExtractKernelArgsFields(string wrapper)
    {
        int start = wrapper.IndexOf("struct KernelArgs", StringComparison.Ordinal);
        Assert.True(start >= 0, "KernelArgs struct not found in the launcher.");
        int open = wrapper.IndexOf('{', start);
        int close = wrapper.IndexOf('}', open);
        return
        [
            .. Regex.Matches(wrapper[(open + 1)..close], @"public\s+\S+\s+(\w+);")
                .Select(m => m.Groups[1].Value)
        ];
    }

    /// <summary>
    /// Returns the top-level comma-separated entries of the balanced parenthesized
    /// list that opens at the given index.
    /// </summary>
    private static List<string> ExtractParenthesizedList(string source, int openIndex)
    {
        Assert.Equal('(', source[openIndex]);
        var entries = new List<string>();
        int depth = 0;
        int entryStart = openIndex + 1;
        for (int i = openIndex; i < source.Length; ++i)
        {
            switch (source[i])
            {
                case '(':
                    ++depth;
                    break;
                case ')':
                    if (--depth == 0)
                    {
                        AddEntry(source[entryStart..i], entries);
                        return entries;
                    }
                    break;
                case ',' when depth == 1:
                    AddEntry(source[entryStart..i], entries);
                    entryStart = i + 1;
                    break;
            }
        }
        Assert.Fail("Unbalanced parentheses in generated source.");
        return entries;
    }

    /// <summary>
    /// Adds a trimmed, non-empty entry to the list.
    /// </summary>
    private static void AddEntry(string entry, List<string> entries)
    {
        entry = entry.Trim();
        if (entry.Length > 0)
            entries.Add(entry);
    }

    /// <summary>
    /// Returns the declared name of a C-style parameter declaration, ignoring
    /// trailing Metal attributes such as <c>[[buffer(0)]]</c>.
    /// </summary>
    private static string ExtractParamName(string declaration)
    {
        int attributeStart = declaration.IndexOf("[[", StringComparison.Ordinal);
        if (attributeStart >= 0)
            declaration = declaration[..attributeStart];
        var match = Regex.Match(declaration.TrimEnd(), @"(\w+)$");
        Assert.True(match.Success, $"No parameter name in '{declaration}'.");
        return match.Groups[1].Value;
    }

    #endregion

    /// <summary>
    /// Resolves a kernel method by name.
    /// </summary>
    private static MethodInfo GetKernel(Type kernelClass, string name) =>
        kernelClass.GetMethod(
            name,
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new ArgumentException(
            $"Method '{name}' not found on type '{kernelClass.Name}'");
}
