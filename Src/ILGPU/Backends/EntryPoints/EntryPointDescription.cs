// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: EntryPointDescription.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPU.Util;
using System;
using System.Collections.Immutable;
using System.Reflection;

namespace ILGPU.Backends.EntryPoints
{
    /// <summary>
    /// Specifies an entry point method including its associated index type.
    /// </summary>
    public readonly struct EntryPointDescription : IEquatable<EntryPointDescription>
    {
        #region Static

        /// <summary>
        /// Creates a new entry point description from the given method source that is
        /// compatible with explicitly grouped kernels.
        /// </summary>
        /// <param name="methodSource">The kernel method source.</param>
        /// <returns>The created entry point description.</returns>
        public static EntryPointDescription FromExplicitlyGroupedKernel(
            MethodInfo methodSource) =>
            new EntryPointDescription(methodSource, null, IndexType.KernelConfig);

        /// <summary>
        /// Creates a new entry point description from the given method source that is
        /// compatible with implicitly grouped kernels.
        /// </summary>
        /// <param name="methodSource">The kernel method source.</param>
        /// <returns>The created entry point description.</returns>
        public static EntryPointDescription FromImplicitlyGroupedKernel(
            MethodInfo methodSource)
        {
            if (methodSource == null)
                throw new ArgumentNullException(nameof(methodSource));
            var parameters = methodSource.GetParameters();
            if (parameters.Length < 1)
            {
                throw new NotSupportedException(
                    ErrorMessages.InvalidEntryPointIndexParameter);
            }

            // Try to get index type from first parameter
            var firstParamType = parameters[0].ParameterType;
            var indexType = firstParamType.GetIndexType();
            if (indexType == IndexType.None || indexType > IndexType.Index3D)
            {
                throw new NotSupportedException(
                    ErrorMessages.InvalidEntryPointIndexParameterOfWrongType);
            }
            return new EntryPointDescription(methodSource, parameters, indexType);
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new entry point description.
        /// </summary>
        /// <param name="methodSource">The method source.</param>
        /// <param name="parameters">The raw array of attached kernel parameters.</param>
        /// <param name="indexType">The index type.</param>
        internal EntryPointDescription(
            MethodInfo methodSource,
            ParameterInfo[] parameters,
            IndexType indexType)
        {
            if (indexType == IndexType.None)
                throw new ArgumentOutOfRangeException(nameof(indexType));
            MethodSource = methodSource ??
                throw new ArgumentNullException(nameof(methodSource));
            IndexType = indexType;

            parameters ??= methodSource.GetParameters();

            KernelIndexParameterOffset = IndexType == IndexType.KernelConfig ? 0 : 1;
            int maxNumParameters = parameters.Length - KernelIndexParameterOffset;
            var parameterTypes = ImmutableArray.CreateBuilder<Type>(maxNumParameters);
            for (int i = KernelIndexParameterOffset, e = parameters.Length; i < e; ++i)
            {
                var type = parameters[i].ParameterType;
                if (type.IsPointer || type.IsPassedViaPtr())
                {
                    throw new NotSupportedException(string.Format(
                        ErrorMessages.NotSupportedKernelParameterType,
                        type));
                }
                parameterTypes.Add(type);
            }
            Parameters = new ParameterCollection(parameterTypes.MoveToImmutable());

            Validate();
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the kernel method.
        /// </summary>
        public MethodInfo MethodSource { get; }

        /// <summary>
        /// Returns the name of the underlying entry point to be used in the scope of
        /// loaded runtime <see cref="Kernel"/> instances.
        /// </summary>
        public readonly string Name => KernelNameAttribute.GetKernelName(MethodSource);

        /// <summary>
        /// Returns the associated index type.
        /// </summary>
        public IndexType IndexType { get; }

        /// <summary>
        /// Returns all parameters.
        /// </summary>
        public ParameterCollection Parameters { get; }

        /// <summary>
        /// Returns the offset for the actual parameter values while taking an implicit
        /// index argument into account.
        /// </summary>
        public int KernelIndexParameterOffset { get; }

        /// <summary>
        /// Returns true if this entry point uses specialized parameters.
        /// </summary>
        public bool HasSpecializedParameters => Parameters.HasSpecializedParameters;

        #endregion

        #region Methods

        /// <summary>
        /// Validates this object and throws a <see cref="NotSupportedException"/> in
        /// the case of an unsupported kernel configuration.
        /// </summary>
        public void Validate()
        {
            if (MethodSource == null)
            {
                throw new NotSupportedException(
                    ErrorMessages.InvalidEntryPointWithoutDotNetMethod);
            }
            if (!MethodSource.IsStatic && !MethodSource.IsNotCapturingLambda())
            {
                throw new NotSupportedException(
                    ErrorMessages.InvalidEntryPointInstanceKernelMethod);
            }
            if (IndexType == IndexType.None)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedKernel);
            }
        }

        /// <summary>
        /// Creates a new launcher method.
        /// </summary>
        /// <param name="runtimeSystem">The current runtime system.</param>
        /// <param name="instanceType">The instance type (if any).</param>
        /// <param name="methodEmitter">The method emitter.</param>
        /// <returns>The acquired scoped lock.</returns>
        internal RuntimeSystem.ScopedLock CreateLauncherMethod(
            RuntimeSystem runtimeSystem,
            Type instanceType,
            out RuntimeSystem.MethodEmitter methodEmitter)
        {
            var parameterTypes = new Type[
                Parameters.Count + Kernel.KernelParameterOffset];

            // Launcher(Kernel, AcceleratorStream, [Index], ...)
            parameterTypes[Kernel.KernelInstanceParamIdx] =
                instanceType ?? typeof(Kernel);
            parameterTypes[Kernel.KernelStreamParamIdx] =
                typeof(AcceleratorStream);
            parameterTypes[Kernel.KernelParamDimensionIdx] =
                IndexType.GetManagedIndexType();
            Parameters.CopyTo(parameterTypes, Kernel.KernelParameterOffset);

            var writeScope = runtimeSystem.DefineRuntimeMethod(
                typeof(void),
                parameterTypes,
                out methodEmitter);
            // TODO: we have to port the following snippet to .Net Core
            // in order to support "in" parameters
            //if (Parameters.IsByRef(i))
            //{
            //    var paramIndex = Kernel.KernelParameterOffset + i;
            //    result.MethodBuilder.DefineParameter(
            //        paramIndex,
            //        ParameterAttributes.In,
            //        null);
            //}

            return writeScope;
        }

        /// <summary>
        /// Returns true if the given description is equal to the current one.
        /// </summary>
        /// <param name="other">The other description.</param>
        /// <returns>True, if the given cached key is equal to the current one.</returns>
        public bool Equals(EntryPointDescription other) =>
            other.MethodSource == MethodSource &&
            other.IndexType == IndexType;

        #endregion

        #region Object

        /// <summary>
        /// Returns true if the given object is equal to the current one.
        /// </summary>
        /// <param name="obj">The other object.</param>
        /// <returns>True, if the given object is equal to the current one.</returns>
        public override bool Equals(object obj) =>
            obj is EntryPointDescription other && Equals(other);

        /// <summary>
        /// Returns the hash code of this object.
        /// </summary>
        /// <returns>The hash code of this object.</returns>
        public override int GetHashCode() =>
            MethodSource.GetHashCode() ^ IndexType.GetHashCode();

        /// <summary>
        /// Returns the string representation of this object.
        /// </summary>
        /// <returns>The string representation of this object.</returns>
        public override string ToString() => $"{MethodSource}({IndexType})";

        #endregion

        #region Operators

        /// <summary>
        /// Returns true if the left and right descriptions are the same.
        /// </summary>
        /// <param name="left">The left description.</param>
        /// <param name="right">The right description.</param>
        /// <returns>True, if the left and right descriptions are the same.</returns>
        public static bool operator ==(
            EntryPointDescription left,
            EntryPointDescription right) =>
            left.Equals(right);

        /// <summary>
        /// Returns true if the left and right descriptions are not the same.
        /// </summary>
        /// <param name="left">The left description.</param>
        /// <param name="right">The right description.</param>
        /// <returns>True, if the left and right descriptions are not the same.</returns>
        public static bool operator !=(
            EntryPointDescription left,
            EntryPointDescription right) =>
            !(left == right);

        #endregion
    }
}
