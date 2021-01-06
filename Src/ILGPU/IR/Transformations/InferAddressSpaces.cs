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
        public InferLocalAddressSpaces() { }

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
                new AutomaticParameterValueContext());
            return Rewriter.Rewrite(
                builder.SourceBlocks,
                builder,
                CreateProcessingData(new LocalDataProvider(result)));
        }

        #endregion
    }

    /// <summary>
    /// Infers kernel address spaces by specializing the address spaces of all parameters
    /// or keeping them and inserting the appropriate address space casts.
    /// </summary>
    /// <remarks>
    /// CAUTION: This program transformation adds additional address-space casts into
    /// the <see cref="MemoryAddressSpace.Generic"/> address space to have a valid IR
    /// program in the end. The additionally introduced casts are intended to be removed
    /// using <see cref="InferLocalAddressSpaces"/> afterwards.
    /// </remarks>
    public sealed class InferKernelAddressSpaces :
        OrderedTransformation<InferKernelAddressSpaces.MethodDataProvider>
    {
        #region Nested Types

        /// <summary>
        /// Represents an intermediate value for processing.
        /// </summary>
        public sealed class MethodDataProvider
        {
            #region Static

            /// <summary>
            /// Creates a new provider instance.
            /// </summary>
            /// <param name="methods">The collection of methods.</param>
            /// <param name="kernelAddressSpace">The target address space.</param>
            public static MethodDataProvider CreateProvider(
                in MethodCollection methods,
                MemoryAddressSpace kernelAddressSpace)
            {
                // Get the main entry point method
                foreach (var method in methods)
                {
                    if (method.HasFlags(MethodFlags.EntryPoint))
                    {
                        var analysis = Create(AnalysisFlags.IgnoreGenericAddressSpace);
                        var result = analysis.AnalyzeGlobalMethod(
                            method,
                            new ConstParameterValueContext(kernelAddressSpace));
                        return new MethodDataProvider(result);
                    }
                }

                // We could not find any entry point
                return default;
            }

            #endregion

            #region Instance

            private readonly Dictionary<Parameter, Parameter> oldParameters;

            /// <summary>
            /// Constructs a new intermediate value.
            /// </summary>
            /// <param name="result">The analysis result.</param>
            public MethodDataProvider(in AnalysisResult result)
            {
                oldParameters = new Dictionary<Parameter, Parameter>();
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

            #region Methods

            /// <summary>
            /// Returns the original target address space for the updated parameter.
            /// </summary>
            /// <param name="parameter">The updated parameter reference.</param>
            public MemoryAddressSpace GetTargetAddressSpace(
                Parameter parameter)
            {
                if (oldParameters.TryGetValue(parameter, out var oldParameter))
                    return this[oldParameter];

                // If we have not seen this parameter, it can be a previously unknown
                // parameter of an external function
                parameter.Assert(parameter.Method.HasFlags(MethodFlags.External));
                return MemoryAddressSpace.Generic;
            }

            /// <summary>
            /// Maps the <paramref name="targetParam"/> to the
            /// <paramref name="parameter"/>.
            /// </summary>
            /// <param name="parameter">The source parameter.</param>
            /// <param name="targetParam">The new target parameter.</param>
            public void Map(Parameter parameter, Parameter targetParam) =>
                oldParameters.Add(targetParam, parameter);

            #endregion
        }

        #endregion

        #region Static

        /// <summary>
        /// Converts the given value into the specified target address space.
        /// </summary>
        /// <param name="context">The current rewriter context.</param>
        /// <param name="value">The current value to convert.</param>
        /// <param name="targetAddressSpace">
        /// The target address space to convert into.
        /// </param>
        /// <returns>The converted value in the correct address space.</returns>
        private static Value ConvertToAddressSpace(
            in RewriterContext context,
            Value value,
            MemoryAddressSpace targetAddressSpace)
        {
            // Check if the current value is affected by the conversion
            var type = value.Type;
            if (!type.HasFlags(TypeFlags.AddressSpaceDependent))
                return value;

            var location = value.Location;
            // If this is a simple scalar value, try to convert it
            if (type is AddressSpaceType)
            {
                return context.Builder.CreateAddressSpaceCast(
                    location,
                    value,
                    targetAddressSpace);
            }
            else
            {
                // We need to create a wrapper structure that has the correct address
                // space information
                var typeConverter = GetAddressSpaceConverter(targetAddressSpace);
                var targetType = typeConverter.ConvertType(context.Builder, value.Type);
                return type == targetType
                    ? value
                    : context.AssembleStructure(
                        targetType as StructureType,
                        value,
                        (ctx, val, access) =>
                        {
                            // Resolve the original source value
                            var fieldValue = ctx.Builder.CreateGetField(
                                val.Location,
                                val,
                                access);

                            // Convert it into the target address space (if possible)
                            return ConvertToAddressSpace(
                                ctx,
                                fieldValue,
                                targetAddressSpace);
                        });
            }
        }

        /// <summary>
        /// Specializes an address-space dependent parameter.
        /// </summary>
        /// <param name="provider">The intermediate value.</param>
        /// <param name="methodBuilder">The target method builder.</param>
        /// <param name="builder">The entry block builder.</param>
        /// <param name="parameter">The source parameter.</param>
        /// <returns>True, if the given parameter was specialized.</returns>
        private static bool SpecializeParameterAddressSpace(
            MethodDataProvider provider,
            Method.Builder methodBuilder,
            BasicBlock.Builder builder,
            Parameter parameter)
        {
            // Determine the target address space
            var targetAddressSpace = provider[parameter];
            var converted = GetAddressSpaceConverter(targetAddressSpace).
                ConvertType(builder, parameter.Type);

            // Append a new parameter using the converted target type
            var targetParam = methodBuilder.AddParameter(converted, parameter.Name);

            // Remember the parameter association
            provider.Map(parameter, targetParam);

            // If the type is the same, skip further address space casts
            if (converted == parameter.Type)
            {
                parameter.Replace(targetParam);
                return false;
            }

            // We have to convert the updated parameter address spaces into the generic
            // address space at this point, since the remainder of this program still
            // assumes operations on the generic address space
            var convertedValue = ConvertToAddressSpace(
                RewriterContext.FromBuilder(builder),
                targetParam,
                MemoryAddressSpace.Generic);

            // Replace the parameter with the converted value
            parameter.Replace(convertedValue);
            return true;
        }

        #endregion

        #region Rewriter Methods

        /// <summary>
        /// Checks if the given call has address-space dependencies.
        /// </summary>
        private static bool CanRewrite(
            MethodDataProvider data,
            MethodCall call)
        {
            foreach (Value argument in call)
            {
                if (argument.Type.HasFlags(TypeFlags.AddressSpaceDependent))
                    return true;
            }
            var returnType = call.Target.ReturnType;
            return returnType.HasFlags(TypeFlags.AddressSpaceDependent);
        }

        /// <summary>
        /// Rewrites method calls that need wrapped address-space casts.
        /// </summary>
        private static void Rewrite(
            RewriterContext context,
            MethodDataProvider data,
            MethodCall call)
        {
            // Rebuild the call
            var target = call.Target;
            var callBuilder = context.Builder.CreateCall(call.Location, call.Target);
            for (int i = 0, e = call.Count; i < e; ++i)
            {
                // Check the target address space of the (potentially) updated parameter
                var parameter = target.Parameters[i];
                var parameterTargetAddressSpace = data.GetTargetAddressSpace(parameter);

                // Convert the argument (if possible) into the target address space
                callBuilder.Add(ConvertToAddressSpace(
                    context,
                    call[i],
                    parameterTargetAddressSpace));
            }

            // Create new call node
            Value newCall = callBuilder.Seal();
            context.MarkConverted(newCall);

            // Check the return type for a potential conversion
            if (newCall.Type.HasFlags(TypeFlags.AddressSpaceDependent))
            {
                // If the return type of the callee changed, we have to emit an address
                // space cast to ensure a valid IR
                newCall = ConvertToAddressSpace(
                    context,
                    newCall,
                    MemoryAddressSpace.Generic);
            }

            // Replace and remove the current call
            context.ReplaceAndRemove(call, newCall);
        }

        /// <summary>
        /// Checks if the given return has address-space dependencies.
        /// </summary>
        private static bool CanRewrite(
            MethodDataProvider data,
            ReturnTerminator terminator)
        {
            var returnType = terminator.Method.ReturnType;
            return returnType.HasFlags(TypeFlags.AddressSpaceDependent);
        }

        /// <summary>
        /// Rewrites return terminators that need a wrapped address-space cast.
        /// </summary>
        private static void Rewrite(
            RewriterContext context,
            MethodDataProvider data,
            ReturnTerminator terminator)
        {
            var targetAddressSpace = data[terminator.Method];
            var newReturnValue = ConvertToAddressSpace(
                context,
                terminator.ReturnValue,
                targetAddressSpace);

            var newReturn = context.Builder.CreateReturn(
                newReturnValue.Location,
                newReturnValue);
            context.MarkConverted(newReturn);
        }

        #endregion

        #region Rewriter

        /// <summary>
        /// The internal rewriter.
        /// </summary>
        private static readonly Rewriter<MethodDataProvider> Rewriter =
            new Rewriter<MethodDataProvider>();

        /// <summary>
        /// Registers all conversion patterns.
        /// </summary>
        static InferKernelAddressSpaces()
        {
            Rewriter.Add<MethodCall>(CanRewrite, Rewrite);
            Rewriter.Add<ReturnTerminator>(CanRewrite, Rewrite);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new address-space specialization pass.
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
        protected override MethodDataProvider CreateIntermediate(
            in MethodCollection methods) =>
            MethodDataProvider.CreateProvider(methods, KernelAddressSpace);

        /// <summary>
        /// Applies the address-space inference transformation.
        /// </summary>
        protected override bool PerformTransformation(
            IRContext context,
            Method.Builder builder,
            in MethodDataProvider intermediate,
            Landscape landscape,
            Landscape.Entry current)
        {
            if (intermediate is null)
                return false;

            // Initialize the main converted and the entry block builder
            var entryBuilder = builder.EntryBlockBuilder;
            entryBuilder.SetupInsertPositionToStart();

            // Specialize all parameters
            bool applied = false;
            for (int i = 0, e = builder.NumParams; i < e; ++i)
            {
                // Specialize the address space of the current parameter
                var parameter = builder[i];
                applied |= SpecializeParameterAddressSpace(
                    intermediate,
                    builder,
                    entryBuilder,
                    parameter);
            }

            // Specialize the return type to use the (potentially) new address space
            if (!builder.Method.IsVoid)
            {
                var targetAddressSpace = intermediate[builder.Method];
                builder.UpdateReturnType(
                    GetAddressSpaceConverter(targetAddressSpace));
            }

            // Adjust all method calls
            return applied && Rewriter.Rewrite(
                builder.SourceBlocks,
                builder,
                intermediate);
        }

        /// <summary>
        /// Performs no operation.
        /// </summary>
        protected override void FinishProcessing(in MethodDataProvider _) { }

        #endregion
    }
}
