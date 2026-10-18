// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2026 ILGPU Project
//                                    www.ilgpu.net
//
// File: AssertionRecorder.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Records the <see cref="Debug.Assert(bool)"/> failures of the compiler code that runs
/// in the current asynchronous flow while the recorder is active. The test host does not
/// fail on assertions, so tests use it to check that compiling a kernel trips none.
/// </summary>
sealed class AssertionRecorder : IDisposable
{
    /// <summary>
    /// Forwards assertion failures to the recorder of the current flow, if any.
    /// </summary>
    private sealed class Listener : TraceListener
    {
        public override void Fail(string? message, string? detailMessage) =>
            s_current.Value?._failures.Add(
                string.IsNullOrEmpty(detailMessage)
                    ? message ?? string.Empty
                    : $"{message}: {detailMessage}");

        public override void Write(string? message) { }

        public override void WriteLine(string? message) { }
    }

    private static readonly AsyncLocal<AssertionRecorder?> s_current = new();
    private static readonly Lazy<Listener> s_listener = new(() =>
    {
        // First in line: the test host's own listener may throw on a failure,
        // which the compiler can catch before later listeners see it
        var listener = new Listener();
        Trace.Listeners.Insert(0, listener);
        return listener;
    });

    private readonly List<string> _failures = [];
    private readonly AssertionRecorder? _previous;

    private AssertionRecorder()
    {
        _ = s_listener.Value;
        _previous = s_current.Value;
        s_current.Value = this;
    }

    /// <summary>
    /// Starts recording the assertion failures of the current flow.
    /// </summary>
    public static AssertionRecorder Start() => new();

    /// <summary>
    /// The messages of the recorded assertion failures.
    /// </summary>
    public IReadOnlyList<string> Failures => _failures;

    /// <summary>
    /// Runs <paramref name="compile"/> and fails the current test if it tripped a
    /// compiler assertion, also when the compiler recovered from it.
    /// </summary>
    public static T Run<T>(Func<T> compile)
    {
        using var recorder = Start();
        var result = compile();
        recorder.AssertNoFailures();
        return result;
    }

    /// <summary>
    /// Runs <paramref name="compile"/> and fails the current test if it tripped a
    /// compiler assertion, also when the compiler recovered from it.
    /// </summary>
    public static async Task<T> RunAsync<T>(Func<Task<T>> compile)
    {
        using var recorder = Start();
        var result = await compile().ConfigureAwait(false);
        recorder.AssertNoFailures();
        return result;
    }

    /// <summary>
    /// Stops recording and fails the current test if an assertion failed.
    /// </summary>
    public void AssertNoFailures()
    {
        Dispose();
        if (_failures.Count > 0)
        {
            Assert.Fail(
                "Compiler assertions failed:" + Environment.NewLine +
                string.Join(Environment.NewLine, _failures));
        }
    }

    /// <summary>
    /// Stops recording.
    /// </summary>
    public void Dispose() => s_current.Value = _previous;
}
