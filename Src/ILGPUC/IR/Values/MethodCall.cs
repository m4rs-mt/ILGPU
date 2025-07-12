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

using ILGPUC.IR.Construction;
using ILGPUC.IR.Types;
using ValueList = ILGPU.Util.InlineList<ILGPUC.IR.Values.ValueReference>;

namespace ILGPUC.IR.Values;

/// <summary>
/// Represents a single function call of the form
/// x = f(a0, ..., an-1) or f(a0, ..., an-1)
/// </summary>
sealed partial class MethodCall : MemoryValue
{
    #region Nested Types

    /// <summary>
    /// An instance builder for method calls.
    /// </summary>
    internal struct Builder
    {
        #region Instance

        private ValueList builder;

        /// <summary>
        /// Initializes a new call builder.
        /// </summary>
        /// <param name="irBuilder">The current IR builder.</param>
        /// <param name="location">The current location.</param>
        /// <param name="target">The target method to call.</param>
        internal Builder(IRBuilder irBuilder, Location location, Method target)
        {
            builder = ValueList.Create(target.NumParameters);
            IRBuilder = irBuilder;
            Location = location;
            Target = target;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent builder.
        /// </summary>
        public IRBuilder IRBuilder { get; }

        /// <summary>
        /// Returns the current location.
        /// </summary>
        public Location Location { get; }

        /// <summary>
        /// Returns the call target.
        /// </summary>
        public Method Target { get; }

        /// <summary>
        /// The number of arguments.
        /// </summary>
        public int Count => builder.Count;

        #endregion

        #region Methods

        /// <summary>
        /// Adds the given value to the call builder.
        /// </summary>
        /// <param name="value">The value to add.</param>
        public void Add(Value value)
        {
            IRBuilder.AssertNotNull(value);
            builder.Add(value);
        }

        /// <summary>
        /// Constructs a new value that represents the current method call.
        /// </summary>
        /// <returns>The resulting value reference.</returns>
        public MethodCall Seal() =>
            IRBuilder.CreateCall(Location, Target, ref builder);

        #endregion
    }

    #endregion

    #region Instance

    /// <summary>
    /// Constructs a new call.
    /// </summary>
    /// <param name="initializer">The value initializer.</param>
    /// <param name="target">The jump target.</param>
    /// <param name="arguments">The arguments of the jump target.</param>
    internal MethodCall(
        in ValueInitializer initializer,
        Method target,
        ref ValueList arguments)
        : base(initializer)
    {
        Target = target;
        Seal(ref arguments);
    }

    #endregion

    #region Properties

    /// <summary>
    /// Returns the call target.
    /// </summary>
    public Method Target { get; }

    #endregion

    #region Methods

    /// <summary cref="Value.ComputeType(in ValueInitializer)"/>
    protected override TypeNode ComputeType(in ValueInitializer initializer) =>
        Target.ReturnType;

    /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
    protected internal override Value Rebuild(
        IRBuilder builder,
        IRRebuilder rebuilder)
    {
        var target = rebuilder.LookupCallTarget(Target);
        var call = builder.CreateCall(Location, target);
        foreach (var arg in Nodes)
            call.Add(rebuilder.Rebuild(arg));
        return call.Seal();
    }

    #endregion

    #region Object

    /// <summary cref="Node.ToPrefixString"/>
    protected override string ToPrefixString() => "call";

    /// <summary cref="Value.ToArgString"/>
    protected override string ToArgString() =>
        Target.ToReferenceString() + base.ToArgString();

    #endregion
}
