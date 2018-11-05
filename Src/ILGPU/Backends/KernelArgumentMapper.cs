// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: KernelArgumentMapper.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Emit;

namespace ILGPU.Backends
{
    /// <summary>
    /// Implements a specific kernel argument mapping.
    /// </summary>
    public abstract class KernelArgumentMapping
    {
        #region Nested Types

        /// <summary>
        /// Represents a source mapping.
        /// </summary>
        private readonly struct Source : KernelArgumentMapper.ISource
        {
            [SuppressMessage("Microsoft.Performance", "CA1811: AvoidUncalledPrivateCode", Justification = "This code will be used in the future")]
            public Source(int index, bool isByRef)
            {
                Index = index;
                IsByRef = isByRef;
            }

            /// <summary>
            /// The current index.
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// True if the aargument is passed by reference.
            /// </summary>
            public bool IsByRef { get; }

            /// <summary cref="KernelArgumentMapper.ISource.EmitLoadSource{TILEmitter}(in TILEmitter)"/>
            public void EmitLoadSource<TILEmitter>(in TILEmitter emitter)
                where TILEmitter : IILEmitter
            {
                if (IsByRef)
                    emitter.Emit(ArgumentOperation.Load, Index);
                else
                    emitter.Emit(ArgumentOperation.LoadAddress, Index);
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new mapping.
        /// </summary>
        /// <param name="parameters">The parameter specification.</param>
        /// <param name="mappings">The parameter mapping.</param>
        protected KernelArgumentMapping(
            in EntryPoint.ParameterSpecification parameters,
            ImmutableArray<KernelArgumentMapper.Mapping> mappings)
        {
            Parameters = parameters;
            Mappings = mappings;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the entry point parameter specification.
        /// </summary>
        public EntryPoint.ParameterSpecification Parameters { get; }

        /// <summary>
        /// Returns the contained mapping.
        /// </summary>
        internal ImmutableArray<KernelArgumentMapper.Mapping> Mappings { get; }

        #endregion
    }

    /// <summary>
    /// Realizes a mapper that maps kernel arguments.
    /// </summary>
    public abstract class KernelArgumentMapper
    {
        #region Nested Types

        /// <summary>
        /// A single kernel target.
        /// </summary>
        public interface IKernelTarget
        {
            /// <summary>
            /// Declares the given type and returns the current target index.
            /// </summary>
            /// <param name="type">The type to declare.</param>
            /// <returns>The returned index.</returns>
            int DeclareType(Type type);
        }

        /// <summary>
        /// An emission source.
        /// </summary>
        public interface ISource
        {
            /// <summary>
            /// Emits a load command.
            /// </summary>
            /// <typeparam name="TILEmitter">The emitter type.</typeparam>
            /// <param name="emitter">The current emitter.</param>
            void EmitLoadSource<TILEmitter>(in TILEmitter emitter)
                where TILEmitter : IILEmitter;
        }

        /// <summary>
        /// An emssision target.
        /// </summary>
        public interface ITarget
        {
            /// <summary>
            /// Emits a target command.
            /// </summary>
            /// <typeparam name="TILEmitter">The emitter type.</typeparam>
            /// <param name="emitter">The current emitter.</param>
            /// <param name="id">The stored emission id.</param>
            void EmitLoadTarget<TILEmitter>(in TILEmitter emitter, int id)
                where TILEmitter : IILEmitter;
        }

        /// <summary>
        /// Represents a basic mapping.
        /// </summary>
        public abstract class Mapping
        {
            #region Instance

            /// <summary>
            /// Constructs a new mapping.
            /// </summary>
            /// <param name="targetId">The target mapping id.</param>
            protected Mapping(int targetId)
            {
                TargetId = targetId;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Represents the stored target id.
            /// </summary>
            public int TargetId { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Emits a conversion operation.
            /// </summary>
            /// <typeparam name="TILEmitter">The emitter type.</typeparam>
            /// <typeparam name="TSource">The emission source.</typeparam>
            /// <typeparam name="TTarget">The emission target.</typeparam>
            /// <param name="emitter">The current emitter.</param>
            /// <param name="source">The current source.</param>
            /// <param name="target">The current target.</param>
            public abstract void EmitConversion<TILEmitter, TSource, TTarget>(
                in TILEmitter emitter,
                in TSource source,
                in TTarget target)
                where TILEmitter : IILEmitter
                where TSource : ISource
                where TTarget : ITarget;

            #endregion
        }

        /// <summary>
        /// Represents an emtpy mapping.
        /// </summary>
        protected internal sealed class EmptyMapping : Mapping
        {
            /// <summary>
            /// Creates a new emtpy mapping.
            /// </summary>
            public EmptyMapping()
                : base(-1)
            { }

            /// <summary cref="Mapping.EmitConversion{TILEmitter, TSource, TTarget}(in TILEmitter, in TSource, in TTarget)"/>
            public override void EmitConversion<TILEmitter, TSource, TTarget>(
                in TILEmitter emitter,
                in TSource source,
                in TTarget target)
            { }
        }

        /// <summary>
        /// Maps source types to their according target types using the idendity.
        /// </summary>
        protected internal sealed class IdentityMapping : Mapping
        {
            /// <summary>
            /// Constructs a new identity mapping.
            /// </summary>
            /// <param name="targetId">The current target id.</param>
            /// <param name="targetType">The target type.</param>
            public IdentityMapping(int targetId, Type targetType)
                : base(targetId)
            {
                TargetType = targetType;
            }

            /// <summary>
            /// Returns the associated target type.
            /// </summary>
            public Type TargetType { get; }

            /// <summary cref="Mapping.EmitConversion{TILEmitter, TSource, TTarget}(in TILEmitter, in TSource, in TTarget)"/>
            public override void EmitConversion<TILEmitter, TSource, TTarget>(
                in TILEmitter emitter,
                in TSource source,
                in TTarget target)
            {
                target.EmitLoadTarget(emitter, TargetId);
                source.EmitLoadSource(emitter);
                emitter.Emit(OpCodes.Cpobj, TargetType);
            }
        }

        /// <summary>
        /// Maps structure types.
        /// </summary>
        protected internal sealed class StructMapping : Mapping
        {
            /// <summary>
            /// Realizes a mapping entry.
            /// </summary>
            internal readonly struct MappingEntry
            {
                /// <summary>
                /// Constructs a new mappinge entry.
                /// </summary>
                /// <param name="mapping">The associated mapping.</param>
                /// <param name="fieldInfo">The associated field info.</param>
                public MappingEntry(
                    Mapping mapping,
                    FieldInfo fieldInfo)
                {
                    Mapping = mapping;
                    FieldInfo = fieldInfo;
                }

                /// <summary>
                /// Returns the associated mapping.
                /// </summary>
                public Mapping Mapping { get; }

                /// <summary>
                /// Returns the associated field info.
                /// </summary>
                public FieldInfo FieldInfo { get; }
            }

            /// <summary>
            /// A structure source.
            /// </summary>
            /// <typeparam name="TParentSource">The parent source type.</typeparam>
            internal readonly struct Source<TParentSource> : ISource
                where TParentSource : ISource
            {
                /// <summary>
                /// /Constructed a new structure source.
                /// </summary>
                /// <param name="parentSource">The parent source.</param>
                /// <param name="sourceField">The source field.</param>
                public Source(in TParentSource parentSource, FieldInfo sourceField)
                {
                    ParentSource = parentSource;
                    SourceField = sourceField;
                }

                /// <summary>
                /// Returns the parent source.
                /// </summary>
                public TParentSource ParentSource { get; }

                /// <summary>
                /// Returns the source field.
                /// </summary>
                public FieldInfo SourceField { get; }

                /// <summary cref="ISource.EmitLoadSource{TILEmitter}(in TILEmitter)"/>
                public void EmitLoadSource<TILEmitter>(in TILEmitter emitter)
                    where TILEmitter : IILEmitter
                {
                    ParentSource.EmitLoadSource(emitter);
                    emitter.Emit(OpCodes.Ldflda, SourceField);
                }
            }

            /// <summary>
            /// Constructs a new structure mapping.
            /// </summary>
            /// <param name="targetId">The current target id.</param>
            /// <param name="mappings">All structure entry mappings.</param>
            internal StructMapping(
                int targetId,
                ImmutableArray<MappingEntry> mappings)
                : base(targetId)
            {
                Mappings = mappings;
            }

            /// <summary>
            /// Returns an internal mapping array that maps mappings to their according fields.
            /// </summary>
            private ImmutableArray<MappingEntry> Mappings { get; }

            /// <summary cref="Mapping.EmitConversion{TILEmitter, TSource, TTarget}(in TILEmitter, in TSource, in TTarget)"/>
            public override void EmitConversion<TILEmitter, TSource, TTarget>(
                in TILEmitter emitter,
                in TSource source,
                in TTarget target)
            {
                for (int i = 0, e = Mappings.Length; i < e; ++i)
                {
                    var mapping = Mappings[i];
                    var fieldSource = new Source<TSource>(source, mapping.FieldInfo);
                    mapping.Mapping.EmitConversion(emitter, fieldSource, target);
                }
            }
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new kernel argument mapper.
        /// </summary>
        /// <param name="context">The current context.</param>
        protected KernelArgumentMapper(Context context)
        {
            Context = context ??
                throw new ArgumentNullException(nameof(context));
            TypeInformationManager = context.TypeInformationManger;
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
        /// Creates a custom mapping for the given primitive type.
        /// </summary>
        /// <typeparam name="TTarget">The target type.</typeparam>
        /// <param name="target">The target builder.</param>
        /// <param name="basicValueType">The basic value type.</param>
        /// <param name="primitiveType">The primitive type.</param>
        /// <returns>An appropriate mapping.</returns>
        protected virtual Mapping CreatePrimitiveTypeMapping<TTarget>(
            ref TTarget target,
            BasicValueType basicValueType,
            Type primitiveType)
            where TTarget : IKernelTarget
            => new IdentityMapping(target.DeclareType(primitiveType), primitiveType);

        /// <summary>
        /// Creates a custom mapping for the given view type.
        /// </summary>
        /// <typeparam name="TTarget">The target type.</typeparam>
        /// <param name="target">The target builder.</param>
        /// <param name="viewType">The source view type.</param>
        /// <param name="elementType">The element type of the view.</param>
        /// <returns>An appropriate mapping.</returns>
        protected abstract Mapping CreateViewTypeMapping<TTarget>(
            ref TTarget target,
            Type viewType,
            Type elementType)
            where TTarget : IKernelTarget;

        /// <summary>
        /// Creates a new struct mapping.
        /// </summary>
        /// <typeparam name="TTarget">The target type.</typeparam>
        /// <param name="target">The target builder.</param>
        /// <param name="structType">The struct type.</param>
        /// <returns>The argument mapping.</returns>
        protected virtual Mapping CreateStructMapping<TTarget>(
            ref TTarget target,
            Type structType)
            where TTarget : IKernelTarget
        {
            Debug.Assert(structType != null, "Invalid struct type");
            var typeInfo = TypeInformationManager.GetTypeInfo(structType);
            var sourceFields = typeInfo.Fields;
            if (sourceFields.Length < 1)
                return new EmptyMapping();
            var mappings = ImmutableArray.CreateBuilder<StructMapping.MappingEntry>(
                sourceFields.Length);
            for (int i = 0, e = sourceFields.Length; i < e; ++i)
            {
                var fieldMapping = CreateMapping(ref target, sourceFields[i].FieldType);
                mappings.Add(new StructMapping.MappingEntry(fieldMapping, sourceFields[i]));
            }
            return new StructMapping(mappings[0].Mapping.TargetId, mappings.MoveToImmutable());
        }

        /// <summary>
        /// Maps the given type to a mapper.
        /// </summary>
        /// <typeparam name="TTarget">The target type.</typeparam>
        /// <param name="target">The target builder.</param>
        /// <param name="type">The source type.</param>
        /// <returns>The argument mapping.</returns>
        protected Mapping CreateMapping<TTarget>(
            ref TTarget target,
            Type type)
            where TTarget : IKernelTarget
        {
            type = TypeInformationManager.MapType(type);

            if (type.IsVoidPtr() || type == typeof(void) || type.IsByRef ||
                type.IsPointer || type.IsDelegate() || type.IsArray || type.IsClass)
                throw new ArgumentOutOfRangeException(nameof(type));

            var basicValueType = type.GetBasicValueType();
            if (basicValueType != BasicValueType.None)
                return CreatePrimitiveTypeMapping(ref target, basicValueType, type);
            else if (type.IsArrayViewType(out Type elementType))
                return CreateViewTypeMapping(ref target, type, elementType);
            else
                return CreateStructMapping(ref target, type);
        }

        /// <summary>
        /// Maps the parameter specifications to a mapper.
        /// </summary>
        /// <typeparam name="TTarget">The target type.</typeparam>
        /// <param name="target">The target builder.</param>
        /// <param name="specification">The parameter specification.</param>
        /// <returns>The argument mapping.</returns>
        protected ImmutableArray<Mapping> CreateMapping<TTarget>(
            ref TTarget target,
            in EntryPoint.ParameterSpecification specification)
            where TTarget : IKernelTarget
        {
            var mappings = ImmutableArray.CreateBuilder<Mapping>(
                specification.NumParameters);
            for (int i = 0, e = specification.NumParameters; i < e; ++i)
            {
                if (specification.IsByRef(i))
                    throw new NotSupportedException("Not supported kernel parameter");
                var type = specification[i];
                mappings.Add(CreateMapping(ref target, type));
            }
            return mappings.MoveToImmutable();
        }

        /// <summary>
        /// Creates a new kernel argument type in the .Net world based on several types from
        /// the .Net world. The given types can then be automatically transformed into
        /// its unified kernel-argument type, which can be automatically marshaled into the
        /// native world.
        /// </summary>
        /// <param name="specification">The parameter specification.</param>
        /// <returns>The argument mapping.</returns>
        public KernelArgumentMapping CreateMapping(
            in EntryPoint.ParameterSpecification specification) =>
            CreateMapping(specification, null);

        /// <summary>
        /// Creates a new kernel argument type in the .Net world based on several types from
        /// the .Net world. The given types can then be automatically transformed into
        /// its unified kernel-argument type, which can be automatically marshaled into the
        /// native world.
        /// </summary>
        /// <param name="specification">The parameter specification.</param>
        /// <param name="nonGroupedIndexType">The index type in the case of an ungrouped kernel.</param>
        /// <returns>The argument mapping.</returns>
        public abstract KernelArgumentMapping CreateMapping(
            in EntryPoint.ParameterSpecification specification,
            Type nonGroupedIndexType);

        #endregion
    }
}
