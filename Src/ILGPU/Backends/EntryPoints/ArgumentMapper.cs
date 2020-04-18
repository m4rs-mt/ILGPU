// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: ArgumentMapper.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

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
        /// An internal target.
        /// </summary>
        protected struct Target
        {
            /// <summary>
            /// The array of all fields to write to.
            /// </summary>
            private readonly FieldInfo[] fields;

            /// <summary>
            /// Constructs a new internal target.
            /// </summary>
            /// <param name="local">The local to write to.</param>
            internal Target(ILLocal local)
            {
                Local = local;
                var parentType = local.VariableType;
                fields = parentType.GetFields();
                Array.Sort(fields, (left, right) =>
                {
                    var leftOffset = Marshal.OffsetOf(parentType, left.Name).ToInt32();
                    var rightOffset = Marshal.OffsetOf(parentType, right.Name).ToInt32();
                    return leftOffset.CompareTo(rightOffset);
                });
                Index = 0;
            }

            /// <summary>
            /// Returns the local to write to.
            /// </summary>
            public ILLocal Local { get; }

            /// <summary>
            /// Returns the current field index.
            /// </summary>
            public int Index { get; private set; }

            /// <summary>
            /// Returns the target type.
            /// </summary>
            internal Type NextTargetType => TargetField.FieldType;

            /// <summary>
            /// Returns the target field.
            /// </summary>
            private FieldInfo TargetField => fields[Index];

            /// <summary>
            /// Emits a target command.
            /// </summary>
            /// <typeparam name="TILEmitter">The emitter type.</typeparam>
            /// <param name="emitter">The current emitter.</param>
            /// <returns>The target type to store.</returns>
            public Type EmitLoadTarget<TILEmitter>(in TILEmitter emitter)
                where TILEmitter : IILEmitter
            {
                var result = NextTargetType;
                emitter.Emit(LocalOperation.LoadAddress, Local);
                emitter.Emit(OpCodes.Ldflda, TargetField);
                NextTarget();
                return result;
            }

            /// <summary>
            /// Moves the target to the next one.
            /// </summary>
            public void NextTarget() => ++Index;
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

            /// <summary>
            /// Emits the address of an argument.
            /// </summary>
            public void EmitLoadSource<TILEmitter>(in TILEmitter emitter)
                where TILEmitter : IILEmitter =>
                emitter.Emit(ArgumentOperation.LoadAddress, ArgumentIndex);
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

            /// <summary>
            /// Emits the address of a local variable.
            /// </summary>
            public void EmitLoadSource<TILEmitter>(in TILEmitter emitter)
                where TILEmitter : IILEmitter =>
                emitter.Emit(LocalOperation.LoadAddress, Local);
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

            /// <summary>
            /// Emits the address of a structure field.
            /// </summary>
            public void EmitLoadSource<TILEmitter>(in TILEmitter emitter)
                where TILEmitter : IILEmitter
            {
                ParentSource.EmitLoadSource(emitter);
                emitter.Emit(OpCodes.Ldflda, SourceField);
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
                AccessChain = viewParameter.SourceChain;
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
            public FieldAccessChain AccessChain { get; }

            /// <summary cref="ISource.EmitLoadSource{TILEmitter}(in TILEmitter)"/>
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            public void EmitLoadSource<TILEmitter>(in TILEmitter emitter)
                where TILEmitter : IILEmitter
            {
                Source.EmitLoadSource(emitter);
                var type = ParameterType;
                foreach (var fieldIndex in AccessChain)
                {
                    emitter.Emit(OpCodes.Ldflda, type.Fields[(int)fieldIndex]);
                    type = type.GetFieldTypeInfo((int)fieldIndex);
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
        private readonly Dictionary<Type, Type> typeMapping =
            new Dictionary<Type, Type>();

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
        /// <param name="elements">
        /// The target element collection to add element types to.
        /// </param>
        protected abstract void MapViewType<TTargetCollection>(
            Type viewType,
            Type elementType,
            TTargetCollection elements)
            where TTargetCollection : ICollection<Type>;

        /// <summary>
        /// Maps the given structure type to a compatible structure type.
        /// </summary>
        /// <param name="structType">The structure type to map.</param>
        /// <param name="elements">
        /// The target element collection to add element types to.
        /// </param>
        protected void MapStructType<TTargetCollection>(
            Type structType,
            TTargetCollection elements)
            where TTargetCollection : ICollection<Type>
        {
            var typeInfo = TypeInformationManager.GetTypeInfo(structType);
            if (typeInfo.NumFields < 1)
            {
                MapType(typeof(byte), elements);
            }
            else
            {
                foreach (var type in typeInfo.FieldTypes)
                    MapType(type, elements);
            }
        }

        /// <summary>
        /// Maps the given source type to a compatible target type.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <param name="elements">
        /// The target element collection to add element types to.
        /// </param>
        protected void MapType<TTargetCollection>(Type type, TTargetCollection elements)
            where TTargetCollection : ICollection<Type>
        {
            if (type.IsVoidPtr() || type == typeof(void) || type.IsByRef ||
                type.IsPointer || type.IsDelegate() || type.IsArray || type.IsClass)
            {
                throw new ArgumentOutOfRangeException(nameof(type));
            }

            if (type.IsPrimitive)
                elements.Add(type);
            else if (type.IsPointer)
                elements.Add(typeof(void*));
            else if (type.IsEnum)
                elements.Add(type.GetEnumUnderlyingType());
            else if (type.IsArrayViewType(out Type elementType))
                MapViewType(type, elementType, elements);
            else
                MapStructType(type, elements);
        }

        /// <summary>
        /// Maps the given source type to a compatible target type.
        /// </summary>
        /// <param name="type">The source type.</param>
        /// <returns>The compatible target type.</returns>
        private Type MapType(Type type)
        {
            Debug.Assert(type != null, "Invalid source type");
            if (typeMapping.TryGetValue(type, out Type mappedType))
                return mappedType;

            var types = new List<Type>();
            MapType(type, types);
            var typeBuilder = Context.DefineRuntimeStruct();
            for (int i = 0, e = types.Count; i < e; ++i)
            {
                typeBuilder.DefineField(
                    "Field" + i,
                    types[i],
                    FieldAttributes.Public);
            }
            // Build wrapper type and return it
            var wrapperType = typeBuilder.CreateType();
            typeMapping.Add(type, wrapperType);
            return wrapperType;
        }

        /// <summary>
        /// Emits a set of commands that map an implementation view instance
        /// and stores the converted instance into the given target.
        /// </summary>
        /// <typeparam name="TILEmitter">The emitter type.</typeparam>
        /// <typeparam name="TSource">The value source type.</typeparam>
        /// <param name="emitter">The current emitter.</param>
        /// <param name="elementType">The element type.</param>
        /// <param name="source">The value source.</param>
        /// <param name="target">The value target.</param>
        protected abstract void MapViewInstance<TILEmitter, TSource>(
            in TILEmitter emitter,
            Type elementType,
            TSource source,
            ref Target target)
            where TILEmitter : IILEmitter
            where TSource : ISource;

        /// <summary>
        /// Maps a specific structure instance.
        /// </summary>
        /// <typeparam name="TILEmitter">The emitter type.</typeparam>
        /// <typeparam name="TSource">The value source type.</typeparam>
        /// <param name="emitter">The current emitter.</param>
        /// <param name="source">The value source.</param>
        /// <param name="target">The value target.</param>
        protected void MapStructInstance<TILEmitter, TSource>(
            in TILEmitter emitter,
            TSource source,
            ref Target target)
            where TILEmitter : IILEmitter
            where TSource : ISource
        {
            // Map all field entries
            var sourceInfo = TypeInformationManager.GetTypeInfo(source.SourceType);
            for (int i = 0, e = sourceInfo.NumFields; i < e; ++i)
            {
                var fieldSource = new StructureSource<TSource>(
                    source,
                    sourceInfo.Fields[i]);
                MapInstance(emitter, fieldSource, ref target);
            }
        }

        /// <summary>
        /// Maps a value instance.
        /// </summary>
        /// <typeparam name="TILEmitter">The emitter type.</typeparam>
        /// <typeparam name="TSource">The value source type.</typeparam>
        /// <param name="emitter">The current emitter.</param>
        /// <param name="source">The value source.</param>
        /// <param name="target">The value target.</param>
        protected void MapInstance<TILEmitter, TSource>(
            in TILEmitter emitter,
            TSource source,
            ref Target target)
            where TILEmitter : IILEmitter
            where TSource : ISource
        {
            var sourceType = source.SourceType;
            if (sourceType == target.NextTargetType ||
                sourceType.IsEnum)
            {
                // Copy object from source to target
                var targetType = target.EmitLoadTarget(emitter);
                source.EmitLoadSource(emitter);
                emitter.Emit(OpCodes.Cpobj, targetType);
            }
            else if (sourceType.IsArrayViewType(out Type elementType))
            {
                MapViewInstance(emitter, elementType, source, ref target);
            }
            else
            {
                MapStructInstance(emitter, source, ref target);
            }
        }

        /// <summary>
        /// Creates code that maps the given parameter specification to
        /// a compatible representation.
        /// </summary>
        /// <typeparam name="TILEmitter">The emitter type.</typeparam>
        /// <typeparam name="TMappingHandler">The handler type.</typeparam>
        /// <param name="emitter">The target emitter to write to.</param>
        /// <param name="mappingHandler">The target mapping handler to use.</param>
        /// <param name="parameters">The parameter collection to map.</param>
        protected void Map<TILEmitter, TMappingHandler>(
            in TILEmitter emitter,
            in TMappingHandler mappingHandler,
            in ParameterCollection parameters)
            where TILEmitter : IILEmitter
            where TMappingHandler : IMappingHandler
        {
            // Map all parameters
            for (int i = 0, e = parameters.Count; i < e; ++i)
            {
                if (parameters.IsByRef(i))
                {
                    throw new NotSupportedException(
                        ErrorMessages.InvalidEntryPointParameter);
                }

                // Load parameter argument and map instance
                var parameterType = parameters.ParameterTypes[i];
                var parameterIndex = i + Kernel.KernelParameterOffset;

                // Map type and check result
                var mappedType = MapType(parameterType);
                var mappingLocal = emitter.DeclareLocal(mappedType);

                // Perform actual instance mapping on local
                var argumentSource = new ArgumentSource(parameterType, parameterIndex);
                var localTarget = new Target(mappingLocal);
                MapInstance(emitter, argumentSource, ref localTarget);

                // Map an indirect argument
                var localSource = new LocalSource(mappingLocal);
                mappingHandler.MapArgument(emitter, localSource, i);
            }
        }

        /// <summary>
        /// Creates code that maps (potentially nested) views of kernel arguments
        /// separately.
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
            for (int i = 0, e = specification.Count; i < e; ++i)
            {
                if (specification.IsByRef(i))
                {
                    throw new NotSupportedException(
                        ErrorMessages.InvalidEntryPointParameter);
                }

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
