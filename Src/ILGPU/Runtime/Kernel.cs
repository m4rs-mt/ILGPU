// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: Kernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends;
using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.IL;
using ILGPU.IR;
using ILGPU.Resources;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Represents the base class for all runtime kernels.
    /// </summary>
    /// <remarks>Members of this class are not thread safe.</remarks>
    public abstract class Kernel : AcceleratorObject
    {
        #region Constants

        internal const int KernelInstanceParamIdx = 0;
        internal const int KernelStreamParamIdx = 1;
        internal const int KernelParamDimensionIdx = 2;
        internal const int KernelParameterOffset = KernelParamDimensionIdx + 1;

        #endregion

        #region Static

        /// <summary>
        /// Implements a <see cref="ISpecializationCacheArgs"/> interface in order to
        /// make the given <paramref name="typeBuilder"/> compatible with a
        /// <see cref="SpecializationCache{TLoader, TArgs, TDelegate}"/> instance.
        /// </summary>
        /// <param name="typeBuilder">The target type builder to use.</param>
        /// <param name="fields">The source fields used for implementation.</param>
        private static void ImplementSpecializationCacheArgs(
            TypeBuilder typeBuilder,
            FieldInfo[] fields)
        {
            var specializedType = typeof(SpecializedValue<>);
            var getArgMethod = typeBuilder.DefineMethod(
                nameof(ISpecializationCacheArgs.GetSpecializedArg),
                MethodAttributes.Public | MethodAttributes.Virtual,
                typeof(object),
                new Type[] { typeof(int) });

            var emitter = new ILEmitter(getArgMethod.GetILGenerator());

            // Declare labels and emit jump table
            var labels = new ILLabel[fields.Length];
            for (int i = 0, e = fields.Length; i < e; ++i)
                labels[i] = emitter.DeclareLabel();
            emitter.Emit(OpCodes.Ldarg_1);
            emitter.EmitSwitch(labels);
            for (int i = 0, e = fields.Length; i < e; ++i)
            {
                var field = fields[i];
                emitter.MarkLabel(labels[i]);
                emitter.Emit(OpCodes.Ldarg_0);
                emitter.Emit(OpCodes.Ldfld, field);

                // Wrap in a specialized instance
                var fieldReturnType = specializedType.MakeGenericType(field.FieldType);
                var instanceConstructor = fieldReturnType.GetConstructor(
                    new Type[]
                    {
                        field.FieldType
                    });
                emitter.EmitNewObject(instanceConstructor);

                emitter.Emit(OpCodes.Box, fieldReturnType);
                emitter.Emit(OpCodes.Ret);
            }

            // Return dummy argument
            emitter.Emit(OpCodes.Ldnull);
            emitter.Emit(OpCodes.Ret);
            emitter.Finish();

            typeBuilder.AddInterfaceImplementation(typeof(ISpecializationCacheArgs));
        }

        /// <summary>
        /// Creates a launcher delegate that uses the
        /// <see cref="SpecializationCache{TLoader, TArgs, TDelegate}"/> to create
        /// dynamically specialized kernels.
        /// </summary>
        /// <typeparam name="TLoader">The associated loader type.</typeparam>
        /// <typeparam name="TDelegate">The launcher delegate type.</typeparam>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="entry">The entry point to compile into a kernel.</param>
        /// <param name="specialization">The kernel specialization.</param>
        /// <param name="kernelMethod">The kernel IR method.</param>
        /// <param name="loader">The loader instance.</param>
        /// <returns>
        /// A dynamic kernel launcher that automatically specializes kernels.
        /// </returns>
        public static TDelegate CreateSpecializedLauncher<TDelegate, TLoader>(
            Accelerator accelerator,
            in EntryPointDescription entry,
            in KernelSpecialization specialization,
            Method kernelMethod,
            in TLoader loader)
            where TDelegate : Delegate
            where TLoader : struct, Accelerator.IKernelLoader
        {
            Debug.Assert(entry.HasSpecializedParameters);
            var runtimeSystem = accelerator.Context.RuntimeSystem;

            // Build customized runtime structure
            var keyStruct = CreateSpecializedLauncherStruct(runtimeSystem, entry);

            // Create new structure instance and assign fields
            var cacheType = typeof(SpecializationCache<,,>).MakeGenericType(
                typeof(TLoader),
                keyStruct,
                typeof(TDelegate));

            // Create the specialized launcher method
            var launcherMethod = CreateSpecializedLauncherMethod<TDelegate>(
                runtimeSystem,
                entry,
                keyStruct,
                cacheType);

            // Build launcher delegate
            var cacheInstance = Activator.CreateInstance(
                cacheType,
                accelerator,
                kernelMethod,
                loader,
                entry,
                specialization);
            return launcherMethod.CreateDelegate(
                typeof(TDelegate),
                cacheInstance) as TDelegate;
        }

        /// <summary>
        /// Creates a specialized launcher method that uses the
        /// <see cref="SpecializationCache{TLoader, TArgs, TDelegate}"/>.
        /// </summary>
        /// <typeparam name="TDelegate">The launcher delegate type.</typeparam>
        /// <param name="runtimeSystem">The current runtime system.</param>
        /// <param name="entry">The entry point to compile into a kernel.</param>
        /// <param name="keyStruct">The key struct.</param>
        /// <param name="cacheType">The parent cache type.</param>
        /// <returns>The specialized launcher method.</returns>
        private static MethodInfo CreateSpecializedLauncherMethod<TDelegate>(
            RuntimeSystem runtimeSystem,
            in EntryPointDescription entry,
            Type keyStruct,
            Type cacheType)
            where TDelegate : Delegate
        {
            var specializedParameters = entry.Parameters.SpecializedParameters;

            using var writeScope = entry.CreateLauncherMethod(
                runtimeSystem,
                cacheType,
                out var method);
            var emitter = new ILEmitter(method.ILGenerator);

            var keyVariable = emitter.DeclareLocal(keyStruct);
            emitter.Emit(LocalOperation.LoadAddress, keyVariable);
            emitter.Emit(OpCodes.Initobj, keyStruct);

            // Assign all fields
            var fields = keyStruct.GetFields();
            for (int i = 0, e = fields.Length; i < e; ++i)
            {
                // Load target field address
                emitter.Emit(LocalOperation.LoadAddress, keyVariable);

                // Load the associated argument address and extract the value to
                // specialize for
                var param = specializedParameters[i];
                emitter.Emit(
                    ArgumentOperation.LoadAddress,
                    KernelParameterOffset + param.Index);

                var valueProperty = param.SpecializedType.GetProperty(
                    nameof(SpecializedValue<int>.Value),
                    BindingFlags.Public | BindingFlags.Instance);
                emitter.EmitCall(valueProperty.GetGetMethod());

                // Store value
                emitter.Emit(OpCodes.Stfld, fields[i]);
            }

            // Resolve kernel and dispatch it
            var getOrCreateMethod = cacheType.GetMethod(
                "GetOrCreateKernel",
                BindingFlags.Public | BindingFlags.Instance);
            emitter.Emit(ArgumentOperation.Load, KernelInstanceParamIdx);
            emitter.Emit(LocalOperation.Load, keyVariable);
            emitter.EmitCall(getOrCreateMethod);

            // Load all arguments
            emitter.Emit(ArgumentOperation.Load, KernelStreamParamIdx);
            emitter.Emit(ArgumentOperation.Load, KernelParamDimensionIdx);
            for (int i = 0, e = entry.Parameters.Count; i < e; ++i)
                emitter.Emit(ArgumentOperation.Load, i + KernelParameterOffset);

            // Dispatch kernel
            var invokeMethod = typeof(TDelegate).GetMethod(
                "Invoke",
                BindingFlags.Public | BindingFlags.Instance);
            emitter.EmitCall(invokeMethod);

            // Return
            emitter.Emit(OpCodes.Ret);
            return method.Finish();
        }

        /// <summary>
        /// Creates a specialized launcher struct to be used with a dictionary cache.
        /// </summary>
        /// <param name="runtimeSystem">The current runtime system.</param>
        /// <param name="entry">The entry point to compile into a kernel.</param>
        /// <returns>The key kernel type.</returns>
        private static Type CreateSpecializedLauncherStruct(
            RuntimeSystem runtimeSystem,
            in EntryPointDescription entry)
        {
            var specializedParameters = entry.Parameters.SpecializedParameters;
            var fieldBuilders = new List<FieldInfo>(specializedParameters.Length);

            using var scopedLock = runtimeSystem.DefineRuntimeStruct(
                out var keyStructBuilder);
            foreach (var param in specializedParameters)
            {
                fieldBuilders.Add(keyStructBuilder.DefineField(
                    "Key" + param.Index,
                    param.ParameterType,
                    FieldAttributes.Public));
            }
            var sourceFields = fieldBuilders.ToArray();

            // Define equals and hash code functions
            keyStructBuilder.GenerateEqualsAndHashCode(sourceFields);

            // Implement ISpecializationCacheArgs interface
            ImplementSpecializationCacheArgs(keyStructBuilder, sourceFields);

            return keyStructBuilder.CreateType();
        }

        #endregion

        #region Instance

        /// <summary>
        /// Constructs a new kernel.
        /// </summary>
        /// <param name="accelerator">The associated accelerator.</param>
        /// <param name="compiledKernel">The source kernel.</param>
        /// <param name="launcher">The launcher method for the given kernel.</param>
        protected Kernel(
            Accelerator accelerator,
            CompiledKernel compiledKernel,
            MethodInfo launcher)
            : base(accelerator)
        {
            Debug.Assert(compiledKernel != null, "Invalid compiled kernel");
            Specialization = compiledKernel.Specialization;
            Launcher = launcher;
            NumParameters = compiledKernel.EntryPoint.Parameters.Count;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated kernel launcher.
        /// </summary>
        public MethodInfo Launcher { get; internal set; }

        /// <summary>
        /// Returns the associated specialization.
        /// </summary>
        public KernelSpecialization Specialization { get; }

        /// <summary>
        /// Returns the number of uniform parameters.
        /// </summary>
        public int NumParameters { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Creates a launcher delegate for this kernel.
        /// </summary>
        /// <typeparam name="TDelegate">The delegate type.</typeparam>
        /// <returns>The created delegate.</returns>
        /// <remarks>Note that the first argument is the accelerator stream.</remarks>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public TDelegate CreateLauncherDelegate<TDelegate>()
            where TDelegate : Delegate =>
            Launcher.CreateDelegate(typeof(TDelegate), this) as object as TDelegate;

        /// <summary>
        /// Invokes the associated launcher via reflection.
        /// </summary>
        /// <typeparam name="T">The index type T.</typeparam>
        /// <param name="dimension">The grid dimension.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="args">The kernel arguments.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private void InvokeLauncher<T>(
            T dimension,
            AcceleratorStream stream,
            object[] args)
            where T : IIndex
        {
            if (NumParameters != args.Length)
            {
                throw new ArgumentException(
                    RuntimeErrorMessages.InvalidNumberOfUniformArgs);
            }

            var reflectionArgs = new object[KernelParameterOffset + args.Length];
            reflectionArgs[KernelInstanceParamIdx] = this;
            reflectionArgs[KernelStreamParamIdx] = stream
                ?? throw new ArgumentNullException(nameof(stream));
            reflectionArgs[KernelParamDimensionIdx] = dimension;
            args.CopyTo(reflectionArgs, KernelParameterOffset);
            Launcher.Invoke(null, reflectionArgs);
        }

        #endregion

        #region Launch Methods

        /// <summary>
        /// Launches the current kernel with the given arguments.
        /// </summary>
        /// <typeparam name="TIndex">The index type.</typeparam>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="dimension">The grid dimension.</param>
        /// <param name="args">The kernel arguments.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Launch<TIndex>(
            AcceleratorStream stream,
            TIndex dimension,
            params object[] args)
            where TIndex : struct, IIndex =>
            InvokeLauncher(dimension, stream, args);

        /// <summary>
        /// Launches the current kernel with the given arguments.
        /// </summary>
        /// <param name="dimension">The grid dimension.</param>
        /// <param name="stream">The accelerator stream.</param>
        /// <param name="args">The kernel arguments.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Launch(
            AcceleratorStream stream,
            int dimension,
            params object[] args) =>
            InvokeLauncher(new Index1(dimension), stream, args);

        #endregion
    }

    /// <summary>
    /// Contains utility methods to work with kernel objects.
    /// </summary>
    public static class KernelUtil
    {
        /// <summary>
        /// Tries to resolve a kernel object from a previously created kernel delegate.
        /// </summary>
        /// <typeparam name="TDelegate">The kernel-delegate type.</typeparam>
        /// <param name="kernelDelegate">The kernel-delegate instance.</param>
        /// <param name="kernel">The resolved kernel object (if any).</param>
        /// <returns>True, if a kernel object could be resolved.</returns>
        public static bool TryGetKernel<TDelegate>(
            this TDelegate kernelDelegate,
            out Kernel kernel)
            where TDelegate : Delegate =>
            (kernel = kernelDelegate.Target as Kernel) != null;

        /// <summary>
        /// Resolves a kernel object from a previously created kernel delegate.
        /// If this is not possible, the method will throw an
        /// <see cref="InvalidOperationException"/>.
        /// </summary>
        /// <typeparam name="TDelegate">The kernel-delegate type.</typeparam>
        /// <param name="kernelDelegate">The kernel-delegate instance.</param>
        /// <returns>The resolved kernel object.</returns>
        public static Kernel GetKernel<TDelegate>(this TDelegate kernelDelegate)
            where TDelegate : Delegate =>
            kernelDelegate.TryGetKernel(out var kernel)
            ? kernel
            : throw new InvalidOperationException();
    }
}
