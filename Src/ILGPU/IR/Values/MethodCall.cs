// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Call.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System.Collections.Immutable;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a single function call of the form
    /// x = f(a0, ..., an-1) or f(a0, ..., an-1)
    /// </summary>
    [ValueKind(ValueKind.MethodCall)]
    public sealed class MethodCall : Value
    {
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
            ImmutableArray<ValueReference> arguments)
            : base(initializer)
        {
            Target = target;
            Seal(arguments);
        }

        #endregion

        #region Properties

        /// <summary cref="Value.ValueKind"/>
        public override ValueKind ValueKind => ValueKind.MethodCall;

        /// <summary>
        /// Returns the call target.
        /// </summary>
        public Method Target { get; }

        /// <summary>
        /// Returns the number of the associated arguments.
        /// </summary>
        public int NumArguments => Nodes.Length;

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
            var args = ImmutableArray.CreateBuilder<ValueReference>(Nodes.Length);
            foreach (var arg in Nodes)
                args.Add(rebuilder.Rebuild(arg));
            var target = rebuilder.LookupCallTarget(Target);
            return builder.CreateCall(
                Location,
                target,
                args.ToImmutable());
        }

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
