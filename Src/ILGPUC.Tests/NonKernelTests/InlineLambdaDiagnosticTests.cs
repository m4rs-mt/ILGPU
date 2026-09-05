// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: InlineLambdaDiagnosticTests.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Roslyn.Analysis;
using ILGPUC.Roslyn.Generation;
using ILGPUC.Tests.Framework;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using System.IO;
using System.Linq;
using System.Reflection;
using Xunit;

namespace ILGPUC.Tests.NonKernelTests;

/// <summary>
/// Tests that validate diagnostic behavior for edge-case inline lambda
/// patterns: assigned captures, reference type captures, captures of the
/// enclosing instance, generic contexts and capturing local functions.
/// </summary>
public sealed class InlineLambdaDiagnosticTests
{
    /// <summary>
    /// Helper: analyzes a program source and returns the analysis result.
    /// </summary>
    private static AnalysisResult AnalyzeSource(string mainBody) =>
        AnalyzeSource(mainBody, out _);

    /// <summary>
    /// Helper: analyzes a program source and returns the analysis result and
    /// the analyzed compilation.
    /// </summary>
    private static AnalysisResult AnalyzeSource(
        string mainBody,
        out CSharpCompilation compilation) =>
        AnalyzeProgram($$"""
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

                    {{mainBody}}

                    stream.Synchronize();
                }
            }
            """,
            out compilation);

    /// <summary>
    /// Helper: analyzes the given type declarations, preceded by the usual
    /// ILGPU using directives.
    /// </summary>
    private static AnalysisResult AnalyzeProgram(string declarations) =>
        AnalyzeProgram(declarations, out _);

    /// <summary>
    /// Helper: analyzes the given type declarations and returns the analysis
    /// result and the analyzed compilation.
    /// </summary>
    private static AnalysisResult AnalyzeProgram(
        string declarations,
        out CSharpCompilation compilation)
    {
        var source = $"""
            using System;
            using ILGPU;
            using ILGPU.Runtime;

            {declarations}
            """;

        compilation = RoslynCompiler.CreateCompilation(
            [source], "DiagnosticTest");
        return CompilationRewriter.Analyze(compilation);
    }

    /// <summary>
    /// Asserts that exactly one diagnostic with the given id was reported and
    /// that it points at <paramref name="expectedSource"/>.
    /// </summary>
    private static KernelDiagnostic AssertSingleDiagnostic(
        AnalysisResult analysis,
        string expectedId,
        string expectedSource)
    {
        var diagnostic = Assert.Single(analysis.Diagnostics);
        Assert.Equal(expectedId, diagnostic.Id);

        var location = diagnostic.Location;
        var text = location.SourceTree!.GetText().ToString(location.SourceSpan);
        Assert.Equal(expectedSource, text);
        return diagnostic;
    }

    /// <summary>
    /// Inline kernels that only read captured values report no diagnostics.
    /// </summary>
    [Fact]
    public void ReadOnlyCaptures_NoDiagnostics()
    {
        var analysis = AnalyzeSource("""
            int offset = 3;
            stream.Launch((Index1D)4, index =>
            {
                int local = index;
                local += offset;
                view[index] = local;
            });
            """);

        Assert.Empty(analysis.Diagnostics);
        Assert.NotNull(Assert.Single(analysis.UniqueKernels).LiftedKernel);
    }

    /// <summary>
    /// Assigning a captured scalar would be lost: kernels receive captured
    /// values by copy, so the assignment is rejected.
    /// </summary>
    [Fact]
    public void WriteOnlyCapturedScalar_ReportsAssignment()
    {
        var analysis = AnalyzeSource("""
            int result = 0;
            stream.Launch((Index1D)4, index =>
            {
                result = index;
                view[index] = 42;
            });
            """);

        AssertSingleDiagnostic(analysis, KernelDiagnostic.AssignsCaptureId, "result");
        Assert.Null(analysis.UniqueKernels[0].LiftedKernel);
    }

    /// <summary>
    /// A compound assignment of a captured scalar is rejected as well.
    /// </summary>
    [Fact]
    public void ReadWriteCapturedScalar_ReportsAssignment()
    {
        var analysis = AnalyzeSource("""
            int counter = 0;
            stream.Launch((Index1D)4, index =>
            {
                counter += index;
                view[index] = counter;
            });
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.AssignsCaptureId, "counter");
        Assert.Contains("'counter'", diagnostic.Message);
    }

    /// <summary>
    /// A MemoryBuffer capture is lowered to its View. This test validates
    /// the lowering happens correctly and the original buffer name is
    /// preserved in the parameter.
    /// </summary>
    [Fact]
    public void CapturedBuffer_LoweredToViewType()
    {
        var analysis = AnalyzeSource("""
            stream.Launch((Index1D)4, index =>
            {
                buffer.View[index] = index * 3;
            });
            """);

        Assert.Empty(analysis.Diagnostics);
        Assert.Single(analysis.UniqueKernels);
        var kernel = analysis.UniqueKernels[0];

        var bufferParam = kernel.Parameters.FirstOrDefault(p => p.Name == "buffer");
        Assert.NotNull(bufferParam);
        Assert.Equal(ParameterKind.View, bufferParam!.Kind);

        // The Type should be the view type, not the buffer type
        Assert.Contains("ArrayView", bufferParam.Type.Name);

        // The lifted kernel receives the view under the buffer's name
        var lifted = kernel.LiftedKernel!.ToString();
        Assert.Contains("buffer[index]", lifted);
        Assert.DoesNotContain("buffer.View", lifted);
    }

    /// <summary>
    /// Capturing an array (a reference type) is rejected.
    /// </summary>
    [Fact]
    public void CapturedReferenceType_ReportsUnsupportedCapture()
    {
        var analysis = AnalyzeSource("""
            var values = new int[] { 1, 2, 3, 4 };
            stream.Launch((Index1D)4, index => view[index] = values[index]);
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.UnsupportedCaptureId, "values");
        Assert.Contains("reference type", diagnostic.Message);
    }

    /// <summary>
    /// The kernel and launching method of issue #463, shared by the tests of
    /// captured delegate parameters below.
    /// </summary>
    private const string UnaryOpHost = """
        static class Kernels
        {
            public static void UnaryOpKernel(
                Index1D index, ArrayView<int> data, Func<int, int> op) =>
                data[index] = op(data[index]);
        }

        static class Host
        {
            public static void UnaryOp(
                AcceleratorStream stream, ArrayView<int> view, Func<int, int> op)
            {
                stream.Launch(
                    view.IntExtent,
                    index => Kernels.UnaryOpKernel(index, view, op));
            }
        }
        """;

    /// <summary>
    /// Helper: analyzes <see cref="UnaryOpHost"/> called from the given
    /// statements, which can use <c>stream</c> and <c>view</c>.
    /// </summary>
    private static AnalysisResult AnalyzeUnaryOpCalls(
        string statements,
        string declarations = "") =>
        AnalyzeProgram($$"""
            {{UnaryOpHost}}

            static class Callers
            {
                {{declarations}}

                static void Run(AcceleratorStream stream, ArrayView<int> view)
                {
                    {{statements}}
                }
            }
            """);

    /// <summary>
    /// A launch lambda passing a delegate parameter of the launching method to
    /// a kernel method (issue #463) is compiled into one kernel for each
    /// target passed by the callers, and the delegates passed are registered
    /// under the keys of their targets. The same static method passed twice
    /// shares a target.
    /// </summary>
    [Fact]
    public void CapturedDelegateParameter_BoundToCallerTargets()
    {
        var analysis = AnalyzeUnaryOpCalls(
            """
            Host.UnaryOp(stream, view, v => -v);
            Host.UnaryOp(stream, view, v => ~v);
            Host.UnaryOp(stream, view, Negate);
            Host.UnaryOp(stream, view, Negate);
            """,
            "static int Negate(int value) => -value;");

        Assert.Empty(analysis.Diagnostics);
        var dispatch = Assert.Single(analysis.DispatchKernels);
        var capture = Assert.Single(dispatch.DelegateCaptures!.Value);
        Assert.Equal("op", capture.Parameter.Name);

        Assert.Equal(3, analysis.UniqueKernels.Count);
        foreach (var kernel in analysis.UniqueKernels)
        {
            Assert.Equal(dispatch.KernelName, kernel.GenericOriginName);
            Assert.Equal(["view"], kernel.Parameters.Select(p => p.Name));
            Assert.Contains(
                "KernelDelegates.Bind",
                kernel.LiftedKernel!.ToFullString());
        }

        // Keys are prefixed with the name of the assembly
        Assert.Equal(
            [
                "DiagnosticTest:Host.UnaryOp.op#0",
                "DiagnosticTest:Host.UnaryOp.op#1",
                "DiagnosticTest:Host.UnaryOp.op#2",
            ],
            analysis.UniqueKernels.Select(k => Assert.Single(k.DelegateKeys!.Value)));

        // Four call sites, the last two register the shared key
        Assert.Equal(4, analysis.DelegateRegistrations.Count);
        Assert.Equal(
            [
                "DiagnosticTest:Host.UnaryOp.op#0",
                "DiagnosticTest:Host.UnaryOp.op#1",
                "DiagnosticTest:Host.UnaryOp.op#2",
                "DiagnosticTest:Host.UnaryOp.op#2",
            ],
            analysis.DelegateRegistrations
                .OrderBy(r => r.Key.SpanStart)
                .Select(r => Assert.Single(r.Value.Keys)));
    }

    /// <summary>
    /// Delegates forwarded by delegate parameters of calling methods are traced
    /// to the callers of these methods, also through recursive forwarding.
    /// </summary>
    [Fact]
    public void ForwardedDelegateParameter_TracedToCallers()
    {
        var analysis = AnalyzeUnaryOpCalls(
            "Forward(stream, view, v => v + 1, 2);",
            """
            static void Forward(
                AcceleratorStream stream,
                ArrayView<int> view,
                Func<int, int> op,
                int depth)
            {
                if (depth > 0)
                    Forward(stream, view, op, depth - 1);
                else
                    Host.UnaryOp(stream, view, op);
            }
            """);

        Assert.Empty(analysis.Diagnostics);
        var kernel = Assert.Single(analysis.UniqueKernels);
        Assert.Equal(
            "DiagnosticTest:Host.UnaryOp.op#0",
            Assert.Single(kernel.DelegateKeys!.Value));
        var registration = Assert.Single(analysis.DelegateRegistrations);
        Assert.Equal("v => v + 1", registration.Key.ToString());
    }

    /// <summary>
    /// A lambda passed to a captured delegate parameter that captures a
    /// variable cannot be bound to a kernel and is reported at the capture.
    /// </summary>
    [Fact]
    public void CapturingDelegateArgument_ReportsCapture()
    {
        var analysis = AnalyzeUnaryOpCalls("""
            int offset = 3;
            Host.UnaryOp(stream, view, v => v + offset);
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.CapturingDelegateArgumentId, "offset");
        Assert.Contains(
            "lambda passed to 'Host.UnaryOp' for 'op' must not capture variables",
            diagnostic.Message);
        Assert.Contains("separate arguments", diagnostic.Message);
    }

    /// <summary>
    /// An argument that cannot be traced to a lambda or a static method, such
    /// as a delegate returned from a method, is reported at the argument.
    /// </summary>
    [Fact]
    public void UntraceableDelegateArgument_ReportsArgument()
    {
        var analysis = AnalyzeUnaryOpCalls(
            "Host.UnaryOp(stream, view, Create());",
            "static Func<int, int> Create() => v => v;");

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.UntraceableDelegateArgumentId, "Create()");
        Assert.Contains(
            "delegate passed to 'Host.UnaryOp' for 'op' cannot be traced",
            diagnostic.Message);
    }

    /// <summary>
    /// Multicast delegates passed to a captured delegate parameter, combined
    /// with <c>+</c>, <c>+=</c> or <c>Delegate.Combine</c>, have no single
    /// target and are reported at the argument. Callers in this compilation
    /// therefore never pass a multicast delegate at runtime.
    /// </summary>
    [Fact]
    public void MulticastDelegateArgument_ReportsArgument()
    {
        var analysis = AnalyzeUnaryOpCalls("""
            Func<int, int> f = v => -v;
            Func<int, int> g = v => ~v;
            Host.UnaryOp(stream, view, f + g);
            Host.UnaryOp(stream, view, (Func<int, int>)Delegate.Combine(f, g));
            f += g;
            Host.UnaryOp(stream, view, f);
            """);

        Assert.Equal(3, analysis.Diagnostics.Count);
        Assert.All(analysis.Diagnostics, diagnostic =>
        {
            Assert.Equal(KernelDiagnostic.UntraceableDelegateArgumentId, diagnostic.Id);
            Assert.Contains("cannot be traced", diagnostic.Message);
        });
        Assert.Equal(
            ["f + g", "(Func<int, int>)Delegate.Combine(f, g)", "f"],
            analysis.Diagnostics.Select(d =>
                d.Location.SourceTree!.GetText().ToString(d.Location.SourceSpan)));
    }

    /// <summary>
    /// An instance method passed to a captured delegate parameter captures its
    /// instance and is reported at the argument.
    /// </summary>
    [Fact]
    public void InstanceMethodDelegateArgument_ReportsArgument()
    {
        var analysis = AnalyzeUnaryOpCalls(
            "Host.UnaryOp(stream, view, new Callers2().Negate);",
            """
            sealed class Callers2
            {
                public int Negate(int value) => -value;
            }
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis,
            KernelDiagnostic.UntraceableDelegateArgumentId,
            "new Callers2().Negate");
        Assert.Contains("is not static", diagnostic.Message);
    }

    /// <summary>
    /// A launching method that is used as a delegate itself hides the
    /// delegates passed to it, which is reported at the reference.
    /// </summary>
    [Fact]
    public void LaunchingMethodUsedAsDelegate_ReportsReference()
    {
        var analysis = AnalyzeUnaryOpCalls("""
            Host.UnaryOp(stream, view, v => -v);
            Action<AcceleratorStream, ArrayView<int>, Func<int, int>> launch =
                Host.UnaryOp;
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.UntraceableDelegateArgumentId, "UnaryOp");
        Assert.Contains("is used as a delegate", diagnostic.Message);
    }

    /// <summary>
    /// A launching method referred to in <c>nameof</c>, also in constants and
    /// interpolated strings, is not used as a delegate, so its kernels are
    /// compiled without diagnostics. Roslyn binds method groups in
    /// <c>nameof</c> to candidate symbols only, which the reference search
    /// skips.
    /// </summary>
    [Fact]
    public void LaunchingMethodInNameOf_NoDiagnostics()
    {
        var analysis = AnalyzeUnaryOpCalls("""
            Host.UnaryOp(stream, view, v => -v);
            Console.WriteLine(nameof(Host.UnaryOp));
            string text = nameof(Host.UnaryOp) + nameof(Callers.Run);
            Console.WriteLine($"{nameof(Host.UnaryOp)}");
            """,
            "const string N = nameof(Host.UnaryOp);");

        Assert.Empty(analysis.Diagnostics);
        Assert.Single(analysis.UniqueKernels);
    }

    /// <summary>
    /// A lambda declared in a generic method cannot be bound to a kernel.
    /// </summary>
    [Fact]
    public void GenericDelegateArgument_ReportsGenericContext()
    {
        var analysis = AnalyzeUnaryOpCalls(
            "Generic<int>(stream, view);",
            """
            static void Generic<T>(AcceleratorStream stream, ArrayView<int> view) =>
                Host.UnaryOp(stream, view, v => v + 1);
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.GenericContextId, "v => v + 1");
        Assert.Contains("generic method", diagnostic.Message);
    }

    /// <summary>
    /// A captured delegate parameter of a launching method that is never
    /// called has no targets, so no kernel can be compiled, which is reported
    /// at the capture.
    /// </summary>
    [Fact]
    public void UncalledDelegateParameter_ReportsCapture()
    {
        var analysis = AnalyzeProgram(UnaryOpHost);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.UnboundDelegateParameterId, "op");
        Assert.Contains("no call of 'Host.UnaryOp'", diagnostic.Message);
    }

    /// <summary>
    /// A delegate parameter assigned in the launching method no longer holds
    /// the delegate passed by its callers and remains rejected.
    /// </summary>
    [Fact]
    public void AssignedDelegateParameter_ReportsUnsupportedCapture()
    {
        var analysis = AnalyzeProgram("""
            static class Host
            {
                public static void UnaryOp(
                    AcceleratorStream stream, ArrayView<int> view, Func<int, int> op)
                {
                    op ??= v => v;
                    stream.Launch(view.IntExtent, index => view[index] = op(index));
                }

                static void Run(AcceleratorStream stream, ArrayView<int> view) =>
                    UnaryOp(stream, view, v => -v);
            }
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.UnsupportedCaptureId, "op");
        Assert.Contains("delegate 'op'", diagnostic.Message);
        Assert.Contains("does not assign", diagnostic.Message);
    }

    /// <summary>
    /// A delegate variable of the launching method remains rejected at the
    /// captured variable.
    /// </summary>
    [Fact]
    public void CapturedDelegateLocal_ReportsUnsupportedCapture()
    {
        var analysis = AnalyzeSource("""
            Func<int, int> op = v => -v;
            stream.Launch((Index1D)4, index => view[index] = op(index));
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.UnsupportedCaptureId, "op");
        Assert.Contains("delegate 'op'", diagnostic.Message);
        Assert.Contains("lambda expression", diagnostic.Message);
    }

    /// <summary>
    /// A launch lambda passing a lambda expression to a kernel method is lifted
    /// as an inline kernel, so the delegate is created inside the kernel and
    /// only the other captures become kernel parameters.
    /// </summary>
    [Fact]
    public void DelegateArgument_LiftedAsInlineKernel()
    {
        var analysis = AnalyzeProgram("""
            static class Kernels
            {
                public static void UnaryOpKernel(
                    Index1D index, ArrayView<int> data, Func<int, int> op) =>
                    data[index] = op(data[index]);
            }

            static class Host
            {
                public static void AddOffset(
                    AcceleratorStream stream, ArrayView<int> view, int offset)
                {
                    stream.Launch(
                        view.IntExtent,
                        index => Kernels.UnaryOpKernel(index, view, x => x + offset));
                }
            }
            """);

        Assert.Empty(analysis.Diagnostics);
        var kernel = Assert.Single(analysis.UniqueKernels);
        Assert.NotNull(kernel.LiftedKernel);
        Assert.Equal(["view", "offset"], kernel.Parameters.Select(p => p.Name));
    }

    /// <summary>
    /// A launching method that overrides an abstract method can be called
    /// through the base type, where the delegates passed to it cannot be
    /// traced, so it is rejected at the method even if it is called directly.
    /// </summary>
    [Fact]
    public void VirtualLaunchingMethod_Reports1110()
    {
        var analysis = AnalyzeProgram("""
            static class Kernels
            {
                public static void UnaryOpKernel(
                    Index1D index, ArrayView<int> data, Func<int, int> op) =>
                    data[index] = op(data[index]);
            }

            abstract class Base
            {
                public abstract void Run(
                    AcceleratorStream stream, ArrayView<int> view, Func<int, int> op);
            }

            sealed class Derived : Base
            {
                public override void Run(
                    AcceleratorStream stream, ArrayView<int> view, Func<int, int> op)
                {
                    stream.Launch(
                        view.IntExtent,
                        index => Kernels.UnaryOpKernel(index, view, op));
                }
            }

            static class Callers
            {
                static void Run(AcceleratorStream stream, ArrayView<int> view)
                {
                    new Derived().Run(stream, view, x => x + 100);
                    Base b = new Derived();
                    b.Run(stream, view, x => -x);
                }
            }
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.VirtualLaunchingMethodId, "Run");
        Assert.Contains("'Derived.Run' captures its delegate parameter 'op'",
            diagnostic.Message);
        Assert.Contains("calls through 'Base.Run' cannot be traced", diagnostic.Message);
        Assert.Empty(analysis.DelegateRegistrations);
    }

    /// <summary>
    /// A launching method implementing an interface member can be called
    /// through the interface and is rejected as well.
    /// </summary>
    [Fact]
    public void InterfaceLaunchingMethod_Reports1110()
    {
        var analysis = AnalyzeUnaryOpCalls(
            "new Runner().Run(stream, view, v => -v);",
            """
            interface IRunner
            {
                void Run(
                    AcceleratorStream stream, ArrayView<int> view, Func<int, int> op);
            }

            sealed class Runner : IRunner
            {
                public void Run(
                    AcceleratorStream stream, ArrayView<int> view, Func<int, int> op) =>
                    Host.UnaryOp(stream, view, op);
            }
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.VirtualLaunchingMethodId, "Run");
        Assert.Contains("'Callers.Runner.Run' forwards its delegate parameter 'op'",
            diagnostic.Message);
        Assert.Contains("calls through 'Callers.IRunner.Run'", diagnostic.Message);
    }

    /// <summary>
    /// A launching method of a base class that implements an interface member
    /// for a derived class can be called through the interface, so it is
    /// rejected although its own class implements no interface.
    /// </summary>
    [Fact]
    public void InheritedInterfaceLaunchingMethod_Reports1110()
    {
        var analysis = AnalyzeUnaryOpCalls(
            "new RunnerBase().Run(stream, view, v => -v);",
            """
            interface IRunner
            {
                void Run(
                    AcceleratorStream stream, ArrayView<int> view, Func<int, int> op);
            }

            class RunnerBase
            {
                public void Run(
                    AcceleratorStream stream, ArrayView<int> view, Func<int, int> op) =>
                    Host.UnaryOp(stream, view, op);
            }

            sealed class Runner : RunnerBase, IRunner { }
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.VirtualLaunchingMethodId, "Run");
        Assert.Contains("'Callers.RunnerBase.Run' forwards", diagnostic.Message);
        Assert.Contains("calls through 'Callers.IRunner.Run'", diagnostic.Message);
    }

    /// <summary>
    /// A virtual launching method can be overridden and called through the
    /// overrides, so it is rejected.
    /// </summary>
    [Fact]
    public void VirtualLaunchingMethodWithoutOverrides_Reports1110()
    {
        var analysis = AnalyzeProgram("""
            class Host
            {
                public virtual void Run(
                    AcceleratorStream stream, ArrayView<int> view, Func<int, int> op)
                {
                    stream.Launch(view.IntExtent, index => view[index] = op(index));
                }

                static void Call(AcceleratorStream stream, ArrayView<int> view) =>
                    new Host().Run(stream, view, v => v * 2);
            }
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.VirtualLaunchingMethodId, "Run");
        Assert.Contains("calls through 'Host.Run'", diagnostic.Message);
    }

    /// <summary>
    /// A public launching method of a library can be called from other
    /// assemblies, whose delegates cannot be traced, so it is rejected at the
    /// method. Other assemblies do not call executables, which bind it to the
    /// delegates of their own callers.
    /// </summary>
    [Fact]
    public void PublicLaunchingMethod_Reports1111InLibraries()
    {
        var analysis = AnalyzeProgram("""
            public static class Host
            {
                public static void UnaryOp(
                    AcceleratorStream stream, ArrayView<int> view, Func<int, int> op)
                {
                    stream.Launch(view.IntExtent, index => view[index] = op(index));
                }
            }

            static class Callers
            {
                static void Run(AcceleratorStream stream, ArrayView<int> view) =>
                    Host.UnaryOp(stream, view, v => -v);
            }
            """,
            out var compilation);

        var diagnostic = Assert.Single(analysis.Diagnostics);
        Assert.Equal(KernelDiagnostic.VisibleLaunchingMethodId, diagnostic.Id);
        Assert.Contains("visible outside this assembly", diagnostic.Message);
        Assert.Contains(": error ILGPU1111: ", diagnostic.ToString());
        var location = diagnostic.Location;
        Assert.Equal(
            "UnaryOp",
            location.SourceTree!.GetText().ToString(location.SourceSpan));

        var executable = CompilationRewriter.Analyze(compilation, isLibrary: false);
        Assert.Empty(executable.Diagnostics);
        var dispatch = Assert.Single(executable.DispatchKernels);
        var capture = Assert.Single(dispatch.DelegateCaptures!.Value);
        Assert.Equal("op", capture.Parameter.Name);
    }

    /// <summary>
    /// Protected launching methods are only accessible through derived types.
    /// Types that other assemblies cannot derive from (sealed types and types
    /// without accessible constructors) do not make them visible, while
    /// protected methods of a public unsealed type are reported.
    /// </summary>
    [Fact]
    public void ProtectedLaunchingMethods_ReportedOnlyIfDerivable()
    {
        const string Method = """
                protected static void UnaryOp(
                    AcceleratorStream stream, ArrayView<int> view, Func<int, int> op)
                {
                    stream.Launch(view.IntExtent, index => view[index] = op(index));
                }

                static void Run(AcceleratorStream stream, ArrayView<int> view) =>
                    UnaryOp(stream, view, v => -v);
            """;
        var analysis = AnalyzeProgram($$"""
            public sealed class SealedHost
            {
                {{Method}}
            }

            public class InternalConstructorHost
            {
                internal InternalConstructorHost() { }

                {{Method}}
            }

            public class DerivableHost
            {
                {{Method}}
            }
            """);

        var diagnostic = Assert.Single(analysis.Diagnostics);
        Assert.Equal(KernelDiagnostic.VisibleLaunchingMethodId, diagnostic.Id);
        Assert.Contains("'DerivableHost.UnaryOp'", diagnostic.Message);
    }

    /// <summary>
    /// An internal launching method is visible outside the assembly when the
    /// assembly grants access to its internals, which is reported as well.
    /// Methods forwarding delegates to it are checked too.
    /// </summary>
    [Fact]
    public void InternalsVisibleToLaunchingMethod_Reports1111()
    {
        var analysis = AnalyzeProgram($$"""
            [assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Other")]

            {{UnaryOpHost}}

            static class Callers
            {
                static void Forward(
                    AcceleratorStream stream, ArrayView<int> view, Func<int, int> op) =>
                    Host.UnaryOp(stream, view, op);

                static void Run(AcceleratorStream stream, ArrayView<int> view) =>
                    Forward(stream, view, v => -v);
            }
            """);

        var diagnostic = Assert.Single(analysis.Diagnostics);
        Assert.Equal(KernelDiagnostic.VisibleLaunchingMethodId, diagnostic.Id);
        Assert.Contains("'Host.UnaryOp' captures", diagnostic.Message);
    }

    /// <summary>
    /// A launch called through a conditional access is not rewritten and
    /// would throw at runtime, so it is rejected.
    /// </summary>
    [Fact]
    public void ConditionalAccessLaunch_Reports1112()
    {
        var analysis = AnalyzeSource("""
            AcceleratorStream? maybe = stream;
            maybe?.Launch(view.IntExtent, index => view[index] = 3);
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.LaunchNotRewritableId, "maybe?.Launch");
        Assert.Contains("direct calls on a stream expression", diagnostic.Message);
        Assert.Empty(analysis.AllKernels);
    }

    /// <summary>
    /// A launch method converted to a delegate cannot be rewritten either.
    /// </summary>
    [Fact]
    public void LaunchMethodGroup_Reports1112()
    {
        var analysis = AnalyzeSource("""
            Action<Index1D, Action<Index1D>> launch = stream.Launch;
            """);

        AssertSingleDiagnostic(
            analysis, KernelDiagnostic.LaunchNotRewritableId, "stream.Launch");
    }

    /// <summary>
    /// Named arguments in a different order are mapped to their parameters.
    /// </summary>
    [Fact]
    public void NamedArgumentsLaunch_Extracted()
    {
        var analysis = AnalyzeSource("""
            stream.Launch(kernel: index => view[index] = 1, extent: (Index1D)4);
            """);

        Assert.Empty(analysis.Diagnostics);
        var launch = Assert.Single(analysis.AllKernels).LaunchInfo;
        Assert.Equal("(Index1D)4", launch.ExtentOrConfig.ToString());
        Assert.IsAssignableFrom<LambdaExpressionSyntax>(launch.KernelArgument);
    }

    /// <summary>
    /// A delegate variable passed as the kernel is rejected at the argument.
    /// </summary>
    [Fact]
    public void DelegateVariableKernel_Reports1113()
    {
        var analysis = AnalyzeSource("""
            Action<Index1D> kernel = index => view[index] = 1;
            stream.Launch((Index1D)4, kernel);
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.UnsupportedKernelArgumentId, "kernel");
        Assert.Contains("delegate variable 'kernel'", diagnostic.Message);
        Assert.Empty(analysis.AllKernels);
    }

    /// <summary>
    /// A conditional expression selecting the kernel is rejected.
    /// </summary>
    [Fact]
    public void ConditionalKernel_Reports1113()
    {
        var analysis = AnalyzeSource("""
            bool flag = view.Length > 2;
            stream.Launch(
                (Index1D)4,
                flag ? (Action<Index1D>)(i => view[i] = 1) : i => view[i] = 2);
            """);

        var diagnostic = Assert.Single(analysis.Diagnostics);
        Assert.Equal(KernelDiagnostic.UnsupportedKernelArgumentId, diagnostic.Id);
        Assert.Contains("a conditional expression", diagnostic.Message);
    }

    /// <summary>
    /// An anonymous method passed as the kernel is rejected with a hint to
    /// write it as a lambda expression.
    /// </summary>
    [Fact]
    public void AnonymousMethodKernel_Reports1113()
    {
        var analysis = AnalyzeSource("""
            stream.Launch((Index1D)4, delegate (Index1D index) { view[index] = 1; });
            """);

        var diagnostic = Assert.Single(analysis.Diagnostics);
        Assert.Equal(KernelDiagnostic.UnsupportedKernelArgumentId, diagnostic.Id);
        Assert.Contains("an anonymous method", diagnostic.Message);
        Assert.Contains("lambda expression instead", diagnostic.Message);
    }

    /// <summary>
    /// The struct with a delegate field shared by the tests below.
    /// </summary>
    private const string HolderTypes = """
        struct Holder { public Func<int, int> F; public int B; }

        struct Outer { public int X; public Holder Inner; }

        static class Kernels
        {
            public static void Apply(Index1D index, ArrayView<int> data, Holder h) =>
                data[index] = h.B;
        }
        """;

    /// <summary>
    /// Helper: analyzes the given statements in a method that can use
    /// <c>stream</c> and <c>view</c>, next to <see cref="HolderTypes"/>.
    /// </summary>
    private static AnalysisResult AnalyzeWithHolder(string statements) =>
        AnalyzeProgram($$"""
            {{HolderTypes}}

            static class Host
            {
                static void Run(AcceleratorStream stream, ArrayView<int> view)
                {
                    {{statements}}
                }
            }
            """);

    /// <summary>
    /// A launch lambda capturing a struct with a delegate field is rejected
    /// at the capture.
    /// </summary>
    [Fact]
    public void CapturedStructWithDelegateField_ReportsUnsupportedCapture()
    {
        var analysis = AnalyzeWithHolder("""
            var h = new Holder { F = x => x * 2, B = 1 };
            stream.Launch(
                view.IntExtent,
                index => { view[index] = h.F(view[index]) + h.B; });
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.UnsupportedCaptureId, "h");
        Assert.Contains("of type 'Holder', whose field 'F' is a delegate",
            diagnostic.Message);
    }

    /// <summary>
    /// Delegate fields of nested structs are found as well.
    /// </summary>
    [Fact]
    public void CapturedNestedStructWithDelegateField_ReportsUnsupportedCapture()
    {
        var analysis = AnalyzeWithHolder("""
            var o = new Outer { X = 2 };
            stream.Launch(view.IntExtent, index => view[index] = o.X);
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.UnsupportedCaptureId, "o");
        Assert.Contains("field 'Inner.F' is a delegate", diagnostic.Message);
    }

    /// <summary>
    /// A struct with a delegate field passed to a kernel method by a launch
    /// lambda is rejected at the argument.
    /// </summary>
    [Fact]
    public void StructWithDelegateFieldArgument_ReportsUnsupportedCapture()
    {
        var analysis = AnalyzeWithHolder("""
            var h = new Holder { F = x => x * 2, B = 1 };
            stream.Launch(view.IntExtent, index => Kernels.Apply(index, view, h));
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.UnsupportedCaptureId, "h");
        Assert.Contains("delegate field 'F'", diagnostic.Message);
    }

    /// <summary>
    /// A launch lambda whose single call passes a value computed from the
    /// lambda parameter is lifted as an inline kernel, as the value only
    /// exists inside the kernel.
    /// </summary>
    [Fact]
    public void LambdaParameterArgument_LiftedAsInlineKernel()
    {
        var analysis = AnalyzeProgram("""
            static class Kernels
            {
                public static void Write(ArrayView<int> data, int group) =>
                    data[group] = group;
            }

            static class Host
            {
                static void Run(AcceleratorStream stream, ArrayView<int> view)
                {
                    stream.Launch(
                        new KernelConfig(1, 4),
                        index => Kernels.Write(view, index.GroupIndex));
                }
            }
            """);

        Assert.Empty(analysis.Diagnostics);
        var kernel = Assert.Single(analysis.UniqueKernels);
        Assert.NotNull(kernel.LiftedKernel);
        Assert.Equal(["view"], kernel.Parameters.Select(p => p.Name));
    }

    /// <summary>
    /// Using an instance field captures <c>this</c>, which is rejected at
    /// the member access.
    /// </summary>
    [Fact]
    public void InstanceFieldUse_ReportsThisCapture()
    {
        var analysis = AnalyzeProgram("""
            sealed class Worker
            {
                private int _scale = 2;

                public void Run(AcceleratorStream stream, ArrayView<int> view)
                {
                    stream.Launch((Index1D)4, index => view[index] = index * _scale);
                }
            }
            """);

        AssertSingleDiagnostic(analysis, KernelDiagnostic.CapturesThisId, "_scale");
    }

    /// <summary>
    /// Primary-constructor parameters are stored on the instance, so using
    /// one inside an inline kernel captures <c>this</c>.
    /// </summary>
    [Fact]
    public void PrimaryConstructorParameterUse_ReportsThisCapture()
    {
        var analysis = AnalyzeProgram("""
            sealed class Worker(int scale)
            {
                public void Run(AcceleratorStream stream, ArrayView<int> view)
                {
                    stream.Launch((Index1D)4, index => view[index] = index * scale);
                }
            }
            """);

        AssertSingleDiagnostic(analysis, KernelDiagnostic.CapturesThisId, "scale");
    }

    /// <summary>
    /// An inline kernel in an instance method that does not touch the
    /// instance is accepted.
    /// </summary>
    [Fact]
    public void InstanceMethodWithoutInstanceUse_NoDiagnostics()
    {
        var analysis = AnalyzeProgram("""
            sealed class Worker(AcceleratorStream stream)
            {
                public void Run(ArrayView<int> view, int scale)
                {
                    stream.Launch((Index1D)4, index => view[index] = index * scale);
                }
            }
            """);

        Assert.Empty(analysis.Diagnostics);
        var kernel = Assert.Single(analysis.UniqueKernels);
        Assert.Equal(["view", "scale"], kernel.Parameters.Select(p => p.Name));
    }

    /// <summary>
    /// Inline kernels in generic methods are rejected at the lambda.
    /// </summary>
    [Fact]
    public void InlineKernelInGenericMethod_ReportsGenericContext()
    {
        var analysis = AnalyzeProgram("""
            static class Helpers
            {
                public static void Fill<T>(
                    AcceleratorStream stream, ArrayView<T> view, T value)
                    where T : unmanaged
                {
                    stream.Launch(view.IntExtent, index => view[index] = value);
                }
            }
            """);

        AssertSingleDiagnostic(
            analysis,
            KernelDiagnostic.GenericContextId,
            "index => view[index] = value");
    }

    /// <summary>
    /// A local function used as a kernel must not capture variables of its
    /// enclosing method.
    /// </summary>
    [Fact]
    public void CapturingLocalFunctionKernel_ReportsCapture()
    {
        var analysis = AnalyzeSource("""
            int offset = 1;
            void LocalKernel(Index1D index, ArrayView1D<int, Stride1D.Dense> data) =>
                data[index] = index + offset;
            stream.Launch((Index1D)4, index => LocalKernel(index, view));
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.LocalFunctionCapturesId, "offset");
        Assert.Contains("'LocalKernel'", diagnostic.Message);
    }

    /// <summary>
    /// A non-capturing local function used as a kernel is accepted.
    /// </summary>
    [Fact]
    public void StaticLocalFunctionKernel_NoDiagnostics()
    {
        var analysis = AnalyzeSource("""
            static void LocalKernel(
                Index1D index, ArrayView1D<int, Stride1D.Dense> data) =>
                data[index] = index;
            stream.Launch((Index1D)4, index => LocalKernel(index, view));
            """);

        Assert.Empty(analysis.Diagnostics);
    }

    /// <summary>
    /// An inline kernel calling a local function that captures a variable of
    /// the enclosing method is rejected at the call.
    /// </summary>
    [Fact]
    public void InlineKernelCallingCapturingLocalFunction_ReportsCapture()
    {
        var analysis = AnalyzeSource("""
            int factor = 3;
            int Scale(int x) => x * factor;
            int Twice(int x) => Scale(x) * 2;
            stream.Launch((Index1D)4, index => view[index] = Twice(index));
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.LocalFunctionCapturesId, "Twice(index)");
        Assert.Contains("'Twice'", diagnostic.Message);
        Assert.Contains("'factor'", diagnostic.Message);
    }

    /// <summary>
    /// A local function used as a kernel that captures a variable through a
    /// local function it calls is rejected at that call.
    /// </summary>
    [Fact]
    public void LocalFunctionKernelCapturingThroughCall_ReportsCapture()
    {
        var analysis = AnalyzeSource("""
            int factor = 3;
            int Scale(int x) => x * factor;
            void LocalKernel(Index1D index, ArrayView1D<int, Stride1D.Dense> data) =>
                data[index] = Scale(index);
            stream.Launch((Index1D)4, index => LocalKernel(index, view));
            """);

        var diagnostic = AssertSingleDiagnostic(
            analysis, KernelDiagnostic.LocalFunctionCapturesId, "Scale(index)");
        Assert.Contains("through the local function 'Scale'", diagnostic.Message);
    }

    /// <summary>
    /// A launch inside an expression is not rewritten and is rejected.
    /// </summary>
    [Fact]
    public void ExpressionBodiedLaunch_ReportsLaunchNotStatement()
    {
        var analysis = AnalyzeSource("""
            Action launch = () => stream.Launch((Index1D)4, index => view[index] = 1);
            launch();
            """);

        AssertSingleDiagnostic(
            analysis,
            KernelDiagnostic.LaunchNotStatementId,
            "stream.Launch((Index1D)4, index => view[index] = 1)");
    }

    /// <summary>
    /// Inline kernels calling non-static local functions that capture nothing
    /// and capturing variables named like C# keywords or like the leading
    /// launcher parameters are lifted into resolvable kernels.
    /// </summary>
    [Fact]
    public void LiftedKernels_CompileAndResolve()
    {
        var analysis = AnalyzeSource("""
            int Square(int x) => x * x;
            stream.Launch((Index1D)4, index => view[index] = Square(index));

            int @class = 2;
            int config = 3;
            int userExtent = 4;
            stream.Launch((Index1D)4, @event =>
                view[@event] = @class + config + userExtent);
            """,
            out var compilation);
        Assert.Empty(analysis.Diagnostics);
        Assert.Equal(2, analysis.UniqueKernels.Count);

        var rewritten = new CompilationRewriter().Rewrite(
            compilation, analysis, liftInlineKernels: true);
        using var stream = new MemoryStream();
        var emitResult = rewritten.Compilation.Emit(stream);
        Assert.True(
            emitResult.Success,
            string.Join("\n", emitResult.Diagnostics));

        var assembly = Assembly.Load(stream.ToArray());
        foreach (var kernel in analysis.UniqueKernels)
        {
            var method = MainKernelProvider.ResolveKernelMethod(assembly, kernel);
            Assert.NotNull(method);
            Assert.Equal(
                kernel.Parameters.Length + 1,
                method!.GetParameters().Length);
        }
    }

    /// <summary>
    /// Diagnostics are formatted in the canonical MSBuild error format with
    /// 1-based line and column numbers.
    /// </summary>
    [Fact]
    public void Diagnostic_FormatsAsMsBuildError()
    {
        var tree = CSharpSyntaxTree.ParseText(
            "class C { }", path: "/src/Program.cs");
        var diagnostic = new KernelDiagnostic(
            KernelDiagnostic.CapturesThisId,
            "Message.",
            Location.Create(tree, new TextSpan(6, 1)));

        Assert.Equal(
            "/src/Program.cs(1,7): error ILGPU1101: Message.",
            diagnostic.ToString());
    }
}
