// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2019 Marcel Koester
//                                www.ilgpu.net
//
// File: KernelLauncherArgument.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends;
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
        /// Emits code to create an Index instance from a loaded integer value.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="emitter">The target IL emitter.</param>
        public static void EmitLoadIndex<TEmitter>(in TEmitter emitter)
            where TEmitter : IILEmitter
        {
            emitter.EmitNewObject(Index.MainConstructor);
        }

        /// <summary>
        /// Emits code for computing and loading the required shared-memory size.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="entryPoint">The entry point for code generation.</param>
        /// <param name="emitter">The target IL emitter.</param>
        public static ILLocal EmitSharedMemorySizeComputation<TEmitter>(
            EntryPoint entryPoint,
            in TEmitter emitter)
            where TEmitter : IILEmitter
        {
            // Compute sizes of dynamic-shared variables
            var sharedMemSize = emitter.DeclareLocal(typeof(int));
            emitter.EmitConstant(entryPoint.SharedMemorySize);
            emitter.Emit(LocalOperation.Store, sharedMemSize);

            return sharedMemSize;
        }

        /// <summary>
        /// Stores all getter methods to resolve all index values of an <see cref="Index3"/>.
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
        /// <param name="indexType">The index type (can be Index, Index2 or Index3).</param>
        /// <returns>The main constructor.</returns>
        private static ConstructorInfo GetMainIndexConstructor(Type indexType)
        {
            return (ConstructorInfo)indexType.GetField(
                nameof(Index.MainConstructor),
                BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
        }

        /// <summary>
        /// Emits code to convert an Index3 to a specific target type.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="ungroupedIndexType">The index type (can be Index, Index2 or Index3).</param>
        /// <param name="emitter">The target IL emitter.</param>
        /// <param name="loadIdx">A callback to load the referenced index value onto the stack.</param>
        public static void EmitConvertIndex3ToTargetType<TEmitter>(
            Type ungroupedIndexType,
            in TEmitter emitter,
            Action loadIdx)
            where TEmitter : IILEmitter
        {
            var numValues = (int)ungroupedIndexType.GetUngroupedIndexType();
            if (numValues > 3)
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedIndexType);
            var idxLocal = emitter.DeclareLocal(typeof(Index3));
            for (int i = 0; i < numValues; ++i)
            {
                loadIdx();

                emitter.Emit(LocalOperation.Store, idxLocal);
                emitter.Emit(LocalOperation.LoadAddress, idxLocal);
                emitter.EmitCall(Index3ValueGetter[i]);
            }
            var mainConstructor = GetMainIndexConstructor(ungroupedIndexType);
            emitter.EmitNewObject(mainConstructor);
        }

        /// <summary>
        /// Emits code to convert a linear index to a specific target type.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="ungroupedIndexType">The index type (can be Index, Index2 or Index3).</param>
        /// <param name="emitter">The target IL emitter.</param>
        /// <param name="loadDimension">A callback to load the referenced dimension value onto the stack.</param>
        public static void EmitConvertFrom1DIndexToTargetIndexType<TEmitter>(
            Type ungroupedIndexType,
            in TEmitter emitter,
            Action loadDimension)
            where TEmitter : IILEmitter
        {
            switch (ungroupedIndexType.GetUngroupedIndexType())
            {
                case IndexType.Index1D:
                    // Invoke default constructor of the index class
                    emitter.EmitNewObject(
                        GetMainIndexConstructor(ungroupedIndexType));
                    break;
                case IndexType.Index2D:
                    loadDimension();
                    emitter.EmitCall(
                        ungroupedIndexType.GetMethod(
                            nameof(Index2.ReconstructIndex),
                            BindingFlags.Public | BindingFlags.Static));
                    break;
                case IndexType.Index3D:
                    loadDimension();
                    emitter.EmitCall(
                        ungroupedIndexType.GetMethod(
                            nameof(Index3.ReconstructIndex),
                            BindingFlags.Public | BindingFlags.Static));
                    break;
                default:
                    throw new NotSupportedException(RuntimeErrorMessages.NotSupportedIndexType);
            }
        }

        /// <summary>
        /// Emits code to load a 3D dimension of a grid or a group index.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="indexType">The index type (can be Index, Index2 or Index3).</param>
        /// <param name="emitter">The target IL emitter.</param>
        /// <param name="loadIdx">A callback to load the referenced index value onto the stack.</param>
        private static void EmitLoadDimensions<TEmitter>(
            Type indexType,
            in TEmitter emitter,
            Action loadIdx)
            where TEmitter : IILEmitter
        {
            EmitLoadDimensions(indexType, emitter, loadIdx, offset => { });
        }

        /// <summary>
        /// Emits code to load a 3D dimension of a grid or a group index.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="indexType">The index type (can be Index, Index2 or Index3).</param>
        /// <param name="emitter">The target IL emitter.</param>
        /// <param name="loadIdx">A callback to load the referenced index value onto the stack.</param>
        /// <param name="manipulateIdx">A callback to manipulate the loaded index of a given dimension.</param>
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
        /// Emits a kernel-dimension configuration.
        /// It pushes 6 integers onto the evaluation stack (gridIdx.X, gridIdx.Y, ...) by default.
        /// Howerver, using a custom <paramref name="convertIndex3Args"/> function allows to create
        /// and instantiate custom grid and group indices.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="emitter">The target IL emitter.</param>
        /// <param name="dimensionIdx">The argument index of the provided launch-dimension index.</param>
        /// <param name="convertIndex3Args">Allows to create and instantiate custom grid and group indices.</param>
        public static void EmitLoadDimensions<TEmitter>(
            EntryPoint entryPoint,
            in TEmitter emitter,
            int dimensionIdx,
            Action convertIndex3Args)
            where TEmitter : IILEmitter
        {
            EmitLoadDimensions(
                entryPoint,
                emitter,
                dimensionIdx,
                convertIndex3Args,
                0);
        }

        /// <summary>
        /// Emits a kernel-dimension configuration.
        /// It pushes 6 integers onto the evaluation stack (gridIdx.X, gridIdx.Y, ...) by default.
        /// Howerver, using a custom <paramref name="convertIndex3Args"/> function allows to create
        /// and instantiate custom grid and group indices.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="emitter">The target IL emitter.</param>
        /// <param name="dimensionIdx">The argument index of the provided launch-dimension index.</param>
        /// <param name="convertIndex3Args">Allows to create and instantiate custom grid and group indices.</param>
        /// <param name="customGroupSize">The custom group size used for automatic blocking.</param>
        public static void EmitLoadDimensions<TEmitter>(
            EntryPoint entryPoint,
            TEmitter emitter,
            int dimensionIdx,
            Action convertIndex3Args,
            int customGroupSize)
            where TEmitter : IILEmitter
        {
            if (!entryPoint.IsGroupedIndexEntry)
            {
                Debug.Assert(customGroupSize >= 0, "Invalid custom group size");

                // Note that the custom group size is currently limited
                // to the X dimension of the scheduled grid.
                // This is no limitation of the current PTX backend.

                EmitLoadDimensions(
                    entryPoint.UngroupedIndexType,
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

                convertIndex3Args();

                // Custom grouping
                emitter.EmitConstant(Math.Max(customGroupSize, 1));
                emitter.Emit(OpCodes.Ldc_I4_1);
                emitter.Emit(OpCodes.Ldc_I4_1);

                convertIndex3Args();
            }
            else
            {
                Debug.Assert(customGroupSize == 0, "Invalid custom group size");

                var groupedIndexType = entryPoint.KernelIndexType;
                var gridIdx = emitter.DeclareLocal(entryPoint.UngroupedIndexType);

                emitter.Emit(ArgumentOperation.LoadAddress, dimensionIdx);
                emitter.EmitCall(
                    groupedIndexType.GetProperty(
                        nameof(GroupedIndex.GridIdx),
                        BindingFlags.Public | BindingFlags.Instance).GetGetMethod(false));
                emitter.Emit(LocalOperation.Store, gridIdx);
                EmitLoadDimensions(
                    entryPoint.UngroupedIndexType,
                    emitter,
                    () => emitter.Emit(LocalOperation.LoadAddress, gridIdx));
                convertIndex3Args();

                var groupIdx = emitter.DeclareLocal(entryPoint.UngroupedIndexType);

                emitter.Emit(ArgumentOperation.LoadAddress, dimensionIdx);
                emitter.EmitCall(
                    groupedIndexType.GetProperty(
                        nameof(GroupedIndex.GroupIdx),
                        BindingFlags.Public | BindingFlags.Instance).GetGetMethod(false));
                emitter.Emit(LocalOperation.Store, groupIdx);
                EmitLoadDimensions(
                    entryPoint.UngroupedIndexType,
                    emitter,
                    () => emitter.Emit(LocalOperation.LoadAddress, groupIdx));
                convertIndex3Args();
            }
        }

        /// <summary>
        /// Emits code for loading a typed kernel from a generic kernel instance.
        /// </summary>
        /// <typeparam name="TEmitter">The emitter type.</typeparam>
        /// <typeparam name="T">The kernel type.</typeparam>
        /// <param name="kernelArgumentIndex">The index of the launcher parameter.</param>
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
        /// Emits code for loading a typed accelerator stream from a generic accelerator-stream instance.
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
