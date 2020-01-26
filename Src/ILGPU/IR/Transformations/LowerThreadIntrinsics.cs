// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: LowerThreadIntrinsics.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Runtime.CompilerServices;

namespace ILGPU.IR.Transformations
{
    /// <summary>
    /// Lowers internal high-level thread intrinsics.
    /// </summary>
    public sealed class LowerThreadIntrinsics : UnorderedTransformation
    {
        #region Nested Types

        /// <summary>
        /// Represents an abstract value lowering.
        /// </summary>
        /// <typeparam name="T">The user-defined lowering arguments.</typeparam>
        private interface IValueLowering<T>
            where T : struct
        {
            /// <summary>
            /// Creates a new lowered node instance.
            /// </summary>
            /// <param name="builder">The current builder.</param>
            /// <param name="value">The current value.</param>
            /// <param name="arguments">The user-defined arguments.</param>
            /// <returns>The created value.</returns>
            Value Create(BasicBlock.Builder builder, Value value, T arguments);
        }

        /// <summary>
        /// Represents a specific <see cref="Broadcast"/> lowering.
        /// </summary>
        private readonly struct BroadcastLowering : IValueLowering<(Value, BroadcastKind)>
        {
            /// <summary cref="IValueLowering{T}.Create(BasicBlock.Builder, Value, T)"/>
            public Value Create(
                BasicBlock.Builder builder,
                Value value,
                (Value, BroadcastKind) arguments) =>
                builder.CreateBroadcast(
                    value,
                    arguments.Item1,
                    arguments.Item2);
        }

        /// <summary>
        /// Represents a specific <see cref="WarpShuffleLowering"/> lowering.
        /// </summary>
        private readonly struct WarpShuffleLowering : IValueLowering<(Value, ShuffleKind)>
        {
            /// <summary cref="IValueLowering{T}.Create(BasicBlock.Builder, Value, T)"/>
            public Value Create(
                BasicBlock.Builder builder,
                Value value,
                (Value, ShuffleKind) arguments) =>
                builder.CreateShuffle(
                    value,
                    arguments.Item1,
                    arguments.Item2);
        }

        /// <summary>
        /// Represents a specific <see cref="SubWarpShuffleLowering"/> lowering.
        /// </summary>
        private readonly struct SubWarpShuffleLowering : IValueLowering<(Value, Value, ShuffleKind)>
        {
            /// <summary cref="IValueLowering{T}.Create(BasicBlock.Builder, Value, T)"/>
            public Value Create(
                BasicBlock.Builder builder,
                Value value,
                (Value, Value, ShuffleKind) arguments) =>
                builder.CreateShuffle(
                    value,
                    arguments.Item1,
                    arguments.Item2,
                    arguments.Item3);
        }

        #endregion

        #region Helpers

        /// <summary>
        /// Returns true if the given value type is supported as built-in thread intrinsic.
        /// </summary>
        /// <param name="basicValueType">The basic value type to check.</param>
        /// <returns>True, if the given type is a supported built-in type..</returns>
        internal static bool IsBuiltinType(BasicValueType basicValueType) =>
            basicValueType >= BasicValueType.Int32;

        #endregion

        /// <summary cref="UnorderedTransformation.PerformTransformation(Method.Builder)"/>
        protected override bool PerformTransformation(Method.Builder builder)
        {
            var scope = builder.CreateScope();

            foreach (Value value in scope.Values)
            {
                switch (value)
                {
                    case Broadcast broadcast when !broadcast.IsBuiltIn:
                        LowerIntrinsic<ValueTuple<Value, BroadcastKind>, BroadcastLowering>(
                            builder,
                            broadcast,
                            broadcast.Variable,
                            (broadcast.Origin, broadcast.Kind));
                        break;
                    case WarpShuffle warpShuffle when !warpShuffle.IsBuiltIn:
                        LowerIntrinsic<ValueTuple<Value, ShuffleKind>, WarpShuffleLowering>(
                            builder,
                            warpShuffle,
                            warpShuffle.Variable,
                            (warpShuffle.Origin, warpShuffle.Kind));
                        break;
                    case SubWarpShuffle subWarpShuffle when !subWarpShuffle.IsBuiltIn:
                        LowerIntrinsic<ValueTuple<Value, Value, ShuffleKind>, SubWarpShuffleLowering>(
                            builder,
                            subWarpShuffle,
                            subWarpShuffle.Variable,
                            (subWarpShuffle.Origin, subWarpShuffle.Width, subWarpShuffle.Kind));
                        break;
                }
            }

            return true;
        }

        /// <summary>
        /// Lowers the given value using the specified value lowering.
        /// </summary>
        /// <typeparam name="T">The user-defined argument type.</typeparam>
        /// <typeparam name="TLowering">The lowering module.</typeparam>
        /// <param name="methodBuilder">The current method builder.</param>
        /// <param name="value">The source value.</param>
        /// <param name="variable">The variable to lower.</param>
        /// <param name="arguments">The user-defined arguments.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static void LowerIntrinsic<T, TLowering>(
            Method.Builder methodBuilder,
            Value value,
            Value variable,
            T arguments)
            where T : struct
            where TLowering : struct, IValueLowering<T>
        {
            var builder = methodBuilder[value.BasicBlock];
            builder.SetupInsertPosition(value);

            var newValue = LowerValue<T, TLowering>(builder, variable, arguments);

            value.Replace(newValue);
            builder.Remove(value);
        }

        /// <summary>
        /// Recursively lowers the given value using the specified value lowering.
        /// </summary>
        /// <typeparam name="T">The user-defined argument type.</typeparam>
        /// <typeparam name="TLowering">The lowering module.</typeparam>
        /// <param name="builder">The current block builder.</param>
        /// <param name="sourceValue">The value to lower.</param>
        /// <param name="arguments">The user-defined arguments.</param>
        private static Value LowerValue<T, TLowering>(
            BasicBlock.Builder builder,
            Value sourceValue,
            T arguments)
            where T : struct
            where TLowering : IValueLowering<T>
        {
            if (sourceValue.Type is PrimitiveType primitiveType)
            {
                Value value = sourceValue;
                if (!IsBuiltinType(primitiveType.BasicValueType))
                {
                    value = builder.CreateConvert(
                        value,
                        builder.GetPrimitiveType(BasicValueType.Int32));
                }

                TLowering converter = default;
                var result = converter.Create(builder, value, arguments);
                if (!IsBuiltinType(primitiveType.BasicValueType))
                    result = builder.CreateConvert(result, sourceValue.Type);

                return result;
            }
            else
            {
                var structureType = (StructureType)sourceValue.Type;
                var instance = builder.CreateStructure(structureType);

                for (int i = 0, e = structureType.NumFields; i < e; ++i)
                {
                    var value = LowerValue<T, TLowering>(
                        builder,
                        builder.CreateGetField(sourceValue, i),
                        arguments);
                    instance = builder.CreateSetField(instance, i, value);
                }

                return instance;
            }
        }
    }
}
