// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: InferAddressSpaces.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Construction;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Diagnostics;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Infers address spaces by removing unnecessary address-space casts.
    /// </summary>
    public sealed class InferAddressSpaces : UnorderedTransformation
    {
        /// <summary>
        /// The desired transformations that should run after
        /// applying this transformation.
        /// </summary>
        private const TransformationFlags FollowUpFlags = TransformationFlags.TransformToCPS;

        /// <summary>
        /// Constructs a new address-space inference pass.
        /// </summary>
        public InferAddressSpaces()
            : base(TransformationFlags.InferAddressSpaces, FollowUpFlags)
        { }

        /// <summary cref="UnorderedTransformation.PerformTransformation(IRBuilder, TopLevelFunction)"/>
        protected override bool PerformTransformation(
            IRBuilder builder,
            TopLevelFunction topLevelFunction)
        {
            var scope = Scope.Create(builder, topLevelFunction);
            var castsToRemove = new List<AddressSpaceCast>(scope.Count >> 1);

            foreach (var node in scope)
            {
                if (node is AddressSpaceCast cast && IsRedundant(scope, cast))
                    castsToRemove.Add(cast);
            }

            if (castsToRemove.Count < 1)
                return false;

            foreach (var cast in castsToRemove)
                cast.Replace(cast.Value);

            return true;
        }

        /// <summary>
        /// Returns true iff the given cast is redundant.
        /// </summary>
        /// <param name="scope">The current scope.</param>
        /// <param name="cast">The cast to check.</param>
        /// <returns>True, iff the given cast is redundant.</returns>
        private static bool IsRedundant(Scope scope, AddressSpaceCast cast)
        {
            Debug.Assert(cast != null, "Invalid cast");
            foreach (var use in scope.GetUses(cast))
            {
                var node = use.Resolve();

                switch (node)
                {
                    case FunctionCall _:
                        // A function call implies an implicit phi node.
                        // We are not allowed to remove this cast in such a case.
                        return false;
                    case Store _:
                        // We are not allowed to remove casts in the case of
                        // alloca stores
                        if (use.Index != 1)
                            return false;
                        break;
                    case SetField setField:
                        // We are not allowed to remove field stores to tuples
                        // with different field types
                        if (setField.StructureType.Children[setField.FieldIndex] != cast.SourceType)
                            return false;
                        break;
                }
            }
            return true;
        }
    }
}
