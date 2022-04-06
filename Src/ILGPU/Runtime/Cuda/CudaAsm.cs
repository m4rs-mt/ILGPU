// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2021-2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: CudaAsm.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Frontend.Intrinsic;
using System;
using System.Runtime.CompilerServices;

#pragma warning disable CA1051 // Do not declare visible instance fields
#pragma warning disable CA1724 // Type names should not match namespaces

namespace ILGPU.Runtime.Cuda
{
    /// <summary>
    /// Provides library calls for inline PTX assembly instructions.
    /// </summary>
    public static partial class CudaAsm
    {
        /// <summary>
        /// Returns true if running on a Cuda accelerator.
        /// </summary>
        public static bool IsSupported
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => Accelerator.CurrentType == AcceleratorType.Cuda;
        }

        /// <summary>
        /// Writes the inline PTX assembly instructions into the kernel.
        /// </summary>
        /// <param name="asm">The PTX assembly instruction string.</param>
        [LanguageIntrinsic(LanguageIntrinsicKind.EmitPTX)]
        public static void Emit(string asm) =>
            throw new NotImplementedException();
    }

    /// <summary>
    /// Base interface used by <see cref="CudaAsm.EmitRef{T0}(string, ref T0)"/> to
    /// ensure only valid generic arguments are supplied.
    /// </summary>
    public interface ICudaAsmEmitParameter
    { }

    /// <summary>
    /// Input parameter for the PTX instructions.
    /// </summary>
    public struct Input<T> : ICudaAsmEmitParameter
        where T : struct
    {
        /// <summary>
        /// Holds the input value.
        /// </summary>
        public T Value;

        /// <summary>
        /// Wraps the value as an input parameter.
        /// </summary>
        public static implicit operator Input<T>(T v) =>
            new Input<T>() { Value = v };
    }

    /// <summary>
    /// Output parameter for the PTX instructions.
    /// </summary>
    public struct Output<T> : ICudaAsmEmitParameter
        where T : struct
    {
        /// <summary>
        /// Filled in with the output value.
        /// </summary>
        public T Value;
    }

    /// <summary>
    /// Input/Output parameter for the PTX instructions.
    /// </summary>
    public struct Ref<T> : ICudaAsmEmitParameter
        where T : struct
    {
        /// <summary>
        /// Holds the input value. Filled in with the output value.
        /// </summary>
        public T Value;

        /// <summary>
        /// Wraps the value as a reference parameter.
        /// </summary>
        public static implicit operator Ref<T>(T v) =>
            new Ref<T>() { Value = v };
    }
}

#pragma warning restore CA1724 // Type names should not match namespaces
#pragma warning restore CA1051 // Do not declare visible instance fields
