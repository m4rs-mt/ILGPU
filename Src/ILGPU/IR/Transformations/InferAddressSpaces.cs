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
using ILGPU.IR.Types;
using ILGPU.IR.Values;

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
        private static bool IsRedundant(AddressSpaceCast cast) =>
            IsRedundantRecursive(cast.TargetAddressSpace, cast);

        /// <summary>
        /// Returns true if the parent cast is redundant.
        /// </summary>
        /// <param name="targetSpace">The target address space.</param>
        /// <param name="value">The current value to check.</param>
        /// <returns>True, if the parent cast is redundant.</returns>
        private static bool IsRedundantRecursive(
            MemoryAddressSpace targetSpace,
            Value value)
        {
            foreach (var use in value.Uses)
            {
                var node = use.Resolve();
                switch (node)
                {
                    case MethodCall call:
                        // We cannot remove casts to other address spaces in case of a
                        // method invocation if the address spaces do not match
                        if (!call.Target.HasImplementation)
                            break;
                        var targetParam = call.Target.Parameters[use.Index];
                        if (targetParam.ParameterType is IAddressSpaceType paramType &&
                            paramType.AddressSpace == targetSpace)
                        {
                            return false;
                        }

                        break;
                    case PhiValue _:
                        // We are not allowed to remove casts in the case of phi values
                        return false;
                    case Store _:
                        // We are not allowed to remove casts in the case of alloca
                        // stores
                        if (use.Index != 0)
                            return false;
                        break;
                    case StructureValue _:
                    case SetField _:
                        // We are not allowed to remove field or array stores to tuples
                        // with different field types
                        return false;
                    case PointerCast _:
                    case LoadElementAddress _:
                    case LoadFieldAddress _:
                        if (!IsRedundantRecursive(targetSpace, node))
                            return false;
                        break;
                }
            }
            return true;
        }

        /// <summary>
        /// Invalidates the type of an affected value.
        /// </summary>
        private static void InvalidateType<TValue>(
            RewriterContext context,
            TValue value)
            where TValue : Value =>
            value.InvalidateType();

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

            // Invalidate types of affected values
            Rewriter.Add<PointerCast>(InvalidateType);
            Rewriter.Add<LoadFieldAddress>(InvalidateType);
            Rewriter.Add<LoadElementAddress>(InvalidateType);
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
            Rewriter.Rewrite(builder.SourceBlocks, builder);

        #endregion
    }
}
