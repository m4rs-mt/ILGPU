// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: ArgumentMapper.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.IR.Types;
using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace ILGPU.Backends.EntryPoints
{
    /// <summary>
    /// Maps kernel arguments to a compatible representation that
    /// can be accessed by the native kernel.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public abstract class ArgumentMapper : ICache
    {
        #region Nested Types

        /// <summary>
        /// An emission source.
        /// </summary>
        protected interface ISource
        {
            /// <summary>
            /// Returns the source type.
            /// </summary>
            Type SourceType { get; }

            /// <summary>
            /// Emits a load command.
            /// </summary>
            /// <typeparam name="TILEmitter">The emitter type.</typeparam>
            /// <param name="emitter">The current emitter.</param>
            void EmitLoadSource<TILEmitter>(in TILEmitter emitter)
                where TILEmitter : IILEmitter;
        }

        /// <summary>
        /// An emission target.
        /// </summary>
        protected interface ITarget
        {
            /// <summary>
            /// Returns the target type.
            /// </summary>
            Type TargetType { get; }

            /// <summary>
            /// Emits a target command.
            /// </summary>
            /// <typeparam name="TILEmitter">The emitter type.</typeparam>
            /// <param name="emitter">The current emitter.</param>
            void EmitLoadTarget<TILEmitter>(in TILEmitter emitter)
                where TILEmitter : IILEmitter;
        }

        /// <summary>
        /// An argument source.
        /// </summary>
        protected readonly struct ArgumentSource : ISource
        {
            /// <summary>
            /// Constructs a new argument source.
            /// </summary>
            /// <param name="type">The argument type.</param>
            /// <param name="argumentIndex">The argument index.</param>
            public ArgumentSource(Type type, int argumentIndex)
            {
                SourceType = type;
                ArgumentIndex = argumentIndex;
            }

            /// <summary cref="ISource.SourceType"/>
            public Type SourceType { get; }

            /// <summary>
            /// Returns the argument index.
            /// </summary>
            public int ArgumentIndex { get; }

            /// <summary cref="ISource.EmitLoadSource{TILEmitter}(in TILEmitter)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EmitLoadSource<TILEmitter>(in TILEmitter emitter)
                where TILEmitter : IILEmitter
            {
                emitter.Emit(ArgumentOperation.LoadAddress, ArgumentIndex);
            }
        }

        /// <summary>
        /// A <see cref="ILLocal"/> source.
        /// </summary>
        protected readonly struct LocalSource : ISource
        {
            /// <summary>
            /// Constructs a new local source.
            /// </summary>
            /// <param name="local">The current local.</param>
            public LocalSource(ILLocal local)
            {
                Local = local;
            }

            /// <summary cref="ISource.SourceType"/>
            public Type SourceType => Local.VariableType;

            /// <summary>
            /// Returns the associated local variable.
            /// </summary>
            public ILLocal Local { get; }

            /// <summary cref="ISource.EmitLoadSource{TILEmitter}(in TILEmitter)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EmitLoadSource<TILEmitter>(in TILEmitter emitter)
                where TILEmitter : IILEmitter
            {
                emitter.Emit(LocalOperation.LoadAddress, Local);
            }
        }

        /// <summary>
        /// A structure source.
        /// </summary>
        /// <typeparam name="TParentSource">The parent source type.</typeparam>
        protected readonly struct StructureSource<TParentSource> : ISource
            where TParentSource : ISource
        {
            /// <summary>
            /// Construct a new structure source.
            /// </summary>
            /// <param name="parentSource">The parent source.</param>
            /// <param name="sourceField">The source field.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public StructureSource(in TParentSource parentSource, FieldInfo sourceField)
            {
                ParentSource = parentSource;
                SourceField = sourceField;
            }

            /// <summary cref="ISource.SourceType"/>
            public Type SourceType => SourceField.FieldType;

            /// <summary>
            /// Returns the parent source.
            /// </summary>
            public TParentSource ParentSource { get; }

            /// <summary>
            /// Returns the source field.
            /// </summary>
            public FieldInfo SourceField { get; }

            /// <summary cref="ISource.EmitLoadSource{TILEmitter}(in TILEmitter)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EmitLoadSource<TILEmitter>(in TILEmitter emitter)
                where TILEmitter : IILEmitter
            {
                ParentSource.EmitLoadSource(emitter);
                emitter.Emit(OpCodes.Ldflda, SourceField);
            }
        }

        /// <summary>
        /// A structure source.
        /// </summary>
        /// <typeparam name="TParentTarget">The parent source type.</typeparam>
        protected readonly struct StructureTarget<TParentTarget> : ITarget
            where TParentTarget : ITarget
        {
            /// <summary>
            /// Constructs a new structure target.
            /// </summary>
            /// <param name="parentTarget">The parent target.</param>
            /// <param name="targetField">The target field.</param>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public StructureTarget(in TParentTarget parentTarget, FieldInfo targetField)
            {
                ParentTarget = parentTarget;
                TargetField = targetField;
            }

            /// <summary cref="ITarget.TargetType"/>
            public Type TargetType => TargetField.FieldType;

            /// <summary>
            /// Returns the parent target.
            /// </summary>
            public TParentTarget ParentTarget { get; }

            /// <summary>
            /// Returns the target field.
            /// </summary>
            public FieldInfo TargetField { get; }

            /// <summary cref="ITarget.EmitLoadTarget{TILEmitter}(in TILEmitter)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EmitLoadTarget<TILEmitter>(in TILEmitter emitter)
                where TILEmitter : IILEmitter
            {
                ParentTarget.EmitLoadTarget(emitter);
                emitter.Emit(OpCodes.Ldflda, TargetField);
            }
        }

        /// <summary>
        /// A <see cref="ILLocal"/> target.
        /// </summary>
        protected readonly struct LocalTarget : ITarget
        {
            /// <summary>
            /// Constructs a new local target.
            /// </summary>
            /// <param name="local">The current local.</param>
            public LocalTarget(ILLocal local)
            {
                Local = local;
            }

            /// <summary cref="ITarget.TargetType"/>
            public Type TargetType => Local.VariableType;

            /// <summary>
            /// Returns the associated local variable.
            /// </summary>
            public ILLocal Local { get; }

            /// <summary cref="ITarget.EmitLoadTarget{TILEmitter}(in TILEmitter)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EmitLoadTarget<TILEmitter>(in TILEmitter emitter)
                where TILEmitter : IILEmitter
            {
                emitter.Emit(LocalOperation.LoadAddress, Local);
            }
        }

        /// <summary>
        /// A view-parameter source.
        /// </summary>
        protected readonly struct ViewSource<TSource> : ISource
            where TSource : ISource
        {
            /// <summary>
            /// Constructs a new view source.
            /// </summary>
            /// <param name="source">The underlying source.</param>
            /// <param name="viewParameter">The view parameter to map.</param>
            public ViewSource(
                in TSource source,
                in SeparateViewEntryPoint.ViewParameter viewParameter)
            {
                Source = source;
                SourceType = viewParameter.ViewType;
                ParameterType = viewParameter.ParameterType;
                AccessChain = viewParameter.AccessChain;
            }

            /// <summary>
            /// Returns the underlying source.
            /// </summary>
            public TSource Source { get; }

            /// <summary cref="ISource.SourceType"/>
            public Type SourceType { get; }

            /// <summary>
            /// Returns the parameter type.
            /// </summary>
            public TypeInformationManager.TypeInformation ParameterType { get; }

            /// <summary>
            /// Returns the access chain to resolve the actual view instance.
            /// </summary>
            public ImmutableArray<int> AccessChain { get; }

            /// <summary cref="ISource.EmitLoadSource{TILEmitter}(in TILEmitter)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EmitLoadSource<TILEmitter>(in TILEmitter emitter)
                where TILEmitter : IILEmitter
            {
                Source.EmitLoadSource(emitter);
                var type = ParameterType;
                foreach (var fieldIndex in AccessChain)
                {
                    emitter.Emit(OpCodes.Ldflda, type.Fields[fieldIndex]);
                    type = type.GetFieldTypeInfo(fieldIndex);
                }
            }
        }

        /// <summary>
        /// An abstract argument mapping handler.
        /// </summary>
        protected interface IMappingHandler
        {
            /// <summary>
            /// Emits a mapping command that maps a kernel argument.
            /// </summary>
            /// <typeparam name="TILEmitter">The emitter type.</typeparam>
            /// <typeparam name="TSource">The value source type.</typeparam>
            /// <param name="emitter">The target emitter.</param>
            /// <param name="source">The value source.</param>
            /// <param name="argumentIndex">The index of the kernel argument.</param>
            void MapArgument<TILEmitter, TSource>(
                in TILEmitter emitter,
                TSource source,
                int argumentIndex)
                where TILEmitter : IILEmitter
                where TSource : ISource;
        }

        /// <summary>
        /// An abstract argument mapping handler.
        /// </summary>
        protected interface ISeparateViewMappingHandler
        {
            /// <summary>
            /// Emits a set of commands that map an implementation view instance
            /// and stores the converted instance into the given target.
            /// </summary>
            /// <typeparam name="TILEmitter">The emitter type.</typeparam>
            /// <typeparam name="TSource">The value source type.</typeparam>
            /// <param name="emitter">The current emitter.</param>
            /// <param name="source">The value source.</param>
            /// <param name="viewParameter">The source view parameter.</param>
            /// <param name="viewArgumentIndex">The argument index.</param>
            void MapViewArgument<TILEmitter, TSource>(
                in TILEmitter emitter,
                in TSource source,
                in SeparateViewEntryPoint.ViewParameter viewParameter,
                int viewArgumentIndex)
                where TILEmitter : IILEmitter
                where TSource : ISource;
        }

        #endregion

        #region Instance

        /// <summary>
        /// The internal type mapping (from old to new types).
        /// </summary>
        private readonly Dictionary<Type, Type> typeMapping = new Dictionary<Type, Type>();

        /// <summary>
        /// Constructs a new argument mapper.
        /// </summary>
        /// <param name="context">The current context.</param>
        protected ArgumentMapper(Context context)
        {
            Context = context ??
                throw new ArgumentNullException(nameof(context));
            TypeInformationManager = context.TypeContext;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated context.
        /// </summary>
        public Context Context { get; }

        /// <summary>
        /// Returns the associated type-information manager.
        /// </summary>
        public TypeInformationManager TypeInformationManager { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Maps a view type to its implementation specific type.
        /// </summary>
        /// <param name="viewType">The view type.</param>
        /// <param name="elementType">The element type.</param>
        /// <returns>The resulting implementation type.</returns>
        protected abstract Type MapViewType(Type viewType, Type elementType);

        /// <summary>
        /// Maps the given structure type to a compatible structure type.
        /// </summary>
        /// <param name="structType">The structure type to map.</param>
        /// <returns>The mapped structure type.</returns>
        protected Type MapStructType(Type structType)
        {
            // Check all element types
            var typeInfo = TypeInformationManager.GetTypeInfo(structType);
            var sourceFields = typeInfo.Fields;
            if (sourceFields.Length < 1)
                return structType;

            var nestedTypes = new List<Type>(sourceFields.Length);
            bool requireCustomType = false;
            for (int i = 0, e = sourceFields.Length; i < e; ++i)
            {
                var sourceFieldType = sourceFields[i].FieldType;
                var fieldType = MapType(sourceFieldType);
                requireCustomType |= fieldType != sourceFieldType;
                nestedTypes.Add(fieldType);
            }
            if (!requireCustomType)
                return structType;

            // We need a custom structure type and map all fields
            var typeBuilder = Context.DefineRuntimeStruct();
            for (int i = 0, e = sourceFields.Length; i < e; ++i)
            {
                typeBuilder.DefineField(
                    "Field" + i,
                    nestedTypes[i],
                    FieldAttributes.Public);
            }
            // Build wrapper type and return it
            return typeBuilder.CreateType();
        }

        /// <summary>
        /// Registers a type mapping entry and returns the mapped type.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <param name="mappedType">The target type.</param>
        /// <returns>The mapped type.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected Type RegisterTypeMapping(Type type, Type mappedType)
        {
            typeMapping.Add(type, mappedType);
            return mappedType;
        }

        /// <summary>
        /// Maps the given source type to a compatible target type.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <returns>The compatible target type.</returns>
        protected Type MapType(Type type)
        {
            Debug.Assert(type != null, "Invalid source type");
            if (typeMapping.TryGetValue(type, out Type mappedType))
                return mappedType;

            if (type.IsVoidPtr() || type == typeof(void) || type.IsByRef ||
                type.IsPointer || type.IsDelegate() || type.IsArray || type.IsClass)
                throw new ArgumentOutOfRangeException(nameof(type));

            if (type.IsPrimitive)
                return RegisterTypeMapping(type, type);
            else if (type.IsPointer)
                return RegisterTypeMapping(type, typeof(void*));
            else if (type.IsEnum)
                return RegisterTypeMapping(type, type.GetEnumUnderlyingType());
            else if (type.IsArrayViewType(out Type elementType))
                return RegisterTypeMapping(type, MapViewType(type, elementType));
            else
                return RegisterTypeMapping(type, MapStructType(type));
        }

        /// <summary>
        /// Emits a set of commands that map an implementation view instance
        /// and stores the converted instance into the given target.
        /// </summary>
        /// <typeparam name="TILEmitter">The emitter type.</typeparam>
        /// <typeparam name="TSource">The value source type.</typeparam>
        /// <typeparam name="TTarget">The value target type.</typeparam>
        /// <param name="emitter">The current emitter.</param>
        /// <param name="source">The value source.</param>
        /// <param name="target">The value target.</param>
        protected abstract void MapViewInstance<TILEmitter, TSource, TTarget>(
            in TILEmitter emitter,
            TSource source,
            TTarget target)
            where TILEmitter : IILEmitter
            where TSource : ISource
            where TTarget : ITarget;

        /// <summary>
        /// Maps a specific structure instance.
        /// </summary>
        /// <typeparam name="TILEmitter">The emitter type.</typeparam>
        /// <typeparam name="TSource">The value source type.</typeparam>
        /// <typeparam name="TTarget">The value target type.</typeparam>
        /// <param name="emitter">The current emitter.</param>
        /// <param name="source">The value source.</param>
        /// <param name="target">The value target.</param>
        protected void MapStructInstance<TILEmitter, TSource, TTarget>(
            in TILEmitter emitter,
            TSource source,
            TTarget target)
            where TILEmitter : IILEmitter
            where TSource : ISource
            where TTarget : ITarget
        {
            // Resolve type info of source and target types
            var sourceInfo = TypeInformationManager.GetTypeInfo(source.SourceType);
            var targetInfo = TypeInformationManager.GetTypeInfo(target.TargetType);
            Debug.Assert(sourceInfo.NumFields == targetInfo.NumFields, "Incompatible types");

            // Map all field entries
            for (int i = 0, e = sourceInfo.NumFields; i < e; ++i)
            {
                var fieldSource = new StructureSource<TSource>(
                    source,
                    sourceInfo.Fields[i]);
                var fieldTarget = new StructureTarget<TTarget>(
                    target,
                    targetInfo.Fields[i]);
                MapInstance(emitter, fieldSource, fieldTarget);
            }
        }

        /// <summary>
        /// Maps a value instance.
        /// </summary>
        /// <typeparam name="TILEmitter">The emitter type.</typeparam>
        /// <typeparam name="TSource">The value source type.</typeparam>
        /// <typeparam name="TTarget">The value target type.</typeparam>
        /// <param name="emitter">The current emitter.</param>
        /// <param name="source">The value source.</param>
        /// <param name="target">The value target.</param>
        protected void MapInstance<TILEmitter, TSource, TTarget>(
            in TILEmitter emitter,
            TSource source,
            TTarget target)
            where TILEmitter : IILEmitter
            where TSource : ISource
            where TTarget : ITarget
        {
            var sourceType = source.SourceType;
            if (sourceType == target.TargetType ||
                sourceType.IsEnum)
            {
                // Copy object from source to target
                target.EmitLoadTarget(emitter);
                source.EmitLoadSource(emitter);
                emitter.Emit(OpCodes.Cpobj, target.TargetType);
            }
            else if (sourceType.IsArrayViewType(out Type _))
                MapViewInstance(emitter, source, target);
            else
                MapStructInstance(emitter, source, target);
        }

        /// <summary>
        /// Creates code that maps the given parameter specification to
        /// a compatible representation.
        /// </summary>
        /// <typeparam name="TILEmitter">The emitter type.</typeparam>
        /// <typeparam name="TMappingHandler">The handler type.</typeparam>
        /// <param name="emitter">The target emitter to write to.</param>
        /// <param name="mappingHandler">The target mapping handler to use.</param>
        /// <param name="specification">The parameter specification to map.</param>
        protected void Map<TILEmitter, TMappingHandler>(
            in TILEmitter emitter,
            in TMappingHandler mappingHandler,
            in EntryPoint.ParameterSpecification specification)
            where TILEmitter : IILEmitter
            where TMappingHandler : IMappingHandler
        {
            // Map all parameters
            for (int i = 0, e = specification.NumParameters; i < e; ++i)
            {
                if (specification.IsByRef(i))
                    throw new NotSupportedException(ErrorMessages.InvalidEntryPointParameter);

                // Load parameter argument and map instance
                var parameterType = specification.ParameterTypes[i];
                var parameterIndex = i + Kernel.KernelParameterOffset;
                var argumentSource = new ArgumentSource(parameterType, parameterIndex);

                // Map type and check result
                var mappedType = MapType(parameterType);
                if (mappedType != parameterType)
                {
                    // Perform actual instance mapping on local
                    var mappingLocal = emitter.DeclareLocal(mappedType);
                    var localTarget = new LocalTarget(mappingLocal);
                    MapInstance(emitter, argumentSource, localTarget);

                    // Map an indirect argument
                    var localSource = new LocalSource(mappingLocal);
                    mappingHandler.MapArgument(emitter, localSource, i);
                }
                else
                {
                    // Map argument directly
                    mappingHandler.MapArgument(emitter, argumentSource, i);
                }
            }
        }

        /// <summary>
        /// Creates code that maps (potentially nested) views of kernel arguments separately.
        /// </summary>
        /// <typeparam name="TILEmitter">The emitter type.</typeparam>
        /// <typeparam name="TMappingHandler">The handler type.</typeparam>
        /// <param name="emitter">The target emitter to write to.</param>
        /// <param name="mappingHandler">The target mapping handler to use.</param>
        /// <param name="entryPoint">The entry point to use.</param>
        protected static void MapViews<TILEmitter, TMappingHandler>(
            in TILEmitter emitter,
            in TMappingHandler mappingHandler,
            SeparateViewEntryPoint entryPoint)
            where TILEmitter : IILEmitter
            where TMappingHandler : ISeparateViewMappingHandler
        {
            Debug.Assert(entryPoint != null, "Invalid entry point");

            // Resolve all information from all kernel arguments
            int viewArgumentIndex = 0;
            var specification = entryPoint.Parameters;
            for (int i = 0, e = specification.NumParameters; i < e; ++i)
            {
                if (specification.IsByRef(i))
                    throw new NotSupportedException(ErrorMessages.InvalidEntryPointParameter);

                // Check for matching view specifications
                if (!entryPoint.TryGetViewParameters(i, out var views))
                    continue;

                // Load parameter argument source and resolve the access chain
                var parameterType = specification.ParameterTypes[i];
                var parameterIndex = i + Kernel.KernelParameterOffset;
                var argumentSource = new ArgumentSource(parameterType, parameterIndex);

                // Map all view parameters
                foreach (var view in views)
                {
                    var viewSource = new ViewSource<ArgumentSource>(argumentSource, view);
                    mappingHandler.MapViewArgument(
                        emitter,
                        viewSource,
                        view,
                        viewArgumentIndex++);
                }
            }
        }

        /// <summary>
        /// Clears internal caches.
        /// </summary>
        /// <param name="mode">The clear mode.</param>
        public void ClearCache(ClearCacheMode mode) =>
            typeMapping.Clear();

        #endregion
    }
}
