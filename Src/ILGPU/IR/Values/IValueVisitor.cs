// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: IValueVisitor.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

namespace ILGPU.IR.Values
{
    /// <summary>
    /// A generic interface to visit values in the IR.
    /// </summary>
    public interface IValueVisitor
    {
        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="methodCall">The node.</param>
        void Visit(MethodCall methodCall);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="phiValue">The node.</param>
        void Visit(PhiValue phiValue);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="parameter">The node.</param>
        void Visit(Parameter parameter);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(UnaryArithmeticValue value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(BinaryArithmeticValue value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(TernaryArithmeticValue value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(CompareValue value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(ConvertValue value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(IntAsPointerCast value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(PointerAsIntCast value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(PointerCast value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(AddressSpaceCast value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(ViewCast value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(FloatAsIntCast value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(IntAsFloatCast value);

        /// <summary>
        /// Visits the given predicate.
        /// </summary>
        /// <param name="predicate">The predicate node.</param>
        void Visit(Predicate predicate);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="atomic">The node.</param>
        void Visit(GenericAtomic atomic);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="atomicCAS">The node.</param>
        void Visit(AtomicCAS atomicCAS);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="alloca">The node.</param>
        void Visit(Alloca alloca);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="barrier">The node.</param>
        void Visit(MemoryBarrier barrier);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="load">The node.</param>
        void Visit(Load load);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="store">The node.</param>
        void Visit(Store store);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(SubViewValue value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(LoadElementAddress value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(LoadFieldAddress value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(NewView value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(GetViewLength value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(AlignViewTo value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(PrimitiveValue value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(StringValue value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(NullValue value);

        // Structures

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(StructureValue value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(GetField value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(SetField value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(AcceleratorTypeValue value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(GridIndexValue value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(GroupIndexValue value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(GridDimensionValue value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(GroupDimensionValue value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(WarpSizeValue value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="value">The node.</param>
        void Visit(LaneIdxValue value);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="barrier">The node.</param>
        void Visit(PredicateBarrier barrier);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="barrier">The node.</param>
        void Visit(Barrier barrier);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="broadcast">The node.</param>
        void Visit(Broadcast broadcast);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="shuffle">The node.</param>
        void Visit(WarpShuffle shuffle);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="shuffle">The node.</param>
        void Visit(SubWarpShuffle shuffle);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="undefined">The node.</param>
        void Visit(UndefinedValue undefined);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="handle">The node.</param>
        void Visit(HandleValue handle);

        // Debug operations

        /// <summary>
        /// Visits the debug operation.
        /// </summary>
        /// <param name="debug">The node.</param>
        void Visit(DebugAssertOperation debug);

        // IO operations

        /// <summary>
        /// Visits the IO write node.
        /// </summary>
        /// <param name="writeToOutput">The write node.</param>
        void Visit(WriteToOutput writeToOutput);

        // Terminators

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="returnTerminator">The node.</param>
        void Visit(ReturnTerminator returnTerminator);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="branch">The node.</param>
        void Visit(UnconditionalBranch branch);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="branch">The node.</param>
        void Visit(IfBranch branch);

        /// <summary>
        /// Visits the node.
        /// </summary>
        /// <param name="branch">The node.</param>
        void Visit(SwitchBranch branch);
    }
}
