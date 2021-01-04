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
using static ILGPU.IR.Transformations.InferAddressSpaces;
using static ILGPU.IR.Transformations.InferKernelAddressSpaces;
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

    /// <summary>
    /// Infers method-local address spaces by removing unnecessary address-space casts.
    /// </summary>
    /// <remarks>
    /// This transformation uses a method-local program analysis to remove address-space
    /// casts that are no longer required.
    /// </remarks>
    public sealed class InferLocalAddressSpaces : UnorderedTransformation
    {
        #region Nested Types

        /// <summary>
        /// A data provider based on local program analysis information.
        /// </summary>
        private readonly struct LocalDataProvider : IAddressSpaceProvider
        {
            internal LocalDataProvider(in AnalysisValueMapping<AddressSpaceInfo> mapping)
            {
                Mapping = mapping;
            }

            /// <summary>
            /// Returns the local information of the <see cref="PointerAddressSpaces"/>
            /// analysis.
            /// </summary>
            private AnalysisValueMapping<AddressSpaceInfo> Mapping { get; }

            /// <summary>
            /// Returns the unified address space of the given value.
            /// </summary>
            public readonly MemoryAddressSpace this[Value value] =>
                Mapping.TryGetValue(value, out var data)
                ? data.Data.UnifiedAddressSpace
                : new DataProvider()[value];
        }

        #endregion

        #region Rewriter

        /// <summary>
        /// The internal rewriter.
        /// </summary>
        private static readonly Rewriter<ProcessingData<LocalDataProvider>> Rewriter =
            new Rewriter<ProcessingData<LocalDataProvider>>();

        /// <summary>
        /// Registers all conversion patterns.
        /// </summary>
        static InferLocalAddressSpaces()
        {
            AddRewriters(Rewriter);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new address-space inference pass.
        /// </summary>
        public InferLocalAddressSpaces()
            : this(MemoryAddressSpace.Generic)
        { }

        /// <summary>
        /// Constructs a new address-space inference pass.
        /// </summary>
        /// <param name="parameterAddressSpace">
        /// The root address space of all method parameters.
        /// </param>
        public InferLocalAddressSpaces(MemoryAddressSpace parameterAddressSpace)
        {
            ParameterAddressSpace = parameterAddressSpace;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parameter address space.
        /// </summary>
        public MemoryAddressSpace ParameterAddressSpace { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Applies the address-space inference transformation.
        /// </summary>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            var analysis = Create(AnalysisFlags.IgnoreGenericAddressSpace);
            var (_, result) = analysis.AnalyzeMethod(
                builder.Method,
                new InitialParameterValueContext(ParameterAddressSpace));
            return Rewriter.Rewrite(
                builder.SourceBlocks,
                builder,
                CreateProcessingData(new LocalDataProvider(result)));
        }

        #endregion
    }

    /// <summary>
    /// Infers kernel address spaces by removing unnecessary address-space casts.
    /// </summary>
    /// <remarks>
    /// This transformation is inteded to run after applying
    /// <see cref="SpecializeKernelParameterAddressSpaces"/>. In contrast to
    /// <see cref="InferLocalAddressSpaces"/>, this transformation uses a global program
    /// analysis to determine detailed address-space information for each method.
    /// </remarks>
    public sealed class InferKernelAddressSpaces :
        UnorderedTransformation<MethodDataProvider>
    {
        #region Nested Types

        /// <summary>
        /// A data provider based on global program analysis information.
        /// </summary>
        public sealed class MethodDataProvider : IAddressSpaceProvider
        {
            #region Instance

            /// <summary>
            /// Creates a new provider instance.
            /// </summary>
            /// <typeparam name="TPredicate">The collection predicate type.</typeparam>
            /// <param name="methods">The collection of methods.</param>
            /// <param name="kernelAddressSpace">The target address space.</param>
            public static MethodDataProvider CreateProvider<TPredicate>(
                in MethodCollection<TPredicate> methods,
                MemoryAddressSpace kernelAddressSpace)
                where TPredicate : IMethodCollectionPredicate
            {
                // Get the main entry point method
                foreach (var method in methods)
                {
                    if (method.HasFlags(MethodFlags.EntryPoint))
                    {
                        var analysis = Create(AnalysisFlags.IgnoreGenericAddressSpace);
                        var result = analysis.AnalyzeGlobalMethod(
                            method,
                            new InitialParameterValueContext(kernelAddressSpace));
                        return new MethodDataProvider(result);
                    }
                }

                // We could not find any entry point
                return default;
            }

            /// <summary>
            /// Constructs a new method data provider.
            /// </summary>
            /// <param name="result">The analysis result.</param>
            private MethodDataProvider(in AnalysisResult result)
            {
                Result = result;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated program analysis result.
            /// </summary>
            private AnalysisResult Result { get; }

            /// <summary>
            /// Returns the return type and the original parameters of the given method.
            /// </summary>
            public MemoryAddressSpace this[Method method] =>
                Result.TryGetReturnData(method, out var data)
                ? data.Data.UnifiedAddressSpace
                : MemoryAddressSpace.Generic;

            /// <summary>
            /// Returns the unified address space of the given value.
            /// </summary>
            public MemoryAddressSpace this[Value value] =>
                Result.TryGetData(value, out var data)
                ? data.Data.UnifiedAddressSpace
                : new DataProvider()[value];

            #endregion
        }

        #endregion

        #region Rewriter

        /// <summary>
        /// The internal rewriter.
        /// </summary>
        private static readonly Rewriter<ProcessingData<MethodDataProvider>> Rewriter =
            new Rewriter<ProcessingData<MethodDataProvider>>();

        /// <summary>
        /// Registers all conversion patterns.
        /// </summary>
        static InferKernelAddressSpaces()
        {
            AddRewriters(Rewriter);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new address-space inference pass.
        /// </summary>
        /// <param name="kernelAddressSpace">
        /// The root address space of all kernel functions.
        /// </param>
        public InferKernelAddressSpaces(MemoryAddressSpace kernelAddressSpace)
        {
            KernelAddressSpace = kernelAddressSpace;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the kernel address space.
        /// </summary>
        public MemoryAddressSpace KernelAddressSpace { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a new <see cref="MethodDataProvider"/> instance based on the main
        /// entry-point method.
        /// </summary>
        protected override MethodDataProvider CreateIntermediate<TPredicate>(
            in MethodCollection<TPredicate> methods) =>
            MethodDataProvider.CreateProvider(methods, KernelAddressSpace);

        /// <summary>
        /// Applies the address-space inference transformation.
        /// </summary>
        protected override bool PerformTransformation(
            Method.Builder builder,
            MethodDataProvider intermediate) =>
            intermediate != null && Rewriter.Rewrite(
                builder.SourceBlocks,
                builder,
                CreateProcessingData(intermediate));

        /// <summary>
        /// Performs no operation.
        /// </summary>
        protected override void FinishProcessing(MethodDataProvider _) { }

        #endregion
    }
}
