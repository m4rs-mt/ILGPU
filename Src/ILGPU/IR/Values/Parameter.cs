// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: Parameter.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Types;
using System.Collections.Immutable;

namespace ILGPU.IR.Values
{
    /// <summary>
    /// Represents a function parameter.
    /// </summary>
    /// <remarks>Note that parameters have not associated basic block.</remarks>
    public sealed class Parameter : Value
    {
        #region Instance

        /// <summary>
        /// Constructs a new parameter.
        /// </summary>
        /// <param name="method">The parent method.</param>
        /// <param name="type">The parameter type.</param>
        /// <param name="name">The parameter name (for debugging purposes).</param>
        internal Parameter(
            Method method,
            TypeNode type,
            string name)
            : base(ValueKind.Parameter, null, type, false)
        {
            Method = method;
            ParameterType = type;
            Name = name ?? "param";
            Seal(ImmutableArray<ValueReference>.Empty);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the actual parameter type.
        /// </summary>
        public TypeNode ParameterType { get; }

        /// <summary>
        /// Returns the parameter name (for debugging purposes).
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Returns the parameter index.
        /// </summary>
        public int Index { get; internal set; } = -1;

        #endregion

        #region Methods

        /// <summary cref="Value.UpdateType(IRContext)"/>
        protected override TypeNode UpdateType(IRContext context) => ParameterType;

        /// <summary cref="Value.Rebuild(IRBuilder, IRRebuilder)"/>
        protected internal override Value Rebuild(IRBuilder builder, IRRebuilder rebuilder)
        {
            // Params have already been mapped in the beginning
            return rebuilder.Rebuild(this);
        }

        /// <summary cref="Value.Accept" />
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => Name;

        /// <summary cref="Value.ToArgString"/>
        protected override string ToArgString()
        {
            return Type.ToString() + " @ " + Method.ToReferenceString();
        }

        /// <summary>
        /// Return the parameter string.
        /// </summary>
        /// <returns>The parameter string.</returns>
        internal string ToParameterString() =>
            $"{Type.ToString()} {ToReferenceString()}";

        #endregion
    }
}
