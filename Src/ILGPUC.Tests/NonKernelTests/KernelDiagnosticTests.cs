// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: KernelDiagnosticTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU;
using ILGPU.Runtime;
using ILGPUC.Backends;
using ILGPUC.Frontend;
using ILGPUC.IR;
using ILGPUC.Roslyn.Generation;
using ILGPUC.Tests.Framework;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;
using MetadataReference = Microsoft.CodeAnalysis.MetadataReference;
using RoslynLocation = Microsoft.CodeAnalysis.Location;
using RoslynOptimizationLevel = Microsoft.CodeAnalysis.OptimizationLevel;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that errors in kernel code are reported as diagnostics (ILGPU12xx) with
/// the identifier, message and source location of the offending code.
/// </summary>
public sealed class KernelDiagnosticTests
{
    #region Kernels

    // Kernels defined here (not in Kernels/) to avoid auto-discovery by
    // KernelRegistry. Every rejected line is marked with a comment naming the
    // test, which the test looks up to check the reported location.
    private static class Kernels
    {
        private static int Apply(Func<int, int> f, int x) => f(x);

        private static Func<int, int> Make() => x => x * 2;

        // r1_returned: the delegate returned from Make was bound to the only
        // other lambda of the method, computing 303 304 305 306
        public static void ReturnedDelegateWithOtherLambdaKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            int a = Apply(x => x + 3, 0);
            var f = Make();
            data[index] = f(data[index]) + 100 * a; // ReturnedDelegateWithOtherLambda
        }

        private static int ApplyIn(in Func<int, int> f, int x) => f(x);

        public static void DelegatePassedByReferenceKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            Func<int, int> g = x => x + 1;
            data[index] = ApplyIn(in g, data[index]); // DelegatePassedByReference
        }

        private static void Touch(ref Func<int, int> f) { }

        // r4_addrtaken: computed 0 10 20 30 instead of 1 2 20 30 on CPU and
        // Metal, as the variable was bound to the last store in code order
        public static void AddressTakenDelegateLocalKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            Func<int, int> f = x => x + 1;
            if (data[index] > 1)
                f = x => x * 10;
            Touch(ref f); // AddressTakenDelegateLocal
            data[index] = f(data[index]);
        }

        private static int ApplyReassigned(Func<int, int> f, int x)
        {
            if (x > 1)
                f = y => y * 10;
            Touch(ref f); // AddressTakenDelegateParameter
            return f(x);
        }

        public static void AddressTakenDelegateParameterKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = ApplyReassigned(x => x + 1, data[index]);
        }

        private static void Pick(int c, out Func<int, int> f) => f = x => x * c;

        public static void OutDelegateKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            Pick(data[index], out var f); // OutDelegate
            data[index] = f(data[index]);
        }

        public static void NullConditionalInvokeKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            Func<int, int> f = x => x + 1;
            data[index] = f?.Invoke(data[index]) ?? -1; // NullConditionalInvoke
        }

        private static int ApplyChecked(Func<int, int>? f, int x) =>
            f != null ? f(x) : -1; // NullCheckInHelper

        public static void NullCheckInHelperKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = ApplyChecked(x => x + 1, data[index]);
        }

        public static void DelegateEqualityKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            Func<int, int> f = x => x + 1;
            Func<int, int> g = x => x * 2;
            data[index] = f == g ? 100 : 7; // DelegateEquality
        }

        public static void DelegateCombineKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            Func<int, int> f = x => x + 1;
            f += x => x * 2; // DelegateCombine
            data[index] = f(data[index]);
        }

        public static void DynamicInvokeKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            Func<int, int> f = x => x + 1;
            data[index] = (int)f.DynamicInvoke(data[index])!; // DynamicInvoke
        }

        public static void CastToDelegateKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            Delegate d = (Func<int, int>)(x => x + 1);
            var f = (Func<int, int>)d; // CastToDelegate
            data[index] = f(data[index]);
        }

        // k26_capshared: crashed with an access violation on CPU and failed to
        // load the kernel on Metal
        public static void ClosureCapturesSharedMemoryKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            var shared = Group.GetSharedMemory<int>(4); // ClosureCapturesSharedMemory
            shared[Group.Index] = Group.Index * 3;
            Group.Barrier();
            Func<int, int> read = j => shared[j];
            data[index] = read(3 - Group.Index);
        }

        // Captured parameters are copied into the closure on entry
        private static int ReadThroughClosure(ArrayView<int> view, int j)
        { // ClosureCapturesSharedMemoryParameter
            Func<int, int> read = k => view[k];
            return read(j);
        }

        public static void ClosureCapturesSharedMemoryParameterKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            var shared = Group.GetSharedMemory<int>(4);
            shared[Group.Index] = Group.Index * 3;
            Group.Barrier();
            data[index] = ReadThroughClosure(shared, 3 - Group.Index);
        }

        public static void LocalFunctionCapturesLocalMemoryKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            var local = Group.GetLocalMemoryPerThread<int>(4); // LocalMemoryCapture
            for (int k = 0; k < 4; ++k)
                local[k] = k * 3;
            int Read(int j) => local[j];
            data[index] = Read(index.X);
        }

        // r3_capdelegate: a closure capturing a delegate
        // Closures are allocated at the start of the scope of their variables
        public static void ClosureCapturesDelegateKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        { // ClosureCapturesDelegate
            Func<int, int> f = x => x + 1;
            Func<int, int> g = y => f(y) * 2;
            data[index] = g(data[index]);
        }

        private struct Holder
        {
            public Func<int, int> F;
            public int B;
        }

        // r5_structret: a structure with a delegate field
        public static void StructWithDelegateFieldKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            var holder = new Holder { F = x => x * 2, B = 1 }; // StructWithDelegateField
            data[index] = holder.F(data[index]) + holder.B;
        }

        // k24_tuple: a tuple with a delegate element
        public static void TupleWithDelegateKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            (Func<int, int> F, int B) t = (x => x + 1, 3); // TupleWithDelegate
            data[index] = t.F(data[index]) + t.B;
        }

        // k23_array: an array of delegates
        public static void DelegateArrayKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            var array = new Func<int, int>[] { x => x + 1 }; // DelegateArray
            data[index] = array[0](data[index]);
        }

        // k36_nestedlambda: a capturing lambda creating another lambda
        public static void NestedLambdaKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        { // NestedLambda
            int k = 3;
            Func<int, int> outer = x =>
            {
                Func<int, int> inner = y => y * k;
                return inner(x) + 1;
            };
            data[index] = outer(data[index]);
        }

        private static readonly Func<int, int> StaticOperation = x => x * 3;

        // p06_staticfield: a static readonly delegate field
        public static void DelegateStaticFieldKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = StaticOperation(data[index]); // DelegateStaticField
        }

        private static readonly ArrayView<int> StaticView = default;

        // A static readonly view compiled to a load from a null pointer
        public static void StaticViewFieldKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = StaticView[index]; // StaticViewField
        }

        private struct Adder
        {
            public int K;
            public readonly int Add(int x) => x + K;
        }

        // k19_structmethodgroup: a delegate created from a struct method
        public static void StructMethodGroupKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            var adder = new Adder { K = 5 };
            Func<int, int> f = adder.Add; // StructMethodGroup
            data[index] = f(data[index]);
        }

        // p15_loopcapture: a closure allocated in a loop
        public static void ClosureInLoopKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            int sum = 0;
            for (int i = 0; i < 4; i++) // ClosureInLoop
            {
                int captured = i;
                Func<int> f = () => captured;
                sum += f();
            }
            data[index] = sum;
        }

        private static int SumOver(Func<int, int> f, int n) =>
            n <= 0 ? 0 : f(n) + SumOver(f, n - 1); // DelegateRecursion

        public static void DelegateRecursionKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = SumOver(x => x * 2, data[index]);
        }

        // k27_expression: an expression tree
        public static void ExpressionKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            System.Linq.Expressions.Expression<Func<int, int>> e = x => x + 1;
            data[index] = e.Parameters.Count;
        }

        // The address of a local variable converted to an integer and back
        public static unsafe void LocalAddressRoundTripKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            int value = 0;
            long address = (long)&value;
            *(int*)address = data[index]; // LocalAddressRoundTrip
            data[index] = value;
        }

        // An integer holding a local address moved in a loop
        public static unsafe void LocalAddressLoopKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            int value = 0;
            long address = (long)&value;
            for (int i = 0; i < data[index]; ++i)
                address += 4;
            *(int*)address = data[index]; // LocalAddressLoop
            data[index] = value;
        }

        // One of two integers holding local addresses
        public static unsafe void LocalAddressSelectKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            int first = 0;
            int second = 0;
            long address = data[index] > 0 ? (long)&first : (long)&second;
            *(int*)address = data[index]; // LocalAddressSelect
            data[index] = first + second;
        }

        // One of two integers holding shared addresses
        public static unsafe void SharedAddressSelectKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            var shared = Group.GetSharedMemory<int>(2);
            long address = data[index] > 0
                ? (long)Unsafe.AsPointer(ref shared[0])
                : (long)Unsafe.AsPointer(ref shared[1]);
            *(int*)address = data[index]; // SharedAddressSelect
            Group.Barrier();
            data[index] = shared[0] + shared[1];
        }

        // The distance of two local addresses, which is never converted back to
        // an address, moved after a conditional
        public static unsafe void LocalOffsetSelectKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            int first = 0;
            int second = 0;
            nint offset = (nint)(&second) - (nint)(&first);
            if (data[index] > 0)
                offset += 1;
            data[index] = (int)offset + first + second;
        }

        // A mask computed from the distance of two local addresses in a loop
        public static unsafe void LocalOffsetLoopKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            int first = 0;
            int second = 0;
            int count = 0;
            nint mask = (nint)(&second) - (nint)(&first);
            for (int i = 0; i < data[index]; ++i)
            {
                mask += 1;
                if (mask != 0)
                    ++count;
                mask &= 7;
            }
            data[index] = (int)mask + count + first + second;
        }

        // Shared memory indexed by one of two distances of shared addresses
        public static unsafe void SharedOffsetIndexKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            var shared = Group.GetSharedMemory<int>(4);
            nint start = (nint)Unsafe.AsPointer(ref shared[0]);
            nint distance = data[index] > 0
                ? (nint)Unsafe.AsPointer(ref shared[2]) - start
                : (nint)Unsafe.AsPointer(ref shared[1]) - start;
            shared[(int)(distance / 4)] = data[index];
            Group.Barrier();
            data[index] = shared[1] + shared[2];
        }

        // One of two native integers holding local addresses, aligned before it
        // is converted to a pointer
        public static unsafe void LocalAddressAlignedSelectKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            int first = 0;
            int second = 0;
            nint address = data[index] > 0 ? (nint)(long)&first : (nint)(long)&second;
            *(int*)(address & ~(nint)3) = data[index]; // LocalAddressAlignedSelect
            data[index] = first + second;
        }

        public struct IntPair
        {
            public int A;
            public int B;
        }

        // A local address moved by a native integer offset computed in a loop
        public static unsafe void LocalAddressOffsetKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            IntPair pair = default;
            nint offset = 0;
            for (int i = 0; i < (data[index] & 1); ++i)
                offset += 4;
            *(int*)((nint)(long)&pair.A + offset) = data[index]; // LocalAddressOffset
            data[index] = pair.A + pair.B;
        }

        // The address of a field of a view element computed from the offset of
        // the field in a local structure
        public static unsafe void GlobalFieldAddressKernel(
            Index1D index,
            ArrayView1D<IntPair, Stride1D.Dense> data)
        {
            IntPair pair = default;
            long offset = (byte*)&pair.B - (byte*)&pair;
            long address = (long)((byte*)Unsafe.AsPointer(ref data[index]) + offset);
            *(int*)address = pair.A + 1;
        }

        public static void DelegateMemberKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            Func<int, int> f = x => x + 1;
            data[index] = f.Equals(null) ? 1 : 0; // DelegateMember
        }

        private struct Pair
        {
            public int First;
            public int Second;
        }

        private static unsafe int* AddressOfLocal(int value)
        {
            int local = value;
            return &local; // ReturnedLocalAddress
        }

        private static unsafe int* AddressOfField(int value)
        {
            var pair = new Pair { First = value, Second = value + 1 };
            return &pair.Second; // ReturnedFieldAddress
        }

        private static unsafe byte* CastAddressOfLocal(int value)
        {
            int local = value;
            return (byte*)&local; // ReturnedCastAddress
        }

        private static unsafe ref int ReferenceToLocal(int value)
        {
            int local = value;
            return ref *&local; // ReturnedReferenceToLocal
        }

        private static unsafe int* AddressOfParameter(int value)
        {
            return &value; // ReturnedParameterAddress
        }

        public static unsafe void ReturnedLocalAddressKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = *AddressOfLocal(data[index]);
        }

        public static unsafe void ReturnedFieldAddressKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = *AddressOfField(data[index]);
        }

        public static unsafe void ReturnedCastAddressKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = *CastAddressOfLocal(data[index]);
        }

        public static void ReturnedReferenceToLocalKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = ReferenceToLocal(data[index]);
        }

        public static unsafe void ReturnedParameterAddressKernel(
            Index1D index,
            ArrayView1D<int, Stride1D.Dense> data)
        {
            data[index] = *AddressOfParameter(data[index]);
        }
    }

    /// <summary>
    /// Compiles the given kernel, which must be rejected with a diagnostic.
    /// </summary>
    private static KernelCompilationException Compile(string kernelName) =>
        Compile(kernelName, BackendType.CPU, new CompilationProperties());

    /// <summary>
    /// Compiles the given kernel for the given backend and optimization level, which
    /// must be rejected with a diagnostic.
    /// </summary>
    private static KernelCompilationException Compile(
        string kernelName,
        BackendType backend,
        CompilationProperties properties)
    {
        using var helper = new CompilationHelper(backend);
        var kernel = typeof(Kernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"Kernel method '{kernelName}' not found.");
        return Assert.Throws<KernelCompilationException>(() =>
            helper.GetNormalizedIR(
                kernel,
                IRDumpPoint.AfterBackendTransforms,
                properties,
                backend));
    }

    /// <summary>
    /// Generates code for the given kernel on the given backend, which must
    /// reject it with a diagnostic.
    /// </summary>
    private static KernelCompilationException Generate(
        string kernelName,
        BackendType backend,
        OptimizationLevel level = OptimizationLevel.O1)
    {
        using var helper = new CompilationHelper(backend);
        return Assert.Throws<KernelCompilationException>(() =>
            helper.GenerateBackendCode(
                GetKernel(kernelName),
                backend,
                new CompilationProperties(OptimizationLevel: level)));
    }

    /// <summary>
    /// Returns the given kernel of <see cref="Kernels"/>.
    /// </summary>
    private static MethodInfo GetKernel(string kernelName) =>
        typeof(Kernels).GetMethod(
            kernelName,
            BindingFlags.Public | BindingFlags.Static)
        ?? throw new InvalidOperationException(
            $"Kernel method '{kernelName}' not found.");

    /// <summary>
    /// Asserts that the given diagnostic has the given identifier and message
    /// and points at the line of this file marked with the given comment.
    /// </summary>
    private static void AssertDiagnostic(
        KernelCompilationException exception,
        string id,
        string message,
        string marker,
        [CallerFilePath] string sourcePath = "")
    {
        Assert.Equal(id, exception.Id);
        Assert.Contains(message, exception.Message, StringComparison.Ordinal);

        var location = Assert.IsAssignableFrom<FileLocation>(exception.Location);
        Assert.Equal(
            Path.GetFileName(sourcePath),
            Path.GetFileName(location.FileName));
        var lines = File.ReadAllLines(sourcePath);
        int line = Array.FindIndex(
            lines,
            l => l.EndsWith("// " + marker, StringComparison.Ordinal)) + 1;
        Assert.True(line > 0, $"Marker '{marker}' not found");
        Assert.Equal(line, location.StartLine);
    }

    /// <summary>
    /// Builds the given kernel class <c>K</c>, whose method <c>Kern</c> is
    /// launched on four elements, through the analysis and kernel compilation
    /// of ilgpuc builds, and asserts that the build fails with the given error
    /// at the line marked with <c>// error</c>.
    /// </summary>
    /// <returns>The diagnostic of the build.</returns>
    private static async Task<string> AssertBuildErrorAsync(
        string kernelClass,
        string id,
        string message,
        RoslynOptimizationLevel optimizationLevel = RoslynOptimizationLevel.Release,
        MetadataReference[]? additionalReferences = null,
        BackendType backend = BackendType.CPU)
    {
        var (source, compilation, analysis) = AnalyzeBuild(
            kernelClass,
            optimizationLevel,
            additionalReferences);
        var exception = await Assert.ThrowsAsync<KernelDiagnosticException>(() =>
            MainKernelProvider.CreateAsync(
                compilation,
                analysis,
                [CompilationHelper.CreateBackend(backend)],
                new KernelCompiler()));
        int line = Array.FindIndex(
            source.Split('\n'),
            l => l.TrimEnd().EndsWith("// error", StringComparison.Ordinal)) + 1;
        Assert.True(line > 0, "Marker '// error' not found");
        var diagnostic = exception.Diagnostic.ToString();
        Assert.StartsWith($"source0.cs({line},", diagnostic, StringComparison.Ordinal);
        Assert.Contains($"): error {id}: ", diagnostic, StringComparison.Ordinal);
        Assert.Contains(message, diagnostic, StringComparison.Ordinal);
        return diagnostic;
    }

    /// <summary>
    /// Builds the given kernel class <c>K</c> like
    /// <see cref="AssertBuildErrorAsync"/> and asserts that the build succeeds.
    /// </summary>
    private static async Task AssertBuildSucceedsAsync(
        string kernelClass,
        RoslynOptimizationLevel optimizationLevel)
    {
        var (_, compilation, analysis) = AnalyzeBuild(kernelClass, optimizationLevel);
        await MainKernelProvider.CreateAsync(
            compilation,
            analysis,
            [CompilationHelper.CreateBackend(BackendType.CPU)],
            new KernelCompiler());
    }

    /// <summary>
    /// Creates and analyzes the program launching <c>K.Kern</c> on four
    /// elements.
    /// </summary>
    private static (
        string Source,
        CSharpCompilation Compilation,
        AnalysisResult Analysis) AnalyzeBuild(
        string kernelClass,
        RoslynOptimizationLevel optimizationLevel,
        MetadataReference[]? additionalReferences = null)
    {
        var source = $$"""
            using System;
            using ILGPU;
            using ILGPU.Runtime;

            {{kernelClass}}

            static class Program
            {
                static void Main()
                {
                    using var context = Context.Create(b => b.Default());
                    using var accelerator = context.GetPreferredDevice(preferCPU: true)
                        .CreateAccelerator(context);
                    var stream = accelerator.DefaultStream;
                    using var buffer = stream.Allocate1D<int>(4);
                    var view = buffer.View;
                    stream.Launch((Index1D)4, index => K.Kern(index, view));
                    stream.Synchronize();
                }
            }
            """;
        var compilation = RoslynCompiler.CreateCompilation(
            [source],
            "KernelDiagnosticTest",
            additionalReferences: additionalReferences,
            optimizationLevel: optimizationLevel);
        var analysis = CompilationRewriter.Analyze(compilation);
        Assert.Empty(analysis.Diagnostics);
        return (source, compilation, analysis);
    }

    /// <summary>
    /// Kernels bound to the delegate targets of a launching method are not
    /// instantiations found through generic code: a target that throws fails
    /// the build instead of skipping its kernel, which the dispatch stub of the
    /// launch would still refer to.
    /// </summary>
    [Fact]
    public async Task ThrowingDelegateTarget_FailsBuild()
    {
        var compilation = RoslynCompiler.CreateCompilation(
            ["""
            using System;
            using ILGPU;
            using ILGPU.Runtime;

            static class Host
            {
                internal static void UnaryOp(
                    AcceleratorStream stream, ArrayView<int> view, Func<int, int> op)
                {
                    stream.Launch(view.IntExtent, index => view[index] = op(view[index]));
                }
            }

            static class Program
            {
                static void Main()
                {
                    using var context = Context.Create(b => b.Default());
                    using var accelerator = context.GetPreferredDevice(preferCPU: true)
                        .CreateAccelerator(context);
                    var stream = accelerator.DefaultStream;
                    using var buffer = stream.Allocate1D<int>(4);
                    var view = buffer.View;
                    Host.UnaryOp(stream, view, v => -v);
                    Host.UnaryOp(stream, view, v => throw new ArgumentException());
                }
            }
            """],
            "KernelDiagnosticTest");
        var analysis = CompilationRewriter.Analyze(compilation);
        Assert.Empty(analysis.Diagnostics);
        Assert.Equal(2, Assert.Single(analysis.DelegateDispatchStubs).Bindings.Length);

        var exception = await Assert.ThrowsAsync<KernelDiagnosticException>(() =>
            MainKernelProvider.CreateAsync(
                compilation,
                analysis,
                [CompilationHelper.CreateBackend(BackendType.CPU)],
                new KernelCompiler()));
        Assert.True(ILGPUC.Frontend.ReachableThrowException.IsCausedBy(exception));
        Assert.Contains(
            "ArgumentException",
            exception.Diagnostic.Message,
            StringComparison.Ordinal);
    }

    #endregion

    #region Unresolved Delegates (ILGPU1202)

    // r1_returned: computed 303 304 305 306 instead of 300 302 304 306 on CPU
    // and Metal, as the returned delegate was bound to the other lambda
    [Fact]
    public Task ReturnedDelegateWithOtherLambda_FailsBuild() =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                static int Apply(Func<int, int> g, int x) => g(x);
                static Func<int, int> Make() => x => x * 2;
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    int a = Apply(x => x + 3, 0);
                    var f = Make();
                    data[i] = f(data[i]) + 100 * a; // error
                }
            }
            """,
            "ILGPU1202",
            "Cannot statically resolve the delegate invoked in method 'Kern': " +
            "it is returned from method 'Make'.");

    [Fact]
    public void ReturnedDelegateWithOtherLambda_ReportsUnresolved() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.ReturnedDelegateWithOtherLambdaKernel)),
            "ILGPU1202",
            "Cannot statically resolve the delegate invoked in method " +
            "'ReturnedDelegateWithOtherLambdaKernel': it is returned from " +
            "method 'Make'.",
            "ReturnedDelegateWithOtherLambda");

    #endregion

    #region Address-Taken Delegates (ILGPU1203)

    [Fact]
    public void DelegatePassedByReference_Reports1203() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.DelegatePassedByReferenceKernel)),
            "ILGPU1203",
            "The delegate variable 'g' in method 'DelegatePassedByReferenceKernel' " +
            "is passed by reference or used through a reference.",
            "DelegatePassedByReference");

    [Fact]
    public void AddressTakenDelegateLocal_Reports1203() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.AddressTakenDelegateLocalKernel)),
            "ILGPU1203",
            "The delegate variable 'f' in method 'AddressTakenDelegateLocalKernel'",
            "AddressTakenDelegateLocal");

    [Fact]
    public void AddressTakenDelegateParameter_Reports1203() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.AddressTakenDelegateParameterKernel)),
            "ILGPU1203",
            "The delegate variable 'f' in method 'ApplyReassigned'",
            "AddressTakenDelegateParameter");

    [Fact]
    public void OutDelegate_Reports1203() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.OutDelegateKernel)),
            "ILGPU1203",
            "The delegate variable 'f' in method 'OutDelegateKernel'",
            "OutDelegate");

    // r4_addrtaken: computed 0 10 20 30 instead of 1 2 20 30 on CPU and Metal
    [Fact]
    public Task AddressTakenDelegateLocal_FailsBuild() =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                static void Touch(ref Func<int, int> f) { }
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    Func<int, int> f = x => x + 1;
                    if (data[i] > 1) f = x => x * 10;
                    Touch(ref f); // error
                    data[i] = f(data[i]);
                }
            }
            """,
            "ILGPU1203",
            "The delegate variable 'f' in method 'Kern' is passed by reference " +
            "or used through a reference.");

    // f8_paramreassign: computed 0 10 20 30 instead of 1 2 20 30 on CPU and Metal
    [Fact]
    public Task AddressTakenDelegateParameter_FailsBuild() =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                static void Touch(ref Func<int, int> f) { }
                static int Apply(Func<int, int> f, int x)
                {
                    if (x > 1) f = y => y * 10;
                    Touch(ref f); // error
                    return f(x);
                }
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
                    d[i] = Apply(x => x + 1, d[i]);
            }
            """,
            "ILGPU1203",
            "The delegate variable 'f' in method 'Apply'");

    #endregion

    #region Delegate Identity (ILGPU1201)

    [Fact]
    public void NullConditionalInvoke_Reports1201() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.NullConditionalInvokeKernel)),
            "ILGPU1201",
            "Kernel code cannot compare delegates or test them for null " +
            "('null check' in method 'NullConditionalInvokeKernel').",
            "NullConditionalInvoke");

    [Fact]
    public void NullCheckInHelper_Reports1201() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.NullCheckInHelperKernel)),
            "ILGPU1201",
            "('null check' in method 'ApplyChecked')",
            "NullCheckInHelper");

    [Fact]
    public void DelegateEquality_Reports1201() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.DelegateEqualityKernel)),
            "ILGPU1201",
            "('==' in method 'DelegateEqualityKernel')",
            "DelegateEquality");

    [Fact]
    public void DelegateCombine_Reports1201() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.DelegateCombineKernel)),
            "ILGPU1201",
            "Kernel code cannot use the delegate operation 'Combine' in method " +
            "'DelegateCombineKernel'.",
            "DelegateCombine");

    // The object array of the arguments is rejected before the call
    [Fact]
    public void DynamicInvoke_ReportsUnsupportedConstruct()
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = typeof(Kernels).GetMethod(
            nameof(Kernels.DynamicInvokeKernel),
            BindingFlags.Public | BindingFlags.Static)!;
        var exception = Assert.Throws<InternalCompilerException>(() =>
            helper.GetNormalizedIR(kernel, IRDumpPoint.AfterBackendTransforms));
        Assert.True(KernelCompilationErrors.TryCreateDiagnostic(
            exception,
            LaunchSite,
            out var diagnostic));
        Assert.Equal("ILGPU1200", diagnostic!.Id);
        Assert.Contains("'System.Object'", diagnostic.Message);
    }

    [Fact]
    public void CastToDelegate_Reports1201() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.CastToDelegateKernel)),
            "ILGPU1201",
            "'cast to Func<Int32, Int32>' in method 'CastToDelegateKernel'",
            "CastToDelegate");

    [Fact]
    public void DelegateMember_Reports1201() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.DelegateMemberKernel)),
            "ILGPU1201",
            "('Equals' in method 'DelegateMemberKernel')",
            "DelegateMember");

    // p12_nullcond: computed -1 -1 -1 -1 instead of 1 2 3 4 on CPU and Metal
    [Fact]
    public Task NullConditionalInvoke_FailsBuild() =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> d)
                {
                    Func<int, int> f = x => x + 1;
                    d[i] = f?.Invoke(d[i]) ?? -1; // error
                }
            }
            """,
            "ILGPU1201",
            "Kernel code cannot compare delegates or test them for null " +
            "('null check' in method 'Kern').");

    // p12b_nullcheck: computed -1 -1 -1 -1 instead of 1 2 3 4 on Metal and
    // failed to compile the generated C# code on CPU
    [Fact]
    public Task NullCheckInHelper_FailsBuild() =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                static int Apply(Func<int, int>? f, int x) =>
                    f != null ? f(x) : -1; // error
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> d)
                {
                    d[i] = Apply(x => x + 1, d[i]);
                }
            }
            """,
            "ILGPU1201",
            "Kernel code cannot compare delegates or test them for null " +
            "('null check' in method 'Apply').");

    // p12c_nullcap: failed to load the kernel at runtime on Metal
    [Fact]
    public Task NullCheckOfCapturingLambda_FailsBuild() =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                static void Inner(Index1D i, ArrayView1D<int, Stride1D.Dense> d, int k)
                {
                    Func<int, int> f = x => x + k;
                    d[i] = f == null ? -1 : f(d[i]); // error
                }
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
                    Inner(i, d, 10);
            }
            """,
            "ILGPU1201",
            "('null check' in method 'Inner')");

    // p13_equality: failed to load the kernel at runtime on Metal
    [Fact]
    public Task DelegateEquality_FailsBuild() =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> d)
                {
                    Func<int, int> f = x => x + 1;
                    Func<int, int> g = x => x * 2;
                    d[i] = f == g ? 100 : 7; // error
                }
            }
            """,
            "ILGPU1201",
            "('==' in method 'Kern')");

    #endregion

    #region Group and Local Memory Captures (ILGPU1204)

    [Fact]
    public void ClosureCapturesSharedMemory_Reports1204() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.ClosureCapturesSharedMemoryKernel)),
            "ILGPU1204",
            "A lambda or local function in method " +
            "'ClosureCapturesSharedMemoryKernel' captures 'shared', which points " +
            "to shared or local memory.",
            "ClosureCapturesSharedMemory");

    [Fact]
    public void ClosureCapturesSharedMemoryParameter_Reports1204() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.ClosureCapturesSharedMemoryParameterKernel)),
            "ILGPU1204",
            "in method 'ReadThroughClosure' captures 'view'",
            "ClosureCapturesSharedMemoryParameter");

    [Fact]
    public void LocalFunctionCapturesLocalMemory_Reports1204() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.LocalFunctionCapturesLocalMemoryKernel)),
            "ILGPU1204",
            "in method 'LocalFunctionCapturesLocalMemoryKernel' captures 'local'",
            "LocalMemoryCapture");

    // k26_capshared: crashed with an access violation on CPU and failed to load
    // the kernel on Metal
    [Fact]
    public Task ClosureCapturesSharedMemory_FailsBuild() =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> d)
                {
                    var sh = Group.GetSharedMemory<int>(4); // error
                    sh[Group.Index] = Group.Index * 3;
                    Group.Barrier();
                    Func<int, int> rd = j => sh[j];
                    d[i] = rd(3 - Group.Index);
                }
            }
            """,
            "ILGPU1204",
            "A lambda or local function in method 'Kern' captures 'sh', which " +
            "points to shared or local memory.");

    #endregion

    #region Exception Handling (ILGPU1200)

    private const string ExceptionHandlingMessage =
        "Exception handling is not supported in kernels: 'K.Kern' contains a " +
        "try block, which C# also creates for using statements, foreach loops " +
        "over disposable enumerators and fixed statements left by a jump";

    private const string ThrowMessage =
        "Exceptions are not supported in kernels: 'K.Kern' throws " +
        "'InvalidOperationException'";

    /// <summary>
    /// A try block is reported at its first statement: C# has no sequence
    /// point for the try keyword.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task TryCatchFinally_FailsBuild(RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    try
                    {
                        data[i] = data[i] + 1; // error
                    }
                    catch (InvalidOperationException)
                    {
                        data[i] = -1;
                    }
                    finally
                    {
                        data[i] = data[i] * 2;
                    }
                }
            }
            """,
            "ILGPU1200",
            ExceptionHandlingMessage,
            optimizationLevel);

    /// <summary>
    /// The variable of a catch clause has an unsupported class type, which
    /// was reported instead of the exception handling.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task CatchExceptionVariable_FailsBuild(
        RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    try
                    {
                        data[i] = data[i] + 1; // error
                    }
                    catch (Exception e)
                    {
                        data[i] = e.HResult;
                    }
                }
            }
            """,
            "ILGPU1200",
            ExceptionHandlingMessage,
            optimizationLevel);

    /// <summary>
    /// The filter of a catch clause ends in an endfilter instruction, which
    /// the disassembler rejected: the kernel was left without a body and the
    /// build failed with an InvalidOperationException without a location.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task ExceptionFilter_FailsBuild(RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    try
                    {
                        data[i] = data[i] + 1; // error
                    }
                    catch (Exception) when (data[i] > 0)
                    {
                        data[i] = -1;
                    }
                }
            }
            """,
            "ILGPU1200",
            ExceptionHandlingMessage,
            optimizationLevel);

    /// <summary>
    /// The variable of a catch clause with a filter is first used in the
    /// filter.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task CatchExceptionVariableWithFilter_FailsBuild(
        RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    try
                    {
                        data[i] = data[i] + 1; // error
                    }
                    catch (Exception e) when (e.HResult > 0)
                    {
                        data[i] = -1;
                    }
                }
            }
            """,
            "ILGPU1200",
            ExceptionHandlingMessage,
            optimizationLevel);

    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task UsingDisposableStruct_FailsBuild(
        RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            struct Doubler : IDisposable
            {
                public ArrayView1D<int, Stride1D.Dense> Data;
                public int Index;
                public void Dispose() => Data[Index] = Data[Index] * 2;
            }

            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    using (new Doubler { Data = data, Index = i }) // error
                    {
                        data[i] = data[i] + 1;
                    }
                }
            }
            """,
            "ILGPU1200",
            ExceptionHandlingMessage,
            optimizationLevel);

    /// <summary>
    /// The try block of a using declaration spans the rest of the method: it
    /// was reported at the closing brace of the method.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task UsingDeclaration_FailsBuild(RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            struct Doubler : IDisposable
            {
                public ArrayView1D<int, Stride1D.Dense> Data;
                public int Index;
                public void Dispose() => Data[Index] = Data[Index] * 2;
            }

            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    using var doubler = new Doubler { Data = data, Index = i }; // error
                    data[i] = data[i] + 1;
                }
            }
            """,
            "ILGPU1200",
            ExceptionHandlingMessage,
            optimizationLevel);

    /// <summary>
    /// A try block that ends in a throw is directly followed by its handler,
    /// which the frontend entered as if the throw fell through to it: with the
    /// exception missing from the stack, which failed the "Invalid stack
    /// counter" assertion. The throw is the error.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task ThrowAtEndOfTry_FailsBuild(RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    try
                    {
                        throw new InvalidOperationException(); // error
                    }
                    catch (InvalidOperationException)
                    {
                        data[i] = 2;
                    }
                }
            }
            """,
            "ILGPU1200",
            ThrowMessage,
            optimizationLevel);

    /// <summary>
    /// A try block that ends in a throw is directly followed by the filter of
    /// its catch clause, which the frontend must not enter either.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task ThrowAtEndOfTryWithFilter_FailsBuild(
        RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    try
                    {
                        throw new InvalidOperationException(); // error
                    }
                    catch (InvalidOperationException) when (data[i] > 0)
                    {
                        data[i] = 2;
                    }
                }
            }
            """,
            "ILGPU1200",
            ThrowMessage,
            optimizationLevel);

    /// <summary>
    /// A try block that ends in a throw is directly followed by its finally
    /// handler, whose endfinally was reported instead of the throw.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task ThrowAtEndOfTryWithFinally_FailsBuild(
        RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    try
                    {
                        throw new InvalidOperationException(); // error
                    }
                    finally
                    {
                        data[i] = 2;
                    }
                }
            }
            """,
            "ILGPU1200",
            ThrowMessage,
            optimizationLevel);

    /// <summary>
    /// The innermost try block that is left is reported, not the enclosing one.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task NestedTry_FailsBuild(RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    try
                    {
                        data[i] = 3;
                        try
                        {
                            data[i] = data[i] + 1; // error
                        }
                        catch (InvalidOperationException)
                        {
                            data[i] = -1;
                        }
                    }
                    finally
                    {
                        data[i] = data[i] * 2;
                    }
                }
            }
            """,
            "ILGPU1200",
            ExceptionHandlingMessage,
            optimizationLevel);

    /// <summary>
    /// A foreach loop over an enumerator that implements IDisposable disposes
    /// it in a finally handler.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task ForeachDisposableEnumerator_FailsBuild(
        RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            struct Counter
            {
                public int Count;
                public Enumerator GetEnumerator() => new Enumerator { Count = Count };

                public struct Enumerator : IDisposable
                {
                    public int Count;
                    public int Current { get; private set; }
                    public bool MoveNext() => ++Current <= Count;
                    public void Dispose() { }
                }
            }

            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    int sum = 0;
                    foreach (int value in new Counter { Count = data[i] }) // error
                        sum += value;
                    data[i] = sum;
                }
            }
            """,
            "ILGPU1200",
            ExceptionHandlingMessage,
            optimizationLevel);

    /// <summary>
    /// A fixed statement left by a jump releases its pinned variable in a
    /// finally handler.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task FixedLeftByJump_FailsBuild(RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static unsafe class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    var values = new int[4];
                    for (int j = 0; j < 4; ++j)
                    {
                        fixed (int* p = &values[0]) // error
                        {
                            if (data[i] > j)
                                break;
                            p[j] = j;
                        }
                    }
                    data[i] = values[1];
                }
            }
            """,
            "ILGPU1200",
            ExceptionHandlingMessage,
            optimizationLevel);

    /// <summary>
    /// A while loop that starts a try block jumps to its condition without a
    /// sequence point: the jump was reported at the statement before the try
    /// block in release builds and at its opening brace in debug builds.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task TryStartingWithWhile_FailsBuild(
        RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    data[i] = 7;
                    try
                    {
                        while (data[i] > 0) // error
                            data[i] = data[i] - 1;
                    }
                    catch (InvalidOperationException)
                    {
                        data[i] = -1;
                    }
                }
            }
            """,
            "ILGPU1200",
            ExceptionHandlingMessage,
            optimizationLevel);

    /// <summary>
    /// A finally handler that calls Dispose itself does not belong to a using
    /// statement: it was reported at the statement before the try block.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task DisposeInFinally_FailsBuild(RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            struct Doubler : IDisposable
            {
                public ArrayView1D<int, Stride1D.Dense> Data;
                public int Index;
                public void Dispose() => Data[Index] = Data[Index] * 2;
            }

            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    var doubler = new Doubler { Data = data, Index = i };
                    data[i] = 5;
                    try
                    {
                        data[i] = data[i] + 1; // error
                    }
                    finally
                    {
                        doubler.Dispose();
                    }
                }
            }
            """,
            "ILGPU1200",
            ExceptionHandlingMessage,
            optimizationLevel);

    /// <summary>
    /// A using statement in a finally handler disposes its resource in the
    /// handler: the try block was reported at the statement before it.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task UsingInFinally_FailsBuild(RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            struct Doubler : IDisposable
            {
                public ArrayView1D<int, Stride1D.Dense> Data;
                public int Index;
                public void Dispose() => Data[Index] = Data[Index] * 2;
            }

            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    data[i] = 1;
                    try
                    {
                        data[i] = data[i] + 1; // error
                    }
                    finally
                    {
                        using (new Doubler { Data = data, Index = i })
                            data[i] = 3;
                    }
                }
            }
            """,
            "ILGPU1200",
            ExceptionHandlingMessage,
            optimizationLevel);

    /// <summary>
    /// An exception variable outside of catch clauses in a method with catch
    /// clauses is not the variable of a catch clause: its class type is
    /// reported, as in methods without catch clauses, at the opening brace of
    /// the method in debug builds. It was reported as exception handling.
    /// </summary>
    [Fact]
    public Task ExceptionVariableOutsideCatch_FailsBuild() =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                { // error
                    if (data[i] > 10)
                    {
                        var exception = new InvalidOperationException();
                        data[i] = exception.HResult;
                        throw exception;
                    }
                    try
                    {
                        data[i] = data[i] + 1;
                    }
                    catch (InvalidOperationException)
                    {
                        data[i] = -1;
                    }
                }
            }
            """,
            "ILGPU1200",
            "Class type 'System.InvalidOperationException' is not supported",
            RoslynOptimizationLevel.Debug);

    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task TryInHelper_FailsBuild(RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                static int Increment(int x)
                {
                    try
                    {
                        return x + 1;
                    }
                    catch
                    {
                        return 0;
                    }
                }

                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    data[i] = Increment(data[i]); // error
                }
            }
            """,
            "ILGPU1200",
            "Exception handling is not supported in kernels: 'K.Increment' " +
            "contains a try block",
            optimizationLevel);

    #endregion

    #region Delegate Storage and Other Frontend Diagnostics (ILGPU1205-1209, 1214, 1200)

    [Fact]
    public void ClosureCapturesDelegate_Reports1205() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.ClosureCapturesDelegateKernel)),
            "ILGPU1205",
            "Method 'ClosureCapturesDelegateKernel' uses a delegate stored in the " +
            "closure of a lambda or local function capturing 'f'.",
            "ClosureCapturesDelegate");

    [Fact]
    public void StructWithDelegateField_Reports1205() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.StructWithDelegateFieldKernel)),
            "ILGPU1205",
            "uses a delegate stored in the field 'F' of 'Holder'.",
            "StructWithDelegateField");

    [Fact]
    public void TupleWithDelegate_Reports1205() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.TupleWithDelegateKernel)),
            "ILGPU1205",
            "uses a delegate stored in the tuple " +
            "'ValueTuple<Func<Int32, Int32>, Int32>'.",
            "TupleWithDelegate");

    [Fact]
    public void DelegateArray_Reports1205() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.DelegateArrayKernel)),
            "ILGPU1205",
            "uses a delegate stored in an array of 'Func<Int32, Int32>'.",
            "DelegateArray");

    [Fact]
    public void NestedLambda_Reports1205() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.NestedLambdaKernel)),
            "ILGPU1205",
            "Method 'NestedLambdaKernel' creates a lambda inside a lambda or " +
            "local function that captures variables, which is not supported.",
            "NestedLambda");

    [Fact]
    public void DelegateStaticField_Reports1206() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.DelegateStaticFieldKernel)),
            "ILGPU1206",
            "Static field 'Kernels.StaticOperation' holds a delegate, which " +
            "kernels cannot load.",
            "DelegateStaticField");

    [Fact]
    public void StructMethodGroup_Reports1207() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.StructMethodGroupKernel)),
            "ILGPU1207",
            "Delegate created from the instance method 'Adder.Add' of a struct " +
            "value is not supported.",
            "StructMethodGroup");

    [Fact]
    public void ClosureInLoop_Reports1208() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.ClosureInLoopKernel)),
            "ILGPU1208",
            "A lambda or local function in method 'ClosureInLoopKernel' allocates " +
            "its closure inside a loop, which is not supported.",
            "ClosureInLoop");

    [Fact]
    public void DelegateRecursion_Reports1209() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.DelegateRecursionKernel)),
            "ILGPU1209",
            "Method 'SumOver' calls itself while passing delegates",
            "DelegateRecursion");

    [Fact]
    public void StaticViewField_Reports1214() =>
        AssertDiagnostic(
            Compile(nameof(Kernels.StaticViewFieldKernel)),
            "ILGPU1214",
            "Static field 'Kernels.StaticView' of type 'ArrayView<int>' holds a " +
            "view or a pointer, which kernels cannot load",
            "StaticViewField");

    // A static readonly view compiled to a load from a null pointer
    [Fact]
    public Task StaticViewField_FailsBuild() =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                static readonly ArrayView1D<int, Stride1D.Dense> Table;
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
                    d[i] = Table[i]; // error
            }
            """,
            "ILGPU1214",
            "Static field 'K.Table' of type 'ArrayView1D<int, Dense>' holds a " +
            "view or a pointer, which kernels cannot load");

    // An internal compiler error while inlining the array in the structure
    [Fact]
    public Task StaticArrayHolder_FailsBuild() =>
        AssertBuildErrorAsync(
            """
            struct H { public int V; public int[] A; }
            static class K
            {
                static readonly H Holder = new() { V = 1, A = [1, 2] };
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
                    d[i] = Holder.V; // error
            }
            """,
            "ILGPU1214",
            "Static field 'K.Holder' of type 'H' contains the field 'H.A' of type " +
            "'int[]', which holds a reference that kernels cannot load, even if it " +
            "is null");

    // An unnamed error that the class type String is not supported
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task StaticString_FailsBuild(RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                static readonly string Name = "abc";
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
                    d[i] = Name.Length; // error
            }
            """,
            "ILGPU1214",
            "Static field 'K.Name' has the reference type 'string', which " +
            "kernels cannot load, even if the field is null",
            optimizationLevel);

    // A call to a getter that could not be compiled, since the class type C
    // is not supported. The error points at the call, as the location of the
    // generated getter is the declaration of the property.
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task StaticClassProperty_FailsBuild(
        RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            sealed class C { public int V = 2; }
            static class K
            {
                static C Box { get; } = new();
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
                    d[i] = Box.V; // error
            }
            """,
            "ILGPU1214",
            "Static field 'K.Box' has the reference type 'C', which kernels " +
            "cannot load, even if the field is null",
            optimizationLevel);

    // Failed with ILGPU1214 although optimization removes the calls to Fail and
    // Bad, as it removes calls to helpers failing with other errors. Only the IR
    // optimizer folds the tuple elements, not the constant evaluator of the
    // frontend, so the helpers are generated.
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task StaticStringInRemovedHelper_Compiles(
        RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildSucceedsAsync(
            """
            sealed class C { public int V = 2; }
            static class K
            {
                static readonly string Name = "abc";
                static readonly C Box = new();
                static int Fail() => throw new InvalidOperationException(Name);
                static int CheckedArg(int m) => m >= 0 ? m : Fail();
                static int Bad() => Name.Length + Box.V;
                static int Pick(int m) => m == 0 ? 1 : Bad();
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
                    d[i] = CheckedArg((((int a, int b))(5, i.X)).a) +
                        Pick((((int a, int b))(0, i.X)).a);
            }
            """,
            optimizationLevel);

    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task StaticStringInSurvivingHelper_FailsBuild(
        RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                static readonly string Name = "abc";
                static int Bad() => Name.Length; // error
                static int Pick(int m) => m == 0 ? 1 : Bad();
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> d) =>
                    d[i] = Pick(i);
            }
            """,
            "ILGPU1214",
            "Static field 'K.Name' has the reference type 'string', which " +
            "kernels cannot load, even if the field is null",
            optimizationLevel);

    [Fact]
    public void Expression_ReportsUnsupportedConstruct()
    {
        using var helper = new CompilationHelper(BackendType.CPU);
        var kernel = typeof(Kernels).GetMethod(
            nameof(Kernels.ExpressionKernel),
            BindingFlags.Public | BindingFlags.Static)!;
        var exception = Assert.ThrowsAny<Exception>(() =>
            helper.GetNormalizedIR(kernel, IRDumpPoint.AfterBackendTransforms));
        Assert.True(KernelCompilationErrors.TryCreateDiagnostic(
            exception,
            LaunchSite,
            out var diagnostic));
        Assert.Equal("ILGPU1200", diagnostic!.Id);
        Assert.Contains("Expression`1", diagnostic.Message);
    }

    /// <summary>
    /// A local of a structure containing itself through a field of a class
    /// type, whose type information overflowed the stack of the compiler.
    /// </summary>
    [Fact]
    public Task RecursiveLocalType_FailsBuild() =>
        AssertBuildErrorAsync(
            """
            struct S
            {
                public int Value;
                public L? Link;
            }

            sealed class L
            {
                public S Node;
            }

            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    var node = new S { Value = i }; // error
                    data[i] = node.Value;
                }
            }
            """,
            "ILGPU1200",
            "Kernel code uses an unsupported construct: Recursive type 'S' is " +
            "not supported in GPU kernels: it refers to itself through " +
            "'S.Link.Node'.");

    #endregion

    #region Returned Local Addresses (ILGPU1200)

    /// <summary>
    /// A helper that returns the address of one of its locals, of a field of a local
    /// structure or of a parameter it receives by value, or a pointer or reference
    /// derived from it, is rejected at the returned address on every backend and at
    /// every optimization level, before inlining moves the local into its caller.
    /// </summary>
    [Theory]
    [InlineData(
        nameof(Kernels.ReturnedLocalAddressKernel),
        "AddressOfLocal",
        "ReturnedLocalAddress")]
    [InlineData(
        nameof(Kernels.ReturnedFieldAddressKernel),
        "AddressOfField",
        "ReturnedFieldAddress")]
    [InlineData(
        nameof(Kernels.ReturnedCastAddressKernel),
        "CastAddressOfLocal",
        "ReturnedCastAddress")]
    [InlineData(
        nameof(Kernels.ReturnedReferenceToLocalKernel),
        "ReferenceToLocal",
        "ReturnedReferenceToLocal")]
    [InlineData(
        nameof(Kernels.ReturnedParameterAddressKernel),
        "AddressOfParameter",
        "ReturnedParameterAddress")]
    public void ReturnedLocalAddress_Reports1200(
        string kernelName,
        string methodName,
        string marker)
    {
        foreach (var backend in new[] { BackendType.CPU, BackendType.Metal })
        {
            foreach (var level in Enum.GetValues<OptimizationLevel>())
            {
                AssertDiagnostic(
                    Compile(
                        kernelName,
                        backend,
                        new CompilationProperties { OptimizationLevel = level }),
                    "ILGPU1200",
                    $"Method '{methodName}' returns the address of one of its " +
                    "local variables",
                    marker);
            }
        }
    }

    #endregion

    #region Unsupported IL Instructions (ILGPU1200)

    /// <summary>
    /// The disassembler rejected mkrefany: the kernel was left without a body
    /// and the build failed with an InvalidOperationException without a
    /// location.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task TypedReference_FailsBuild(RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    int value = data[i];
                    TypedReference reference = __makeref(value); // error
                    data[i] = __refvalue(reference, int) + 1;
                }
            }
            """,
            "ILGPU1200",
            "Typed references are not supported in kernels: 'K.Kern' uses " +
            "__makeref, __refvalue or __reftype",
            optimizationLevel);

    /// <summary>
    /// A helper that uses a typed reference is reported at its call.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task TypedReferenceInHelper_FailsBuild(
        RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                static int Read(int value)
                {
                    TypedReference reference = __makeref(value);
                    return __refvalue(reference, int);
                }

                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    data[i] = Read(data[i]); // error
                }
            }
            """,
            "ILGPU1200",
            "Typed references are not supported in kernels: 'K.Read' uses " +
            "__makeref, __refvalue or __reftype",
            optimizationLevel);

    /// <summary>
    /// The disassembler resolved the signature token of calli as a method,
    /// which failed: the kernel was left without a body and the build failed
    /// with an InvalidOperationException without a location. C# keeps a
    /// function pointer that is called with arguments in a variable, whose
    /// function pointer type is reported at its first use.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task FunctionPointer_FailsBuild(RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static unsafe class K
            {
                static int Inc(int value) => value + 1;

                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    delegate*<int, int> f = &Inc; // error
                    data[i] = f(data[i]);
                }
            }
            """,
            "ILGPU1200",
            "Function pointers are not supported in kernels: 'K.Kern' uses " +
            "delegate*",
            optimizationLevel);

    /// <summary>
    /// A function pointer that is called without arguments is not kept in a
    /// variable: calli itself is reported.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task FunctionPointerWithoutArguments_FailsBuild(
        RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static unsafe class K
            {
                static int Get() => 42;

                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    data[i] = ((delegate*<int>)&Get)(); // error
                }
            }
            """,
            "ILGPU1200",
            "Function pointers are not supported in kernels: 'K.Kern' uses " +
            "delegate*",
            optimizationLevel);

    /// <summary>
    /// A call to a helper that takes or returns a function pointer reported an
    /// unsupported class type for the function pointer type.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug, "Apply", "Apply(&Inc, data[i])")]
    [InlineData(RoslynOptimizationLevel.Release, "Apply", "Apply(&Inc, data[i])")]
    [InlineData(RoslynOptimizationLevel.Debug, "Get", "Get() == null ? 0 : 1")]
    [InlineData(RoslynOptimizationLevel.Release, "Get", "Get() == null ? 0 : 1")]
    public Task FunctionPointerParameter_FailsBuild(
        RoslynOptimizationLevel optimizationLevel,
        string helper,
        string value) =>
        AssertBuildErrorAsync(
            $$"""
            static unsafe class K
            {
                static int Inc(int value) => value + 1;

                static int Apply(delegate*<int, int> f, int value) => f(value);

                static delegate*<int, int> Get() => &Inc;

                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    data[i] = {{value}}; // error
                }
            }
            """,
            "ILGPU1200",
            $"Function pointers are not supported in kernels: 'K.{helper}' " +
            "uses delegate*",
            optimizationLevel);

    /// <summary>
    /// The disassembler rejected the arglist instruction of a method with a
    /// variable argument list, which reported no error until the call failed
    /// with an unrelated message.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task ArgList_FailsBuild(RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                static int Count(__arglist) =>
                    new ArgIterator(__arglist).GetRemainingCount();

                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    data[i] = Count(__arglist(data[i], 2)); // error
                }
            }
            """,
            "ILGPU1200",
            "Variable argument lists are not supported in kernels: 'K.Count' " +
            "uses __arglist",
            optimizationLevel);

    /// <summary>
    /// Creates a static method with the given IL code, which returns an int
    /// and takes a pointer to an int.
    /// </summary>
    private static MethodInfo CreateMethod(Action<ILGenerator> emit)
    {
        var type = AssemblyBuilder.DefineDynamicAssembly(
            new AssemblyName("KernelDiagnosticInvalidIL"),
            AssemblyBuilderAccess.RunAndCollect)
            .DefineDynamicModule("KernelDiagnosticInvalidIL")
            .DefineType("Methods", TypeAttributes.Public);
        var method = type.DefineMethod(
            "Method",
            MethodAttributes.Public | MethodAttributes.Static,
            typeof(int),
            [typeof(int*)]);
        emit(method.GetILGenerator());
        return type.CreateType().GetMethod(method.Name)!;
    }

    /// <summary>
    /// Disassembles a static method with the given invalid IL code and returns
    /// the error.
    /// </summary>
    private static DisassemblerException DisassembleInvalidIL(
        Action<ILGenerator> emit)
    {
        var method = CreateMethod(emit);
        return Assert.Throws<DisassemblerException>(
            () => Disassembler.TryDisassemble(method));
    }

    /// <summary>
    /// Module.ResolveMethod rejects an invalid token with an ArgumentException,
    /// which reports IL code that cannot be disassembled at its instruction.
    /// </summary>
    [Fact]
    public void InvalidToken_ThrowsDisassemblerException()
    {
        var exception = DisassembleInvalidIL(il =>
        {
            il.Emit(OpCodes.Nop);
            // A member reference that the module does not define
            il.Emit(OpCodes.Call, 0x0A00FFFF);
            il.Emit(OpCodes.Ret);
        });
        Assert.Equal(1, exception.ILOffset);
        Assert.IsAssignableFrom<ArgumentException>(exception.InnerException);
    }

    /// <summary>
    /// IL code that ends within the operand of an instruction cannot be
    /// disassembled either.
    /// </summary>
    [Fact]
    public void TruncatedOperand_ThrowsDisassemblerException()
    {
        var exception = DisassembleInvalidIL(il =>
        {
            il.Emit(OpCodes.Nop);
            // ldc.i4 with one of its four operand bytes
            il.Emit(OpCodes.Ldc_I4, (byte)1);
        });
        Assert.Equal(1, exception.ILOffset);
        Assert.Equal("The IL code ends within an instruction", exception.Message);
    }

    /// <summary>
    /// A switch whose number of targets exceeds the remaining IL code ends
    /// within the instruction, which is reported before its targets are
    /// allocated. The number of targets uint.MaxValue failed with an
    /// IndexOutOfRangeException, which is an internal error.
    /// </summary>
    [Fact]
    public void TruncatedSwitch_ThrowsDisassemblerException()
    {
        var exception = DisassembleInvalidIL(il =>
        {
            il.Emit(OpCodes.Nop);
            // switch with uint.MaxValue targets, none of which follow
            il.Emit(OpCodes.Switch, -1);
            il.Emit(OpCodes.Ldc_I4_0);
            il.Emit(OpCodes.Ret);
        });
        Assert.Equal(1, exception.ILOffset);
        Assert.Equal("The IL code ends within an instruction", exception.Message);
    }

    /// <summary>
    /// Collects the offsets of an instruction and its prefixes.
    /// </summary>
    private readonly struct CollectOffsets(List<int> offsets)
        : IILInstructionOffsetOperation
    {
        public void Apply(ILInstruction instruction, int offset) =>
            offsets.Add(offset);
    }

    /// <summary>
    /// The prefixes no. and unaligned. take a one-byte operand, which was
    /// disassembled as the next instruction. The offsets of the instruction
    /// include the start of its prefixes, which branches may target.
    /// </summary>
    [Fact]
    public void PrefixOperands_AreSkipped()
    {
        var method = CreateMethod(il =>
        {
            il.Emit(OpCodes.Ldarg_0);
            // no. 1, which has no OpCodes field: its bytes are FE 19 01
            il.Emit(OpCodes.Prefix1);
            il.Emit(OpCodes.Ldc_I4_3);
            il.Emit(OpCodes.Break);
            il.Emit(OpCodes.Unaligned, (byte)1);
            il.Emit(OpCodes.Ldind_I4);
            il.Emit(OpCodes.Ret);
        });
        var disassembled = Disassembler.TryDisassemble(method);
        Assert.NotNull(disassembled);
        Assert.Equal(3, disassembled.Count);
        Assert.Equal(ILInstructionType.Ldarg, disassembled[0].InstructionType);
        Assert.Equal(ILInstructionType.Ldind, disassembled[1].InstructionType);
        Assert.True(disassembled[1].HasFlags(
            ILInstructionFlags.Unchecked | ILInstructionFlags.Unaligned));
        Assert.Equal(ILInstructionType.Ret, disassembled[2].InstructionType);

        var offsets = new List<int>();
        disassembled[1].ForEachOffset(new CollectOffsets(offsets));
        Assert.Equal([7, 4, 1], offsets);
    }

    private const string MissingLibraryName = "KernelDiagnosticMissingLibrary";

    /// <summary>
    /// Returns a reference to a library that the compilation is built against,
    /// but that cannot be loaded when its kernels are compiled.
    /// </summary>
    private static MetadataReference[] GetMissingLibraryReference() =>
        [
            RoslynCompiler.CreateCompilation(
                ["""
                public static class Library
                {
                    public static int Twice(int value) => 2 * value;
                }
                """],
                MissingLibraryName,
                Microsoft.CodeAnalysis.OutputKind.DynamicallyLinkedLibrary)
                .ToMetadataReference(),
        ];

    /// <summary>
    /// The kernel calls a method of a library that cannot be loaded, so that
    /// its IL code cannot be disassembled: this left the kernel without a body
    /// and the build failed with an InvalidOperationException without a
    /// location. The call is not the first statement, which tells its location
    /// apart from the start of the method.
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public Task UnresolvableCall_FailsBuild(RoslynOptimizationLevel optimizationLevel) =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    int value = data[i] + 1;
                    data[i] = Library.Twice(value); // error
                }
            }
            """,
            "ILGPU1200",
            "The IL code of 'K.Kern' cannot be compiled: Could not load file or " +
            $"assembly '{MissingLibraryName}",
            optimizationLevel,
            GetMissingLibraryReference());

    /// <summary>
    /// A helper that calls a method of a library that cannot be loaded fails
    /// to disassemble, which is reported once a call to it survives. The
    /// reason is the error of the helper at its location, which was wrapped in
    /// "An internal compiler error has been detected".
    /// </summary>
    [Theory]
    [InlineData(RoslynOptimizationLevel.Debug)]
    [InlineData(RoslynOptimizationLevel.Release)]
    public async Task UnresolvableCallInHelper_FailsBuild(
        RoslynOptimizationLevel optimizationLevel)
    {
        var diagnostic = await AssertBuildErrorAsync(
            """
            static class K
            {
                static int Twice(int value) => Library.Twice(value);

                public static void Kern(Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    data[i] = Twice(data[i]); // error
                }
            }
            """,
            "ILGPU1200",
            "'K.Kern' calls 'K.Twice', which could not be compiled: The IL code " +
            "of 'K.Twice' cannot be compiled: Could not load file or assembly " +
            $"'{MissingLibraryName}",
            optimizationLevel,
            GetMissingLibraryReference());
        Assert.EndsWith(
            ". in file 'source0.cs', line 7 column 36 to 56",
            diagnostic,
            StringComparison.Ordinal);
    }

    #endregion

    #region Integers Converted to Pointers (ILGPU1215)

    // Integers are traced through phis, as the addresses they hold remain the
    // addresses of local or shared memory
    [Theory]
    [InlineData(BackendType.Metal, OptimizationLevel.O0)]
    [InlineData(BackendType.Metal, OptimizationLevel.O1)]
    [InlineData(BackendType.Metal, OptimizationLevel.O2)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O0)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O1)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O2)]
    public void LocalAddressLoop_WithoutGenericPointers_Reports1215(
        BackendType backend,
        OptimizationLevel level) =>
        AssertDiagnostic(
            Generate(nameof(Kernels.LocalAddressLoopKernel), backend, level),
            "ILGPU1215",
            "Method 'Kernels.LocalAddressLoopKernel' converts an integer that " +
            "holds the address of local memory to a pointer",
            "LocalAddressLoop");

    [Theory]
    [InlineData(BackendType.Metal, OptimizationLevel.O0)]
    [InlineData(BackendType.Metal, OptimizationLevel.O1)]
    [InlineData(BackendType.Metal, OptimizationLevel.O2)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O0)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O1)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O2)]
    public void LocalAddressSelect_WithoutGenericPointers_Reports1215(
        BackendType backend,
        OptimizationLevel level) =>
        AssertDiagnostic(
            Generate(nameof(Kernels.LocalAddressSelectKernel), backend, level),
            "ILGPU1215",
            "Method 'Kernels.LocalAddressSelectKernel' converts an integer that " +
            "holds the address of local memory to a pointer",
            "LocalAddressSelect");

    [Theory]
    [InlineData(BackendType.Metal, OptimizationLevel.O0)]
    [InlineData(BackendType.Metal, OptimizationLevel.O1)]
    [InlineData(BackendType.Metal, OptimizationLevel.O2)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O0)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O1)]
    [InlineData(BackendType.OpenCL, OptimizationLevel.O2)]
    public void SharedAddressSelect_WithoutGenericPointers_Reports1215(
        BackendType backend,
        OptimizationLevel level) =>
        AssertDiagnostic(
            Generate(nameof(Kernels.SharedAddressSelectKernel), backend, level),
            "ILGPU1215",
            "Method 'Kernels.SharedAddressSelectKernel' converts an integer that " +
            "holds the address of shared memory to a pointer",
            "SharedAddressSelect");

    // Integers are traced through the native integers they were converted
    // from, such as a native integer selected from two local addresses and
    // aligned, through fields of structures, and through native integers moved
    // by an offset that holds the address
    [Theory]
    [InlineData(
        nameof(Kernels.LocalAddressAlignedSelectKernel),
        "Kernels.LocalAddressAlignedSelectKernel",
        "local",
        "LocalAddressAlignedSelect")]
    [InlineData(
        nameof(Kernels.LocalAddressOffsetKernel),
        "Kernels.LocalAddressOffsetKernel",
        "local",
        "LocalAddressOffset")]
    public void TracedLocalAddress_WithoutGenericPointers_Reports1215(
        string kernelName,
        string methodName,
        string space,
        string marker)
    {
        foreach (var backend in new[] { BackendType.Metal, BackendType.OpenCL })
        {
            foreach (var level in new[]
            {
                OptimizationLevel.O0,
                OptimizationLevel.O1,
                OptimizationLevel.O2,
            })
            {
                AssertDiagnostic(
                    Generate(kernelName, backend, level),
                    "ILGPU1215",
                    $"Method '{methodName}' converts an integer that holds the " +
                    $"address of {space} memory to a pointer, which {backend} " +
                    "does not support",
                    marker);
            }
        }
    }

    // Native integers that are never converted back to addresses, such as the
    // distance of two local or shared addresses, are integers, also after
    // conditionals, in loops or added to global addresses
    [SkippableTheory]
    [InlineData(nameof(Kernels.LocalOffsetSelectKernel), BackendType.Metal)]
    [InlineData(nameof(Kernels.LocalOffsetSelectKernel), BackendType.OpenCL)]
    [InlineData(nameof(Kernels.LocalOffsetLoopKernel), BackendType.Metal)]
    [InlineData(nameof(Kernels.LocalOffsetLoopKernel), BackendType.OpenCL)]
    [InlineData(nameof(Kernels.SharedOffsetIndexKernel), BackendType.Metal)]
    [InlineData(nameof(Kernels.SharedOffsetIndexKernel), BackendType.OpenCL)]
    [InlineData(nameof(Kernels.GlobalFieldAddressKernel), BackendType.Metal)]
    [InlineData(nameof(Kernels.GlobalFieldAddressKernel), BackendType.OpenCL)]
    [InlineData(nameof(Kernels.LocalAddressRoundTripKernel), BackendType.Metal)]
    [InlineData(nameof(Kernels.LocalAddressRoundTripKernel), BackendType.OpenCL)]
    public async Task AddressDistance_WithoutGenericPointers_Compiles(
        string kernelName,
        BackendType backend)
    {
        Skip.IfNot(
            await Availability.IsCompilerAvailableAsync(backend),
            $"Native compiler for {backend} is not available");
        using var helper = new CompilationHelper(backend);
        foreach (var level in new[]
        {
            OptimizationLevel.O0,
            OptimizationLevel.O1,
            OptimizationLevel.O2,
        })
        {
            var result = await AssertionRecorder.RunAsync(() =>
                helper.CompileToNativeAsync(
                    GetKernel(kernelName),
                    backend,
                    new CompilationProperties(OptimizationLevel: level)));
            Assert.NotNull(result);
            Assert.True(result!.Success, $"{level}: {result.StdErr}");
        }
    }

    // The build reports the loop of Release IL at its line
    [Fact]
    public Task LocalAddressLoop_InReleaseBuild_FailsBuild() =>
        AssertBuildErrorAsync(
            """
            static class K
            {
                public static unsafe void Kern(
                    Index1D i, ArrayView1D<int, Stride1D.Dense> data)
                {
                    int value = 0;
                    long address = (long)&value;
                    for (int k = 0; k < data[i]; ++k)
                        address += 4;
                    *(int*)address = data[i]; // error
                    data[i] = value;
                }
            }
            """,
            "ILGPU1215",
            "Method 'K.Kern' converts an integer that holds the address of " +
            "local memory to a pointer, which Metal does not support",
            backend: BackendType.Metal);

    #endregion

    #region Build Diagnostics

    /// <summary>
    /// The location of the launch site errors without a source location are
    /// reported at.
    /// </summary>
    private static readonly RoslynLocation LaunchSite = RoslynLocation.Create(
        "/src/Launch.cs",
        default,
        new LinePositionSpan(new LinePosition(4, 8), new LinePosition(4, 20)));

    private static string? Report(Exception exception) =>
        KernelCompilationErrors.TryCreateDiagnostic(
            exception,
            LaunchSite,
            out var diagnostic)
        ? diagnostic!.ToString()
        : null;

    [Fact]
    public void KernelCompilationException_IsReportedAtItsSourceLocation()
    {
        var exception = new KernelCompilationException(
            "ILGPU1201",
            "Message.",
            new FileLocation("/src/Kernels.cs", 9, 20, 12, 12));

        Assert.Equal(
            "/src/Kernels.cs(12,9): error ILGPU1201: Message.",
            Report(exception));
    }

    [Fact]
    public void KernelCompilationException_WithoutLocation_IsReportedAtLaunchSite()
    {
        var exception = new KernelCompilationException(
            "ILGPU1202",
            "Message.",
            Location.Unknown);

        Assert.Equal(
            "/src/Launch.cs(5,9): error ILGPU1202: Message.",
            Report(exception));
    }

    [Fact]
    public void UnsupportedConstruct_IsReportedAsILGPU1200()
    {
        var location = new FileLocation("/src/Kernels.cs", 5, 9, 3, 3);
        var exception = location.GetNotSupportedException("Not supported {0}", "X");

        Assert.Equal(
            "/src/Kernels.cs(3,5): error ILGPU1200: Kernel code uses an " +
            "unsupported construct: Not supported X",
            Report(exception));
    }

    [Fact]
    public void UnsupportedConstruct_WithoutLocation_IsReportedAtLaunchSite()
    {
        Assert.Equal(
            "/src/Launch.cs(5,9): error ILGPU1200: Kernel code uses an " +
            "unsupported construct: Not supported",
            Report(new NotSupportedException("Not supported")));
    }

    [Fact]
    public void InternalCompilerErrors_AreNotReportedAsDiagnostics()
    {
        var location = new FileLocation("/src/Kernels.cs", 5, 9, 3, 3);
        Assert.Null(Report(location.GetInvalidOperationException()));
        Assert.Null(Report(new InvalidOperationException()));
    }

    #endregion
}
