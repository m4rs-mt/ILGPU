// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: EntryPoint.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.Resources;
using ILGPU.Runtime;
using ILGPUC.IR;
using ILGPUC.Util;
using System;
using System.Collections.Immutable;
using System.Reflection;

namespace ILGPUC.Backends.EntryPoints;

/// <summary>
/// Represents a kernel entry point.
/// </summary>
class EntryPoint
{
    /// <summary>
    /// Constructs a new entry point targeting the given method.
    /// </summary>
    /// <param name="method">The entry point method.</param>
    /// <param name="isGrouped">
    /// True if the kernel method is an explicitly grouped kernel.
    /// </param>
    public EntryPoint(MethodInfo method, bool isGrouped)
    {
        Method = method;
        IsExplicitlyGrouped = isGrouped;
        Specialization =
            method.GetCustomAttribute<KernelSpecializationAttribute>()?.Specialization
            ?? new();

        var parameters = method.GetParameters();
        if (!isGrouped && parameters.Length < 1)
        {
            throw new NotSupportedException(
                ErrorMessages.InvalidEntryPointIndexParameter);
        }

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
        for (int i = 0, e = Parameters.Count; i < e; ++i)
            HasByRefParameters |= Parameters.IsByRef(i);

        KernelIndexParameterOffset = isGrouped ? 0 : 1;
    }

    /// <summary>
    /// Represents the unique id of this kernel.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    /// <summary>
    /// Returns the associated kernel function name.
    /// </summary>
    public string Name => CompiledKernelData.GetKernelName(Id);

    /// <summary>
    /// Returns the associated method info.
    /// </summary>
    public MethodInfo Method { get; }

    /// <summary>
    /// Returns the offset for the actual parameter values while taking an implicit
    /// index argument into account.
    /// </summary>
    public int KernelIndexParameterOffset { get; }

    /// <summary>
    /// Returns true if the entry point represents an explicitly grouped kernel.
    /// </summary>
    public bool IsExplicitlyGrouped { get; }

    /// <summary>
    /// Returns true if the entry point represents an implicitly grouped kernel.
    /// </summary>
    public bool IsImplicitlyGrouped => !IsExplicitlyGrouped;

    /// <summary>
    /// Returns the compiled kernel type of this instance.
    /// </summary>
    public CompiledKernelType KernelType =>
        IsExplicitlyGrouped ? CompiledKernelType.Grouped : CompiledKernelType.Auto;

    /// <summary>
    /// Returns the parameter specification of arguments that are passed to the
    /// kernel.
    /// </summary>
    public ParameterCollection Parameters { get; }

    /// <summary>
    /// Returns true if the parameter specification contains by reference parameters.
    /// </summary>
    public bool HasByRefParameters { get; }

    /// <summary>
    /// Returns the associated launch specification.
    /// </summary>
    public KernelSpecialization Specialization { get; }

    /// <summary>
    /// Creates new compiled kernel data.
    /// </summary>
    /// <param name="sharedMemoryMode">The shared memory mode.</param>
    /// <param name="sharedMemorySize">The required shared memory size in bytes.</param>
    /// <param name="localMemorySize">The required local memory size in bytes.</param>
    /// <param name="data">The binary kernel data.</param>
    /// <param name="customAttributes">Custom attributes stored.</param>
    public CompiledKernelData CreateCompiledKernelData(
        CompiledKernelSharedMemoryMode sharedMemoryMode,
        int sharedMemorySize,
        int localMemorySize,
        ReadOnlyMemory<byte> data,
        ReadOnlyMemory<byte>? customAttributes = null)
    {
        customAttributes ??= ReadOnlyMemory<byte>.Empty;
        return new(
            IRContext.IRVersion,
            Id,
            KernelType,
            sharedMemoryMode,
            Specialization,
            sharedMemorySize,
            localMemorySize,
            data,
            customAttributes.Value);
    }
}
