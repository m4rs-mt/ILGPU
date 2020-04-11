// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: KernelLauncherArgument.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.Backends.EntryPoints;
using ILGPU.Backends.IL;
using ILGPU.Resources;
using System;
using System.Diagnostics;
using System.Reflection;
using System.Reflection.Emit;

namespace ILGPU.Runtime
{
    /// <summary>
    /// Builder methods for kernel launchers.
    /// </summary>
    static class KernelLauncherBuilder
    {
        #region Methods

        /// <summary>
        /// Stores all getter methods to resolve all index values of an
        /// <see cref="Index3"/>.
        /// </summary>
        private static readonly MethodInfo[] Index3ValueGetter =
        {
            typeof(Index3).GetProperty(
                nameof(Index3.X),
                BindingFlags.Public | BindingFlags.Instance).GetGetMethod(false),
            typeof(Index3).GetProperty(
                nameof(Index3.Y),
                BindingFlags.Public | BindingFlags.Instance).GetGetMethod(false),
            typeof(Index3).GetProperty(
                nameof(Index3.Z),
                BindingFlags.Public | BindingFlags.Instance).GetGetMethod(false),
        };

        /// <summary>
        /// Resolves the main constructor of the given index type.
        /// </summary>
        /// <param name="indexType">
        /// The index type (can be Index, Index2 or Index3).
        /// </param>
        /// <returns>The main constructor.</returns>
        private static ConstructorInfo GetMainIndexConstructor(Type indexType) =>
            (ConstructorInfo)indexType.GetField(
                nameof(Index1.MainConstructor),
                BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);

        /// <summary>
        /// Emits code to convert an Index3 to a specific target type.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="indexType">
        /// The index type (can be Index, Index2 or Index3).
        /// </param>
        /// <param name="emitter">The target IL emitter.</param>
        /// <param name="loadIdx">
        /// A callback to load the referenced index value onto the stack.
        /// </param>
        public static void EmitConvertIndex3ToTargetType<TEmitter>(
            IndexType indexType,
            in TEmitter emitter,
            Action loadIdx)
            where TEmitter : IILEmitter
        {
            var numValues = (int)indexType;
            if (numValues > 3)
            {
                throw new NotSupportedException(
                    RuntimeErrorMessages.NotSupportedIndexType);
            }

            var idxLocal = emitter.DeclareLocal(typeof(Index3));
            for (int i = 0; i < numValues; ++i)
            {
                loadIdx();

                emitter.Emit(LocalOperation.Store, idxLocal);
                emitter.Emit(LocalOperation.LoadAddress, idxLocal);
                emitter.EmitCall(Index3ValueGetter[i]);
            }
            var mainConstructor = GetMainIndexConstructor(
                indexType.GetManagedIndexType());
            emitter.EmitNewObject(mainConstructor);
        }

        /// <summary>
        /// Emits code to convert a linear index to a specific target type.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="indexType">
        /// The index type (can be Index, Index2 or Index3).
        /// </param>
        /// <param name="emitter">The target IL emitter.</param>
        /// <param name="loadDimension">
        /// A callback to load the referenced dimension value onto the stack.
        /// </param>
        public static void EmitConvertFrom1DIndexToTargetIndexType<TEmitter>(
            IndexType indexType,
            in TEmitter emitter,
            Action loadDimension)
            where TEmitter : IILEmitter
        {
            var managedIndexType = indexType.GetManagedIndexType();
            switch (indexType)
            {
                case IndexType.Index1D:
                    // Invoke default constructor of the index class
                    emitter.EmitNewObject(
                        GetMainIndexConstructor(managedIndexType));
                    break;
                case IndexType.Index2D:
                    loadDimension();
                    emitter.EmitCall(
                        managedIndexType.GetMethod(
                            nameof(Index2.ReconstructIndex),
                            BindingFlags.Public | BindingFlags.Static));
                    break;
                case IndexType.Index3D:
                    loadDimension();
                    emitter.EmitCall(
                        managedIndexType.GetMethod(
                            nameof(Index3.ReconstructIndex),
                            BindingFlags.Public | BindingFlags.Static));
                    break;
                default:
                    throw new NotSupportedException(
                        RuntimeErrorMessages.NotSupportedIndexType);
            }
        }

        /// <summary>
        /// Emits code to load a 3D dimension of a grid or a group index.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="indexType">
        /// The index type (can be Index, Index2 or Index3).
        /// </param>
        /// <param name="emitter">The target IL emitter.</param>
        /// <param name="loadIdx">
        /// A callback to load the referenced index value onto the stack.
        /// </param>
        private static void EmitLoadDimensions<TEmitter>(
            Type indexType,
            in TEmitter emitter,
            Action loadIdx)
            where TEmitter : IILEmitter =>
            EmitLoadDimensions(indexType, emitter, loadIdx, offset => { });

        /// <summary>
        /// Emits code to load a 3D dimension of a grid or a group index.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="indexType">
        /// The index type (can be Index, Index2 or Index3).
        /// </param>
        /// <param name="emitter">The target IL emitter.</param>
        /// <param name="loadIdx">
        /// A callback to load the referenced index value onto the stack.
        /// </param>
        /// <param name="manipulateIdx">
        /// A callback to manipulate the loaded index of a given dimension.
        /// </param>
        private static void EmitLoadDimensions<TEmitter>(
            Type indexType,
            in TEmitter emitter,
            Action loadIdx,
            Action<int> manipulateIdx)
            where TEmitter : IILEmitter
        {
            var indexFieldGetter = new MethodInfo[]
            {
                indexType.GetProperty(
                    nameof(Index3.X),
                    BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod(false),
                indexType.GetProperty(
                    nameof(Index3.Y),
                    BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod(false),
                indexType.GetProperty(
                    nameof(Index3.Z),
                    BindingFlags.Public | BindingFlags.Instance)?.GetGetMethod(false),
            };

            // Load field indices
            int offset = 0;
            for (int e = indexFieldGetter.Length; offset < e; ++offset)
            {
                var fieldGetter = indexFieldGetter[offset];
                if (fieldGetter == null)
                    break;
                loadIdx();
                emitter.EmitCall(fieldGetter);
                manipulateIdx(offset);
            }

            // Fill empty zeros
            for (; offset < indexFieldGetter.Length; ++offset)
                emitter.Emit(OpCodes.Ldc_I4_1);
        }

        /// <summary>
        /// Emits code for loading a <see cref="SharedMemorySpecification"/> instance.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="entryPoint">The entry point for code generation.</param>
        /// <param name="emitter">The target IL emitter.</param>
        public static void EmitSharedMemorySpeficiation<TEmitter>(
            EntryPoint entryPoint,
            in TEmitter emitter)
            where TEmitter : IILEmitter
        {
            emitter.EmitConstant(entryPoint.SharedMemory.StaticSize);
            emitter.Emit(
                entryPoint.SharedMemory.HasDynamicMemory ?
                OpCodes.Ldc_I4_1 :
                OpCodes.Ldc_I4_0);
            emitter.EmitNewObject(SharedMemorySpecification.Constructor);
        }

        /// <summary>
        /// Emits a kernel-dimension configuration. In the case of an ungrouped index
        /// type, all arguments will be transformed into a <see cref="KernelConfig"/>
        /// instance. Otherwise, the passed kernel configuration will be used without
        /// any modifications.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="emitter">The target IL emitter.</param>
        /// <param name="dimensionIdx">
        /// The argument index of the provided launch-dimension index.
        /// </param>
        /// <param name="customGroupSize">
        /// The custom group size used for automatic blocking.
        /// </param>
        public static void EmitLoadKernelConfig<TEmitter>(
            EntryPoint entryPoint,
            TEmitter emitter,
            int dimensionIdx,
            int customGroupSize = 0)
            where TEmitter : IILEmitter
        {
            if (entryPoint.IsImplictlyGrouped)
            {
                Debug.Assert(customGroupSize >= 0, "Invalid custom group size");

                // Note that the custom group size is currently limited
                // to the X dimension of the scheduled grid.
                // This is no limitation of the current PTX backend.

                EmitLoadDimensions(
                    entryPoint.KernelIndexType,
                    emitter,
                    () => emitter.Emit(ArgumentOperation.LoadAddress, dimensionIdx),
                    dimIdx =>
                    {
                        if (dimIdx != 0 || customGroupSize < 1)
                            return;
                        // Convert requested index range to blocked range
                        emitter.EmitConstant(customGroupSize - 1);
                        emitter.Emit(OpCodes.Add);
                        emitter.EmitConstant(customGroupSize);
                        emitter.Emit(OpCodes.Div);
                    });

                // Custom grouping
                emitter.EmitConstant(Math.Max(customGroupSize, 1));
                emitter.Emit(OpCodes.Ldc_I4_1);
                emitter.Emit(OpCodes.Ldc_I4_1);

                emitter.EmitNewObject(KernelConfig.ImplicitlyGroupedKernelConstructor);
            }
            else
            {
                Debug.Assert(customGroupSize == 0, "Invalid custom group size");

                emitter.Emit(ArgumentOperation.Load, dimensionIdx);
            }
        }

        /// <summary>
        /// Emits a new runtime kernel configuration.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="emitter">The target IL emitter.</param>
        /// <param name="dimensionIdx">
        /// The argument index of the provided launch-dimension index.
        /// </param>
        /// <param name="customGroupSize">
        /// The custom group size used for automatic blocking.
        /// </param>
        public static void EmitLoadRuntimeKernelConfig<TEmitter>(
            EntryPoint entryPoint,
            TEmitter emitter,
            int dimensionIdx,
            int customGroupSize = 0)
            where TEmitter : IILEmitter
        {
            EmitLoadKernelConfig(
                entryPoint,
                emitter,
                dimensionIdx,
                customGroupSize);
            EmitSharedMemorySpeficiation(entryPoint, emitter);

            emitter.EmitNewObject(RuntimeKernelConfig.Constructor);
        }

        /// <summary>
        /// Emits code for loading a typed kernel from a generic kernel instance.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <typeparam name="T">The kernel type.</typeparam>
        /// <param name="kernelArgumentIndex">
        /// The index of the launcher parameter.
        /// </param>
        /// <param name="emitter">The target IL emitter.</param>
        public static void EmitLoadKernelArgument<T, TEmitter>(
            int kernelArgumentIndex,
            in TEmitter emitter)
            where T : Kernel
            where TEmitter : IILEmitter
        {
            Debug.Assert(kernelArgumentIndex >= 0);
            Debug.Assert(emitter != null);

            emitter.Emit(ArgumentOperation.Load, kernelArgumentIndex);
            emitter.Emit(OpCodes.Castclass, typeof(T));
        }

        /// <summary>
        /// Emits code for loading a typed accelerator stream from a generic
        /// accelerator-stream instance.
        /// </summary>
        /// <typeparam name="T">The kernel type.</typeparam>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="streamArgumentIndex">The index of the stream parameter.</param>
        /// <param name="emitter">The target IL emitter.</param>
        public static void EmitLoadAcceleratorStream<T, TEmitter>(
            int streamArgumentIndex,
            in TEmitter emitter)
            where T : AcceleratorStream
            where TEmitter : IILEmitter
        {
            Debug.Assert(streamArgumentIndex >= 0);
            Debug.Assert(emitter != null);

            emitter.Emit(ArgumentOperation.Load, streamArgumentIndex);
            emitter.Emit(OpCodes.Castclass, typeof(T));
        }

        #endregion
    }
}
