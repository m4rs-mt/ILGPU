// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: TestTypes.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using ILGPUC.Backends;
using Xunit;

namespace ILGPUC.Tests.Framework;

/// <summary>
/// Represents Debug (assertions enabled) vs Release (assertions stripped) compilation.
/// </summary>
public enum CompilationMode
{
    /// <summary>Assertions and IO operations are enabled.</summary>
    Debug,

    /// <summary>Assertions and IO operations are stripped.</summary>
    Release,
}

/// <summary>
/// Extension methods for <see cref="CompilationMode"/>.
/// </summary>
public static class CompilationModeExtensions
{
    /// <summary>
    /// Applies the given <paramref name="mode"/> to the compilation properties.
    /// </summary>
    public static CompilationProperties WithMode(
        this CompilationProperties props, CompilationMode mode) => mode switch
    {
        CompilationMode.Debug => props with
            { EnableAssertions = true, EnableIOOperations = true },
        CompilationMode.Release => props with
            { EnableAssertions = false, EnableIOOperations = false },
        _ => props,
    };
}

/// <summary>
/// Provides TheoryData instances for parameterized tests, replacing T4 templates.
/// </summary>
public static class TestTypes
{
    public static readonly Type[] AllIntegerTypes =
    [
        typeof(sbyte), typeof(byte),
        typeof(short), typeof(ushort),
        typeof(int), typeof(uint),
        typeof(long), typeof(ulong),
    ];

    public static readonly Type[] AllFloatTypes =
    [
        typeof(Half), typeof(float), typeof(double),
    ];

    public static readonly Type[] AllNumericTypes =
    [
        typeof(sbyte), typeof(byte),
        typeof(short), typeof(ushort),
        typeof(int), typeof(uint),
        typeof(long), typeof(ulong),
        typeof(Half), typeof(float), typeof(double),
    ];

    public static TheoryData<OptimizationLevel> OptLevels => new()
    {
        OptimizationLevel.O0,
        OptimizationLevel.O1,
        OptimizationLevel.O2,
    };

    public static TheoryData<Type> IntegerTypes
    {
        get
        {
            var data = new TheoryData<Type>();
            foreach (var t in AllIntegerTypes)
                data.Add(t);
            return data;
        }
    }

    public static TheoryData<Type> FloatTypes
    {
        get
        {
            var data = new TheoryData<Type>();
            foreach (var t in AllFloatTypes)
                data.Add(t);
            return data;
        }
    }

    public static TheoryData<Type, OptimizationLevel> IntegerTypeAndOptLevel
    {
        get
        {
            var data = new TheoryData<Type, OptimizationLevel>();
            foreach (var t in AllIntegerTypes)
            foreach (var opt in new[]
                { OptimizationLevel.O0, OptimizationLevel.O1, OptimizationLevel.O2 })
                data.Add(t, opt);
            return data;
        }
    }

    public static TheoryData<Type, OptimizationLevel> FloatTypeAndOptLevel
    {
        get
        {
            var data = new TheoryData<Type, OptimizationLevel>();
            foreach (var t in AllFloatTypes)
            foreach (var opt in new[]
                { OptimizationLevel.O0, OptimizationLevel.O1, OptimizationLevel.O2 })
                data.Add(t, opt);
            return data;
        }
    }

    public static TheoryData<Type, OptimizationLevel> NumericTypeAndOptLevel
    {
        get
        {
            var data = new TheoryData<Type, OptimizationLevel>();
            foreach (var t in AllNumericTypes)
            foreach (var opt in new[]
                { OptimizationLevel.O0, OptimizationLevel.O1, OptimizationLevel.O2 })
                data.Add(t, opt);
            return data;
        }
    }

    public static TheoryData<Type, OptimizationLevel, CompilationMode>
        IntegerTypeAndOptLevelAndMode
    {
        get
        {
            var data = new TheoryData<Type, OptimizationLevel, CompilationMode>();
            foreach (var t in AllIntegerTypes)
            foreach (var opt in new[]
                { OptimizationLevel.O0, OptimizationLevel.O1, OptimizationLevel.O2 })
            foreach (var mode in new[]
                { CompilationMode.Debug, CompilationMode.Release })
                data.Add(t, opt, mode);
            return data;
        }
    }

    public static TheoryData<Type, OptimizationLevel, CompilationMode>
        FloatTypeAndOptLevelAndMode
    {
        get
        {
            var data = new TheoryData<Type, OptimizationLevel, CompilationMode>();
            foreach (var t in AllFloatTypes)
            foreach (var opt in new[]
                { OptimizationLevel.O0, OptimizationLevel.O1, OptimizationLevel.O2 })
            foreach (var mode in new[]
                { CompilationMode.Debug, CompilationMode.Release })
                data.Add(t, opt, mode);
            return data;
        }
    }

    public static TheoryData<CompilationMode> CompilationModes => new()
    {
        CompilationMode.Debug,
        CompilationMode.Release,
    };

    public static TheoryData<OptimizationLevel, CompilationMode> OptLevelsAndModes
    {
        get
        {
            var data = new TheoryData<OptimizationLevel, CompilationMode>();
            foreach (var opt in new[]
                { OptimizationLevel.O0, OptimizationLevel.O1, OptimizationLevel.O2 })
            foreach (var mode in new[]
                { CompilationMode.Debug, CompilationMode.Release })
                data.Add(opt, mode);
            return data;
        }
    }

    public static readonly BackendType[] AllBackendTypes =
    [
        BackendType.CPU,
        BackendType.Cuda,
        BackendType.Metal,
        BackendType.OpenCL,
        BackendType.ROCm,
    ];

    public static TheoryData<BackendType, OptimizationLevel, CompilationMode>
        BackendTypesAndOptLevelsAndModes
    {
        get
        {
            var data = new TheoryData<BackendType, OptimizationLevel, CompilationMode>();
            foreach (var backend in AllBackendTypes)
            foreach (var opt in new[]
                { OptimizationLevel.O0, OptimizationLevel.O1, OptimizationLevel.O2 })
            foreach (var mode in new[]
                { CompilationMode.Debug, CompilationMode.Release })
                data.Add(backend, opt, mode);
            return data;
        }
    }

    public static TheoryData<Type, BackendType, OptimizationLevel, CompilationMode>
        IntegerTypeAndBackendAndOptLevelAndMode
    {
        get
        {
            var data = new TheoryData<Type, BackendType, OptimizationLevel, CompilationMode>();
            foreach (var t in AllIntegerTypes)
            foreach (var backend in AllBackendTypes)
            foreach (var opt in new[]
                { OptimizationLevel.O0, OptimizationLevel.O1, OptimizationLevel.O2 })
            foreach (var mode in new[]
                { CompilationMode.Debug, CompilationMode.Release })
                data.Add(t, backend, opt, mode);
            return data;
        }
    }

    public static TheoryData<Type, BackendType, OptimizationLevel, CompilationMode>
        FloatTypeAndBackendAndOptLevelAndMode
    {
        get
        {
            var data = new TheoryData<Type, BackendType, OptimizationLevel, CompilationMode>();
            foreach (var t in AllFloatTypes)
            foreach (var backend in AllBackendTypes)
            foreach (var opt in new[]
                { OptimizationLevel.O0, OptimizationLevel.O1, OptimizationLevel.O2 })
            foreach (var mode in new[]
                { CompilationMode.Debug, CompilationMode.Release })
                data.Add(t, backend, opt, mode);
            return data;
        }
    }
}
