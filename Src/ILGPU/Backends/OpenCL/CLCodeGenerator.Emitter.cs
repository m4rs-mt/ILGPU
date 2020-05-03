// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: CLCodeGenerator.Emitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Diagnostics;
using System.Globalization;
using System.Text;

namespace ILGPU.Backends.OpenCL
{
    partial class CLCodeGenerator
    {
        #region Nested Types

        /// <summary>
        /// Generates OpenCL source statements.
        /// </summary>
        public struct StatementEmitter : IDisposable
        {
            #region Instance

            private readonly StringBuilder stringBuilder;
            private bool argMode;
            private int argumentCount;

            /// <summary>
            /// Constructs a new statement emitter using the given target.
            /// </summary>
            /// <param name="codeGenerator">The parent code generator.</param>
            internal StatementEmitter(CLCodeGenerator codeGenerator)
            {
                CodeGenerator = codeGenerator;
                stringBuilder = codeGenerator.Builder;
                argumentCount = 0;
                argMode = false;

                codeGenerator.AppendIndent();
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated code generator.
            /// </summary>
            public CLCodeGenerator CodeGenerator { get; }

            #endregion

            #region Methods

            /// <summary>
            /// Starts a target assignment.
            /// </summary>
            /// <param name="target">The target.</param>
            /// <param name="appendNew">True, to append a new variable target.</param>
            private void BeginAppendTarget(Variable target, bool appendNew = true)
            {
                if (appendNew)
                {
                    var variableType = CodeGenerator.GetVariableType(target);
                    stringBuilder.Append(variableType);
                    stringBuilder.Append(' ');
                }
                stringBuilder.Append(target.ToString());
            }

            /// <summary>
            /// Appends a target declaration.
            /// </summary>
            /// <param name="target">The target declaration.</param>
            internal void AppendDeclaration(Variable target) =>
                BeginAppendTarget(target);

            /// <summary>
            /// Appends a target.
            /// </summary>
            /// <param name="target">The target.</param>
            /// <param name="newTarget">True, if this a new target.</param>
            internal void AppendTarget(Variable target, bool newTarget = true)
            {
                BeginAppendTarget(target, newTarget);
                stringBuilder.Append(" = ");
            }

            /// <summary>
            /// Appends an indexer target.
            /// </summary>
            /// <param name="target">The target.</param>
            /// <param name="indexer">The indexer variable.</param>
            internal void AppendIndexedTarget(Variable target, Variable indexer)
            {
                BeginAppendTarget(target, false);
                AppendIndexer(indexer);
                stringBuilder.Append(" = ");
            }

            /// <summary>
            /// Appends a field target.
            /// </summary>
            /// <param name="target">The target.</param>
            /// <param name="fieldSpan">The field span.</param>
            internal void AppendFieldTarget(Variable target, FieldSpan fieldSpan)
            {
                BeginAppendTarget(target, false);
                AppendField(fieldSpan);
                stringBuilder.Append(" = ");
            }

            /// <summary>
            /// Appends an indexer.
            /// </summary>
            /// <param name="indexer">The indexer variable.</param>
            public void AppendIndexer(Variable indexer) =>
                AppendIndexer(indexer.ToString());

            /// <summary>
            /// Appends an indexer.
            /// </summary>
            /// <param name="indexer">The indexer expression.</param>
            public void AppendIndexer(string indexer)
            {
                stringBuilder.Append('[');
                stringBuilder.Append(indexer);
                stringBuilder.Append(']');
            }

            /// <summary>
            /// Appends an unsafe pointer cast expression.
            /// </summary>
            /// <param name="typeExpression">The type expression.</param>
            public void AppendPointerCast(string typeExpression) =>
                AppendCast(typeExpression + CLInstructions.DereferenceOperation);

            /// <summary>
            /// Appends an unsafe cast expression.
            /// </summary>
            /// <param name="typeExpression">The type expression.</param>
            public void AppendCast(string typeExpression)
            {
                stringBuilder.Append('(');
                stringBuilder.Append(typeExpression);
                stringBuilder.Append(')');
            }

            /// <summary>
            /// Appends a pointer cast to an intrinsic atomic pointer type.
            /// </summary>
            /// <param name="type">The arithmetic type to use.</param>
            public void AppendAtomicCast(ArithmeticBasicValueType type)
            {
                var typeExpression = CLTypeGenerator.GetAtomicType(type);
                if (typeExpression == null)
                    return;
                AppendCast(typeExpression + CLInstructions.DereferenceOperation);
            }

            /// <summary>
            /// Appends a cast to the given basic value type.
            /// </summary>
            /// <param name="type">The target type.</param>
            public void AppendCast(BasicValueType type)
            {
                var typeExpression = CLTypeGenerator.GetBasicValueType(type);
                AppendCast(typeExpression);
            }

            /// <summary>
            /// Appends a cast to the given arithmetic basic value type.
            /// </summary>
            /// <param name="type">The target type.</param>
            public void AppendCast(ArithmeticBasicValueType type)
            {
                var typeExpression = CLTypeGenerator.GetBasicValueType(type);
                AppendCast(typeExpression);
            }

            /// <summary>
            /// Appends a cast to the given type.
            /// </summary>
            /// <param name="type">The target type.</param>
            public void AppendCast(TypeNode type)
            {
                var typeExpression = CodeGenerator.TypeGenerator[type];
                AppendCast(typeExpression);
            }

            /// <summary>
            /// Appends the given raw command.
            /// </summary>
            /// <param name="command">The command to append.</param>
            public void AppendCommand(string command)
            {
                stringBuilder.Append(' ');
                stringBuilder.Append(command);
                stringBuilder.Append(' ');
            }

            /// <summary>
            /// Appends the specified field name.
            /// </summary>
            /// <param name="fieldIndex">The field index.</param>
            private void AppendFieldName(int fieldIndex)
            {
                var fieldName = CLTypeGenerator.GetFieldName(fieldIndex);
                stringBuilder.Append(fieldName);
            }

            /// <summary>
            /// Appends the referenced field accessor.
            /// </summary>
            /// <param name="fieldAccess">The field access.</param>
            public void AppendFieldViaPtr(FieldAccess fieldAccess)
            {
                stringBuilder.Append("->");
                AppendFieldName(fieldAccess.Index);
            }

            /// <summary>
            /// Appends the referenced field accessor.
            /// </summary>
            /// <param name="fieldAccess">The field access.</param>
            public void AppendField(FieldSpan? fieldAccess)
            {
                if (!fieldAccess.HasValue)
                    return;
                AppendField(fieldAccess.Value);
            }

            /// <summary>
            /// Appends the referenced field accessor.
            /// </summary>
            /// <param name="fieldAccess">The field access.</param>
            public void AppendField(FieldSpan fieldAccess)
            {
                stringBuilder.Append('.');
                AppendFieldName(fieldAccess.Index);
            }

            /// <summary>
            /// Appends a referenced field via an access chain.
            /// </summary>
            /// <param name="accessChain">The field access chain.</param>
            public void AppendField(FieldAccessChain accessChain)
            {
                foreach (var fieldIndex in accessChain)
                    AppendField(fieldIndex);
            }

            /// <summary>
            /// Opens a parenthesis.
            /// </summary>
            public void OpenParen() =>
                stringBuilder.Append('(');

            /// <summary>
            /// Closes a parenthesis.
            /// </summary>
            public void CloseParen() =>
                stringBuilder.Append(')');

            /// <summary>
            /// Starts a function-call argument list.
            /// </summary>
            public void BeginArguments()
            {
                argMode = true;
                OpenParen();
            }

            /// <summary>
            /// Ends a function-call argument list.
            /// </summary>
            public void EndArguments()
            {
                CloseParen();
                argMode = false;
            }

            /// <summary>
            /// Appends code to finish an appended argument.
            /// </summary>
            public void AppendArgument()
            {
                if (!argMode)
                {
                    stringBuilder.Append(' ');
                }
                else
                {
                    if (argumentCount > 0)
                        stringBuilder.Append(", ");
                    ++argumentCount;
                }
            }

            /// <summary>
            /// Appends the given variable directly.
            /// </summary>
            /// <param name="variable">The variable to append.</param>
            public void Append(Variable variable) =>
                stringBuilder.Append(variable.ToString());

            /// <summary>
            /// Appends the given register argument.
            /// </summary>
            /// <param name="argument">The argument to append.</param>
            public void AppendArgument(Variable argument)
            {
                AppendArgument();
                Append(argument);
            }

            /// <summary>
            /// Appends the address of the given register argument.
            /// </summary>
            /// <param name="argument">The argument to append.</param>
            public void AppendArgumentAddress(Variable argument)
            {
                AppendArgument();
                AppendCommand(CLInstructions.AddressOfOperation);
                Append(argument);
            }
            /// <summary>
            /// Appends the address of the given register argument with a cast.
            /// </summary>
            /// <param name="argument">The argument to append.</param>
            /// <param name="type">The target type to cast to.</param>
            public void AppendArgumentAddressWithCast(Variable argument, TypeNode type)
            {
                AppendArgument();
                AppendCast(type);
                AppendCommand(CLInstructions.AddressOfOperation);
                Append(argument);
            }

            /// <summary>
            /// Append the given operation.
            /// </summary>
            /// <param name="operation">The operation to append.</param>
            public void AppendOperation(string operation) =>
                stringBuilder.Append(operation);

            /// <summary>
            /// Appends a constant.
            /// </summary>
            /// <param name="value">The constant to append.</param>
            public void AppendConstant(long value)
            {
                AppendArgument();
                stringBuilder.Append(value);
            }

            /// <summary>
            /// Appends a constant.
            /// </summary>
            /// <param name="value">The constant to append.</param>
            [CLSCompliant(false)]
            public void AppendConstant(ulong value)
            {
                AppendArgument();
                stringBuilder.Append(value);
            }

            /// <summary>
            /// Appends a constant.
            /// </summary>
            /// <param name="value">The constant to append.</param>
            public void AppendConstant(float value)
            {
                AppendArgument();

                if (float.IsNaN(value))
                {
                    stringBuilder.Append("NAN");
                }
                else if (float.IsPositiveInfinity(value))
                {
                    stringBuilder.Append("INFINITY");
                }
                else if (float.IsNegativeInfinity(value))
                {
                    stringBuilder.Append("-INFINITY");
                }
                else
                {
                    stringBuilder.Append(
                        value.ToString(CultureInfo.InvariantCulture));
                    if (value % 1.0f == 0.0f)
                        stringBuilder.Append(".0f");
                    else
                        stringBuilder.Append('f');
                }
            }

            /// <summary>
            /// Appends a constant.
            /// </summary>
            /// <param name="value">The constant to append.</param>
            public void AppendConstant(double value)
            {
                AppendArgument();

                if (double.IsNaN(value))
                {
                    stringBuilder.Append("NAN");
                }
                else if (double.IsPositiveInfinity(value))
                {
                    stringBuilder.Append("INFINITY");
                }
                else if (double.IsNegativeInfinity(value))
                {
                    stringBuilder.Append("-INFINITY");
                }
                else
                {
                    stringBuilder.Append(
                        value.ToString(CultureInfo.InvariantCulture));
                }
            }

            /// <summary>
            /// Finishes the current statement.
            /// </summary>
            public void Finish() => stringBuilder.AppendLine(";");

            #endregion

            #region IDisposable

            /// <summary cref="IDisposable.Dispose"/>
            void IDisposable.Dispose() => Finish();

            #endregion
        }

        #endregion

        #region Properties

        /// <summary>
        /// The current indentation level.
        /// </summary>
        public int Indent { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Increases the current indentation level.
        /// </summary>
        public void PushIndent() => ++Indent;

        /// <summary>
        /// Decreases the current indentation level.
        /// </summary>
        public void PopIndent()
        {
            Debug.Assert(Indent > 0);
            --Indent;
        }

        /// <summary>
        /// Appends the current indentation level to the builder.
        /// </summary>
        public void AppendIndent() => Builder.Append('\t', Indent);

        /// <summary>
        /// Pushes the current indentation level and appends it to the builder.
        /// </summary>
        public void PushAndAppendIndent()
        {
            PushIndent();
            AppendIndent();
        }

        /// <summary>
        /// Declares a variable
        /// </summary>
        /// <param name="target">The target variable to declare.</param>
        public void Declare(Variable target)
        {
            using var emitter = new StatementEmitter(this);
            emitter.AppendDeclaration(target);
        }

        /// <summary>
        /// Emits a new goto statement to the given target block.
        /// </summary>
        /// <param name="block">The target block to jump to.</param>
        public void GotoStatement(BasicBlock block)
        {
            using var statement = BeginStatement(CLInstructions.GotoStatement);
            statement.AppendOperation(blockLookup[block]);
        }

        /// <summary>
        /// Emits a move operation.
        /// </summary>
        /// <param name="target">The target variable to assign to.</param>
        /// <param name="source">The source variable to assign to.</param>
        public void Move(Variable target, Variable source)
        {
            using var emitter = new StatementEmitter(this);
            emitter.AppendTarget(target, false);
            emitter.Append(source);
        }

        /// <summary>
        /// Begins a new statement.
        /// </summary>
        /// <param name="target">The target variable to assign to.</param>
        /// <returns>The created statement emitter.</returns>
        public StatementEmitter BeginStatement(Variable target)
        {
            var emitter = new StatementEmitter(this);
            emitter.AppendTarget(target);
            return emitter;
        }

        /// <summary>
        /// Begins a new statement.
        /// </summary>
        /// <param name="target">The target variable to assign to.</param>
        /// <param name="fieldAccess">The field access to use.</param>
        /// <returns>The created statement emitter.</returns>
        public StatementEmitter BeginStatement(
            Variable target,
            FieldAccess? fieldAccess) =>
            !fieldAccess.HasValue
            ? BeginStatement(target)
            : BeginStatement(target, fieldAccess.Value);

        /// <summary>
        /// Begins a new statement.
        /// </summary>
        /// <param name="target">The target variable to assign to.</param>
        /// <param name="fieldAccess">The field access to use.</param>
        /// <returns>The created statement emitter.</returns>
        public StatementEmitter BeginStatement(Variable target, FieldAccess fieldAccess)
        {
            var emitter = new StatementEmitter(this);
            emitter.AppendFieldTarget(target, fieldAccess);
            return emitter;
        }

        /// <summary>
        /// Begins a new statement.
        /// </summary>
        /// <param name="target">The target variable to assign to.</param>
        /// <param name="indexer">The indexer variable to use.</param>
        /// <returns>The created statement emitter.</returns>
        public StatementEmitter BeginStatement(Variable target, Variable indexer)
        {
            var emitter = new StatementEmitter(this);
            emitter.AppendIndexedTarget(target, indexer);
            return emitter;
        }

        /// <summary>
        /// Begins a new statement.
        /// </summary>
        /// <param name="target">The target variable to assign to.</param>
        /// <param name="command">The initial command to emit.</param>
        /// <returns>The created statement emitter.</returns>
        public StatementEmitter BeginStatement(Variable target, string command)
        {
            var emitter = BeginStatement(target);
            emitter.AppendCommand(command);
            return emitter;
        }

        /// <summary>
        /// Begins a new statement.
        /// </summary>
        /// <param name="command">The initial command to emit.</param>
        /// <returns>The created statement emitter.</returns>
        public StatementEmitter BeginStatement(string command)
        {
            var emitter = new StatementEmitter(this);
            emitter.AppendCommand(command);
            return emitter;
        }

        #endregion
    }
}
