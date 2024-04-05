using ILGPU.IR;
using ILGPU.IR.Types;
using ILGPU.IR.Values;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System;
using System.Collections.Generic;
using System.Linq;

using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace ILGPU.Backends.IR
{
    partial class IRBuilderCodeGenerator : IBackendCodeGenerator<MethodDeclarationSyntax>
    {
        private readonly Dictionary<NodeId, string> nodes;

        private MethodDeclarationSyntax builder;

        public Method EntryPointMethod { get; }

        public IRBuilderCodeGenerator(Method entryPointMethod)
        {
            nodes = new();

            builder = MethodDeclaration(ParseTypeName("ILGPU.IR.Method"), Identifier("GetMethod"))
                .AddParameterListParameters(
                    Parameter(SingletonList(AttributeList()), TokenList(), ParseTypeName("ILGPU.Context"), Identifier("context"), null)
                    )
                .AddBodyStatements(
                    DeclareVariable("irContext", "ILGPU.IR.IRContext", "context.GetIRContext()")
                    );

            EntryPointMethod = entryPointMethod;
        }

        private static LocalDeclarationStatementSyntax DeclareVariable(string varName, string typeName, string expr) =>
            LocalDeclarationStatement(
                        VariableDeclaration(
                            ParseTypeName(typeName),
                            SeparatedList([
                                VariableDeclarator(
                                    Identifier(varName), null,
                                    EqualsValueClause(
                                        ParseExpression(expr)
                                        )
                                    )
                                ])
                            )
                        );

        private void GenerateCode(TypeNode type)
        {
            if (nodes.ContainsKey(type.Id))
            {
                return;
            }
            else
            {
                nodes.Add(type.Id, $"node_{type.Id}");
            }

            if (type is StructureType structure)
            {
                builder = builder.AddBodyStatements(
                    DeclareVariable($"builder_{structure.Id}", "ILGPU.IR.Types.StructureType.Builder", "irContext.CreateStructureType(0)")
                    );

                foreach (var field in structure.Fields)
                {
                    GenerateCode(field);
                }

                builder = builder
                    .AddBodyStatements(structure.Fields.Select(fld =>
                        ParseStatement($"builder_{structure.Id}.Add(node_{fld.Id});")
                        ).ToArray())
                    .AddBodyStatements(DeclareVariable($"node_{structure.Id}", "ILGPU.IR.Types.StructureType", $"builder_{structure.Id}.Seal()"));
            }
            else if (type is PrimitiveType primitive)
            {
                builder = builder.AddBodyStatements(
                    DeclareVariable($"node_{primitive.Id}", "ILGPU.IR.Types.PrimitiveType", $"irContext.GetPrimitiveType((BasicValueType){(int)primitive.BasicValueType})")
                    );
            }
            else if (type is ArrayType array)
            {
                GenerateCode(array.ElementType);
                builder = builder.AddBodyStatements(
                    DeclareVariable($"node_{array.Id}", "ILGPU.IR.Types.ArrayType", $"irContext.CreateArrayType(node_{array.Id}, {array.NumDimensions})")
                    );
            }
            else if (type is ViewType view)
            {
                GenerateCode(view.ElementType);
                builder = builder.AddBodyStatements(
                    DeclareVariable($"node_{view.Id}", "ILGPU.IR.Types.ViewType", $"irContext.CreateViewType(node_{view.Id}, (ILGPU.IR.MemoryAddressSpace){(int)view.AddressSpace})")
                    );
            }
            else if (type is PointerType pointer)
            {
                GenerateCode(pointer.ElementType);
                builder = builder.AddBodyStatements(
                    DeclareVariable($"node_{pointer.Id}", "ILGPU.IR.Types.ViewType", $"irContext.CreatePointerType(node_{pointer.Id}, (ILGPU.IR.MemoryAddressSpace){(int)pointer.AddressSpace})")
                    );
            }
            else if (type.IsStringType)
            {
                builder = builder.AddBodyStatements(
                    DeclareVariable($"node_{type.Id}", "ILGPU.IR.Types.StringType", "irContext.StringType")
                    );
            }
            else if (type.IsVoidType)
            {
                builder = builder.AddBodyStatements(
                    DeclareVariable($"node_{type.Id}", "ILGPU.IR.Types.VoidType", "irContext.VoidType")
                    );
            }
        }

        private void GenerateCode(Method method)
        {
            if (nodes.ContainsKey(method.Id))
            {
                return;
            }
            else
            {
                nodes.Add(method.Id, $"node_{method.Id}");
            }

            GenerateCode(method.ReturnType);
            foreach (var parameter in method.Parameters)
            {
                GenerateCode(parameter.Type);
                nodes.Add(parameter.Id, $"node_{parameter.Id}");
            }

            builder = builder.AddBodyStatements(
                DeclareVariable($"node_{method.Id}", "ILGPU.IR.Method", $"irContext.Declare(new MethodDeclaration(\"{method.Name}\", node_{method.ReturnType.Id})")
                ).AddBodyStatements(
                method.Parameters.Select(prm =>
                DeclareVariable($"node_{prm.Id}", "ILGPU.IR.Values.Parameter", $"node_{method.Id}.Builder.AddParameter(node_{prm.Type.Id})"))
                .ToArray()
                );

            foreach (BasicBlock block in method.Blocks)
            {
                if (nodes.ContainsKey(block.Id))
                {
                    continue;
                }
                else
                {
                    nodes.Add(block.Id, $"node_{block.Id}");
                }

                builder = builder.AddBodyStatements(
                    DeclareVariable($"node_{block.Id}", "ILGPU.IL.BasicBlock.Builder", $"method_{method.Id}.CreateBasicBlock(Location.Unknown)")
                    );

                foreach (Value value in block)
                {
                    this.GenerateCodeFor(value);
                }
            }
        }

        public void GenerateCode() => GenerateCode(EntryPointMethod);
        public void GenerateCode(MethodCall methodCall)
        {
            GenerateCode(methodCall.Method);
            builder = builder.AddBodyStatements(
                DeclareVariable($"node_{methodCall.Id}", "ILGPU.IR.Values.MethodCall", $"node_{methodCall.BasicBlock.Id}.CreateCall(Location.Unknown, node_{methodCall.Method.Id}))")
                );
        }
        public void GenerateCode(PhiValue phiValue) => throw new NotImplementedException();
        public void GenerateCode(Parameter parameter) => throw new NotImplementedException();
        public void GenerateCode(UnaryArithmeticValue value) => throw new NotImplementedException();
        public void GenerateCode(BinaryArithmeticValue value) => throw new NotImplementedException();
        public void GenerateCode(TernaryArithmeticValue value) => throw new NotImplementedException();
        public void GenerateCode(CompareValue value) => throw new NotImplementedException();
        public void GenerateCode(ConvertValue value) => throw new NotImplementedException();
        public void GenerateCode(IntAsPointerCast cast) => throw new NotImplementedException();
        public void GenerateCode(PointerAsIntCast cast) => throw new NotImplementedException();
        public void GenerateCode(PointerCast cast) => throw new NotImplementedException();
        public void GenerateCode(AddressSpaceCast value) => throw new NotImplementedException();
        public void GenerateCode(FloatAsIntCast value) => throw new NotImplementedException();
        public void GenerateCode(IntAsFloatCast value) => throw new NotImplementedException();
        public void GenerateCode(Predicate predicate) => throw new NotImplementedException();
        public void GenerateCode(GenericAtomic atomic) => throw new NotImplementedException();
        public void GenerateCode(AtomicCAS atomicCAS) => throw new NotImplementedException();
        public void GenerateCode(Alloca alloca) => throw new NotImplementedException();
        public void GenerateCode(MemoryBarrier barrier) => throw new NotImplementedException();
        public void GenerateCode(Load load) => throw new NotImplementedException();
        public void GenerateCode(Store store) => throw new NotImplementedException();
        public void GenerateCode(LoadElementAddress value) => throw new NotImplementedException();
        public void GenerateCode(LoadFieldAddress value) => throw new NotImplementedException();
        public void GenerateCode(AlignTo value) => throw new NotImplementedException();
        public void GenerateCode(AsAligned value) => throw new NotImplementedException();
        public void GenerateCode(PrimitiveValue value) => throw new NotImplementedException();
        public void GenerateCode(StringValue value) => throw new NotImplementedException();
        public void GenerateCode(NullValue value) => throw new NotImplementedException();
        public void GenerateCode(StructureValue value) => throw new NotImplementedException();
        public void GenerateCode(GetField value) => throw new NotImplementedException();
        public void GenerateCode(SetField value) => throw new NotImplementedException();
        public void GenerateCode(GridIndexValue value) => throw new NotImplementedException();
        public void GenerateCode(GroupIndexValue value) => throw new NotImplementedException();
        public void GenerateCode(GridDimensionValue value) => throw new NotImplementedException();
        public void GenerateCode(GroupDimensionValue value) => throw new NotImplementedException();
        public void GenerateCode(WarpSizeValue value) => throw new NotImplementedException();
        public void GenerateCode(LaneIdxValue value) => throw new NotImplementedException();
        public void GenerateCode(DynamicMemoryLengthValue value) => throw new NotImplementedException();
        public void GenerateCode(PredicateBarrier barrier) => throw new NotImplementedException();
        public void GenerateCode(Barrier barrier) => throw new NotImplementedException();
        public void GenerateCode(Broadcast broadcast) => throw new NotImplementedException();
        public void GenerateCode(WarpShuffle shuffle) => throw new NotImplementedException();
        public void GenerateCode(SubWarpShuffle shuffle) => throw new NotImplementedException();
        public void GenerateCode(DebugAssertOperation debug) => throw new NotImplementedException();
        public void GenerateCode(WriteToOutput writeToOutput) => throw new NotImplementedException();
        public void GenerateCode(ReturnTerminator returnTerminator) => throw new NotImplementedException();
        public void GenerateCode(UnconditionalBranch branch) => throw new NotImplementedException();
        public void GenerateCode(IfBranch branch) => throw new NotImplementedException();
        public void GenerateCode(SwitchBranch branch) => throw new NotImplementedException();
        public void GenerateCode(LanguageEmitValue emit) => throw new NotImplementedException();
        public void GenerateConstants(MethodDeclarationSyntax builder) => throw new NotImplementedException();
        public void GenerateHeader(MethodDeclarationSyntax builder) => throw new NotImplementedException();
        public void Merge(MethodDeclarationSyntax builder) => throw new NotImplementedException();
    }
}
