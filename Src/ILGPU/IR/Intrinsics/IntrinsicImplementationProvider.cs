// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: IntrinsicImplementationProvider.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Frontend;
using ILGPU.IR.Values;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Intrinsics
{
    /// <summary>
    /// Represents an intrinsic provider that caches intrinsic remappings and
    /// implementations.
    /// </summary>
    /// <typeparam name="TDelegate">The backend-specific delegate type.</typeparam>
    public sealed class IntrinsicImplementationProvider<TDelegate> : DisposeBase, ICache
        where TDelegate : Delegate
    {
        #region Nested Types

        /// <summary>
        /// Represents an implementation transformer to convert high-level intrinsic
        /// values into instantiated intrinsic mappings.
        /// </summary>
        private readonly struct ImplementationTransformer :
            IIntrinsicImplementationTransformer<
                IntrinsicImplementationManager.ImplementationEntry,
                IntrinsicMapping<TDelegate>>
        {
            private readonly Dictionary<
                IntrinsicImplementation,
                IntrinsicMapping<TDelegate>> mappings;

            public ImplementationTransformer(Backend backend)
            {
                Debug.Assert(backend != null, "Invalid backend");
                mappings = new Dictionary<
                    IntrinsicImplementation,
                    IntrinsicMapping<TDelegate>>();
                Backend = backend;
            }

            /// <summary>
            /// Returns the associated backend.
            /// </summary>
            public Backend Backend { get; }

            /// <summary cref="IIntrinsicImplementationTransformer{TFirst, TSecond}.
            /// Transform(TFirst)"/>
            public IntrinsicMapping<TDelegate> Transform(
                IntrinsicImplementationManager.ImplementationEntry entry)
            {
                if (entry != null &&
                    CheckImplementations(Backend, entry, out var mainImplementation))
                {
                    if (!mappings.TryGetValue(mainImplementation, out var mapping))
                    {
                        mapping = mainImplementation.ResolveMapping<TDelegate>();
                        mappings.Add(mainImplementation, mapping);
                    }
                    return mapping;
                }
                return null;
            }

            /// <summary>
            /// Checks the given intrinsic implementations.
            /// </summary>
            /// <param name="backend">The current backend.</param>
            /// <param name="implementations">
            /// The available intrinsic implementations.
            /// </param>
            /// <param name="mainImplementation">
            /// The resolved main implementation.
            /// </param>
            /// <returns>
            /// True, if at least a single implementation could be resolved.
            /// </returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool CheckImplementations(
                Backend backend,
                IntrinsicImplementationManager.ImplementationEntry implementations,
                out IntrinsicImplementation mainImplementation)
            {
                mainImplementation = null;
                foreach (var implementation in implementations)
                {
                    if (implementation.CanHandleBackend(backend))
                    {
                        mainImplementation = implementation;
                        return true;
                    }
                }
                return false;
            }
        }

        /// <summary>
        /// Represents a mapping entry.
        /// </summary>
        private readonly struct MappingEntry
        {
            /// <summary>
            /// Constructs a new mapping entry.
            /// </summary>
            /// <param name="mapping">The parent mapping.</param>
            /// <param name="mappingKey">The current mapping key.</param>
            /// <param name="codeGenerationResult">
            /// The intermediate code-generation result.
            /// </param>
            public MappingEntry(
                IntrinsicMapping<TDelegate> mapping,
                IntrinsicMapping.MappingKey mappingKey,
                CodeGenerationResult codeGenerationResult)
            {
                Mapping = mapping;
                MappingKey = mappingKey;
                CodeGenerationResult = codeGenerationResult;
            }

            /// <summary>
            /// The associated mapping.
            /// </summary>
            public IntrinsicMapping<TDelegate> Mapping { get; }

            /// <summary>
            /// The associated method mapping key.
            /// </summary>
            public IntrinsicMapping.MappingKey MappingKey { get; }

            /// <summary>
            /// The code-generation result from the IL frontend.
            /// </summary>
            public CodeGenerationResult CodeGenerationResult { get; }

            /// <summary>
            /// Applies the code-generation result to the underlying mapping.
            /// </summary>
            public void Apply() =>
                Mapping.ProvideImplementation(MappingKey, CodeGenerationResult.Result);
        }

        /// <summary>
        /// Represents a code generation phase for intrinsic methods.
        /// </summary>
        public struct IRSpecializationPhase : IDisposable
        {
            #region Instance

            private readonly Dictionary<MethodInfo, MappingEntry> mappings;

            private readonly ContextCodeGenerationPhase contextCodeGenerationPhase;
            private readonly CodeGenerationPhase codeGenerationPhase;

            internal IRSpecializationPhase(
                IntrinsicImplementationProvider<TDelegate> provider,
                ContextCodeGenerationPhase currentPhase)
            {
                Debug.Assert(provider != null, "Invalid mapping");
                Debug.Assert(currentPhase != null, "Invalid code-generation phase");

                Provider = provider;
                contextCodeGenerationPhase = currentPhase;
                codeGenerationPhase = contextCodeGenerationPhase
                    .BeginFrontendCodeGeneration();

                mappings = new Dictionary<MethodInfo, MappingEntry>();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated provider.
            /// </summary>
            public IntrinsicImplementationProvider<TDelegate> Provider { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Internal method to register an intrinsic.
            /// </summary>
            /// <typeparam name="TResolver">
            /// The generic argument resolver type.
            /// </typeparam>
            /// <param name="resolver">The argument resolver.</param>
            /// <param name="mapping">The current mapping instance.</param>
            /// <returns>True, if the intrinsic could be registered.</returns>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private bool TryRegisterIntrinsic<TResolver>(
                TResolver resolver,
                IntrinsicMapping<TDelegate> mapping)
                where TResolver : struct, IntrinsicMapping.IGenericArgumentResolver
            {
                if (mapping.Mode != IntrinsicImplementationMode.Redirect)
                    return false;

                lock (mappings)
                {
                    var redirect = mapping.ResolveRedirect(
                        resolver,
                        out var genericMapping);
                    if (!mappings.ContainsKey(redirect))
                    {
                        var result = codeGenerationPhase.GenerateCode(redirect);
                        mappings.Add(
                            redirect,
                            new MappingEntry(mapping, genericMapping, result));
                    }
                }
                return true;
            }

            /// <summary>
            /// Tries to register an intrinsic for the given method.
            /// </summary>
            /// <param name="method">The method to register.</param>
            /// <returns>True, if an intrinsic mapping could be resolved.</returns>
            public bool RegisterIntrinsic(Method method) =>
                Provider.TryGetMapping(method, out var methodInfo, out var mapping) &&
                TryRegisterIntrinsic(
                    new IntrinsicMapping.MethodInfoArgumentResolver(methodInfo),
                    mapping);

            /// <summary>
            /// Tries to register an intrinsic for the given value.
            /// </summary>
            /// <param name="value">The value to register.</param>
            /// <returns>True, if an intrinsic mapping could be resolved.</returns>
            public bool RegisterIntrinsic(Value value) =>
                Provider.TryGetMapping(value, out var mapping) &&
                TryRegisterIntrinsic(
                    new IntrinsicMapping.ValueArgumentResolver(value),
                    mapping);

            #endregion

            #region IDisposable

            /// <summary>
            /// Ends the current specialization phase.
            /// </summary>
            public void Dispose()
            {
                codeGenerationPhase.Dispose();
                contextCodeGenerationPhase.Optimize();
                contextCodeGenerationPhase.Dispose();

                foreach (var mappingEntry in mappings.Values)
                    mappingEntry.Apply();
                mappings.Clear();
            }

            #endregion
        }

        /// <summary>
        /// Represents an abstract data provider. It can be used in combination
        /// with the <see cref="TryGetData{TResult, TDataProvider}(
        /// Value, out TResult)"/> method.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        private interface IDataProvider<TResult>
        {
            /// <summary>
            /// Returns the compatible implementation mode.
            /// </summary>
            IntrinsicImplementationMode Mode { get; }

            /// <summary>
            /// Gets data from the given intrinsic mapping.
            /// </summary>
            /// <typeparam name="TResolver">The resolver type.</typeparam>
            /// <param name="mapping">The mapping instance.</param>
            /// <param name="resolver">The resolver instance.</param>
            /// <returns>The resolved result.</returns>
            TResult GetData<TResolver>(
                IntrinsicMapping<TDelegate> mapping,
                TResolver resolver)
                where TResolver : struct, IntrinsicMapping.IGenericArgumentResolver;
        }

        /// <summary>
        /// Resolves IR implementations from mappings.
        /// </summary>
        private readonly struct ImplementationProvider : IDataProvider<Method>
        {
            /// <summary cref="IDataProvider{TResult}.Mode"/>
            public IntrinsicImplementationMode Mode =>
                IntrinsicImplementationMode.Redirect;

            /// <summary cref="IDataProvider{TResult}.GetData{TResolver}(
            /// IntrinsicMapping{TDelegate}, TResolver)"/>
            public Method GetData<TResolver>(
                IntrinsicMapping<TDelegate> mapping,
                TResolver resolver)
                where TResolver : struct, IntrinsicMapping.IGenericArgumentResolver =>
                mapping.ResolveImplementation(resolver);
        }

        /// <summary>
        /// Resolves code generators from mappings.
        /// </summary>
        private readonly struct CodeGeneratorProvider : IDataProvider<TDelegate>
        {
            /// <summary cref="IDataProvider{TResult}.Mode"/>
            public IntrinsicImplementationMode Mode =>
                IntrinsicImplementationMode.GenerateCode;

            /// <summary cref="IDataProvider{TResult}.GetData{TResolver}(
            /// IntrinsicMapping{TDelegate}, TResolver)"/>
            public TDelegate GetData<TResolver>(
                IntrinsicMapping<TDelegate> mapping,
                TResolver resolver)
                where TResolver : struct, IntrinsicMapping.IGenericArgumentResolver =>
                mapping.ResolveCodeGenerator(resolver);
        }

        #endregion

        #region Instance

        private readonly IntrinsicMethodMatcher<
            IntrinsicMapping<TDelegate>> methodMatcher;
        private readonly BaseIntrinsicValueMatcher<
            IntrinsicMapping<TDelegate>>[] valueMatchers;
        private readonly IRContext intrinsicContext;

        /// <summary>
        /// Constructs a new intrinsic implementation mapping.
        /// </summary>
        /// <param name="container">The source intrinsic container.</param>
        /// <param name="backend">The associated backend.</param>
        internal IntrinsicImplementationProvider(
            IntrinsicImplementationManager.BackendContainer container,
            Backend backend)
        {
            Debug.Assert(backend != null, "Invalid backend");

            Context = backend.Context;
            intrinsicContext = new IRContext(Context);

            var allMatchers = IntrinsicMatcher.CreateMatchers<
                IntrinsicMapping<TDelegate>>();
            container.TransformTo(new ImplementationTransformer(backend), allMatchers);
            methodMatcher = allMatchers[(int)IntrinsicMatcher.MatcherKind.Method]
                as IntrinsicMethodMatcher<IntrinsicMapping<TDelegate>>;

            // Build a fast value-kind specific lookup
            valueMatchers = new BaseIntrinsicValueMatcher<
                IntrinsicMapping<TDelegate>>[ValueKinds.NumValueKinds];
            foreach (var matcher in allMatchers)
            {
                if (matcher is BaseIntrinsicValueMatcher<
                    IntrinsicMapping<TDelegate>> valueMatcher)
                {
                    valueMatchers[(int)valueMatcher.ValueKind] = valueMatcher;
                }
            }
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated context.
        /// </summary>
        public Context Context { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Begins a new specialization phase.
        /// </summary>
        /// <returns>The specialization context.</returns>
        public IRSpecializationPhase BeginIRSpecialization() =>
            new IRSpecializationPhase(
                this,
                Context.BeginCodeGeneration(intrinsicContext));

        /// <summary>
        /// Resolves the intrinsic mapping for the given method.
        /// </summary>
        /// <param name="method">The method to resolve an implementation for.</param>
        /// <param name="mapping">The resolved mapping.</param>
        /// <returns>True, if the given method could be resolved to a mapping.</returns>
        public bool TryGetMapping(
            Method method,
            out IntrinsicMapping<TDelegate> mapping) =>
            TryGetMapping(method, out var _, out mapping);

        /// <summary>
        /// Resolves the intrinsic mapping for the given method.
        /// </summary>
        /// <param name="method">The method to resolve an implementation for.</param>
        /// <param name="methodInfo">
        /// The resolved method information object (if any).
        /// </param>
        /// <param name="mapping">The resolved mapping.</param>
        /// <returns>True, if the given method could be resolved to a mapping.</returns>
        public bool TryGetMapping(
            Method method,
            out MethodInfo methodInfo,
            out IntrinsicMapping<TDelegate> mapping)
        {
            mapping = default;
            return (methodInfo = method.Source as MethodInfo) != null &&
                method.HasFlags(MethodFlags.Intrinsic) &&
                TryGetMapping(methodInfo, out mapping);
        }

        /// <summary>
        /// Resolves the intrinsic mapping for the given method.
        /// </summary>
        /// <param name="method">The method to resolve an implementation for.</param>
        /// <param name="mapping">The resolved mapping.</param>
        /// <returns>True, if the given method could be resolved to a mapping.</returns>
        public bool TryGetMapping(
            MethodInfo method,
            out IntrinsicMapping<TDelegate> mapping) =>
            methodMatcher.TryGetImplementation(method, out mapping);

        /// <summary>
        /// Resolves the intrinsic mapping for the given value kind.
        /// </summary>
        /// <param name="value">The value to resolve an implementation for.</param>
        /// <param name="mapping">The resolved mapping.</param>
        /// <returns>True, if the given method could be resolved to a mapping.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool TryGetMapping(Value value, out IntrinsicMapping<TDelegate> mapping)
        {
            var matchers = valueMatchers[(int)value.ValueKind];
            if (matchers != null)
                return matchers.TryGetImplementation(value, out mapping);
            mapping = default;
            return false;
        }

        /// <summary>
        /// Tries to resolve data from the given value.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <typeparam name="TDataProvider">The resolver type.</typeparam>
        /// <param name="value">The value to resolve.</param>
        /// <param name="result">The resulting value.</param>
        /// <returns>
        /// True, if the value could be resolved to an intrinsic value.
        /// </returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private bool TryGetData<TResult, TDataProvider>(Value value, out TResult result)
            where TResult : class
            where TDataProvider : struct, IDataProvider<TResult>
        {
            result = null;

            TDataProvider dataProvider = default;
            IntrinsicMapping<TDelegate> mapping;
            if (value is MethodCall call)
            {
                if (!TryGetMapping(call.Target, out var methodInfo, out mapping) ||
                    mapping.Mode != dataProvider.Mode)
                {
                    return false;
                }

                // Resolve method-specific code-generator
                result = dataProvider.GetData(
                    mapping,
                    new IntrinsicMapping.MethodInfoArgumentResolver(methodInfo));
                return true;
            }

            if (!TryGetMapping(value, out mapping) || mapping.Mode != dataProvider.Mode)
                return false;

            result = dataProvider.GetData(
                mapping,
                new IntrinsicMapping.ValueArgumentResolver(value));
            return true;
        }

        /// <summary>
        /// Resolves the intrinsic implementation (if any) for the given value kind.
        /// </summary>
        /// <param name="value">The value to resolve an implementation for.</param>
        /// <param name="irImplementation">The resolved IR implementation.</param>
        /// <returns>
        /// True, if the given method could be resolved to an IR implementation.
        /// </returns>
        public bool TryGetImplementation(Value value, out Method irImplementation) =>
            TryGetData<Method, ImplementationProvider>(value, out irImplementation);

        /// <summary>
        /// Resolves the intrinsic code generator (if any) for the given value kind.
        /// </summary>
        /// <param name="value">The value to resolve an implementation for.</param>
        /// <param name="codeGenerator">The resolved code generator.</param>
        /// <returns>
        /// True, if the given method could be resolved to a code generator.
        /// </returns>
        public bool TryGetCodeGenerator(Value value, out TDelegate codeGenerator) =>
            TryGetData<TDelegate, CodeGeneratorProvider>(value, out codeGenerator);

        /// <summary>
        /// Clears all internal caches.
        /// </summary>
        /// <param name="mode">The clear mode.</param>
        /// <remarks>This method is not thread-safe.</remarks>
        public void ClearCache(ClearCacheMode mode) =>
            intrinsicContext.ClearCache(mode);

        #endregion

        #region IDisposable

        /// <summary cref="DisposeBase.Dispose(bool)"/>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
                intrinsicContext.Dispose();
            base.Dispose(disposing);
        }

        #endregion
    }
}
