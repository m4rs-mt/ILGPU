// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: FrontendLocationTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Backends;
using ILGPUC.Frontend;
using ILGPUC.IR;
using ILGPUC.Tests.Framework;
using ILGPUC.Tests.Kernels;
using System;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Verifies that the IL frontend attaches sequence-point locations to disassembled
/// methods shared through <see cref="ILFrontendCache"/> completely, exactly once and
/// from the method that was actually disassembled.
/// </summary>
public sealed class FrontendLocationTests
{
    /// <summary>
    /// How long the concurrent frontend gets to (wrongly) finish while the first
    /// attachment of its kernel body is still in progress.
    /// </summary>
    private static readonly TimeSpan PendingAttachmentWindow =
        TimeSpan.FromMilliseconds(250);

    private static string GetAssemblyDir(MethodBase method) =>
        Path.GetDirectoryName(method.DeclaringType!.Assembly.Location)!;

    private static ILFrontend LoadMethods(MethodInfo kernel, ILFrontendCache? cache)
    {
        var frontend = new ILFrontend(BackendType.CPU, cache, GetAssemblyDir(kernel));
        frontend.LoadMethods([kernel]);
        return frontend;
    }

    private static string[] GetLocations(DisassembledMethod method) =>
        [.. method.Instructions.ToArray().Select(t => t.Location.ToString() ?? "")];

    /// <summary>
    /// A frontend loading a cached body while another frontend is still attaching
    /// its locations must wait for that attachment instead of continuing with a
    /// body whose locations are missing.
    /// </summary>
    [Fact]
    public async Task SharedBody_ConcurrentLoad_WaitsForLocationAttachment()
    {
        using var helper = new CompilationHelper();
        var kernel = typeof(PerfRegressionKernels)
            .GetMethod(nameof(PerfRegressionKernels.VectorMul))!;

        // Uncached frontend providing the reference locations
        var reference = LoadMethods(kernel, cache: null);
        var expected = GetLocations(reference.GetDisassembledMethod(kernel)!);
        Assert.All(expected, location => Assert.NotEqual(
            Location.Unknown.ToString(),
            location));

        // Publish a fresh body and start attaching its locations on a separate
        // thread, holding the attachment open until released
        var cache = new ILFrontendCache();
        var body = Disassembler.TryDisassemble(kernel)!;
        cache.Disassembly[kernel] = new(body, DisassembleStatus.Disassembled);

        using var attaching = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        var firstAttachment = Task.Run(() =>
            body.EnsureLocationsAttached(method =>
            {
                attaching.Set();
                release.Wait();
                return reference.LoadSequencePoints(method);
            }));
        attaching.Wait();

        // A second frontend sharing the cache must block on the pending attachment
        var concurrentLoad = Task.Run(() =>
            GetLocations(LoadMethods(kernel, cache).GetDisassembledMethod(kernel)!));
        var winner = await Task.WhenAny(
            concurrentLoad,
            Task.Delay(PendingAttachmentWindow));
        Assert.NotSame(concurrentLoad, winner);

        release.Set();
        await firstAttachment;
        Assert.Equal(expected, await concurrentLoad);
    }

    /// <summary>
    /// Intrinsic remappings register the implementation body under the remapped
    /// method as well; the body must receive the locations of the implementation
    /// regardless of which registration is processed first.
    /// </summary>
    [Fact]
    public void RemappedIntrinsic_KeepsImplementationLocations()
    {
        using var helper = new CompilationHelper();
        var kernel = typeof(Matrix4x4Kernels)
            .GetMethod(nameof(Matrix4x4Kernels.Matrix4x4CreateRotationXKernel))!;
        var remapped = typeof(Matrix4x4)
            .GetMethod(nameof(Matrix4x4.CreateRotationX), [typeof(float)])!;

        var frontend = LoadMethods(kernel, new ILFrontendCache());
        var body = frontend.GetDisassembledMethod(remapped);

        Assert.NotNull(body);
        Assert.NotEqual(remapped, body.Method);
        var location = Assert.IsAssignableFrom<FileLocation>(body.FirstLocation);
        Assert.EndsWith("XMatrix4x4.cs", location.FileName, StringComparison.Ordinal);
    }

    /// <summary>
    /// Bodies disassembled on the fly during code generation (here: helpers in an
    /// assembly the eager walk skips) must receive their locations before code is
    /// generated from them.
    /// </summary>
    [Fact]
    public void OnTheFlyDisassembledBody_ReceivesLocations()
    {
        using var helper = new CompilationHelper();
        var kernel = typeof(DeepCallStackKernels)
            .GetMethod(nameof(DeepCallStackKernels.DeepChainAbsKernel))!;
        var layer = typeof(DeepCallStackKernels).GetMethod(
            "Layer1",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var cache = new ILFrontendCache();
        cache.ForcedNonWalkableAssemblyNames.Add(layer.Module.Assembly.GetName().Name!);
        _ = helper.CompileToModule(kernel, frontendCache: cache);

        Assert.True(cache.Disassembly.TryGetValue(layer, out var entry));
        Assert.NotNull(entry.Body);
        var location = Assert.IsAssignableFrom<FileLocation>(entry.Body.FirstLocation);
        Assert.EndsWith(
            "DeepCallStackKernels.cs",
            location.FileName,
            StringComparison.Ordinal);
    }
}
