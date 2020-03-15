// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2020 Marcel Koester
//                                www.ilgpu.net
//
// File: VariableAllocator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.Util;
using System;
using System.Collections.Generic;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents a generic high-level variable allocator.
    /// </summary>
    /// <remarks>The members of this class are not thread safe.</remarks>
    public abstract class VariableAllocator
    {
        #region Nested Types

        /// <summary>
        /// A variable that can be accessed and allocated.
        /// </summary>
        public abstract class Variable
        {
            /// <summary>
            /// Constructs a new variable.
            /// </summary>
            /// <param name="id">The current variable id.</param>
            internal Variable(int id)
            {
                Id = id;
                VariableName = "var" + id.ToString();
            }

            /// <summary>
            /// Returns the unique variable id.
            /// </summary>
            public int Id { get; }

            /// <summary>
            /// Returns the associated variable name.
            /// </summary>
            public string VariableName { get; }

            /// <summary>
            /// Returns the string representation of this variable.
            /// </summary>
            /// <returns>The string representation of this variable.</returns>
            public override string ToString() => VariableName;
        }

        /// <summary>
        /// A primitive variable.
        /// </summary>
        public sealed class PrimitiveVariable : Variable
        {
            /// <summary>
            /// Constructs a new primitive variable.
            /// </summary>
            /// <param name="id">The current variable id.</param>
            /// <param name="basicValueType">The basic value type.</param>
            internal PrimitiveVariable(int id, ArithmeticBasicValueType basicValueType)
                : base(id)
            {
                BasicValueType = basicValueType;
            }

            /// <summary>
            /// Returns the associated basic value type.
            /// </summary>
            public ArithmeticBasicValueType BasicValueType { get; }
        }

        /// <summary>
        /// A typed variable.
        /// </summary>
        public abstract class TypedVariable : Variable
        {
            /// <summary>
            /// Constructs a new typed variable.
            /// </summary>
            /// <param name="id">The current variable id.</param>
            /// <param name="type">The type.</param>
            protected TypedVariable(int id, TypeNode type)
                : base(id)
            {
                Type = type;
            }

            /// <summary>
            /// Returns the underlying type.
            /// </summary>
            public TypeNode Type { get; }
        }

        /// <summary>
        /// A pointer variable.
        /// </summary>
        public sealed class PointerVariable : TypedVariable
        {
            /// <summary>
            /// Constructs a new pointer variable.
            /// </summary>
            /// <param name="id">The current variable id.</param>
            /// <param name="pointerType">The pointer type.</param>
            internal PointerVariable(
                int id,
                PointerType pointerType)
                : base(id, pointerType)
            { }

            /// <summary>
            /// Returns the represented IR type.
            /// </summary>
            public new PointerType Type => base.Type as PointerType;
        }

        /// <summary>
        /// An object variable.
        /// </summary>
        public sealed class ObjectVariable : TypedVariable
        {
            /// <summary>
            /// Constructs a new object variable.
            /// </summary>
            /// <param name="id">The current variable id.</param>
            /// <param name="type">The object type.</param>
            internal ObjectVariable(int id, ObjectType type)
                : base(id, type)
            { }

            /// <summary>
            /// Returns the represented IR type.
            /// </summary>
            public new ObjectType Type => base.Type as ObjectType;
        }

        #endregion

        #region Instance

        private readonly Dictionary<Value, Variable> variableLookup =
            new Dictionary<Value, Variable>();
        private int idCounter = 0;

        /// <summary>
        /// Constructs a new variable allocator.
        /// </summary>
        protected VariableAllocator() { }

        #endregion

        #region Methods

        /// <summary>
        /// Allocates a new variable.
        /// </summary>
        /// <param name="value">The value to allocate.</param>
        /// <returns>The allocated variable.</returns>
        public Variable Allocate(Value value)
        {
            if (variableLookup.TryGetValue(value, out Variable variable))
                return variable;
            variable = AllocateType(value.Type);
            variableLookup.Add(value, variable);
            return variable;
        }

        /// <summary>
        /// Allocates a new variable.
        /// </summary>
        /// <param name="value">The value to allocate.</param>
        /// <param name="basicValueType">The actual type to allocate.</param>
        /// <returns>The allocated variable.</returns>
        public Variable Allocate(Value value, ArithmeticBasicValueType basicValueType)
        {
            if (variableLookup.TryGetValue(value, out Variable variable))
                return variable;
            variable = AllocateType(basicValueType);
            variableLookup.Add(value, variable);
            return variable;
        }

        /// <summary>
        /// Allocates a new variable as type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="value">The value to allocate.</param>
        /// <returns>The allocated variable.</returns>
        public T AllocateAs<T>(Value value)
            where T : Variable =>
            Allocate(value) as T;

        /// <summary>
        /// Allocates the given type.
        /// </summary>
        /// <param name="basicValueType">The type to allocate.</param>
        /// <returns>The allocated variable.</returns>
        public Variable AllocateType(ArithmeticBasicValueType basicValueType) =>
            new PrimitiveVariable(idCounter++, basicValueType);

        /// <summary>
        /// Allocates the given type.
        /// </summary>
        /// <param name="basicValueType">The type to allocate.</param>
        /// <returns>The allocated variable.</returns>
        public Variable AllocateType(BasicValueType basicValueType) =>
            new PrimitiveVariable(idCounter++, basicValueType.GetArithmeticBasicValueType(false));

        /// <summary>
        /// Allocates a pointer type.
        /// </summary>
        /// <param name="pointerType">The pointer type type to allocate.</param>
        /// <returns>The allocated variable.</returns>
        public PointerVariable AllocatePointerType(PointerType pointerType) =>
            new PointerVariable(idCounter++, pointerType);

        /// <summary>
        /// Allocates the given type.
        /// </summary>
        /// <param name="typeNode">The type to allocate.</param>
        /// <returns>The allocated variable.</returns>
        public Variable AllocateType(TypeNode typeNode)
        {
            switch (typeNode)
            {
                case PrimitiveType primitiveType:
                    return AllocateType(primitiveType.BasicValueType);
                case PointerType pointerType:
                    return AllocatePointerType(pointerType);
                case ObjectType objectType:
                    return new ObjectVariable(idCounter++, objectType);
                default:
                    throw new NotSupportedException();
            }
        }

        /// <summary>
        /// Loads the given value.
        /// </summary>
        /// <param name="value">The value to load.</param>
        /// <returns>The loaded variable.</returns>
        public Variable Load(Value value) =>
            variableLookup[value];

        /// <summary>
        /// Loads the given value as variable type <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type to load.</typeparam>
        /// <param name="value">The value to load.</param>
        /// <returns>The loaded variable.</returns>
        public T LoadAs<T>(Value value)
            where T : Variable
        {
            var variable = Load(value);
            if (variable is T result)
                return result;
            throw new InvalidCodeGenerationException();
        }

        /// <summary>
        /// Binds the given value to the target variable.
        /// </summary>
        /// <param name="node">The node to bind.</param>
        /// <param name="targetVariable">The target variable to bind to.</param>
        public void Bind(Value node, Variable targetVariable) =>
            variableLookup[node] = targetVariable;

        #endregion
    }
}
