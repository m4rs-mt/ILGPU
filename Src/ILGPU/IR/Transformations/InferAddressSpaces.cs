// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: InferAddressSpaces.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Rewriting;
using ILGPU.IR.Values;
using System.Diagnostics;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Infers address spaces by removing unnecessary address-space casts.
    /// </summary>
    public sealed class InferAddressSpaces : UnorderedTransformation
    {
        #region Static

        /// <summary>
        /// Returns true if the given cast is redundant.
        /// </summary>
        /// <param name="cast">The cast to check.</param>
        /// <returns>True, if the given cast is redundant.</returns>
        private static bool IsRedundant(AddressSpaceCast cast)
        {
            Debug.Assert(cast != null, "Invalid cast");
            foreach (var use in cast.Uses)
            {
                var node = use.Resolve();

                switch (node)
                {
                    case MethodCall _:
                        // We cannot remove casts to other address spaces in case
                        // of a method invocation.
                        return false;
                    case PhiValue _:
                        // We are not allowed to remove casts from phi node operands.
                        return false;
                    case Store _:
                        // We are not allowed to remove casts in the case of alloca
                        // stores
                        if (use.Index != 0)
                            return false;
                        break;
                    case SetArrayElement _:
                        return false;
                    case SetField setField:
                        // We are not allowed to remove field stores to tuples
                        // with different field types
                        var structureType = setField.StructureType;
                        if (structureType[setField.FieldSpan.Access] != cast.SourceType)
                            return false;
                        break;
                }
            }
            return true;
        }

        #endregion

        #region Rewriter

        /// <summary>
        /// The internal rewriter.
        /// </summary>
        private static readonly Rewriter Rewriter = new Rewriter();

        /// <summary>
        /// Registers all conversion patterns.
        /// </summary>
        static InferAddressSpaces()
        {
            // Rewrites address space casts that are not required.
            Rewriter.Add<AddressSpaceCast>(
                IsRedundant,
                (context, cast) => context.ReplaceAndRemove(cast, cast.Value));
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new address-space inference pass.
        /// </summary>
        public InferAddressSpaces() { }

        #endregion

        #region Methods

        /// <summary>
        /// Applies the address-space inference transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder) =>
            Rewriter.Rewrite(builder);

        #endregion
    }
}
