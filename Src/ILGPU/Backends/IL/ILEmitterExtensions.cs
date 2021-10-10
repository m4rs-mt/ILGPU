﻿// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: ILEmitterExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.Reflection;
using System.Reflection.Emit;

namespace ILGPU.Backends.IL
{
    /// <summary>
    /// General IL emitter extensions methods.
    /// </summary>
    public static class ILEmitterExtensions
    {
        private static readonly MethodInfo GetHashCodeInfo = typeof(object).GetMethod(
            nameof(object.GetHashCode),
            BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo EqualsInfo = typeof(object).GetMethod(
            nameof(object.Equals),
            BindingFlags.Public | BindingFlags.Instance);

        /// <summary>
        /// Generates hash code and equals functions for the given fields.
        /// </summary>
        /// <param name="typeBuilder">The type builder to use.</param>
        /// <param name="fieldsToUse">All fields to use to compute the hash code.</param>
        public static void GenerateEqualsAndHashCode(
            this TypeBuilder typeBuilder,
            FieldInfo[] fieldsToUse)
        {
            GenerateHashCode(typeBuilder, fieldsToUse);
            var equalityEquals = GenerateEquals(typeBuilder, fieldsToUse);
            GenerateEquals(typeBuilder, equalityEquals);
        }

        /// <summary>
        /// Generates a new hash code function.
        /// </summary>
        /// <param name="typeBuilder">The type builder to use.</param>
        /// <param name="fieldsToUse">All fields to use to compute the hash code.</param>
        /// <returns>The created hash code function.</returns>
        public static MethodInfo GenerateHashCode(
            this TypeBuilder typeBuilder,
            FieldInfo[] fieldsToUse)
        {
            var getHashCode = typeBuilder.DefineMethod(
                GetHashCodeInfo.Name,
                MethodAttributes.Public | MethodAttributes.Virtual,
                typeof(int),
                Array.Empty<Type>());

            var emitter = new ILEmitter(getHashCode.GetILGenerator());
            var result = emitter.DeclareLocal(typeof(int));
            emitter.EmitConstant(0);
            emitter.Emit(LocalOperation.Store, result);

            foreach (var field in fieldsToUse)
            {
                emitter.Emit(LocalOperation.Load, result);

                emitter.Emit(OpCodes.Ldarg_0);

                var fieldHashCode = field.FieldType.GetMethod(
                    GetHashCodeInfo.Name,
                    BindingFlags.Public | BindingFlags.Instance);
                if (!field.FieldType.IsValueType)
                {
                    emitter.Emit(OpCodes.Ldfld, field);
                }
                else if (fieldHashCode.DeclaringType != field.FieldType)
                {
                    // to call GetHashCode inherited from ValueType, struct must be boxed
                    emitter.Emit(OpCodes.Ldfld, field);
                    emitter.Emit(OpCodes.Box, field.FieldType);
                }
                else
                {
                    emitter.Emit(OpCodes.Ldflda, field);
                }

                emitter.EmitCall(fieldHashCode);

                emitter.Emit(OpCodes.Xor);
                emitter.Emit(LocalOperation.Store, result);
            }

            emitter.Emit(LocalOperation.Load, result);
            emitter.Emit(OpCodes.Ret);

            emitter.Finish();
            typeBuilder.DefineMethodOverride(getHashCode, GetHashCodeInfo);

            return getHashCode;
        }

        /// <summary>
        /// Generates a new typed equals method using the given fields.
        /// </summary>
        /// <param name="typeBuilder">The type builder to use.</param>
        /// <param name="fieldsToUse">All fields to use to compute the hash code.</param>
        /// <returns>The created equals function.</returns>
        public static MethodInfo GenerateEquals(
            this TypeBuilder typeBuilder,
            FieldInfo[] fieldsToUse)
        {
            var equals = typeBuilder.DefineMethod(
                EqualsInfo.Name,
                MethodAttributes.Public | MethodAttributes.Virtual |
                MethodAttributes.NewSlot | MethodAttributes.Final,
                typeof(bool),
                new Type[] { typeBuilder });

            var emitter = new ILEmitter(equals.GetILGenerator());
            var falseLabel = emitter.DeclareLabel();

            foreach (var field in fieldsToUse)
            {
                emitter.Emit(OpCodes.Ldarg_0);
                emitter.Emit(OpCodes.Ldflda, field);

                emitter.Emit(ArgumentOperation.Load, 1);
                emitter.Emit(OpCodes.Ldfld, field);

                emitter.EmitCall(field.FieldType.GetMethod(
                    EqualsInfo.Name,
                    new Type[] { field.FieldType }));

                // IMPORTANT: Each field can branch to the false label. However, if we
                // have a large number of fields, depending on the number of IL bytes we
                // emit per field, we may not be able to reach the false label using a
                // 1-byte branch. In that case, we should instead use a 4-byte branch.
                emitter.Emit(
                    fieldsToUse.Length < 7 ? OpCodes.Brfalse_S : OpCodes.Brfalse,
                    falseLabel);
            }

            emitter.EmitConstant(1);
            emitter.Emit(OpCodes.Ret);

            emitter.MarkLabel(falseLabel);
            emitter.EmitConstant(0);
            emitter.Emit(OpCodes.Ret);

            emitter.Finish();
            var equalityInstance = typeof(IEquatable<>).MakeGenericType(typeBuilder);
            typeBuilder.AddInterfaceImplementation(equalityInstance);

            return equals;
        }

        /// <summary>
        /// Generates a new object equals method using the given typed equals overload.
        /// </summary>
        /// <param name="typeBuilder">The type builder to use.</param>
        /// <param name="equalsInfo">The typed equals function to call.</param>
        /// <returns>The created equals function.</returns>
        public static MethodInfo GenerateEquals(
            this TypeBuilder typeBuilder,
            MethodInfo equalsInfo)
        {
            var equals = typeBuilder.DefineMethod(
                EqualsInfo.Name,
                MethodAttributes.Public | MethodAttributes.Virtual,
                typeof(bool),
                new Type[] { typeof(object) });

            var emitter = new ILEmitter(equals.GetILGenerator());
            var falseLabel = emitter.DeclareLabel();

            // Check type
            emitter.Emit(OpCodes.Ldarg_1);
            emitter.Emit(OpCodes.Isinst, typeBuilder);
            emitter.Emit(OpCodes.Brfalse_S, falseLabel);

            // Call typed equals method
            emitter.Emit(OpCodes.Ldarg_0);
            emitter.Emit(OpCodes.Ldarg_1);
            emitter.Emit(OpCodes.Unbox_Any, typeBuilder);
            emitter.EmitCall(equalsInfo);
            emitter.Emit(OpCodes.Ret);

            // Return false
            emitter.MarkLabel(falseLabel);
            emitter.EmitConstant(0);
            emitter.Emit(OpCodes.Ret);

            emitter.Finish();
            typeBuilder.DefineMethodOverride(equals, EqualsInfo);

            return equals;
        }
    }
}
