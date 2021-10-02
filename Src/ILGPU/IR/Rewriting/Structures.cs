// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: Structures.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Rewriting
{
    /// <summary>
    /// Extension methods for generation and destruction of structure values.
    /// </summary>
    public static class RewriterStructureExtensions
    {
        /// <summary>
        /// Assembles a structure value using the lowering provided.
        /// </summary>
        /// <typeparam name="T">The rewriter context type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="context">The current rewriter context instance.</param>
        /// <param name="structureType">The structure type to use.</param>
        /// <param name="value">The source value.</param>
        /// <param name="lowering">The lowering functionality.</param>
        /// <returns>The assembled structure value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value AssembleStructure<T, TValue>(
            this T context,
            StructureType structureType,
            TValue value,
            Func<T, TValue, FieldAccess, Value> lowering)
            where T : IRewriterContext
            where TValue : Value
        {
            var instance = context.Builder.CreateStructure(
                value.Location,
                structureType);

            // Invoke the lowering implementation for all fields
            for (int i = 0, e = structureType.NumFields; i < e; ++i)
            {
                // Invoke lowering implementation
                instance.Add(lowering(context, value, new FieldAccess(i)));
            }

            // Create new structure instance
            return instance.Seal();
        }
        /// <summary>
        /// Disassembled a structure value using the lowering provided.
        /// </summary>
        /// <typeparam name="T">The rewriter context type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="context">The current rewriter context instance.</param>
        /// <param name="structureType">The structure type to use.</param>
        /// <param name="value">The source value.</param>
        /// <param name="lowering">The lowering functionality.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void DisassembleStructure<T, TValue>(
            this T context,
            StructureType structureType,
            TValue value,
            Action<T, TValue, Value, FieldAccess> lowering)
            where T : IRewriterContext
            where TValue : Value
        {
            // Invoke the lowering implementation for all fields
            for (int i = 0, e = structureType.NumFields; i < e; ++i)
            {
                var access = new FieldAccess(i);
                var getField = context.Builder.CreateGetField(
                    value.Location,
                    value,
                    new FieldSpan(access));

                // Invoke lowering implementation
                lowering(context, value, getField, new FieldAccess(i));

                // Mark value as converted
                context.MarkConverted(getField);
            }
        }

        /// <summary>
        /// Lowers the given value by applying the lowering provided to each field value.
        /// Primitive values will be lowered once.
        /// </summary>
        /// <typeparam name="T">The rewriter context type.</typeparam>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="context">The current rewriter context instance.</param>
        /// <param name="source">The source value.</param>
        /// <param name="variable">The variable value.</param>
        /// <param name="lowering">The lowering functionality.</param>
        /// <returns>The assembled structure value holding the result value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static Value LowerValue<T, TValue>(
            this T context,
            TValue source,
            Value variable,
            Func<T, TValue, Value, Value> lowering)
            where T : IRewriterContext
            where TValue : Value
        {
            if (source.Type is PrimitiveType)
            {
                return lowering(context, source, source);
            }
            else
            {
                var structureType = (StructureType)source.Type;
                return AssembleStructure(
                    context,
                    structureType,
                    source,
                    (ctx, _, access) =>
                    {
                        // Extract field information
                        var getField = ctx.Builder.CreateGetField(
                            source.Location,
                            variable,
                            new FieldSpan(access));
                        var result = lowering(ctx, source, getField);

                        // Mark value as converted
                        ctx.MarkConverted(getField);
                        return result;
                    });
            }
        }

    }
}
