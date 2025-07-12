// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2024 ILGPU Project
//                                    www.ilgpu.net
//
// File: CompiledKernel.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;

namespace ILGPU.Runtime;

/// <summary>
/// The type of a compiled kernel to differentiate between grouped and automatic kernels.
/// </summary>
public enum CompiledKernelType
{
    /// <summary>
    /// An automatic kernel that relies on runtime group information.
    /// </summary>
    Auto,

    /// <summary>
    /// An explicitly grouped kernel that provides fixed grouping information.
    /// </summary>
    Grouped,
}

/// <summary>
/// Specifies modes to provide shared memory at runtime.
/// </summary>
public enum CompiledKernelSharedMemoryMode
{
    /// <summary>
    /// Static shared memory provided by the compiler.
    /// </summary>
    Static,

    /// <summary>
    /// Dynamic shared memory provided by the runtime.
    /// </summary>
    Dynamic,

    /// <summary>
    /// Dynamic shared memory provided by the runtime and also static shared memory
    /// required to be provided by the compiler. This can be treated as dynamic by
    /// the runtime.
    /// </summary>
    Hybrid,
}

/// <summary>
/// Adjusts kernel configurations to adjust specific settings like shared memory.
/// </summary>
/// <param name="kernelConfig">The kernel configuration to adjust.</param>
public delegate void CompiledKernelConfigAdjustment(ref KernelConfig kernelConfig);

/// <summary>
/// Represents the base class for all runtime kernels.
/// </summary>
/// <param name="Version">The ILGPUC version used to compile the kernel.</param>
/// <param name="Guid">The global id.</param>
/// <param name="KernelType">The kernel type.</param>
/// <param name="SharedMemoryMode">The shared memory mode.</param>
/// <param name="KernelSpecialization">The specialization used.</param>
/// <param name="SharedMemorySize">The required shared memory size in bytes.</param>
/// <param name="LocalMemorySize">The required local memory size in bytes.</param>
/// <param name="Data">The binary kernel data.</param>
/// <param name="CustomAttributes">Custom attributes stored.</param>
public record class CompiledKernelData(
    Version Version,
    Guid Guid,
    CompiledKernelType KernelType,
    CompiledKernelSharedMemoryMode SharedMemoryMode,
    KernelSpecialization KernelSpecialization,
    int SharedMemorySize,
    int LocalMemorySize,
    ReadOnlyMemory<byte> Data,
    ReadOnlyMemory<byte> CustomAttributes)
{
    /// <summary>
    /// Determines a kernel name of a kernel function.
    /// </summary>
    /// <param name="id">The unique kernel id.</param>
    /// <returns>The kernel name string.</returns>
    public static string GetKernelName(Guid id) => $"Kernel{id:N}";

    /// <summary>
    /// Writes this compiled kernel to the given writer.
    /// </summary>
    /// <param name="writer">The target writer to write to.</param>
    public void Write(BinaryWriter writer)
    {
        writer.Write(Version.ToString());
        writer.Write(Guid.ToByteArray());
        writer.Write((byte)KernelType);
        writer.Write((byte)SharedMemoryMode);
        KernelSpecialization.Write(writer);
        writer.Write(SharedMemorySize);
        writer.Write(LocalMemorySize);

        writer.Write(Data.Length);
        writer.Write(Data.Span);
        writer.Write(CustomAttributes.Length);
        writer.Write(CustomAttributes.Span);
    }

    /// <summary>
    /// Loads a compiled kernel from the given resource stream.
    /// </summary>
    /// <param name="reader">The reader to read from.</param>
    /// <returns>The loaded kernel.</returns>
    public static CompiledKernelData Read(BinaryReader reader)
    {
        var version = Version.Parse(reader.ReadString());
        var guid = new Guid(reader.ReadBytes(16));
        var kernelType = (CompiledKernelType)reader.ReadByte();
        var sharedMemoryMode = (CompiledKernelSharedMemoryMode)reader.ReadByte();
        var specialization = KernelSpecialization.Read(reader);

        var sharedMemorySize = reader.ReadInt32();
        var localMemorySize = reader.ReadInt32();

        int dataLength = reader.ReadInt32();
        var data = reader.ReadBytes(dataLength);
        int customAttributesLength = reader.ReadInt32();
        var customAttributes = reader.ReadBytes(customAttributesLength);

        return new(
            version,
            guid,
            kernelType,
            sharedMemoryMode,
            specialization,
            sharedMemorySize,
            localMemorySize,
            data,
            customAttributes);
    }

    /// <summary>
    /// Returns the underlying kernel data as string.
    /// </summary>
    /// <returns>The UTF8 string representation of the underlying data.</returns>
    public string GetSourceAsString() =>
        System.Text.Encoding.UTF8.GetString(Data.Span);
}

/// <summary>
/// Represents a compiled kernel object generated by ILGPUC.
/// </summary>
/// <param name="data">The underlying serialized binary data.</param>
public abstract class CompiledKernel(CompiledKernelData data)
{
    /// <summary>
    /// Returns compiled kernel data for this kernel.
    /// </summary>
    public CompiledKernelData Data { get; } = data;

    /// <summary>
    /// Represents the kernel name.
    /// </summary>
    public string KernelName => CompiledKernelData.GetKernelName(Guid);

    /// <summary>
    /// Returns the unique kernel id.
    /// </summary>
    public Guid Guid { get; } = data.Guid;

    /// <summary>
    /// Returns the desired maximum number of threads per group (if any).
    /// </summary>
    public int? MaxNumThreadsPerGroup =>
        Data.KernelSpecialization.MaxNumThreadsPerGroup;

    /// <summary>
    /// Returns the underlying kernel data as string.
    /// </summary>
    /// <returns>The UTF8 string representation of the underlying data.</returns>
    public string GetSourceAsString() => Data.GetSourceAsString();
}

/// <summary>
/// Represents an abstract collection of compiled kernels.
/// </summary>
public abstract class CompiledKernelSource(int count)
{
    /// <summary>
    /// An enumerator to iterate over all elements in a compiled kernel source.
    /// </summary>
    /// <param name="source">The source</param>
    public struct Enumerator(CompiledKernelSource source)
    {
        private int _index = -1;

        /// <inheritdoc cref="IEnumerator{T}.Current"/>
        public readonly CompiledKernel Current => source.Load(_index);

        /// <inheritdoc cref="IEnumerator.MoveNext"/>
        public bool MoveNext() => ++_index < source.Length;
    }

    /// <summary>
    /// Returns the number of compiled kernels.
    /// </summary>
    public int Length { get; } = count;

    /// <summary>
    /// Loads the i-th compiled kernel.
    /// </summary>
    public abstract CompiledKernel Load(int index);

    /// <summary>
    /// Returns an enumerator to iterate over all elements in this source.
    /// </summary>
    public Enumerator GetEnumerator() => new(this);
}
