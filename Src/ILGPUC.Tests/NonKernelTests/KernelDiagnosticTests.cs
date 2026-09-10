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
using ILGPUC.IR;
using ILGPUC.Roslyn.Generation;
using ILGPUC.Tests.Framework;
using Microsoft.CodeAnalysis.Text;
using System;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Xunit;
using RoslynLocation = Microsoft.CodeAnalysis.Location;

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
    private static async Task AssertBuildErrorAsync(
        string kernelClass,
        string id,
        string message)
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
            "KernelDiagnosticTest");
        var analysis = CompilationRewriter.Analyze(compilation);
        Assert.Empty(analysis.Diagnostics);

        var exception = await Assert.ThrowsAsync<KernelDiagnosticException>(() =>
            MainKernelProvider.CreateAsync(
                compilation,
                analysis,
                [CompilationHelper.CreateBackend(BackendType.CPU)],
                new KernelCompiler()));
        int line = Array.FindIndex(
            source.Split('\n'),
            l => l.TrimEnd().EndsWith("// error", StringComparison.Ordinal)) + 1;
        Assert.True(line > 0, "Marker '// error' not found");
        var diagnostic = exception.Diagnostic.ToString();
        Assert.StartsWith($"source0.cs({line},", diagnostic, StringComparison.Ordinal);
        Assert.Contains($"): error {id}: ", diagnostic, StringComparison.Ordinal);
        Assert.Contains(message, diagnostic, StringComparison.Ordinal);
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
