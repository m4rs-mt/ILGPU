// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2018-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: MethodCall.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Serialization;
using ILGPU.IR.Types;
using System.Runtime.CompilerServices;
using ValueList = ILGPU.Util.InlineList<ILGPU.IR.Values.ValueReference>;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a single function call of the form
    /// x = f(a0, ..., an-1) or f(a0, ..., an-1)
    /// </summary>
    [ValueKind(ValueKind.MethodCall)]
    public sealed class MethodCall : MemoryValue
    {
        #region Nested Types

        /// <summary>
        /// An instance builder for method calls.
        /// </summary>
        public struct Builder
        {
            #region Instance

            private ValueList builder;

            /// <summary>
            /// Initializes a new call builder.
            /// </summary>
            /// <param name="irBuilder">The current IR builder.</param>
            /// <param name="location">The current location.</param>
            /// <param name="target">The target method to call.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
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

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.MethodCall;

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

        /// <summary cref="Value.Write{T}(T)"/>
        protected internal override void Write<T>(T writer) =>
            writer.Write(nameof(Target), Target.Id);

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "call";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString() =>
            Target.ToReferenceString() + base.ToArgString();

        #endregion
    }
}
