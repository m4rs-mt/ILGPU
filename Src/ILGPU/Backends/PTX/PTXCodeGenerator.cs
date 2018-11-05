// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: PTXCodeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ILGPU.Backends.PTX
{
    /// <summary>
    /// Generates PTX code out of IR values.
    /// </summary>
    /// <remarks>The code needs to be prepared for this code generator.</remarks>
    partial class PTXCodeGenerator : IValueVisitor
    {
        #region Constants

        /// <summary>
        /// The supported PTX version.
        /// </summary>
        public const string PTXVersion = "6.0";

        #endregion

        #region Nested Types

        /// <summary>
        /// Generation arguments for code-generator construction.
        /// </summary>
        public readonly ref struct GeneratorArgs
        {
            public GeneratorArgs(
                EntryPoint entryPoint,
                StringBuilder stringBuilder,
                PTXArchitecture architecture,
                ABI abi,
                bool fastMath,
                bool enableAssertions)
            {
                EntryPoint = entryPoint;
                StringBuilder = stringBuilder;
                Architecture = architecture;
                ABI = abi;
                FastMath = fastMath;
                EnableAssertions = enableAssertions;
            }

            public EntryPoint EntryPoint { get; }
            public StringBuilder StringBuilder { get; }
            public PTXArchitecture Architecture { get; }
            public ABI ABI { get; }
            public bool FastMath { get; }
            public bool EnableAssertions { get; }
        }

        /// <summary>
        /// Represents a parameter that is mapped to PTX.
        /// </summary>
        protected readonly struct MappedParameter
        {
            #region Instance

            /// <summary>
            /// Constructs a new mapped parameter.
            /// </summary>
            /// <param name="register">The PTX register.</param>
            /// <param name="ptxType">The PTX type.</param>
            /// <param name="ptxName">The name of the parameter in PTX code.</param>
            /// <param name="parameter">The source parameter.</param>
            public MappedParameter(
                PTXRegister register,
                PTXType ptxType,
                string ptxName,
                Parameter parameter)
            {
                PTXRegister = register;
                PTXType = ptxType;
                PTXName = ptxName;
                Parameter = parameter;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the associated PTX register.
            /// </summary>
            public PTXRegister PTXRegister { get; }

            /// <summary>
            /// Returns the mapped PTX parameter type.
            /// </summary>
            public PTXType PTXType { get; }

            /// <summary>
            /// Returns the name of the parameter in PTX code.
            /// </summary>
            public string PTXName { get; }

            /// <summary>
            /// Returns the source parameter.
            /// </summary>
            public Parameter Parameter { get; }

            #endregion
        }

        /// <summary>
        /// Represents a setup logic for function parameters.
        /// </summary>
        protected interface IParameterSetupLogic
        {
            /// <summary>
            /// Handles an intrinsic parameter and returns the
            /// associated allocated register (if any).
            /// </summary>
            /// <param name="parameterOffset">The current intrinsic parameter index.</param>
            /// <param name="parameter">The intrinsic parameter.</param>
            /// <param name="paramType">The parameter type.</param>
            /// <returns>The allocated register (if any).</returns>
            PTXRegister? HandleIntrinsicParameters(
                int parameterOffset,
                Parameter parameter,
                PTXType paramType);
        }

        /// <summary>
        /// Represents an empty parameter setup logic.
        /// </summary>
        protected readonly struct EmptyParameterSetupLogic : IParameterSetupLogic
        {
            /// <summary cref="IParameterSetupLogic.HandleIntrinsicParameters(int, Parameter, PTXType)"/>
            public PTXRegister? HandleIntrinsicParameters(
                int parameterOffset,
                Parameter parameter,
                PTXType paramType) => null;
        }

        #endregion

        #region Static

        /// <summary>
        /// Returns the PTX function name for the given function.
        /// </summary>
        /// <param name="function">The function.</param>
        /// <returns>The resolved PTX function name.</returns>
        protected static string GetFunctionName(TopLevelFunction function)
        {
            var handleName = function.Handle.Name;
            if (function.HasFlags(TopLevelFunctionFlags.External))
                return handleName;
            var chars = handleName.ToCharArray();
            for (int i = 0, e = chars.Length; i < e; ++i)
            {
                ref var charValue = ref chars[i];
                if (!char.IsLetterOrDigit(charValue))
                    charValue = '_';
            }
            return new string(chars) + function.Id.ToString();
        }

        #endregion

        #region Instance

        private int labelCounter = 0;
        private readonly NodeMarker functionMarker;
        private readonly PTXRegisterAllocator registerAllocator;
        private readonly HashSet<DeviceConstantValue> emittedConstants = new HashSet<DeviceConstantValue>();
        private readonly Dictionary<FunctionValue, string> blockLookup =
            new Dictionary<FunctionValue, string>();
        private readonly Dictionary<Value, (string, PTXRegister)> constants =
            new Dictionary<Value, (string, PTXRegister)>();
        private readonly string labelPrefix;
        protected readonly string returnParamName;

        /// <summary>
        /// Constructs a new PTX generator.
        /// </summary>
        /// <param name="args">The generator arguments.</param>
        /// <param name="scope">The current scope.</param>
        protected PTXCodeGenerator(in GeneratorArgs args, Scope scope)
        {
            Builder = args.StringBuilder;
            Scope = scope;
            CFG = CFG.Create(Scope);
            TopLevelFunction = Scope.Entry as TopLevelFunction;

            ABI = args.ABI;
            functionMarker = Scope.Context.NewNodeMarker();

            Architecture = args.Architecture;
            FastMath = args.FastMath;
            EnableAssertions = args.EnableAssertions;

            registerAllocator = new PTXRegisterAllocator(ABI);
            labelPrefix = "L_" + TopLevelFunction.Id.ToString();
            returnParamName = "retval_" + TopLevelFunction.Id;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the associated top-level function.
        /// </summary>
        public TopLevelFunction TopLevelFunction { get; }

        /// <summary>
        /// Returns the current function scope.
        /// </summary>
        public Scope Scope { get; }

        /// <summary>
        /// Returns the current CFG.
        /// </summary>
        public CFG CFG { get; }

        /// <summary>
        /// Returns the current ABI.
        /// </summary>
        public ABI ABI { get; }

        /// <summary>
        /// Returns the currently used PTX architecture.
        /// </summary>
        public PTXArchitecture Architecture { get; }

        /// <summary>
        /// Returns true if fast math is active.
        /// </summary>
        public bool FastMath { get; }

        /// <summary>
        /// Returns true if assertions are enabled.
        /// </summary>
        public bool EnableAssertions { get; }

        /// <summary>
        /// Returns the associated string builder.
        /// </summary>
        public StringBuilder Builder { get; }

        #endregion

        #region Register Allocation

        /// <summary cref="PTXRegisterAllocator.Allocate(Value, PTXRegisterKind)"/>
        protected PTXRegister Allocate(Value node, PTXRegisterKind kind) =>
            registerAllocator.Allocate(node, kind);

        /// <summary cref="PTXRegisterAllocator.Alias(Value, Value)"/>
        protected void Alias(Value node, Value alias) =>
            registerAllocator.Alias(node, alias);

        /// <summary cref="PTXRegisterAllocator.AllocatePlatformRegister(out PTXType)"/>
        protected PTXRegister AllocatePlatformRegister(out PTXType postFix) =>
            registerAllocator.AllocatePlatformRegister(out postFix);

        /// <summary cref="PTXRegisterAllocator.AllocatePlatformRegister(Value, out PTXType)"/>
        protected PTXRegister AllocatePlatformRegister(Value node, out PTXType postFix) =>
            registerAllocator.AllocatePlatformRegister(node, out postFix);

        /// <summary cref="PTXRegisterAllocator.AllocateRegister(PTXRegisterKind)"/>
        protected PTXRegister AllocateRegister(PTXRegisterKind kind) =>
            registerAllocator.AllocateRegister(kind);

        /// <summary cref="PTXRegisterAllocator.Free(Value)"/>
        protected void Free(Value value) =>
            registerAllocator.Free(value);

        /// <summary cref="PTXRegisterAllocator.FreeRegister(PTXRegister)"/>
        protected void FreeRegister(PTXRegister register) =>
            registerAllocator.FreeRegister(register);

        /// <summary cref="PTXRegisterAllocator.Load(Value)"/>
        protected PTXRegister Load(Value node)
        {
            if (node is DeviceConstantValue)
                node.Accept(this);
            return registerAllocator.Load(node);
        }

        #endregion

        #region General Code Generation

        /// <summary>
        /// Declares a new label.
        /// </summary>
        /// <returns>The declared label.</returns>
        private string DeclareLabel() => labelPrefix + labelCounter++;

        /// <summary>
        /// Marks the given label.
        /// </summary>
        /// <param name="label">The label to mark.</param>
        protected void MarkLabel(string label)
        {
            Builder.Append('\t');
            Builder.Append(label);
            Builder.AppendLine(":");
        }

        /// <summary>
        /// Generates code for all CFG nodes.
        /// </summary>
        protected void GenerateCode(
            int registerOffset,
            ref int constantOffset)
        {
            var placement = Placement.CreateCSEPlacement(CFG);

            // Build branch targets and allocate phi registers
            foreach (var cfgNode in placement.CFG)
            {
                var functionValue = cfgNode.FunctionValue;
                if (functionValue != TopLevelFunction)
                    blockLookup.Add(functionValue, DeclareLabel());

                var call = functionValue.Target.ResolveAs<FunctionCall>();
                if (call.IsTopLevelCall)
                {
                    foreach (var successor in cfgNode.Successors)
                    {
                        foreach (var param in successor.FunctionValue.AttachedParameters)
                        {
                            if (param.Type.IsMemoryType)
                                continue;

                            var ptxType = PTXType.GetPTXType(param.Type, ABI);
                            Allocate(param, ptxType.RegisterKind);
                        }
                    }
                }
                else
                {
                    var phiParameters = new Value[call.NumArguments];
                    foreach (var successor in cfgNode.Successors)
                    {
                        for (int i = 0, e = phiParameters.Length; i < e; ++i)
                        {
                            var param = successor.FunctionValue.AttachedParameters[i];
                            if (param.Type.IsMemoryType)
                                continue;

                            ref var phiParam = ref phiParameters[i];
                            if (phiParam == null)
                            {
                                var ptxType = PTXType.GetPTXType(param.Type, ABI);
                                Allocate(param, ptxType.RegisterKind);
                                phiParam = param;
                            }
                            else
                                Alias(param, phiParam);
                        }
                    }
                }
            }
            Builder.AppendLine();

            // Generate code
            foreach (var cfgNode in placement.CFG)
            {
                var functionValue = cfgNode.FunctionValue;
                if (functionValue != TopLevelFunction)
                    functionValue.Accept(this);
                using (var placementEnumerator = placement[cfgNode])
                {
                    while (placementEnumerator.MoveNext())
                        placementEnumerator.Current.Accept(this);
                }
                functionValue.Target.Accept(this);
                Builder.AppendLine();
            }

            // Finish kernel and append register information
            Builder.AppendLine("}");

            var registerInfo = registerAllocator.GenerateRegisterInformation("\t");
            Builder.Insert(registerOffset, registerInfo);
            var constantDeclarations = GenerateConstantDeclarations();
            Builder.Insert(constantOffset, constantDeclarations);
            constantOffset += constantDeclarations.Length;
        }

        /// <summary>
        /// Setups local allocations.
        /// </summary>
        /// <param name="allocas">The allocations to setup.</param>
        /// <returns>A list of pairs associating alloca nodes with thei local variable names.</returns>
        protected List<(Alloca, string)> SetupLocalAllocations(Allocas allocas)
        {
            var result = new List<(Alloca, string)>();

            var offset = 0;
            foreach (var allocaInfo in allocas.LocalAllocations)
            {
                Builder.Append('\t');
                Builder.Append(".local ");
                var elementType = allocaInfo.ElementType;
                var elementSize = ABI.GetSizeOf(elementType);

                Builder.Append(".align ");
                Builder.Append(ABI.PointerSize.ToString());
                Builder.Append(" .b8 ");

                var name = "__local_depot" + offset++;
                Builder.Append(name);
                Builder.Append('[');
                Builder.Append(allocaInfo.ArraySize * elementSize);
                Builder.AppendLine("];");

                result.Add((allocaInfo.Alloca, name));
            }
            Builder.AppendLine();

            return result;
        }

        protected List<MappedParameter> SetupParameters<TSetupLogic>(
            ref TSetupLogic logic,
            int paramOffset)
                where TSetupLogic : IParameterSetupLogic
        {
            var attachComma = false;
            var implicitParameters = TopLevelFunction.ParametersOffset;
            var parameters = new List<MappedParameter>(
                TopLevelFunction.AttachedParameters.Length -
                implicitParameters -
                paramOffset);

            var offset = 0;
            foreach (var param in TopLevelFunction.Parameters)
            {
                if (implicitParameters > 0)
                {
                    --implicitParameters;
                    continue;
                }
                var paramType = PTXType.GetPTXType(param.Type, ABI);

                PTXRegister? register = null;
                if (offset < paramOffset)
                {
                    register = logic.HandleIntrinsicParameters(offset, param, paramType);
                    offset++;
                }
                else
                    register = Allocate(param, paramType.RegisterKind);

                if (!register.HasValue)
                    continue;

                if (attachComma)
                {
                    Builder.Append(',');
                    Builder.AppendLine();
                }

                Builder.Append("\t.param .");
                Builder.Append(paramType);
                Builder.Append(' ');

                var paramName = "_param" + param.Id;
                Builder.Append(paramName);
                parameters.Add(
                    new MappedParameter(
                        register.Value,
                        paramType,
                        paramName,
                        param));

                attachComma = true;
            }

            return parameters;
        }

        /// <summary>
        /// Binds the given mapped parameters.
        /// </summary>
        /// <param name="parameters">A list with mapped parameters.</param>
        protected void BindParameters(List<MappedParameter> parameters)
        {
            foreach (var mappedParameter in parameters)
            {
                using (var command = BeginCommand(
                    Instructions.LoadParamOperation, mappedParameter.PTXType))
                {
                    command.AppendArgument(mappedParameter.PTXRegister);
                    command.AppendRawValue(mappedParameter.PTXName);
                }

                // Check for special predicate type
                if (mappedParameter.Parameter.BasicValueType == BasicValueType.Int1)
                {
                    Free(mappedParameter.Parameter);
                    var newParamRegister = Allocate(mappedParameter.Parameter, PTXRegisterKind.Predicate);
                    using (var command = BeginCommand(Instructions.GetCompareOperation(
                        CompareKind.NotEqual,
                        ArithmeticBasicValueType.UInt32)))
                    {
                        command.AppendArgument(newParamRegister);
                        command.AppendArgument(mappedParameter.PTXRegister);
                        command.AppendConstant(0);
                    }
                }
            }
        }

        /// <summary>
        /// Binds the given list of allocations.
        /// </summary>
        /// <param name="allocations">A list associating alloca nodes with thei local names.</param>
        protected void BindAllocations(List<(Alloca, string)> allocations)
        {
            foreach (var allocaEntry in allocations)
            {
                var allocaType = PTXType.GetPTXType(ABI.PointerArithmeticType);
                var targetRegister = Allocate(allocaEntry.Item1, allocaType.RegisterKind);
                using (var command = BeginCommand(
                    Instructions.MoveOperation, allocaType))
                {
                    command.AppendArgument(targetRegister);
                    command.AppendRawValueReference(allocaEntry.Item2);
                }
            }
        }

        /// <summary>
        /// Generate global constant declarations.
        /// </summary>
        /// <returns>The declared global constants in PTX format.</returns>
        private string GenerateConstantDeclarations()
        {
            var declBuilder = new StringBuilder();
            foreach (var constantEntry in constants)
            {
                switch (constantEntry.Key)
                {
                    case StringValue stringValue:
                        declBuilder.Append(".global .align ");
                        declBuilder.Append(ABI.PointerSize.ToString());
                        declBuilder.Append(" .b8 ");
                        declBuilder.Append(constantEntry.Value.Item1);
                        var stringBytes = Encoding.ASCII.GetBytes(stringValue.String);
                        declBuilder.Append("[");
                        declBuilder.Append(stringBytes.Length + 1);
                        declBuilder.Append("]");
                        declBuilder.Append(" = {");
                        foreach (var value in stringBytes)
                        {
                            declBuilder.Append(value);
                            declBuilder.Append(", ");
                        }
                        declBuilder.AppendLine("0};");
                        break;
                    default:
                        throw new InvalidCodeGenerationException();
                }
            }
            return declBuilder.ToString();
        }

        /// <summary>
        /// Determines the PTX on-chip ABI specific size of the given node.
        /// </summary>
        /// <param name="typeNode">The type node.</param>
        /// <returns>The computed PTX on-chip size.</returns>
        protected int GetDeviceCallSizeOf(TypeNode typeNode)
        {
            if (typeNode is PrimitiveType primitiveType)
            {
                switch (primitiveType.BasicValueType)
                {
                    case BasicValueType.Int1:
                    case BasicValueType.Int8:
                    case BasicValueType.Int16:
                    case BasicValueType.Int32:
                    case BasicValueType.Float32:
                        return 4;
                    case BasicValueType.Int64:
                    case BasicValueType.Float64:
                        return 8;
                    default:
                        throw new InvalidCodeGenerationException();
                }
            }
            else if (typeNode is PointerType)
                return ABI.PointerSize;
            else
            {
                var containerType = typeNode as ContainerType;
                Debug.Assert(containerType != null, "Invalid container type");
                int size = 0;
                foreach (var child in containerType.Children)
                    ABI.Align(ref size, GetDeviceCallSizeOf(child));
                return size;
            }
        }

        /// <summary>
        /// Appends return parameter information.
        /// </summary>
        /// <param name="returnType">The return type.</param>
        /// <param name="returnName">The name of the return argument.</param>
        protected void AppendReturnParamInfo(TypeNode returnType, string returnName)
        {
            Builder.Append(".param .");
            if (returnType is PrimitiveType primitiveType)
            {
                var basicRegisterType = PTXType.GetPTXParameterType(primitiveType, ABI);
                Builder.Append(basicRegisterType.Name);
                Builder.Append(" ");
                Builder.Append(returnName);
            }
            else
            {
                Builder.Append("align ");
                Builder.Append(ABI.PointerSize);
                Builder.Append(" .b8 ");
                Builder.Append(returnName);
                Builder.Append("[");
                Builder.Append(GetDeviceCallSizeOf(returnType));
                Builder.Append("]");
            }
        }

        /// <summary>
        /// Creates a return-compatible argument.
        /// </summary>
        /// <param name="argument">The argument to return.</param>
        /// <returns>The compatible ptx register that holds the argument.</returns>
        private PTXRegister ToReturnValue(Value argument)
        {
            var argumentValue = Load(argument);

            // Do we have to convert the parameter?
            if (argument.BasicValueType == BasicValueType.Int1)
            {
                var tempRegister = AllocateRegister(PTXRegisterKind.Int32);
                using (var command = BeginCommand(Instructions.GetSelectValueOperation(
                    BasicValueType.Int32)))
                {
                    command.AppendArgument(tempRegister);
                    command.AppendConstant(1);
                    command.AppendConstant(0);
                    command.AppendArgument(argumentValue);
                }
                argumentValue = tempRegister;
            }

            return argumentValue;
        }

        /// <summary>
        /// Creates a return instruction.
        /// </summary>
        /// <param name="functionCall">The return call.</param>
        private void MakeReturn(FunctionCall functionCall)
        {
            var firstParamIndex = TopLevelFunction.MemoryParameterIndex + 1;
            int offset = 0;
            for (int i = firstParamIndex, e = functionCall.NumArguments; i < e; ++i)
            {
                var argument = functionCall.GetArgument(i);
                var argumentType = PTXType.GetPTXParameterType(argument.Type, ABI);
                var argumentValue = ToReturnValue(argument);
                using (var command = BeginCommand(Instructions.StoreParamOperation, argumentType))
                {
                    var fieldOffset = ABI.Align(ref offset, argument.Type);
                    command.AppendRawValue(returnParamName, fieldOffset);
                    command.AppendArgument(argumentValue);
                }
            }
            Command(Instructions.ReturnOperation, null);
        }

        /// <summary>
        /// Constructs a call to a top-level function.
        /// </summary>
        /// <param name="functionCall">The function call.</param>
        private void MakeGlobalCall(FunctionCall functionCall)
        {
            const string ReturnValueName = "callRetVal";
            const string CallParamName = "callParam";

            var target = functionCall.Target.ResolveAs<TopLevelFunction>();
            Debug.Assert(functionCall.IsTopLevelCall);

            // Create call sequence
            Builder.AppendLine("\t{");

            var numParams = target.AttachedParameters.Length - TopLevelFunction.ParametersOffset;
            for (int i = 0; i < numParams; ++i)
            {
                var argument = functionCall.GetArgument(i + TopLevelFunction.ParametersOffset);
                var paramName = CallParamName + i;
                Builder.Append("\t.param .");
                var basicRegisterType = PTXType.GetPTXParameterType(argument.Type, ABI);
                Builder.Append(basicRegisterType.Name);
                Builder.Append(" ");
                Builder.Append(paramName);
                Builder.AppendLine(";");

                var argumentValue = ToReturnValue(argument);

                // Emit store param command
                using (var command = BeginCommand(Instructions.StoreParamOperation))
                {
                    command.AppendPostFix(basicRegisterType);
                    command.AppendRawValue(paramName);
                    command.AppendArgument(argumentValue);
                }
            }

            // Reserve a sufficient amount of memory
            var returnType = target.ReturnType;
            if (!returnType.IsVoidType)
            {
                Builder.Append("\t");
                AppendReturnParamInfo(returnType, ReturnValueName);
                Builder.AppendLine(";");
                Builder.Append("\tcall ");
                Builder.Append("(");
                Builder.Append(ReturnValueName);
                Builder.Append("), ");
            }
            else
            {
                Builder.Append("\tcall ");
            }
            Builder.Append(GetFunctionName(target));
            Builder.AppendLine(", (");
            for (int i = 0; i < numParams; ++i)
            {
                Builder.Append("\t\t");
                Builder.Append(CallParamName);
                Builder.Append(i);
                if (i + 1 < numParams)
                    Builder.AppendLine(",");
                else
                    Builder.AppendLine();
            }
            Builder.AppendLine("\t);");

            if (!returnType.IsVoidType)
            {
                // Take the return parameters from the continuation
                var returnContinuation = functionCall.Arguments[
                    TopLevelFunction.ReturnParameterIndex].ResolveAs<FunctionValue>();
                int offset = 0;
                for (int i = TopLevelFunction.MemoryParameterIndex + 1,
                    e = returnContinuation.AttachedParameters.Length; i < e; ++i)
                {
                    var attachedParameter = returnContinuation.AttachedParameters[i];
                    var attachedRegister = Load(attachedParameter);

                    PTXRegister returnRegister;
                    if (attachedParameter.BasicValueType == BasicValueType.Int1)
                        returnRegister = AllocateRegister(PTXRegisterKind.Int32);
                    else
                        returnRegister = attachedRegister;

                    var registerType = PTXType.GetPTXParameterType(attachedParameter.Type, ABI);
                    using (var command = BeginCommand(Instructions.LoadParamOperation, registerType))
                    {
                        var fieldOffset = ABI.Align(ref offset, attachedParameter.Type);
                        command.AppendArgument(returnRegister);
                        command.AppendRawValue(ReturnValueName, fieldOffset);
                    }

                    if (returnRegister.Kind != attachedRegister.Kind)
                    {
                        using (var command = BeginCommand(Instructions.GetCompareOperation(
                            CompareKind.NotEqual,
                            ArithmeticBasicValueType.UInt32)))
                        {
                            command.AppendArgument(attachedRegister);
                            command.AppendArgument(returnRegister);
                            command.AppendConstant(0);
                        }
                    }
                }
            }

            Builder.AppendLine("\t}");
        }

        /// <summary>
        /// Constructs local call to a basic-block-like local function.
        /// </summary>
        /// <param name="functionCall">The function call.</param>
        private void MakeLocalCall(FunctionCall functionCall)
        {
            var target = functionCall.Target.Resolve();
            Debug.Assert(!functionCall.IsTopLevelCall);

            switch (target)
            {
                case Predicate predicate:
                    var trueTarget = predicate.TrueValue.ResolveAs<FunctionValue>();
                    WirePhiArguments(functionCall, trueTarget);
                    var condition = Load(predicate.Condition);
                    Debug.Assert(
                        !trueTarget.IsTopLevel,
                        "Not supported top-level predicate");
                    Debug.Assert(
                        !predicate.FalseValue.ResolveAs<FunctionValue>().IsTopLevel,
                        "Not supported top-level predicate");
                    using (var command = BeginCommand(
                        Instructions.BranchOperation,
                        null,
                        new PredicateConfiguration(condition, true)))
                        command.AppendLabel(blockLookup[trueTarget]);
                    break;
                case SelectPredicate selectPredicate:
                    WirePhiArguments(functionCall, selectPredicate.DefaultArgument.ResolveAs<FunctionValue>());

                    var idx = Load(selectPredicate.Condition);
                    var predicateRegister = AllocateRegister(PTXRegisterKind.Predicate);
                    var targetPredicateRegister = AllocateRegister(PTXRegisterKind.Predicate);

                    // Emit less than
                    var lessThanCommand = Instructions.GetCompareOperation(
                        CompareKind.LessThan,
                        ArithmeticBasicValueType.Int32);
                    using (var command = BeginCommand(
                        lessThanCommand))
                    {
                        command.AppendArgument(predicateRegister);
                        command.AppendArgument(idx);
                        command.AppendConstant(0);
                    }
                    using (var command = BeginCommand(
                        Instructions.BranchIndexRangeComparison))
                    {
                        command.AppendArgument(targetPredicateRegister);
                        command.AppendArgument(idx);
                        command.AppendConstant(selectPredicate.NumCasesWithoutDefault);
                        command.AppendArgument(predicateRegister);
                    }
                    using (var command = BeginCommand(
                        Instructions.BranchOperation,
                        null,
                        new PredicateConfiguration(targetPredicateRegister, true)))
                    {
                        command.AppendLabel(blockLookup[
                            selectPredicate.DefaultArgument.ResolveAs<FunctionValue>()]);
                    }

                    var targetLabel = DeclareLabel();
                    MarkLabel(targetLabel);
                    Builder.Append('\t');
                    Builder.Append(Instructions.BranchTargetsDeclaration);
                    Builder.Append(' ');
                    for (int i = 0, e = selectPredicate.NumCasesWithoutDefault; i < e; ++i)
                    {
                        Builder.Append(blockLookup[
                            selectPredicate.GetCaseArgument(i).ResolveAs<FunctionValue>()]);
                        if (i + 1 < e)
                            Builder.Append(", ");
                    }
                    Builder.AppendLine(";");

                    using (var command = BeginCommand(
                        Instructions.BranchIndexOperation))
                    {
                        command.AppendArgument(idx);
                        command.AppendLabel(targetLabel);
                    }

                    FreeRegister(predicateRegister);
                    FreeRegister(targetPredicateRegister);

                    break;
                case Parameter _:
                    // This is the return parameter
                    MakeReturn(functionCall);
                    break;
                case FunctionValue function:
                    // Emit local branch
                    WirePhiArguments(functionCall, function);
                    using (var command = BeginCommand(Instructions.BranchOperation, null))
                        command.AppendLabel(blockLookup[function]);
                    break;
                default:
                    throw new InvalidCodeGenerationException();
            }
        }

        /// <summary>
        /// Connects arguments to phi parameters of the given call.
        /// </summary>
        /// <param name="functionCall">The function call.</param>
        /// <param name="functionTarget">A representative function target.</param>
        private void WirePhiArguments(FunctionCall functionCall, FunctionValue functionTarget)
        {
            for (int i = 0, e = functionCall.NumArguments; i < e; ++i)
            {
                var arg = functionCall.GetArgument(i);
                if (arg.Type.IsMemoryType)
                    continue;
                var argumentRegister = Load(arg);
                var parameterRegister = Load(functionTarget.AttachedParameters[i]);
                Move(argumentRegister, parameterRegister);
            }
        }

        #endregion
    }
}
