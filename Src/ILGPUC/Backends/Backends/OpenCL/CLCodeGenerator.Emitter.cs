// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2019-2023 ILGPU Project
//                                    www.ilgpu.net
//
// File: CLCodeGenerator.Emitter.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using ILGPU.Resources;
using ILGPU.Util;
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
            #region Static

            /// <summary>
            /// Indicates char tokens in a formatted floating-point literal that
            /// do not require a ".0f" suffix.
            /// </summary>
            private static readonly char[] FormattedFloatLiteralTokens =
            {
                '.',
                'E'
            };

            #endregion

            #region Instance

            private readonly StringBuilder stringBuilder;
            private readonly StringBuilder variableBuilder;
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
                variableBuilder = codeGenerator.VariableBuilder;
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
                    AppendDeclaration(target);
                stringBuilder.Append(target.ToString());
            }

            /// <summary>
            /// Appends a target declaration.
            /// </summary>
            /// <param name="target">The target declaration.</param>
            internal void AppendDeclaration(Variable target)
            {
                var variableType = CodeGenerator.GetVariableType(target);
                variableBuilder.Append('\t');
                variableBuilder.Append(variableType);
                variableBuilder.Append(' ');
                variableBuilder.Append(target.ToString());
                variableBuilder.AppendLine(";");
            }

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
            public void AppendIndexer(Variable indexer)
            {
                stringBuilder.Append('[');
                if (indexer is ConstantVariable constantVariable)
                    Append(constantVariable);
                else
                    stringBuilder.Append(indexer.ToString());
                stringBuilder.Append(']');
            }

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
                var typeExpression = CodeGenerator.TypeGenerator.GetAtomicType(type);
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
                var typeExpression = CodeGenerator.TypeGenerator.GetBasicValueType(type);
                AppendCast(typeExpression);
            }

            /// <summary>
            /// Appends a cast to the given arithmetic basic value type.
            /// </summary>
            /// <param name="type">The target type.</param>
            public void AppendCast(ArithmeticBasicValueType type)
            {
                var typeExpression = CodeGenerator.TypeGenerator.GetBasicValueType(type);
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
            public void AppendCommand(char command)
            {
                stringBuilder.Append(' ');
                stringBuilder.Append(command);
                stringBuilder.Append(' ');
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
            /// Appends the given constant variable.
            /// </summary>
            /// <param name="variable">The variable to append.</param>
            public void Append(ConstantVariable variable)
            {
                var value = variable.Value;
                switch (value.BasicValueType)
                {
                    case BasicValueType.Int1:
                        AppendConstant(value.Int1Value ? 1 : 0);
                        break;
                    case BasicValueType.Int8:
                        AppendConstant(value.UInt8Value);
                        break;
                    case BasicValueType.Int16:
                        AppendConstant(value.UInt16Value);
                        break;
                    case BasicValueType.Int32:
                        AppendConstant(value.UInt32Value);
                        break;
                    case BasicValueType.Int64:
                        AppendConstant(value.UInt64Value);
                        break;
                    case BasicValueType.Float16:
                        AppendConstant(value.Float16Value);
                        break;
                    case BasicValueType.Float32:
                        AppendConstant(value.Float32Value);
                        break;
                    case BasicValueType.Float64:
                        AppendConstant(value.Float64Value);
                        break;
                    default:
                        throw new InvalidCodeGenerationException();
                }
            }

            /// <summary>
            /// Appends the given variable directly.
            /// </summary>
            /// <param name="variable">The variable to append.</param>
            public void Append(Variable variable)
            {
                if (variable is ConstantVariable constantVariable)
                    Append(constantVariable);
                else
                    stringBuilder.Append(variable.ToString());
            }

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
            /// Appends the given register argument.
            /// </summary>
            /// <param name="argument">The argument to append.</param>
            /// <param name="valueType">The value type.</param>
            public void AppendArgumentWithCast(
                Variable argument,
                ArithmeticBasicValueType valueType)
            {
                AppendArgument();
                AppendCast(valueType);
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
            /// <param name="valueType">The value type.</param>
            public void AppendArgumentAddressWithCast(
                Variable argument,
                ArithmeticBasicValueType valueType)
            {
                AppendArgument();
                stringBuilder.Append('(');
                stringBuilder.Append(
                    CodeGenerator.TypeGenerator.GetBasicValueType(valueType));
                stringBuilder.Append(CLInstructions.DereferenceOperation);
                stringBuilder.Append(')');
                AppendCommand(CLInstructions.AddressOfOperation);
                Append(argument);
            }

            /// <summary>
            /// Append the given operation.
            /// </summary>
            /// <param name="operation">The operation to append.</param>
            public void AppendOperation(FormattableString operation) =>
                AppendOperation(operation.Format, operation.GetArguments());

            /// <summary>
            /// Append the given operation.
            /// </summary>
            /// <param name="operation">The operation to append.</param>
            public void AppendOperation(RawString operation) =>
                stringBuilder.Append(operation.Value);

            /// <summary>
            /// Append the given operation.
            /// </summary>
            /// <param name="operation">The operation to append.</param>
            /// <param name="arguments">The string format arguments.</param>
            public void AppendOperation(
                RawString operation,
                params object?[] arguments)
            {
                var formatExpression = operation.Value;
                if (!FormatString.TryParse(formatExpression, out var expressions))
                {
                    throw new NotSupportedException(string.Format(
                        ErrorMessages.NotSupportedWriteFormat,
                        formatExpression));
                }

                // Validate all expressions
                foreach (var expression in expressions)
                {
                    if (!expression.HasArgument)
                        continue;
                    if (expression.Argument < 0 ||
                        expression.Argument >= arguments.Length)
                    {
                        throw new NotSupportedException(string.Format(
                            ErrorMessages.NotSupportedWriteFormatArgumentRef,
                            formatExpression,
                            expression.Argument));
                    }
                }

                // Emit the operation
                foreach (var expression in expressions)
                {
                    if (!expression.HasArgument)
                    {
                        AppendOperation(expression.String.AsNotNull());
                    }
                    else
                    {
                        var argument = arguments[expression.Argument];
                        var argumentType = argument?.GetType();
                        switch (Type.GetTypeCode(argumentType))
                        {
                            case TypeCode.Boolean:
                                AppendConstant((bool)argument.AsNotNull() ? 1 : 0);
                                break;
                            case TypeCode.SByte:
                                AppendConstant((sbyte)argument.AsNotNull());
                                break;
                            case TypeCode.Byte:
                                AppendConstant((byte)argument.AsNotNull());
                                break;
                            case TypeCode.Int16:
                                AppendConstant((short)argument.AsNotNull());
                                break;
                            case TypeCode.UInt16:
                                AppendConstant((ushort)argument.AsNotNull());
                                break;
                            case TypeCode.Int32:
                                AppendConstant((int)argument.AsNotNull());
                                break;
                            case TypeCode.UInt32:
                                AppendConstant((uint)argument.AsNotNull());
                                break;
                            case TypeCode.Int64:
                                AppendConstant((long)argument.AsNotNull());
                                break;
                            case TypeCode.UInt64:
                                AppendConstant((ulong)argument.AsNotNull());
                                break;
                            case TypeCode.Single:
                                AppendConstant((float)argument.AsNotNull());
                                break;
                            case TypeCode.Double:
                                AppendConstant((double)argument.AsNotNull());
                                break;
                            case TypeCode.String:
                                AppendOperation((string)argument.AsNotNull());
                                break;
                            default:
                                if (argument is Variable variable)
                                {
                                    Append(variable);
                                    break;
                                }
                                else if (argument is BasicBlock block)
                                {
                                    AppendOperation(CodeGenerator.blockLookup[block]);
                                    break;
                                }
                                else if (argumentType == typeof(Half))
                                {
                                    AppendConstant((Half)argument.AsNotNull());
                                    break;
                                }
                                throw new NotSupportedException(string.Format(
                                    ErrorMessages.NotSupportedWriteFormatArgumentType,
                                    formatExpression,
                                    argumentType?.ToString()));
                        }
                    }
                }
            }

            /// <summary>
            /// Appends a constant.
            /// </summary>
            /// <param name="value">The constant to append.</param>
            public void AppendConstant(string value)
            {
                AppendOperation("\"");
                AppendOperation(value);
                AppendOperation("\"");
            }

            /// <summary>
            /// Appends a constant.
            /// </summary>
            /// <param name="value">The constant to append.</param>
            public void AppendConstant(long value) =>
                stringBuilder.Append(value);

            /// <summary>
            /// Appends a constant.
            /// </summary>
            /// <param name="value">The constant to append.</param>
            public void AppendConstant(ulong value) =>
                stringBuilder.Append(value);

            /// <summary>
            /// Appends a constant.
            /// </summary>
            /// <param name="value">The constant to append.</param>
            public void AppendConstant(float value)
            {
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
                    // In C#, the floating point value "1.0f" can be shortened to "1f".
                    // However, in the C programming language, it is necessary to include
                    // the ".0f" suffix. However, if the stringified value already
                    // contains a decimal point, or the exponent notation, appending the
                    // "f" suffix is sufficient.
                    var formattedValue =
                        value.ToString("G9", CultureInfo.InvariantCulture);
                    stringBuilder.Append(formattedValue);

                    var idx = formattedValue.IndexOfAny(FormattedFloatLiteralTokens);
                    if (idx == -1)
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
                        value.ToString("G17", CultureInfo.InvariantCulture));
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
        public StatementEmitter BeginStatement(RawString command)
        {
            var emitter = new StatementEmitter(this);
            emitter.AppendCommand(command.Value);
            return emitter;
        }

        /// <summary>
        /// Begins a new statement.
        /// </summary>
        /// <param name="command">The initial command to emit.</param>
        /// <returns>The created statement emitter.</returns>
        public StatementEmitter BeginStatement(FormattableString command)
        {
            var emitter = new StatementEmitter(this);
            emitter.AppendOperation(command);
            return emitter;
        }

        /// <summary>
        /// Begins the function body, switching to variable capturing mode.
        /// </summary>
        protected void BeginFunctionBody()
        {
            // Start the function body.
            Builder.AppendLine("{");
            PushIndent();

#if DEBUG
            Builder.AppendLine();
            Builder.AppendLine("\t// Variable declarations");
            Builder.AppendLine();
#endif

            // Switch to the alternate builder, so that we can capture the code and
            // variable declarations separately.
            prefixBuilder = Builder;
            Builder = suffixBuilder;
        }

        /// <summary>
        /// Finishes the function body, ending variable capturing mode.
        /// </summary>
        protected void FinishFunctionBody()
        {
            // Restore the original builder, containing code before the variable
            // declarations.
            Builder = prefixBuilder;

            // Add the variable declarations at the start of the function, to avoid
            // issues with OpenCL compilers that are not C99 compliant, and cannot
            // handle variable declarations intermingled with other code.
            Builder.Append(VariableBuilder);
            Builder.AppendLine();

            // Add the code that was generated along with the variable declarations.
            Builder.Append(suffixBuilder);

            // Close the function body.
            PopIndent();
            Builder.AppendLine("}");
        }

        #endregion
    }
}
