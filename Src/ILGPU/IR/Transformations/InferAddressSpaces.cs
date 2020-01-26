// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: InferAddressSpaces.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System.Diagnostics;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Infers address spaces by removing unnecessary address-space casts.
    /// </summary>
    public sealed class InferAddressSpaces : UnorderedTransformation
    {
        /// <summary>
        /// Constructs a new address-space inference pass.
        /// </summary>
        public InferAddressSpaces() { }

        /// <summary cref="UnorderedTransformation.PerformTransformation(Method.Builder)"/>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            var scope = builder.CreateScope();

            bool result = false;
            foreach (var block in scope)
            {
                var blockBuilder = builder[block];

                foreach (var valueEntry in block)
                {
                    if (valueEntry.Value is AddressSpaceCast cast && IsRedundant(cast))
                    {
                        cast.Replace(cast.Value);
                        blockBuilder.Remove(cast);
                        result = true;
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Returns true iff the given cast is redundant.
        /// </summary>
        /// <param name="cast">The cast to check.</param>
        /// <returns>True, iff the given cast is redundant.</returns>
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
                        // We are not allowed to remove casts in the case of
                        // alloca stores
                        if (use.Index != 0)
                            return false;
                        break;
                    case SetField setField:
                        // We are not allowed to remove field stores to tuples
                        // with different field types
                        if (setField.StructureType.Fields[setField.FieldIndex] != cast.SourceType)
                            return false;
                        break;
                }
            }
            return true;
        }
    }
}
