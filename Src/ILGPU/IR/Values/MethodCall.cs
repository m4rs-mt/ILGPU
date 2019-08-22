// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: Call.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Text;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a single function call of the form
    /// x = f(a0, ..., an-1) or f(a0, ..., an-1)
    /// </summary>
    public sealed class MethodCall : Value
    {
        #region Static

        /// <summary>
        /// Computes a method call node type.
        /// </summary>
        /// <param name="target">The called target method.</param>
        /// <returns>The resolved type node.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static TypeNode ComputeType(Method target) =>
            target.ReturnType;

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new call.
        /// </summary>
        /// <param name="basicBlock">The parent basic block.</param>
        /// <param name="target">The jump target.</param>
        /// <param name="arguments">The arguments of the jump target.</param>
        internal MethodCall(
            BasicBlock basicBlock,
            Method target,
            ImmutableArray<ValueReference> arguments)
            : base(ValueKind.MethodCall, basicBlock, ComputeType(target))
        {
            Target = target;
            Seal(arguments);
        }

        #endregion

        #region Properties

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

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) =>
            ComputeType(Target);

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder)
        {
            var args = ImmutableArray.CreateBuilder<ValueReference>(Nodes.Length);
            foreach (var arg in Nodes)
                args.Add(rebuilder.Rebuild(arg));
            var target = rebuilder.LookupCallTarget(Target);
            return builder.CreateCall(target, args.ToImmutable());
        }

        /// <summary cref="Value.Accept"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "call";

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString()
        {
            var result = new StringBuilder();
            result.Append(Target.ToReferenceString());
            result.Append('(');
            for (int i = 0, e = NumArguments; i < e; ++i)
            {
                result.Append(this[i].ToString());
                if (i + 1 < e)
                    result.Append(", ");
            }
            result.Append(')');
            return result.ToString();
        }

        #endregion
    }
}
