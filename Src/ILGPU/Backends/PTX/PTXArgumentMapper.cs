// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXArgumentMapper.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends.IL;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Constructs mappings for PTX kernels.
    /// </summary>
    public sealed class PTXArgumentMapper : PtrArrayViewImplementationArgumentMapper
    {
        #region Constants

        /// <summary>
        /// The name of length field in scope of a kernel argument.
        /// </summary>
        private const string KernelLengthFieldName = "IndexField";

        /// <summary>
        /// The general kernel field name prefix that is used
        /// for all declared types.
        /// </summary>
        private const string KernelFieldNamePrefix = "Data_";

        #endregion

        #region Nested Types

        /// <summary>
        /// Represents the general kernel target.
        /// </summary>
        private struct Target : IKernelTarget
        {
            /// <summary>
            /// Constructs a new kernel target.
            /// </summary>
            /// <param name="typeBuilder">The current tpye builder.</param>
            public Target(TypeBuilder typeBuilder)
            {
                Counter = 0;
                TypeBuilder = typeBuilder;
            }

            /// <summary>
            /// The current counter value.
            /// </summary>
            public int Counter { get; private set; }

            /// <summary>
            /// The current type builder.
            /// </summary>
            public TypeBuilder TypeBuilder { get; }

            /// <summary cref="KernelArgumentMapper.IKernelTarget.DeclareType"/>
            public int DeclareType(Type type)
            {
                var id = Counter++;
                TypeBuilder.DefineField(
                    KernelFieldNamePrefix + id,
                    type,
                    FieldAttributes.Public);
                return id;
            }
        }

        /// <summary>
        /// The internal entry point mapping of a Cuda kernel.
        /// </summary>
        internal sealed class EntryPointMapping : KernelArgumentMapping
        {
            #region Nested Types

            /// <summary>
            /// Represents a source value.
            /// </summary>
            private readonly struct Source : ISource
            {
                public Source(int index, bool isByRef)
                {
                    Index = index;
                    IsByRef = isByRef;
                }

                /// <summary>
                /// Returns the associated source index.
                /// </summary>
                public int Index { get; }

                /// <summary>
                /// Returns true if this argument is passed by reference.
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

            /// <summary>
            /// Represents a single variable target.
            /// </summary>
            private readonly struct Target : ITarget
            {
                public Target(ILLocal local, EntryPointMapping parent)
                {
                    Local = local;
                    Parent = parent;
                }

                /// <summary>
                /// Returns the associated temporary local variable.
                /// </summary>
                public ILLocal Local { get; }

                /// <summary>
                /// Returns the parent entry-point mapping.
                /// </summary>
                public EntryPointMapping Parent { get; }

                /// <summary cref="KernelArgumentMapper.ITarget.EmitLoadTarget{TILEmitter}(in TILEmitter, int)"/>
                public void EmitLoadTarget<TILEmitter>(
                    in TILEmitter emitter,
                    int id)
                    where TILEmitter : IILEmitter
                {
                    emitter.Emit(LocalOperation.LoadAddress, Local);
                    emitter.Emit(OpCodes.Ldflda, Parent.Fields[id]);
                }
            }

            #endregion

            #region Instance

            /// <summary>
            /// Constructs a new entry-point mapping.
            /// </summary>
            /// <param name="parameterSpecification">Information about all kernel parameters.</param>
            /// <param name="mappings">All nested mapping.</param>
            /// <param name="targetType">The target type.</param>
            /// <param name="numFields">The current number of fields.</param>
            internal EntryPointMapping(
                in EntryPoint.ParameterSpecification parameterSpecification,
                ImmutableArray<Mapping> mappings,
                Type targetType,
                int numFields)
                : base(parameterSpecification, mappings)
            {
                TargetType = targetType;
                KernelLengthField = targetType.GetField(KernelLengthFieldName);

                var fields = ImmutableArray.CreateBuilder<FieldInfo>(numFields);
                for (int i = 0; i < numFields; ++i)
                    fields.Add(targetType.GetField(KernelFieldNamePrefix + i));
                Fields = fields.MoveToImmutable();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the main target type.
            /// </summary>
            public Type TargetType { get; }

            /// <summary>
            /// Returns the argument size in bytes.
            /// </summary>
            public int ArgumentSize => TargetType.SizeOf();

            /// <summary>
            /// Returns the target kernel-length field.
            /// </summary>
            public FieldInfo KernelLengthField { get; }

            /// <summary>
            /// Returns all fields.
            /// </summary>
            public ImmutableArray<FieldInfo> Fields { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Emits this entry-point mapping to the given emitter.
            /// </summary>
            /// <typeparam name="TILEmitter">The target emitter type.</typeparam>
            /// <param name="emitter">The target emitter.</param>
            /// <param name="firstArgumentIndex">The index of the first kernel argument.</param>
            /// <returns>The emitted local variable.</returns>
            public ILLocal EmitEntryPointMapping<TILEmitter>(
                in TILEmitter emitter,
                int firstArgumentIndex)
                where TILEmitter : IILEmitter
            {
                Debug.Assert(firstArgumentIndex >= 0, "Invalid first argument index");
                // Entry point address is on the stack
                var targetLocal = emitter.DeclareLocal(TargetType);
                var target = new Target(targetLocal, this);

                for (int i = 0, e = Mappings.Length; i < e; ++i)
                {
                    var source = new Source(firstArgumentIndex + i, Parameters.IsByRef(i));
                    Mappings[i].EmitConversion(emitter, source, target);
                }

                return targetLocal;
            }

            #endregion
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new PTX argument mapper.
        /// </summary>
        /// <param name="context">The current context.</param>
        public PTXArgumentMapper(Context context)
            : base(context)
        { }

        #endregion

        #region Methods

        /// <summary cref="KernelArgumentMapper.CreateMapping(in EntryPoint.ParameterSpecification, Type)"/>
        public override KernelArgumentMapping CreateMapping(
            in EntryPoint.ParameterSpecification specification,
            Type nonGroupedIndexType)
        {
            var resultingType = Context.DefineRuntimeStruct();
            if (nonGroupedIndexType != null)
            {
                resultingType.DefineField(
                    KernelLengthFieldName,
                    nonGroupedIndexType,
                    FieldAttributes.Public);
            }

            var target = new Target(resultingType);
            var mappings = CreateMapping(ref target, specification);

            var structType = resultingType.CreateTypeInfo().AsType();
            return new EntryPointMapping(
                specification,
                mappings,
                structType,
                target.Counter);
        }


        #endregion
    }
}
