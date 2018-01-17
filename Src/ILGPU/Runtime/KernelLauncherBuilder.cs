// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: KernelLauncherArgument.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.Backends;
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
        /// <param name="ilGenerator">The target IL-instruction generator.</param>
        public static void EmitLoadIndex(ILGenerator ilGenerator)
        {
            ilGenerator.Emit(OpCodes.Newobj, Index.MainConstructor);
        }

        /// <summary>
        /// Emits code for computing and loading the required shared-memory size.
        /// </summary>
        /// <param name="entryPoint">The entry point for code generation.</param>
        /// <param name="ilGenerator">The target IL-instruction generator.</param>
        /// <param name="getDynSharedMemSizeParam">A function that resolves the length parameter for the referenced dynamically-sized shared-memory variable.</param>
        public static LocalBuilder EmitSharedMemorySizeComputation(
            EntryPoint entryPoint,
            ILGenerator ilGenerator,
            Func<int, ParameterInfo> getDynSharedMemSizeParam)
        {
            var dynSizedSharedMemVars = entryPoint.NumDynamicallySizedSharedMemoryVariables;

            // Compute sizes of dynamic-shared variables
            var sharedMemSize = ilGenerator.DeclareLocal(typeof(int));

            // Compute known shared-memory size
            int staticSharedMemSize = 0;
            foreach(var sharedMemVariable in entryPoint.SharedMemoryVariables)
            {
                if (sharedMemVariable.IsDynamicallySizedArray)
                    continue;
                staticSharedMemSize += sharedMemVariable.Size;
            }

            ilGenerator.Emit(OpCodes.Ldc_I4, staticSharedMemSize);
            ilGenerator.Emit(OpCodes.Stloc, sharedMemSize);

            // Emit code to compute the required amount of dynamic shared memory.
            for (int i = 0; i < dynSizedSharedMemVars; ++i)
            {
                // Compute byte size for shared-mem variable i
                var param = getDynSharedMemSizeParam(i + entryPoint.NumUniformVariables);
                ilGenerator.Emit(OpCodes.Ldarg, param.Position);
                ilGenerator.Emit(OpCodes.Ldc_I4, entryPoint.SharedMemoryVariables[i].ElementSize);
                ilGenerator.Emit(OpCodes.Mul);

                ilGenerator.Emit(OpCodes.Ldloc, sharedMemSize);
                ilGenerator.Emit(OpCodes.Add);

                ilGenerator.Emit(OpCodes.Stloc, sharedMemSize);
            }

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
        /// <param name="ungroupedIndexType">The index type (can be Index, Index2 or Index3).</param>
        /// <param name="ilGenerator">The target IL-instruction generator.</param>
        /// <param name="loadIdx">A callback to load the referenced index value onto the stack.</param>
        public static void EmitConvertIndex3ToTargetType(
            Type ungroupedIndexType,
            ILGenerator ilGenerator,
            Action loadIdx)
        {
            var numValues = (int)ungroupedIndexType.GetUngroupedIndexType();
            if (numValues > 3)
                throw new NotSupportedException(RuntimeErrorMessages.NotSupportedIndexType);
            var idxLocal = ilGenerator.DeclareLocal(typeof(Index3));
            for (int i = 0; i < numValues; ++i)
            {
                loadIdx();

                ilGenerator.Emit(OpCodes.Stloc, idxLocal);
                ilGenerator.Emit(OpCodes.Ldloca, idxLocal);
                ilGenerator.Emit(OpCodes.Call, Index3ValueGetter[i]);
            }
            var mainConstructor = GetMainIndexConstructor(ungroupedIndexType);
            ilGenerator.Emit(OpCodes.Newobj, mainConstructor);
        }

        /// <summary>
        /// Emits code to convert a linear index to a specific target type.
        /// </summary>
        /// <param name="ungroupedIndexType">The index type (can be Index, Index2 or Index3).</param>
        /// <param name="ilGenerator">The target IL-instruction generator.</param>
        /// <param name="loadDimension">A callback to load the referenced dimension value onto the stack.</param>
        public static void EmitConvertFrom1DIndexToTargetIndexType(
            Type ungroupedIndexType,
            ILGenerator ilGenerator,
            Action loadDimension)
        {
            switch (ungroupedIndexType.GetUngroupedIndexType())
            {
                case IndexType.Index1D:
                    // Invoke default constructor of the index class
                    ilGenerator.Emit(
                        OpCodes.Newobj,
                        GetMainIndexConstructor(ungroupedIndexType));
                    break;
                case IndexType.Index2D:
                    loadDimension();
                    ilGenerator.Emit(
                        OpCodes.Call,
                        ungroupedIndexType.GetMethod(
                            nameof(Index2.ReconstructIndex),
                            BindingFlags.Public | BindingFlags.Static));
                    break;
                case IndexType.Index3D:
                    loadDimension();
                    ilGenerator.Emit(
                        OpCodes.Call,
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
        /// <param name="indexType">The index type (can be Index, Index2 or Index3).</param>
        /// <param name="ilGenerator">The target IL-instruction generator.</param>
        /// <param name="loadIdx">A callback to load the referenced index value onto the stack.</param>
        private static void EmitLoadDimensions(Type indexType, ILGenerator ilGenerator, Action loadIdx)
        {
            EmitLoadDimensions(indexType, ilGenerator, loadIdx, offset => { });
        }

        /// <summary>
        /// Emits code to load a 3D dimension of a grid or a group index.
        /// </summary>
        /// <param name="indexType">The index type (can be Index, Index2 or Index3).</param>
        /// <param name="ilGenerator">The target IL-instruction generator.</param>
        /// <param name="loadIdx">A callback to load the referenced index value onto the stack.</param>
        /// <param name="manipulateIdx">A callback to manipulate the loaded index of a given dimension.</param>
        private static void EmitLoadDimensions(
            Type indexType,
            ILGenerator ilGenerator,
            Action loadIdx,
            Action<int> manipulateIdx)
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
                ilGenerator.Emit(OpCodes.Call, fieldGetter);
                manipulateIdx(offset);
            }

            // Fill empty zeros
            for (; offset < indexFieldGetter.Length; ++offset)
                ilGenerator.Emit(OpCodes.Ldc_I4_1);
        }

        /// <summary>
        /// Emits a kernel-dimension configuration.
        /// It pushes 6 integers onto the evaluation stack (gridIdx.X, gridIdx.Y, ...) by default.
        /// Howerver, using a custom <paramref name="convertIndex3Args"/> function allows to create
        /// and instantiate custom grid and group indices.
        /// </summary>
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="ilGenerator">The target IL-instruction generator.</param>
        /// <param name="dimensionIdx">The argument index of the provided launch-dimension index.</param>
        /// <param name="convertIndex3Args">Allows to create and instantiate custom grid and group indices.</param>
        public static void EmitLoadDimensions(
            EntryPoint entryPoint,
            ILGenerator ilGenerator,
            int dimensionIdx,
            Action convertIndex3Args)
        {
            EmitLoadDimensions(
                entryPoint,
                ilGenerator,
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
        /// <param name="entryPoint">The entry point.</param>
        /// <param name="ilGenerator">The target IL-instruction generator.</param>
        /// <param name="dimensionIdx">The argument index of the provided launch-dimension index.</param>
        /// <param name="convertIndex3Args">Allows to create and instantiate custom grid and group indices.</param>
        /// <param name="customGroupSize">The custom group size used for automatic blocking.</param>
        public static void EmitLoadDimensions(
            EntryPoint entryPoint,
            ILGenerator ilGenerator,
            int dimensionIdx,
            Action convertIndex3Args,
            int customGroupSize)
        {
            if (!entryPoint.IsGroupedIndexEntry)
            {
                Debug.Assert(customGroupSize >= 0, "Invalid custom group size");

                // Note that the custom group size is currently limited
                // to the X dimension of the scheduled grid.
                // This is no limitation of the current PTX backend.

                EmitLoadDimensions(
                    entryPoint.UngroupedIndexType,
                    ilGenerator,
                    () => ilGenerator.Emit(OpCodes.Ldarga, dimensionIdx),
                    dimIdx =>
                    {
                        if (dimIdx != 0 || customGroupSize < 1)
                            return;
                        // Convert requested index range to blocked range
                        ilGenerator.Emit(OpCodes.Ldc_I4, customGroupSize - 1);
                        ilGenerator.Emit(OpCodes.Add);
                        ilGenerator.Emit(OpCodes.Ldc_I4, customGroupSize);
                        ilGenerator.Emit(OpCodes.Div);
                    });

                convertIndex3Args();

                // Custom grouping
                ilGenerator.Emit(OpCodes.Ldc_I4, Math.Max(customGroupSize, 1));
                ilGenerator.Emit(OpCodes.Ldc_I4_1);
                ilGenerator.Emit(OpCodes.Ldc_I4_1);

                convertIndex3Args();
            }
            else
            {
                Debug.Assert(customGroupSize == 0, "Invalid custom group size");

                var groupedIndexType = entryPoint.KernelIndexType;
                var gridIdx = ilGenerator.DeclareLocal(entryPoint.UngroupedIndexType);

                ilGenerator.Emit(OpCodes.Ldarga, dimensionIdx);
                ilGenerator.Emit(
                    OpCodes.Call,
                    groupedIndexType.GetProperty(
                        nameof(GroupedIndex.GridIdx),
                        BindingFlags.Public | BindingFlags.Instance).GetGetMethod(false));
                ilGenerator.Emit(OpCodes.Stloc, gridIdx);
                EmitLoadDimensions(
                    entryPoint.UngroupedIndexType,
                    ilGenerator,
                    () => ilGenerator.Emit(OpCodes.Ldloca, gridIdx));
                convertIndex3Args();

                var groupIdx = ilGenerator.DeclareLocal(entryPoint.UngroupedIndexType);

                ilGenerator.Emit(OpCodes.Ldarga, dimensionIdx);
                ilGenerator.Emit(
                    OpCodes.Call,
                    groupedIndexType.GetProperty(
                        nameof(GroupedIndex.GroupIdx),
                        BindingFlags.Public | BindingFlags.Instance).GetGetMethod(false));
                ilGenerator.Emit(OpCodes.Stloc, groupIdx);
                EmitLoadDimensions(
                    entryPoint.UngroupedIndexType,
                    ilGenerator,
                    () => ilGenerator.Emit(OpCodes.Ldloca, groupIdx));
                convertIndex3Args();
            }
        }

        /// <summary>
        /// Emits code for loading a typed kernel from a generic kernel instance.
        /// </summary>
        /// <typeparam name="T">The kernel type.</typeparam>
        /// <param name="kernelArgumentIndex">The index of the launcher parameter.</param>
        /// <param name="ilGenerator">The target IL-instruction generator.</param>
        public static void EmitLoadKernelArgument<T>(int kernelArgumentIndex, ILGenerator ilGenerator)
            where T : Kernel
        {
            Debug.Assert(kernelArgumentIndex >= 0);
            Debug.Assert(ilGenerator != null);

            ilGenerator.Emit(OpCodes.Ldarg, kernelArgumentIndex);
            ilGenerator.Emit(OpCodes.Castclass, typeof(T));
        }

        /// <summary>
        /// Emits code for loading a typed accelerator stream from a generic accelerator-stream instance.
        /// </summary>
        /// <typeparam name="T">The kernel type.</typeparam>
        /// <param name="streamArgumentIndex">The index of the stream parameter.</param>
        /// <param name="ilGenerator">The target IL-instruction generator.</param>
        public static void EmitLoadAcceleratorStream<T>(int streamArgumentIndex, ILGenerator ilGenerator)
            where T : AcceleratorStream
        {
            Debug.Assert(streamArgumentIndex >= 0);
            Debug.Assert(ilGenerator != null);

            ilGenerator.Emit(OpCodes.Ldarg, streamArgumentIndex);
            ilGenerator.Emit(OpCodes.Castclass, typeof(T));
        }

        #endregion
    }
}
