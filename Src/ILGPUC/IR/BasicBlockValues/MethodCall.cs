// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: MethodCall.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.IR.BasicBlockValues.Construction;
using ILGPUC.IR.ModuleValues;
using System;
using System.Runtime.CompilerServices;

namespace ILGPUC.IR.BasicBlockValues;

/// <summary>
/// Represents a single function call of the form
/// x = f(a0, ..., an-1) or f(a0, ..., an-1)
/// </summary>
sealed partial class MethodCall : MemoryValue
{
    /// <summary>
    /// An instance builder for method calls.
    /// </summary>
    /// <param name="bbBuilder">The current IR builder.</param>
    /// <param name="location">The current location.</param>
    /// <param name="target">The target method to call.</param>
    internal struct Builder(BasicBlockBuilder bbBuilder, Location location, Method target)
    {
        private ValueBuilderList _builder = ValueBuilderList.Create(
            bbBuilder.Generation,
            target.NumParameters);

        /// <summary>
        /// Returns the current location.
        /// </summary>
        public Location Location { get; } = location;

        /// <summary>
        /// The number of arguments.
        /// </summary>
        public readonly int Count => _builder.Count;

        /// <summary>
        /// Adds the given value to the call builder.
        /// </summary>
        /// <param name="value">The value to add.</param>
        public void Add(Value? value) => _builder.Add(value);

        /// <summary>
        /// Constructs a new value that represents the current method call.
        /// </summary>
        /// <returns>The resulting value reference.</returns>
        public MethodCall Seal() =>
            bbBuilder.CreateCall(Location, target, ref _builder);
    }

    /// <summary>
    /// Constructs a new call.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="target">The jump target.</param>
    /// <param name="arguments">The arguments of the jump target.</param>
    public MethodCall(
        in BasicBlockValueInitializer initializer,
        Method target,
        ref ValueBuilderList arguments)
        : base(initializer, target.Type)
    {
        arguments.AddFront(target);
        Seal(ref arguments);
    }

    /// <summary>
    /// Returns the call target.
    /// </summary>
    public Method Target => GetValue<Method>(0);

    /// <summary>
    /// Returns all call arguments.
    /// </summary>
    public ReadOnlySpan<Value> Arguments => Values.Length > 1 ? Values[1..] : [];

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    public override Value? Rewrite<TRewriter>(in TRewriter rewriter)
    {
        var newTarget = rewriter.Rewrite(Target);
        if (newTarget is not Method method) return newTarget;

        var arguments = ValueBuilderList.Create(
            newTarget.Generation,
            method.NumParameters);
        foreach (var argument in Arguments)
            arguments.Add(rewriter.Rewrite(argument));

        // Rewrite the actual method call
        return rewriter.Builder.CreateCall(Location, method, ref arguments);
    }

    /// <summary cref="Value.ToPrefixString"/>
    protected override string ToPrefixString() => "call";

    /// <inheritdoc cref="Value.ToArgString()"/>
    protected override string ToArgString() =>
        $"{Target.ToReferenceString()}{ToArgString(offset: 1)}";
}
