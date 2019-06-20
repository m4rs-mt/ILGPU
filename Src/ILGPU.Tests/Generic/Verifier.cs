using ILGPU.IR;
using ILGPU.IR.Values;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ILGPU.Tests
{
    sealed class Verifier
    {
        #region Nested Types

        private sealed class Visitor : IValueVisitor
        {
            public Visitor(Value referenceValue)
            {
                Reference = referenceValue;
            }

            public Value Reference { get; }

            public bool Success { get; private set; } = true;

            public T Get<T>()
                where T : Value => (T)Reference;

            public void Fail() => Success = false;

            public void Visit(MethodCall methodCall) { }

            public void Visit(Parameter parameter) { }

            public void Visit(UnaryArithmeticValue value)
            {
                if (Get<UnaryArithmeticValue>().Kind != value.Kind)
                    Fail();
            }

            public void Visit(BinaryArithmeticValue value)
            {
                if (Get<BinaryArithmeticValue>().Kind != value.Kind ||
                    Get<BinaryArithmeticValue>().Flags != value.Flags)
                    Fail();
            }

            public void Visit(TernaryArithmeticValue value)
            {
                if (Get<TernaryArithmeticValue>().Kind != value.Kind ||
                    Get<TernaryArithmeticValue>().Flags != value.Flags)
                    Fail();
            }

            public void Visit(CompareValue value)
            {
                if (Get<CompareValue>().Kind != value.Kind ||
                    Get<CompareValue>().Flags != value.Flags)
                    Fail();
            }

            public void Visit(ConvertValue value) { }

            public void Visit(PointerCast value) { }

            public void Visit(AddressSpaceCast value)
            {
                if (Get<AddressSpaceCast>().TargetAddressSpace != value.TargetAddressSpace)
                    Fail();
            }

            public void Visit(ViewCast value) { }

            public void Visit(FloatAsIntCast value) { }

            public void Visit(IntAsFloatCast value) { }

            public void Visit(Predicate predicate) { }

            public void Visit(GenericAtomic atomic)
            {
                if (Get<GenericAtomic>().Kind != atomic.Kind)
                    Fail();
            }

            public void Visit(AtomicCAS atomicCAS) { }

            public void Visit(Alloca alloca)
            {
                if (Get<Alloca>().AddressSpace != alloca.AddressSpace)
                    Fail();
            }

            public void Visit(MemoryBarrier barrier) { }

            public void Visit(Load load) { }

            public void Visit(Store store) { }

            public void Visit(SubViewValue value) { }

            public void Visit(LoadElementAddress value) { }

            public void Visit(LoadFieldAddress value) { }

            public void Visit(NewView value) { }

            public void Visit(GetViewLength value) { }

            public void Visit(PrimitiveValue value)
            {
                if (Get<PrimitiveValue>().RawValue != value.RawValue)
                    Fail();
            }

            public void Visit(StringValue value) { }

            public void Visit(NullValue value) { }

            public void Visit(SizeOfValue value) { }

            public void Visit(GetField value) { }

            public void Visit(SetField value) { }

            public void Visit(GridDimensionValue value) { }

            public void Visit(GroupDimensionValue value) { }

            public void Visit(WarpSizeValue value) { }

            public void Visit(LaneIdxValue value) { }

            public void Visit(PredicateBarrier barrier)
            {
                if (Get<PredicateBarrier>().Kind != barrier.Kind)
                    Fail();
            }

            public void Visit(Barrier barrier) { }

            public void Visit(Shuffle shuffle)
            {
                if (Get<Shuffle>().Kind != shuffle.Kind)
                    Fail();
            }

            public void Visit(DebugAssertFailed assert) { }

            public void Visit(DebugTrace assert) { }
        }

        #endregion

        #region Static

        public static bool Verify(
            Context context,
            IRContext irContext,
            Method entryPoint,
            Stream serializedStream)
        {
            var deserialized = IRContext.Deserialize(
                context.TypeInformationManger,
                serializedStream,
                IRContextSerializationMode.Binary,
                IRContextSerializationFlags.None,
                out IRContextDeserializationInfo info);
            var functionHandles = info.TopLevelFunctions;

            var otherHandle = functionHandles.First();
            var verifier = new Verifier(
                irContext,
                entryPoint,
                deserialized,
                deserialized.GetFunction(otherHandle));
            return verifier.Verify();
        }

        #endregion

        #region Instance

        private readonly HashSet<Value> currentVisited = new HashSet<Value>();
        private readonly HashSet<Value> referenceVisited = new HashSet<Value>();

        public Verifier(
            IRContext currentContext,
            TopLevelFunction current,
            IRContext referenceContext,
            TopLevelFunction reference)
        {
            CurrentContext = currentContext;
            Current = current;

            ReferenceContext = referenceContext;
            Reference = reference;
        }

        #endregion

        public IRContext CurrentContext { get; }

        public Method Current { get; }

        public IRContext ReferenceContext { get; }

        public Method Reference { get; }

        public bool Verify()
        {
            currentVisited.Clear();
            referenceVisited.Clear();
            return VerifyNode(Current, Reference);
        }

        private bool VerifyNode(Value current, Value reference)
        {
            if (currentVisited.Contains(current))
            {
                if (!referenceVisited.Contains(reference))
                    return false;
                return true;
            }
            else if (referenceVisited.Contains(reference))
            {
                if (!currentVisited.Contains(current))
                    return false;
                return true;
            }

            currentVisited.Add(current);
            referenceVisited.Add(reference);

            if (current.Nodes.Length != reference.Nodes.Length ||
                !Equals(current, reference))
                return false;

            for (int i = 0, e = current.Nodes.Length; i < e; ++i)
            {
                if (!VerifyNode(current.Nodes[i], reference.Nodes[i]))
                    return false;
            }

            return true;
        }

        private static bool Equals(Value current, Value reference)
        {
            if (current.GetType() != reference.GetType() ||
                current.Type.GetType() != reference.Type.GetType())
                return false;
            var visitor = new Visitor(reference);
            current.Accept(visitor);
            return visitor.Success;
        }
    }
}
