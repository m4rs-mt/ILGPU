// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: ILEmitterExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
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
        #region Static Instance

        private static readonly MethodInfo GetHashCodeInfo = typeof(object).GetMethod(
            nameof(object.GetHashCode),
            BindingFlags.Public | BindingFlags.Instance);
        private static readonly MethodInfo EqualsInfo = typeof(object).GetMethod(
            nameof(object.Equals),
            BindingFlags.Public | BindingFlags.Instance);

        /// <summary>
        /// Caches all constant op codes.
        /// </summary>
        private static readonly OpCode[] ConstantOpCodes =
            new OpCode[]
            {
                OpCodes.Ldc_I4_M1,
                OpCodes.Ldc_I4_0,
                OpCodes.Ldc_I4_1,
                OpCodes.Ldc_I4_2,
                OpCodes.Ldc_I4_3,
                OpCodes.Ldc_I4_4,
                OpCodes.Ldc_I4_5,
                OpCodes.Ldc_I4_6,
                OpCodes.Ldc_I4_7,
                OpCodes.Ldc_I4_8,
            };

        /// <summary>
        /// Stores the constructor of the <see cref="Half"/> type.
        /// </summary>
        private static readonly ConstructorInfo HalfConstructor =
            typeof(Half).GetConstructor(
                BindingFlags.NonPublic | BindingFlags.CreateInstance,
                null,
                new Type[] { typeof(ushort) },
                null);

        #endregion

        #region Methods

        /// <summary>
        /// Emits optimized code to load an integer constant.
        /// </summary>
        public static void LoadIntegerConstant<TILEmitter>(
            this TILEmitter emitter,
            int constant)
            where TILEmitter : IILEmitter
        {
            if (constant >= -1 && constant < ConstantOpCodes.Length)
                emitter.Emit(ConstantOpCodes[constant + 1]);
            else
                emitter.EmitConstant(constant);
        }

        /// <summary>
        /// Calls a compatible shuffle method.
        /// </summary>
        public static void LoadConstant<TILEmitter>(
            this TILEmitter emitter,
            PrimitiveValue value,
            ref ILLocal? temporaryHalf)
            where TILEmitter : IILEmitter
        {
            switch (value.BasicValueType)
            {
                case BasicValueType.Int1:
                    if (value.Int1Value)
                        emitter.Emit(OpCodes.Ldc_I4_0);
                    else
                        emitter.Emit(OpCodes.Ldc_I4_1);
                    break;
                case BasicValueType.Int16:
                    emitter.LoadIntegerConstant(value.Int16Value);
                    break;
                case BasicValueType.Int32:
                    emitter.LoadIntegerConstant(value.Int32Value);
                    break;
                case BasicValueType.Int64:
                    emitter.EmitConstant(value.Int64Value);
                    break;
                case BasicValueType.Float16:
                    // Allocate a temporary variable and invoke the half constructor
                    temporaryHalf ??= emitter.DeclareLocal(typeof(Half));
                    emitter.Emit(LocalOperation.LoadAddress, temporaryHalf.Value);
                    emitter.EmitConstant(value.Float16Value.RawValue);
                    emitter.EmitNewObject(HalfConstructor);
                    emitter.Emit(LocalOperation.Load, temporaryHalf.Value);
                    break;
                case BasicValueType.Float32:
                    emitter.EmitConstant(value.Float32Value);
                    break;
                case BasicValueType.Float64:
                    emitter.EmitConstant(value.Float64Value);
                    break;
                default:
                    throw new NotSupportedIntrinsicException(
                        value.BasicValueType.ToString());
            }
        }

        /// <summary>
        /// Loads an object from an address in memory.
        /// </summary>
        /// <param name="emitter">The emitter instance.</param>
        /// <param name="typeToLoad">The manged type to load.</param>
        public static void LoadObject<TILEmitter>(
            this TILEmitter emitter,
            Type typeToLoad)
            where TILEmitter : IILEmitter =>
            emitter.Emit(OpCodes.Ldobj, typeToLoad);

        /// <summary>
        /// Generates code that loads a null value.
        /// </summary>
        public static ILLocal LoadNull<TILEmitter>(
            this TILEmitter emitter,
            Type type)
            where TILEmitter : IILEmitter
        {
            var nullLocal = emitter.DeclareLocal(type);
            // Check whether the given value is a reference type
            if (type.IsClass)
            {
                // Emit a null reference
                emitter.Emit(OpCodes.Ldnull, type);
                emitter.Emit(LocalOperation.Store, nullLocal);
            }
            else
            {
                // Emit a new local variable that is initialized with null
                emitter.Emit(LocalOperation.LoadAddress, nullLocal);
                emitter.Emit(OpCodes.Initobj, type);
            }
            return nullLocal;
        }

        /// <summary>
        /// Gets managed field info from a pre-defined converted structure type.
        /// </summary>
        /// <param name="type">The managed structure type.</param>
        /// <param name="fieldIndex">The internal field index.</param>
        /// <returns>The corresponding field info.</returns>
        public static FieldInfo GetFieldInfo(Type type, int fieldIndex)
        {
            var fieldName = StructureType.GetFieldName(fieldIndex);
            return type.GetField(fieldName);
        }

        /// <summary>
        /// Emits code to load a field.
        /// </summary>
        public static void LoadField<TILEmitter>(
            this TILEmitter emitter,
            Type type,
            int fieldIndex)
            where TILEmitter : IILEmitter
        {
            var fieldInfo = GetFieldInfo(type, fieldIndex);
            emitter.Emit(OpCodes.Ldfld, fieldInfo);
        }

        /// <summary>
        /// Emits code to load the address of a field.
        /// </summary>
        public static void LoadFieldAddress<TILEmitter>(
            this TILEmitter emitter,
            Type type,
            int fieldIndex)
            where TILEmitter : IILEmitter
        {
            var fieldInfo = GetFieldInfo(type, fieldIndex);
            emitter.Emit(OpCodes.Ldflda, fieldInfo);
        }

        /// <summary>
        /// Emits code to store a value to a field.
        /// </summary>
        public static void StoreField<TILEmitter>(
            this TILEmitter emitter,
            Type type,
            int fieldIndex)
            where TILEmitter : IILEmitter
        {
            var fieldInfo = GetFieldInfo(type, fieldIndex);
            emitter.Emit(OpCodes.Stfld, fieldInfo);
        }

        #endregion

        #region Hash Code and Equals

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

        #endregion
    }
}
