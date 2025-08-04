// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2024-2025 ILGPU Project
//                                    www.ilgpu.net
//
// File: ImplementIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPUC.Frontend;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace ILGPUC.IR.Transformations;

/// <summary>
/// Ab abstract intrinsic mapper to be used during intrinsic specialization.
/// </summary>
interface IIntrinsicMapper
{
    /// <summary>
    /// Tests whether the given value can be implemented using the method returned by
    /// implementations of this method.
    /// </summary>
    /// <param name="value">The value to be implemented.</param>
    /// <param name="methodInfo">The method info to be resolved.</param>
    /// <returns>
    /// True if the given value can be mapped to a method implementation for this
    /// backend.
    /// </returns>
    bool CanMapTo(Value value, [NotNullWhen(true)] out MethodInfo? methodInfo);
}

/// <summary>
/// An intrinsic code generator handler.
/// </summary>
/// <param name="context">The current IR context.</param>
/// <param name="methodBuilder">The current method builder.</param>
/// <param name="builder">The basic block builder.</param>
/// <param name="value">The value to generate code for.</param>
delegate void IntrinsicCodeGenerator(
    IRContext context,
    Method.Builder methodBuilder,
    BasicBlock.Builder builder,
    Value value);

/// <summary>
/// An abstract intrinsic code generator.
/// </summary>
interface IIntrinsicCodeGenerator
{
    /// <summary>
    /// Tries to generate code for the given value using builders provided.
    /// </summary>
    /// <param name="context">The current IR context.</param>
    /// <param name="methodBuilder">The current method builder.</param>
    /// <param name="builder">The basic block builder.</param>
    /// <param name="value">The value to generate code for.</param>
    /// <returns></returns>
    bool Generate(
        IRContext context,
        Method.Builder methodBuilder,
        BasicBlock.Builder builder,
        Value value);
}

/// <summary>
/// Static helper class to implement intrinsics.
/// </summary>
static class ImplementIntrinsics
{
    /// <summary>
    /// Adds intrinsic implementation passes to the given transformer pipeline.
    /// </summary>
    /// <typeparam name="TMapper">Mapper implementation to be used.</typeparam>
    /// <typeparam name="TCodeGenerator">Generator implementation to be used.</typeparam>
    /// <param name="builder">The current transformer builder.</param>
    /// <param name="frontend">The IL frontend instance to use.</param>
    public static void AddImplementIntrinsics<TMapper, TCodeGenerator>(
        this Transformer.Builder builder,
        ILFrontend frontend)
        where TMapper : IIntrinsicMapper, new()
        where TCodeGenerator : IIntrinsicCodeGenerator, new()
    {
        builder.Add(new UnreachableCodeElimination());
        builder.Add(new DeadCodeElimination());
        builder.Add(new ImplementIntrinsicsPass1<TMapper>(frontend));

        builder.Add(new Inliner());
        builder.Add(new UnreachableCodeElimination());
        builder.Add(new DeadCodeElimination());
        builder.Add(new ImplementIntrinsicsPass2<TCodeGenerator>());
    }
}

/// <summary>
/// Implements intrinsics using <see cref="IIntrinsicMapper"/> implementations.
/// </summary>
/// <typeparam name="TMapper">Mapper implementation to be used.</typeparam>
/// <param name="frontend">The frontend instance to use.</param>
sealed class ImplementIntrinsicsPass1<TMapper>(ILFrontend frontend) : Transformation
    where TMapper : IIntrinsicMapper, new()
{
    private readonly TMapper _mapper = new();

    /// <summary>
    /// Gathers all remapped intrinsics based on given methods.
    /// </summary>
    /// <param name="methods">Input methods.</param>
    /// <param name="valueMapping">Input methods.</param>
    /// <returns>List of all gathered methods to implement.</returns>
    private HashSet<MethodBase> GatherIntrinsics(
        in MethodCollection methods,
        Dictionary<Value, MethodBase> valueMapping)
    {
        var newMethods = new HashSet<MethodBase>(methods.Count * 2);
        foreach (var method in methods)
        {
            foreach (var value in method.Values)
            {
                if (_mapper.CanMapTo(value, out var newMethod))
                {
                    newMethods.Add(newMethod);
                    valueMapping.Add(value, newMethod);
                }
            }
        }
        return newMethods;
    }

    /// <summary>
    /// Imports intrinsic functions and generates code for remapped methods.
    /// </summary>
    /// <param name="context">Target context to import into.</param>
    /// <param name="newMethods">Set of new methods to import.</param>
    /// <returns>True if new methods were imported.</returns>
    private bool ImportIntrinsics(IRContext context, HashSet<MethodBase> newMethods)
    {
        // Prepare for code generation of new methods
        frontend.PushScope();

        // Disassemble all methods
        frontend.LoadMethods(newMethods);

        // Generate code for all new methods
        return frontend.GenerateCode(context);
    }

    /// <summary>
    /// Transforms all methods in the given context.
    /// </summary>
    /// <param name="methods">The methods to transform.</param>
    protected override void PerformTransformation(in MethodCollection methods)
    {
        // Import all dependencies iteratively
        var valueMapping = new Dictionary<Value, MethodBase>(methods.Count * 2);
        for (var newMethods = GatherIntrinsics(methods, valueMapping);
            ImportIntrinsics(methods.Context, newMethods);
            newMethods = GatherIntrinsics(methods.Context.Methods, valueMapping)) ;

        // Check whether values need to be implemented
        if (valueMapping.Count < 1)
            return;

        // Wire intrinsic functions
        foreach (var (value, entry) in valueMapping)
        {
            var blockBuilder = GetBuilder(value);
            var targetMethod = methods.Context.GetMethod(entry);

            blockBuilder.ReplaceWithCall(value, targetMethod);
        }
    }
}

/// <summary>
/// Implements intrinsics using <see cref="IIntrinsicMapper"/> implementations.
/// </summary>
/// <typeparam name="TCodeGenerator">Generator implementation to be used.</typeparam>
sealed class ImplementIntrinsicsPass2<TCodeGenerator> :
    UnorderedTransformation
    where TCodeGenerator : IIntrinsicCodeGenerator, new()
{
    private readonly TCodeGenerator _codeGenerator = new();

    /// <inheritdoc cref="UnorderedTransformation.PerformTransformation(
    /// IRContext, Method.Builder)"/>
    protected override void PerformTransformation(
        IRContext context,
        Method.Builder builder)
    {
        foreach (var value in builder.Method.Values)
            _codeGenerator.Generate(context, builder, GetBuilder(value), value);
    }
}
