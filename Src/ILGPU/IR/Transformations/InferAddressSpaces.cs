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

using ILGPU.IR.Analyses;
using ILGPU.IR.Rewriting;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Generic;
using static ILGPU.IR.Analyses.PointerAddressSpaces;
using static ILGPU.IR.Types.AddressSpaceType;
using AnalysisResult = ILGPU.IR.Analyses.GlobalAnalysisValueResult<
    ILGPU.IR.Analyses.PointerAddressSpaces.AddressSpaceInfo>;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Infers address spaces by removing unnecessary address-space casts.
    /// </summary>
    /// <remarks>
    /// This transformation is a light-weight address-space inference pass that uses
    /// trivial conditions to remove unnecessary casts. Use
    /// <see cref="InferLocalAddressSpaces"/> or <see cref="InferKernelAddressSpaces"/>
    /// for better results.
    /// </remarks>
    public sealed class InferAddressSpaces : UnorderedTransformation
    {
        #region Nested Types

        /// <summary>
        /// Represents a provider for address-space information.
        /// </summary>
        public interface IAddressSpaceProvider
        {
            /// <summary>
            /// Returns the determined address space of the given value.
            /// </summary>
            /// <param name="value">The value to get the address space for.</param>
            /// <returns>The determined address space.</returns>
            MemoryAddressSpace this[Value value] { get; }
        }

        /// <summary>
        /// Represents the default implementation of the interface
        /// <see cref="IAddressSpaceProvider"/>.
        /// </summary>
        public readonly struct DataProvider : IAddressSpaceProvider
        {
            /// <summary>
            /// Returns the target address space of the underling type.
            /// </summary>
            public readonly MemoryAddressSpace this[Value value] =>
                value.Type is IAddressSpaceType type
                ? type.AddressSpace
                : MemoryAddressSpace.Generic;
        }

        /// <summary>
        /// Represents a data wrapper that represents processing data required for the
        /// internal rewriter implementation.
        /// </summary>
        /// <typeparam name="TProvider">The address-space provider type.</typeparam>
        public readonly struct ProcessingData<TProvider>
            where TProvider : IAddressSpaceProvider
        {
            #region Instance

            /// <summary>
            /// Constructs a new processing-data instance.
            /// </summary>
            internal ProcessingData(TProvider provider)
            {
                Provider = provider;
                ToProcess = new Stack<Value>(10);
            }

            #endregion

            #region Properties

            /// <summary>
            /// Return the current address-space provider to use.
            /// </summary>
            public TProvider Provider { get; }

            /// <summary>
            /// Returns the current processing stack reference.
            /// </summary>
            private Stack<Value> ToProcess { get; }

            /// <summary>
            /// Returns the determined address space of the given value.
            /// </summary>
            /// <param name="value">The value to check.</param>
            /// <returns>The determined address space.</returns>
            public readonly MemoryAddressSpace this[Value value] =>
                Provider[value];

            #endregion

            #region Methods

            /// <summary>
            /// Pushes the given value onto the processing stack.
            /// </summary>
            public readonly void Push(Value value) => ToProcess.Push(value);

            /// <summary>
            /// Tries to pop a value from the current processing stack.
            /// </summary>
            public readonly bool TryPop(out Value value)
            {
                value = default;
                if (ToProcess.Count < 1)
                    return false;
                value = ToProcess.Pop();
                return true;
            }

            /// <summary>
            /// Clears the current processing stack.
            /// </summary>
            public readonly void Clear() => ToProcess.Clear();

            #endregion
        }

        #endregion

        #region Static

        /// <summary>
        /// Creates a new <see cref="ProcessingData{TProvider}"/> instance.
        /// </summary>
        /// <typeparam name="TProvider">The provider type.</typeparam>
        /// <param name="provider">The provider instance.</param>
        /// <returns>The processing data instance.</returns>
        public static ProcessingData<TProvider> CreateProcessingData<TProvider>(
            TProvider provider)
            where TProvider : IAddressSpaceProvider =>
            new ProcessingData<TProvider>(provider);

        /// <summary>
        /// Returns true if the given cast is redundant.
        /// </summary>
        /// <param name="data">The current processing data.</param>
        /// <param name="cast">The cast to check.</param>
        /// <returns>True, if the given cast is redundant.</returns>
        private static bool IsRedundantCast<TProvider>(
            in ProcessingData<TProvider> data,
            AddressSpaceCast cast)
            where TProvider : IAddressSpaceProvider
        {
            // Check for trivial situations which can occur due to compiler optimizations
            if (cast.TargetAddressSpace == cast.SourceType.AddressSpace)
                return true;

            // Initialize the processing loop
            data.Clear();
            data.Push(cast);

            // Check all uses recursively
            while (data.TryPop(out var value))
            {
                foreach (var use in value.Uses)
                {
                    // If we cannot remove this cast, return false
                    if (!IsRedundantCastUse(data, use, cast.TargetAddressSpace))
                        return false;
                }
            }

            // All uses are compatible
            return true;
        }

        /// <summary>
        /// Returns true if the parent cast is redundant.
        /// </summary>
        /// <param name="data">The current processing data.</param>
        /// <param name="targetSpace">The target address space.</param>
        /// <param name="use">The current use to check.</param>
        /// <returns>True, if the parent cast is redundant.</returns>
        private static bool IsRedundantCastUse<TProvider>(
            in ProcessingData<TProvider> data,
            Use use,
            MemoryAddressSpace targetSpace)
            where TProvider : IAddressSpaceProvider
        {
            var value = use.Resolve();
            switch (value)
            {
                case MethodCall call:
                    // We cannot remove casts to other address spaces in case of a
                    // method invocation if the address spaces do not match
                    if (!call.Target.HasImplementation)
                        break;
                    var targetParam = call.Target.Parameters[use.Index];
                    if (targetParam.Type is IAddressSpaceType &&
                        data[targetParam] == targetSpace)
                    {
                        return false;
                    }
                    break;
                case PhiValue _:
                case Predicate _:
                case ReturnTerminator _:
                    // We are not allowed to remove casts in the case of phi values,
                    // predicates and returns
                    if (value.Type is IAddressSpaceType && data[value] == targetSpace)
                        return false;
                    break;
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
                case SubViewValue _:
                case PointerCast _:
                case LoadElementAddress _:
                case LoadFieldAddress _:
                    data.Push(value);
                    break;
            }
            return true;
        }

        /// <summary>
        /// Rewrites address-space casts.
        /// </summary>
        private static void Rewrite<TProvider>(
            RewriterContext context,
            ProcessingData<TProvider> data,
            AddressSpaceCast cast)
            where TProvider : IAddressSpaceProvider
        {
            if (!IsRedundantCast(data, cast))
                return;

            // We can safely remove this cast without introducing a new one
            context.ReplaceAndRemove(cast, cast.Value);
        }

        /// <summary>
        /// Returns true if the given value has an address-space type and can be updated
        /// using analysis information.
        /// </summary>
        private static bool CanRewrite<TValue, TProvider>(
            ProcessingData<TProvider> data,
            TValue value)
            where TValue : Value
            where TProvider : IAddressSpaceProvider =>
            value.Type is IAddressSpaceType type &&
            type.AddressSpace != data[value];

        /// <summary>
        /// Rewrites phi values.
        /// </summary>
        private static void Rewrite<TProvider>(
            RewriterContext context,
            ProcessingData<TProvider> data,
            PhiValue phiValue)
            where TProvider : IAddressSpaceProvider
        {
            var builder = context.Builder;
            var location = phiValue.Location;
            var targetAddressSpace = data[phiValue];

            // Create a new target type
            var targetType = builder.SpecializeAddressSpaceType(
                phiValue.Type.As<AddressSpaceType>(location),
                targetAddressSpace);
            var phiBuilder = builder.CreatePhi(location, targetType, phiValue.Count);

            // Convert all phi values
            for (int i = 0, e = phiValue.Count; i < e; ++i)
            {
                var argument = builder.CreateAddressSpaceCast(
                    location,
                    phiValue.Nodes[i],
                    targetAddressSpace);
                phiBuilder.AddArgument(phiValue.Sources[i], argument);
            }
            context.ReplaceAndRemove(phiValue, phiBuilder.Seal());
        }

        /// <summary>
        /// Rewrites predicates.
        /// </summary>
        private static void Rewrite<TProvider>(
            RewriterContext context,
            ProcessingData<TProvider> data,
            Predicate predicate)
            where TProvider : IAddressSpaceProvider
        {
            var builder = context.Builder;
            var location = predicate.Location;
            var targetAddressSpace = data[predicate];

            // Convert the true and false values
            var trueValue = builder.CreateAddressSpaceCast(
                location,
                predicate.TrueValue,
                targetAddressSpace);
            var falseValue = builder.CreateAddressSpaceCast(
                location,
                predicate.FalseValue,
                targetAddressSpace);

            // Build the converted predicate and replace the old one
            var newPredicate = builder.CreatePredicate(
                location,
                predicate.Condition,
                trueValue,
                falseValue);
            context.ReplaceAndRemove(predicate, newPredicate);
        }

        /// <summary>
        /// Invalidates the type of an affected value.
        /// </summary>
        private static void InvalidateType<TValue, TProvider>(
            RewriterContext context,
            ProcessingData<TProvider> provider,
            TValue value)
            where TValue : Value
            where TProvider : IAddressSpaceProvider =>
            value.InvalidateType();

        #endregion

        #region Rewriter

        /// <summary>
        /// The internal rewriter.
        /// </summary>
        private static readonly Rewriter<ProcessingData<DataProvider>> Rewriter =
            new Rewriter<ProcessingData<DataProvider>>();

        /// <summary>
        /// Registers all conversion patterns.
        /// </summary>
        static InferAddressSpaces()
        {
            AddRewriters(Rewriter);
        }

        /// <summary>
        /// Adds all internal rewriters to the given rewriter instance.
        /// </summary>
        /// <typeparam name="TProvider">The provider type.</typeparam>
        /// <param name="rewriter">The target rewriter instance.</param>
        public static void AddRewriters<TProvider>(
            Rewriter<ProcessingData<TProvider>> rewriter)
            where TProvider : IAddressSpaceProvider
        {
            // Rewrites address space casts that are not required
            rewriter.Add<AddressSpaceCast>(Rewrite);
            rewriter.Add<PhiValue>(CanRewrite, Rewrite);
            rewriter.Add<Predicate>(CanRewrite, Rewrite);

            // Invalidate types of affected values
            rewriter.Add<PointerCast>(InvalidateType);
            rewriter.Add<LoadFieldAddress>(InvalidateType);
            rewriter.Add<LoadElementAddress>(InvalidateType);
            rewriter.Add<ReturnTerminator>(InvalidateType);
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
            Rewriter.Rewrite(
                builder.SourceBlocks,
                builder,
                CreateProcessingData(new DataProvider()));

        #endregion
    }
}
