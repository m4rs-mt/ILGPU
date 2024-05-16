using ILGPU.IR.Types;
using ILGPU.IR.Values;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ILGPU.IR
{
    internal sealed class IRImporter
    {
        private readonly IRContainer.Exported container;

        private readonly Dictionary<long, TypeNode> types;

        private readonly Dictionary<long, Method.Builder> methods;
        private readonly Dictionary<long, BasicBlock.Builder> blocks;
        private readonly Dictionary<long, Value> values;

        public static IEnumerable<T> TSort<T>(IEnumerable<T> source, Func<T, IEnumerable<T>> dependencies, bool throwOnCycle = false)
        {
            var sorted = new List<T>();
            var visited = new HashSet<T>();

            foreach (var item in source)
                Visit(item, visited, sorted, dependencies, throwOnCycle);

            return sorted;
        }

        private static void Visit<T>(T item, HashSet<T> visited, List<T> sorted, Func<T, IEnumerable<T>> dependencies, bool throwOnCycle)
        {
            if (!visited.Contains(item))
            {
                visited.Add(item);

                foreach (var dep in dependencies(item))
                    Visit(dep, visited, sorted, dependencies, throwOnCycle);

                sorted.Add(item);
            }
            else
            {
                if (throwOnCycle && !sorted.Contains(item))
                    throw new Exception("Cyclic dependency found");
            }
        }

        public IRImporter(IRContainer.Exported container)
        {
            this.container = container;

            types = new Dictionary<long, TypeNode>();

            methods = new Dictionary<long, Method.Builder>();
            blocks = new Dictionary<long, BasicBlock.Builder>();
            values = new Dictionary<long, Value>();
        }

        public IReadOnlyDictionary<long, Method> ImportInto(IRContext context)
        {
            foreach (var type in TSort(container.Types, x => container.Types.Where(y => x.Nodes.Contains(y.Id))))
            {
                switch (type.Class)
                {
                    case IRType.Classifier.Void:
                        types.Add(type.Id, context.VoidType);
                        break;
                    case IRType.Classifier.String:
                        types.Add(type.Id, context.StringType);
                        break;
                    case IRType.Classifier.Primitive:
                        types.Add(type.Id, context.GetPrimitiveType(type.BasicValueType));
                        break;
                    case IRType.Classifier.Padding:
                        types.Add(type.Id, context.TypeContext.GetPaddingType(type.BasicValueType));
                        break;
                    case IRType.Classifier.Pointer:
                        types.Add(type.Id, context.CreatePointerType(types[type.Nodes[0]], (MemoryAddressSpace)type.Data));
                        break;
                    case IRType.Classifier.View:
                        types.Add(type.Id, context.CreateViewType(types[type.Nodes[0]], (MemoryAddressSpace)type.Data));
                        break;
                    case IRType.Classifier.Array:
                        types.Add(type.Id, context.CreateArrayType(types[type.Nodes[0]], (int)type.Data));
                        break;
                    case IRType.Classifier.Structure:
                        var builder = context.CreateStructureType();
                        foreach (var field in type.Nodes)
                        {
                            builder.Add(types[field]);
                        }
                        types.Add(type.Id, builder.Seal());
                        break;
                }
            }

            foreach (var method in container.Methods)
            {
                var irMethod = context.Declare(new MethodDeclaration(new MethodHandle(method.Id, method.Name), types[method.ReturnType]), out bool created);
                methods.Add(method.Id, irMethod.CreateBuilder());

                irMethod.EntryBlock.GetOrCreateBuilder(irMethod.MethodBuilder, out BasicBlock.Builder entryBuilder);
                blocks.Add(method.Blocks[0], entryBuilder);

                foreach (var block in method.Blocks.Skip(1))
                {
                    var builder = irMethod.MethodBuilder.CreateBasicBlock(Location.Unknown);
                    blocks.Add(block, builder);
                }
            }

            List<(IRValue, PhiValue.Builder)> phiBuilders = new();
            foreach (var value in TSort(container.Values, x => x.ValueKind == ValueKind.Phi ? [] : container.Values.Where(y => x.Nodes.Contains(y.Id))))
            {
                Value irValue;
                if (value.BasicBlock < 0 && value.ValueKind == ValueKind.Parameter)
                {
                    // CASE: ValueKind.Parameter
                    irValue = methods[value.Method].AddParameter(types[value.Type], value.Tag);
                }
                else
                {
                    BasicBlock.Builder builder = blocks[value.BasicBlock];
                    switch (value.ValueKind)
                    {
                        case ValueKind.MethodCall:
                            var call = builder.CreateCall(Location.Unknown, methods[value.Data].Method);
                            foreach (var arg in value.Nodes)
                            {
                                call.Add(values[arg]);
                            }
                            irValue = call.Seal();
                            break;
                        case ValueKind.Phi:
                            var phi = builder.CreatePhi(Location.Unknown, types[value.Type]);
                            irValue = phi.PhiValue;
                            phiBuilders.Add((value, phi));
                            break;
                        case ValueKind.UnaryArithmetic:
                            irValue = builder.CreateArithmetic(Location.Unknown,
                                values[value.Nodes[0]], (UnaryArithmeticKind)value.Data);
                            break;
                        case ValueKind.BinaryArithmetic:
                            irValue = builder.CreateArithmetic(Location.Unknown,
                                values[value.Nodes[0]], values[value.Nodes[1]],
                                (BinaryArithmeticKind)value.Data);
                            break;
                        case ValueKind.TernaryArithmetic:
                            irValue = builder.CreateArithmetic(Location.Unknown,
                                values[value.Nodes[0]], values[value.Nodes[1]], values[value.Nodes[2]],
                                (TernaryArithmeticKind)value.Data);
                            break;
                        case ValueKind.Compare:
                            irValue = builder.CreateCompare(Location.Unknown,
                                values[value.Nodes[0]], values[value.Nodes[1]],
                                (CompareKind)(value.Data >> 32), (CompareFlags)value.Data
                                );
                            break;
                        case ValueKind.Convert:
                            if (types[value.Type].IsPrimitiveType)
                            {
                                irValue = builder.CreateConvert(Location.Unknown,
                                    values[value.Nodes[0]], types[value.Type].BasicValueType
                                    );
                            }
                            else
                            {
                                irValue = builder.CreateConvert(Location.Unknown,
                                    values[value.Nodes[0]], types[value.Type],
                                    (ConvertFlags)value.Data
                                    );
                            }
                            break;
                        case ValueKind.IntAsPointerCast:
                            irValue = builder.CreateIntAsPointerCast(Location.Unknown,
                                values[value.Nodes[0]]);
                            break;
                        case ValueKind.PointerAsIntCast:
                            irValue = builder.CreatePointerAsIntCast(Location.Unknown,
                                values[value.Nodes[0]], types[value.Type].BasicValueType);
                            break;
                        case ValueKind.PointerCast:
                            irValue = builder.CreatePointerCast(Location.Unknown,
                                values[value.Nodes[0]],
                                ((AddressSpaceType)types[value.Type]).ElementType);
                            break;
                        case ValueKind.AddressSpaceCast:
                            irValue = builder.CreateAddressSpaceCast(Location.Unknown,
                                values[value.Nodes[0]], (MemoryAddressSpace)value.Data);
                            break;
                        case ValueKind.ViewCast:
                            irValue = builder.CreateViewCast(Location.Unknown,
                                values[value.Nodes[0]],
                                ((AddressSpaceType)types[value.Type]).ElementType);
                            break;
                        case ValueKind.ArrayToViewCast:
                            irValue = builder.CreateArrayToViewCast(Location.Unknown,
                                values[value.Nodes[0]]);
                            break;
                        case ValueKind.FloatAsIntCast:
                            irValue = builder.CreateFloatAsIntCast(Location.Unknown,
                                values[value.Nodes[0]]);
                            break;
                        case ValueKind.IntAsFloatCast:
                            irValue = builder.CreateIntAsFloatCast(Location.Unknown,
                                values[value.Nodes[0]]);
                            break;
                        case ValueKind.Predicate:
                            irValue = builder.CreatePredicate(Location.Unknown,
                                values[value.Nodes[0]], values[value.Nodes[1]], values[value.Nodes[2]]);
                            break;
                        case ValueKind.GenericAtomic:
                            irValue = builder.CreateAtomic(Location.Unknown,
                                values[value.Nodes[0]], values[value.Nodes[1]],
                                (AtomicKind)(value.Data >> 32), (AtomicFlags)value.Data);
                            break;
                        case ValueKind.AtomicCAS:
                            irValue = builder.CreateAtomicCAS(Location.Unknown,
                                values[value.Nodes[0]], values[value.Nodes[1]], values[value.Nodes[2]],
                                (AtomicFlags)value.Data);
                            break;
                        case ValueKind.Alloca:
                            if (value.Nodes.Length > 0 && values.TryGetValue(value.Nodes[0], out Value? allocaLen))
                            {
                                irValue = builder.CreateAlloca(Location.Unknown,
                                    ((AddressSpaceType)types[value.Type]).ElementType,
                                    (MemoryAddressSpace)value.Data,
                                    allocaLen);
                            }
                            else
                            {
                                irValue = builder.CreateAlloca(Location.Unknown,
                                    ((AddressSpaceType)types[value.Type]).ElementType,
                                    (MemoryAddressSpace)value.Data);
                            }
                            break;
                        case ValueKind.MemoryBarrier:
                            irValue = builder.CreateMemoryBarrier(Location.Unknown,
                                (MemoryBarrierKind)value.Data);
                            break;
                        case ValueKind.Load:
                            irValue = builder.CreateLoad(Location.Unknown,
                                values[value.Nodes[0]]);
                            break;
                        case ValueKind.Store:
                            irValue = builder.CreateStore(Location.Unknown,
                                values[value.Nodes[0]], values[value.Nodes[1]]);
                            break;
                        case ValueKind.SubView:
                            irValue = builder.CreateSubViewValue(Location.Unknown,
                                values[value.Nodes[0]], values[value.Nodes[1]], values[value.Nodes[2]]);
                            break;
                        case ValueKind.LoadElementAddress:
                            irValue = builder.CreateLoadElementAddress(Location.Unknown,
                                values[value.Nodes[0]], values[value.Nodes[1]]);
                            break;
                        case ValueKind.LoadArrayElementAddress:
                            var loadArrElem = builder.CreateLoadArrayElementAddress(Location.Unknown,
                                values[value.Nodes[0]]);
                            foreach (var dim in value.Nodes.Skip(1))
                            {
                                loadArrElem.Add(values[dim]);
                            }
                            irValue = loadArrElem.Seal();
                            break;
                        case ValueKind.LoadFieldAddress:
                            irValue = builder.CreateLoadFieldAddress(Location.Unknown,
                                values[value.Nodes[0]], new FieldSpan((int)(value.Data >> 32), (int)value.Data));
                            break;
                        case ValueKind.NewView:
                            irValue = builder.CreateNewView(Location.Unknown,
                                values[value.Nodes[0]], values[value.Nodes[1]]);
                            break;
                        case ValueKind.GetViewLength:
                            irValue = builder.CreateGetViewLength(Location.Unknown,
                                values[value.Nodes[0]], types[value.Type].BasicValueType);
                            break;
                        case ValueKind.AlignTo:
                            irValue = builder.CreateAlignTo(Location.Unknown,
                                values[value.Nodes[0]], values[value.Nodes[1]]);
                            break;
                        case ValueKind.AsAligned:
                            irValue = builder.CreateAsAligned(Location.Unknown,
                                values[value.Nodes[0]], values[value.Nodes[1]]);
                            break;
                        case ValueKind.Array:
                            var newArr = builder.CreateNewArray(Location.Unknown,
                                (ArrayType)types[value.Type]);
                            foreach (var dim in value.Nodes)
                            {
                                newArr.Add(values[dim]);
                            }
                            irValue = newArr.Seal();
                            break;
                        case ValueKind.GetArrayLength:
                            if (value.Nodes.Length > 1 && values.TryGetValue(value.Nodes[1], out Value? lenDimValue))
                            {
                                irValue = builder.CreateGetArrayLength(Location.Unknown,
                                    values[value.Nodes[0]], values[value.Nodes[1]]);
                            }
                            else
                            {
                                irValue = builder.CreateGetArrayLength(Location.Unknown,
                                    values[value.Nodes[0]]);
                            }
                            break;
                        case ValueKind.Primitive:
                            irValue = builder.CreatePrimitiveValue(Location.Unknown,
                                types[value.Type].BasicValueType, value.Data);
                            break;
                        case ValueKind.String:
                            irValue = builder.CreatePrimitiveValue(Location.Unknown,
                                value.Tag ?? string.Empty, Encoding.GetEncoding((int)value.Data));
                            break;
                        case ValueKind.Null:
                            irValue = builder.CreateNull(Location.Unknown,
                                types[value.Type]);
                            break;
                        case ValueKind.Structure:
                            var @struct = builder.CreateStructure(Location.Unknown,
                                (StructureType)types[value.Type]);
                            foreach (var field in value.Nodes)
                            {
                                @struct.Add(values[field]);
                            }
                            irValue = @struct.Seal();
                            break;
                        case ValueKind.GetField:
                            irValue = builder.CreateGetField(Location.Unknown,
                                values[value.Nodes[0]], new FieldSpan((int)(value.Data >> 32), (int)value.Data));
                            break;
                        case ValueKind.SetField:
                            irValue = builder.CreateSetField(Location.Unknown,
                                values[value.Nodes[0]], new FieldSpan((int)(value.Data >> 32), (int)value.Data), values[value.Nodes[1]]);
                            break;
                        case ValueKind.AcceleratorType:
                            irValue = builder.CreateAcceleratorTypeValue(Location.Unknown);
                            break;
                        case ValueKind.GridIndex:
                            irValue = builder.CreateGridIndexValue(Location.Unknown,
                                (DeviceConstantDimension3D)value.Data);
                            break;
                        case ValueKind.GroupIndex:
                            irValue = builder.CreateGroupIndexValue(Location.Unknown,
                                (DeviceConstantDimension3D)value.Data);
                            break;
                        case ValueKind.GridDimension:
                            irValue = builder.CreateGridDimensionValue(Location.Unknown,
                                (DeviceConstantDimension3D)value.Data);
                            break;
                        case ValueKind.GroupDimension:
                            irValue = builder.CreateGroupDimensionValue(Location.Unknown,
                                (DeviceConstantDimension3D)value.Data);
                            break;
                        case ValueKind.WarpSize:
                            irValue = builder.CreateWarpSizeValue(Location.Unknown);
                            break;
                        case ValueKind.LaneIdx:
                            irValue = builder.CreateLaneIdxValue(Location.Unknown);
                            break;
                        case ValueKind.DynamicMemoryLength:
                            irValue = builder.CreateDynamicMemoryLengthValue(Location.Unknown,
                                types[value.Type], (MemoryAddressSpace)value.Data);
                            break;
                        case ValueKind.PredicateBarrier:
                            irValue = builder.CreateBarrier(Location.Unknown,
                                values[value.Nodes[0]], (PredicateBarrierKind)value.Data);
                            break;
                        case ValueKind.Barrier:
                            irValue = builder.CreateBarrier(Location.Unknown,
                                (BarrierKind)value.Data);
                            break;
                        case ValueKind.Broadcast:
                            irValue = builder.CreateBroadcast(Location.Unknown,
                                values[value.Nodes[0]], values[value.Nodes[1]],
                                (BroadcastKind)value.Data);
                            break;
                        case ValueKind.WarpShuffle:
                            irValue = builder.CreateShuffle(Location.Unknown,
                                values[value.Nodes[0]], values[value.Nodes[1]],
                                (ShuffleKind)value.Data);
                            break;
                        case ValueKind.SubWarpShuffle:
                            irValue = builder.CreateShuffle(Location.Unknown,
                                values[value.Nodes[0]], values[value.Nodes[1]], values[value.Nodes[2]],
                                (ShuffleKind)value.Data);
                            break;
                        case ValueKind.Undefined:
                            irValue = builder.CreateUndefined();
                            break;
                        case ValueKind.Handle:
                            irValue = builder.CreateRuntimeHandle(Location.Unknown,
                                new object());
                            break;
                        case ValueKind.DebugAssert:
                            irValue = builder.CreateDebugAssert(Location.Unknown,
                                values[value.Nodes[0]], values[value.Nodes[1]]);
                            break;
                        case ValueKind.WriteToOutput:
                            irValue = builder.CreateUndefined();
                            break;
                        case ValueKind.Return:
                            if (values.TryGetValue(value.Nodes[0], out Value? retVal))
                            {
                                irValue = builder.CreateReturn(Location.Unknown,
                                    values[value.Nodes[0]]);
                            }
                            else
                            {
                                irValue = builder.CreateReturn(Location.Unknown);
                            }
                            break;
                        case ValueKind.UnconditionalBranch:
                            irValue = builder.CreateBranch(Location.Unknown,
                                blocks[value.Nodes[0]]);
                            break;
                        case ValueKind.IfBranch:
                            irValue = builder.CreateIfBranch(Location.Unknown,
                                values[value.Nodes[0]], blocks[value.Nodes[1]], blocks[value.Nodes[2]],
                                (IfBranchFlags)value.Data);
                            break;
                        case ValueKind.SwitchBranch:
                            var switchBr = builder.CreateSwitchBranch(Location.Unknown,
                                values[value.Nodes[0]]);
                            foreach (var target in value.Nodes.Skip(1))
                            {
                                switchBr.Add(blocks[target]);
                            }
                            irValue = switchBr.Seal();
                            break;
                        case ValueKind.LanguageEmit:
                            irValue = builder.CreateUndefined();
                            break;
                        default:
                            throw new InvalidOperationException($"Cannot import {value.ValueKind}!");
                    }
                }

                values.Add(value.Id, irValue);
            }

            foreach (var (value, phi) in phiBuilders)
            {
                foreach (var (block, arg) in value.Nodes
                                .Select((x, i) => (x, i))
                                .GroupBy(x => x.i / 2)
                                .Select(g => (g.ElementAt(0).x, g.ElementAt(1).x))
                                )
                {
                    phi.AddArgument(blocks[block], values[arg]);
                }

                phi.Seal();
            }

            foreach (var blockBuilder in blocks.Values)
            {
                blockBuilder.Dispose();
            }

            var resultMapping = new Dictionary<long, Method>();
            foreach (var (id, methodBuilder) in methods)
            {
                resultMapping.Add(id, methodBuilder.Method);

                methodBuilder.Complete();
                methodBuilder.Dispose();
            }

            return resultMapping;
        }
    }
}
