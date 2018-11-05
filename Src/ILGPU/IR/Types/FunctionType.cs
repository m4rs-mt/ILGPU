// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: FunctionType.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using System;
using System.Collections.Immutable;

namespace ILGPU.IR.Types
{
    /// <summary>
    /// Represents a structure type.
    /// </summary>
    public sealed class FunctionType : ContainerType
    {
        #region Instance

        /// <summary>
        /// Constructs a new structure type.
        /// </summary>
        /// <param name="parameterTypes">The parameter types.</param>
        /// <param name="parameterNames">The parameter names.</param>
        /// <param name="source">The original source type (or null).</param>
        internal FunctionType(
            ImmutableArray<TypeNode> parameterTypes,
            ImmutableArray<string> parameterNames,
            Type source)
            : base(parameterTypes, parameterNames, source)
        {
            IsHigherOrder = false;
            foreach (var param in parameterTypes)
                IsHigherOrder |= param.IsFunctionType;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns if this is function type with at least
        /// one function parameter.
        /// </summary>
        public bool IsHigherOrder { get; }

        #endregion

        #region Methods

        /// <summary cref="TypeNode.Accept{T}(T)"/>
        public override void Accept<T>(T visitor) => visitor.Visit(this);

        /// <summary cref="ContainerType.Rebuild(IRBuilder, IRTypeRebuilder, ImmutableArray{TypeNode})"/>
        protected override TypeNode Rebuild(
            IRBuilder builder,
            IRTypeRebuilder rebuilder,
            ImmutableArray<TypeNode> children) =>
            builder.CreateFunctionType(children, Names, Source);

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => "Fn";

        /// <summary cref="TypeNode.GetHashCode"/>
        public override int GetHashCode() =>
            base.GetHashCode() ^ 0x34BFD2A1;

        /// <summary cref="TypeNode.Equals(object)"/>
        public override bool Equals(object obj) =>
            obj is FunctionType && base.Equals(obj);

        #endregion
    }
}
