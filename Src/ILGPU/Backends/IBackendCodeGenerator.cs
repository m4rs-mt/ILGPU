// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: IBackendCodeGenerator.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR;
using ILGPU.IR.Values;

namespace ILGPU.Backends
{
    /// <summary>
    /// Represents an abstract code generator that works on a given data type.
    /// </summary>
    /// <typeparam name="TKernelBuilder">
    /// The data type on which this code generator can work.
    /// </typeparam>
    public interface IBackendCodeGenerator<TKernelBuilder> : IBackendCodeGenerator
    {
        /// <summary>
        /// Generates all constant definitions (if any).
        /// </summary>
        /// <param name="builder">The current builder.</param>
        void GenerateConstants(TKernelBuilder builder);

        /// <summary>
        /// Generates a header definition (if any).
        /// </summary>
        /// <param name="builder">The current builder.</param>
        void GenerateHeader(TKernelBuilder builder);

        /// <summary>
        /// Generates the actual function code.
        /// </summary>
        void GenerateCode();

        /// <summary>
        /// Merges all changes inside the current code generator into the given builder.
        /// </summary>
        /// <param name="builder">The builder to merge with.</param>
        void Merge(TKernelBuilder builder);
    }

    /// <summary>
    /// An abstract backend code generator.
    /// </summary>
    public interface IBackendCodeGenerator
    {
        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="methodCall">The node.</param>
        void GenerateCode(MethodCall methodCall);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="phiValue">The node.</param>
        void GenerateCode(PhiValue phiValue);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="parameter">The node.</param>
        void GenerateCode(Parameter parameter);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(UnaryArithmeticValue value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(BinaryArithmeticValue value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(TernaryArithmeticValue value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(CompareValue value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(ConvertValue value);

        /// <summary>
        /// Generates code for the given int to pointer cast.
        /// </summary>
        /// <param name="cast">The cast node.</param>
        void GenerateCode(IntAsPointerCast cast);

        /// <summary>
        /// Generates code for the given pointer to int cast.
        /// </summary>
        /// <param name="cast">The cast node.</param>
        void GenerateCode(PointerAsIntCast cast);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="cast">The cast node.</param>
        void GenerateCode(PointerCast cast);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(AddressSpaceCast value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(FloatAsIntCast value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(IntAsFloatCast value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="predicate">The predicate node.</param>
        void GenerateCode(Predicate predicate);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="atomic">The node.</param>
        void GenerateCode(GenericAtomic atomic);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="atomicCAS">The node.</param>
        void GenerateCode(AtomicCAS atomicCAS);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="alloca">The node.</param>
        void GenerateCode(Alloca alloca);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="barrier">The node.</param>
        void GenerateCode(MemoryBarrier barrier);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="load">The node.</param>
        void GenerateCode(Load load);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="store">The node.</param>
        void GenerateCode(Store store);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(LoadElementAddress value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(LoadFieldAddress value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(PrimitiveValue value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(StringValue value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(NullValue value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(StructureValue value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(GetField value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(SetField value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(GridIndexValue value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(GroupIndexValue value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(GridDimensionValue value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(GroupDimensionValue value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(WarpSizeValue value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(LaneIdxValue value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="value">The node.</param>
        void GenerateCode(DynamicMemoryLengthValue value);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="barrier">The node.</param>
        void GenerateCode(PredicateBarrier barrier);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="barrier">The node.</param>
        void GenerateCode(Barrier barrier);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="broadcast">The node.</param>
        void GenerateCode(Broadcast broadcast);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="shuffle">The node.</param>
        void GenerateCode(WarpShuffle shuffle);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="shuffle">The node.</param>
        void GenerateCode(SubWarpShuffle shuffle);

        // Debug operations

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="debug">The node.</param>
        void GenerateCode(DebugOperation debug);

        // Terminators

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="returnTerminator">The node.</param>
        void GenerateCode(ReturnTerminator returnTerminator);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="branch">The node.</param>
        void GenerateCode(UnconditionalBranch branch);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="branch">The node.</param>
        void GenerateCode(IfBranch branch);

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <param name="branch">The node.</param>
        void GenerateCode(SwitchBranch branch);
    }

    /// <summary>
    /// Extension methods for <see cref="IBackendCodeGenerator"/> instances.
    /// </summary>
    public static class BackendCodeGenerator
    {
        private readonly struct BackendValueVisitor<TCodeGenerator> : IValueVisitor
            where TCodeGenerator : IBackendCodeGenerator
        {
            /// <summary>
            /// Creates a new code-generation visitor wrapper.
            /// </summary>
            /// <param name="codeGenerator">The parent code generator.</param>
            public BackendValueVisitor(TCodeGenerator codeGenerator)
            {
                CodeGenerator = codeGenerator;
            }

            /// <summary>
            /// Returns the parent code generator.
            /// </summary>
            public TCodeGenerator CodeGenerator { get; }

            /// <summary cref="IValueVisitor.Visit(MethodCall)"/>
            public void Visit(MethodCall methodCall) =>
                CodeGenerator.GenerateCode(methodCall);

            /// <summary cref="IValueVisitor.Visit(PhiValue)"/>
            public void Visit(PhiValue phiValue) =>
                CodeGenerator.GenerateCode(phiValue);

            /// <summary cref="IValueVisitor.Visit(Parameter)"/>
            public void Visit(Parameter parameter) =>
                CodeGenerator.GenerateCode(parameter);

            /// <summary cref="IValueVisitor.Visit(UnaryArithmeticValue)"/>
            public void Visit(UnaryArithmeticValue value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(BinaryArithmeticValue)"/>
            public void Visit(BinaryArithmeticValue value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(TernaryArithmeticValue)"/>
            public void Visit(TernaryArithmeticValue value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(CompareValue)"/>
            public void Visit(CompareValue value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(ConvertValue)"/>
            public void Visit(ConvertValue value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(IntAsPointerCast)"/>
            public void Visit(IntAsPointerCast value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(PointerAsIntCast)"/>
            public void Visit(PointerAsIntCast value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(PointerCast)"/>
            public void Visit(PointerCast value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(AddressSpaceCast)"/>
            public void Visit(AddressSpaceCast value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(ViewCast)"/>
            public void Visit(ViewCast value) =>
                throw new InvalidCodeGenerationException();

            /// <summary cref="IValueVisitor.Visit(FloatAsIntCast)"/>
            public void Visit(FloatAsIntCast value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(IntAsFloatCast)"/>
            public void Visit(IntAsFloatCast value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(Predicate)"/>
            public void Visit(Predicate predicate) =>
                CodeGenerator.GenerateCode(predicate);

            /// <summary cref="IValueVisitor.Visit(GenericAtomic)"/>
            public void Visit(GenericAtomic atomic) =>
                CodeGenerator.GenerateCode(atomic);

            /// <summary cref="IValueVisitor.Visit(AtomicCAS)"/>
            public void Visit(AtomicCAS atomicCAS) =>
                CodeGenerator.GenerateCode(atomicCAS);

            /// <summary cref="IValueVisitor.Visit(Alloca)"/>
            public void Visit(Alloca alloca) =>
                CodeGenerator.GenerateCode(alloca);

            /// <summary cref="IValueVisitor.Visit(MemoryBarrier)"/>
            public void Visit(MemoryBarrier barrier) =>
                CodeGenerator.GenerateCode(barrier);

            /// <summary cref="IValueVisitor.Visit(Load)"/>
            public void Visit(Load load) =>
                CodeGenerator.GenerateCode(load);

            /// <summary cref="IValueVisitor.Visit(Store)"/>
            public void Visit(Store store) =>
                CodeGenerator.GenerateCode(store);

            /// <summary cref="IValueVisitor.Visit(SubViewValue)"/>
            public void Visit(SubViewValue value) =>
                throw new InvalidCodeGenerationException();

            /// <summary cref="IValueVisitor.Visit(LoadElementAddress)"/>
            public void Visit(LoadElementAddress value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(LoadElementAddress)"/>
            public void Visit(LoadFieldAddress value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(NewView)"/>
            public void Visit(NewView value) =>
                throw new InvalidCodeGenerationException();

            /// <summary cref="IValueVisitor.Visit(AlignViewTo)"/>
            public void Visit(AlignViewTo value) =>
                throw new InvalidCodeGenerationException();

            /// <summary cref="IValueVisitor.Visit(GetViewLength)"/>
            public void Visit(GetViewLength value) =>
                throw new InvalidCodeGenerationException();

            /// <summary cref="IValueVisitor.Visit(PrimitiveValue)"/>
            public void Visit(PrimitiveValue value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(StringValue)"/>
            public void Visit(StringValue value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(NullValue)"/>
            public void Visit(NullValue value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(StructureValue)"/>
            public void Visit(StructureValue value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(GetField)"/>
            public void Visit(GetField value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(SetField)"/>
            public void Visit(SetField value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(AcceleratorTypeValue)"/>
            public void Visit(AcceleratorTypeValue value) =>
                throw new InvalidCodeGenerationException();

            /// <summary cref="IValueVisitor.Visit(GridIndexValue)"/>
            public void Visit(GridIndexValue value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(GroupIndexValue)"/>
            public void Visit(GroupIndexValue value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(GridDimensionValue)"/>
            public void Visit(GridDimensionValue value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(GroupDimensionValue)"/>
            public void Visit(GroupDimensionValue value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(WarpSizeValue)"/>
            public void Visit(WarpSizeValue value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(LaneIdxValue)"/>
            public void Visit(LaneIdxValue value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(DynamicMemoryLengthValue)"/>
            public void Visit(DynamicMemoryLengthValue value) =>
                CodeGenerator.GenerateCode(value);

            /// <summary cref="IValueVisitor.Visit(PredicateBarrier)"/>
            public void Visit(PredicateBarrier barrier) =>
                CodeGenerator.GenerateCode(barrier);

            /// <summary cref="IValueVisitor.Visit(Barrier)"/>
            public void Visit(Barrier barrier) =>
                CodeGenerator.GenerateCode(barrier);

            /// <summary cref="IValueVisitor.Visit(Broadcast)"/>
            public void Visit(Broadcast broadcast) =>
                CodeGenerator.GenerateCode(broadcast);

            /// <summary cref="IValueVisitor.Visit(WarpShuffle)"/>
            public void Visit(WarpShuffle shuffle) =>
                CodeGenerator.GenerateCode(shuffle);

            /// <summary cref="IValueVisitor.Visit(SubWarpShuffle)"/>
            public void Visit(SubWarpShuffle shuffle) =>
                CodeGenerator.GenerateCode(shuffle);

            /// <summary cref="IValueVisitor.Visit(UndefinedValue)"/>
            public void Visit(UndefinedValue undefined) =>
                throw new InvalidCodeGenerationException();

            /// <summary cref="IValueVisitor.Visit(HandleValue)"/>
            public void Visit(HandleValue handle) =>
                throw new InvalidCodeGenerationException();

            /// <summary cref="IValueVisitor.Visit(DebugOperation)"/>
            public void Visit(DebugOperation debug) =>
                CodeGenerator.GenerateCode(debug);

            /// <summary cref="IValueVisitor.Visit(WriteToOutput)"/>
            public void Visit(WriteToOutput writeToOutput) =>
                throw new InvalidCodeGenerationException();

            /// <summary cref="IValueVisitor.Visit(ReturnTerminator)"/>
            public void Visit(ReturnTerminator returnTerminator) =>
                CodeGenerator.GenerateCode(returnTerminator);

            /// <summary cref="IValueVisitor.Visit(UnconditionalBranch)"/>
            public void Visit(UnconditionalBranch branch) =>
                CodeGenerator.GenerateCode(branch);

            /// <summary cref="IValueVisitor.Visit(IfBranch)"/>
            public void Visit(IfBranch branch) =>
                CodeGenerator.GenerateCode(branch);

            /// <summary cref="IValueVisitor.Visit(SwitchBranch)"/>
            public void Visit(SwitchBranch branch) =>
                CodeGenerator.GenerateCode(branch);
        }

        /// <summary>
        /// Generates code for the given value.
        /// </summary>
        /// <typeparam name="TCodeGenerator">The actual code-generator type.</typeparam>
        /// <param name="codeGenerator">The code-generator instance.</param>
        /// <param name="value">The value to generate code for.</param>
        public static void GenerateCodeFor<TCodeGenerator>(
            this TCodeGenerator codeGenerator,
            Value value)
            where TCodeGenerator : IBackendCodeGenerator
        {
            var visitor = new BackendValueVisitor<TCodeGenerator>(codeGenerator);
            value.Accept(visitor);
        }
    }
}
