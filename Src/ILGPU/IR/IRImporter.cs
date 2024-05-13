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
        private readonly Dictionary<long, BasicBlock.Builder> basicBlocks;
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
            basicBlocks = new Dictionary<long, BasicBlock.Builder>();
            values = new Dictionary<long, Value>();
        }

        public void ImportInto(IRContext context)
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
                var irMethod = new Method(context, new MethodDeclaration(new MethodHandle(method.Id, method.Name), types[method.ReturnType]), Location.Unknown);
                irMethod.CreateBuilder();
                methods.Add(method.Id, irMethod.MethodBuilder);
            }

            foreach (var value in TSort(container.Values, x => container.Values.Where(y => x.Nodes.Contains(y.Id))))
            { 
                if (!basicBlocks.TryGetValue(value.BasicBlock, out BasicBlock.Builder? builder))
                {
                    builder = methods[value.Method].CreateBasicBlock(Location.Unknown);
                    basicBlocks.Add(value.BasicBlock, builder);
                }

                Value irValue;
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
                    case ValueKind.Parameter:
                        irValue = builder.MethodBuilder.AddParameter(types[value.Type], value.Tag);
                        break;
                    case ValueKind.Phi:
                        var phi = builder.CreatePhi(Location.Unknown, types[value.Type]);
                        foreach (var (block, arg) in value.Nodes
                            .Select((x, i) => (x, i))
                            .GroupBy(x => x.i / 2)
                            .Select(g => (g.ElementAt(0).x, g.ElementAt(1).x))
                            )
                        {
                            phi.AddArgument(basicBlocks[block], values[arg]);
                        }
                        irValue = phi.Seal();
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
                        irValue = builder.CreateConvert(Location.Unknown,
                            values[value.Nodes[0]], types[value.Type],
                            (ConvertFlags)value.Data
                            );
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
                            values[value.Nodes[0]], types[value.Type]);
                        break;
                    case ValueKind.AddressSpaceCast:
                        irValue = builder.CreateAddressSpaceCast(Location.Unknown,
                            values[value.Nodes[0]], (MemoryAddressSpace)value.Data);
                        break;
                    case ValueKind.ViewCast:
                        irValue = builder.CreateViewCast(Location.Unknown,
                            values[value.Nodes[0]], types[value.Type]);
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
                        irValue = builder.CreateAlloca(Location.Unknown,
                            types[value.Type], (MemoryAddressSpace)value.Data);
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
                            values[value.Nodes[0]]);
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
                        irValue = builder.CreateGetArrayLength(Location.Unknown,
                            values[value.Nodes[0]], values[value.Nodes[1]]);
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
                    case ValueKind.DebugAssert:
                        irValue = builder.CreateDebugAssert(Location.Unknown,
                            values[value.Nodes[0]], values[value.Nodes[1]]);
                        break;
                    case ValueKind.Return:
                        irValue = builder.CreateReturn(Location.Unknown,
                            values[value.Nodes[0]]);
                        break;
                    case ValueKind.UnconditionalBranch:
                        irValue = builder.CreateBranch(Location.Unknown,
                            basicBlocks[value.Nodes[0]]);
                        break;
                    case ValueKind.IfBranch:
                        irValue = builder.CreateIfBranch(Location.Unknown,
                            values[value.Nodes[0]], basicBlocks[value.Nodes[1]], basicBlocks[value.Nodes[2]],
                            (IfBranchFlags)value.Data);
                        break;
                    case ValueKind.SwitchBranch:
                        var switchBr = builder.CreateSwitchBranch(Location.Unknown,
                            values[value.Nodes[0]]);
                        foreach (var target in value.Nodes.Skip(1))
                        {
                            switchBr.Add(basicBlocks[target]);
                        }
                        irValue = switchBr.Seal();
                        break;
                    default:
                        throw new InvalidOperationException();
                }
                values.Add(value.Id, irValue);
            }

            foreach (var block in basicBlocks.Values)
            {
                block.Dispose();
            }

            foreach (var method in methods.Values)
            {
                method.Dispose();
            }
        }
    }
}
