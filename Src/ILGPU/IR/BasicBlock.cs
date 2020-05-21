// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: BasicBlock.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.CompilerServices;

namespace ILGPU.IR
{
    /// <summary>
    /// Represents a single basic block.
    /// </summary>
    [SuppressMessage(
        "Microsoft.Naming",
        "CA1710: IdentifiersShouldHaveCorrectSuffix",
        Justification = "This is the correct name of the current entity")]
    public sealed partial class BasicBlock :
        ValueParent,
        IReadOnlyCollection<BasicBlock.ValueEntry>
    {
        #region Nested Types

        /// <summary>
        /// Represents a visitor for values.
        /// </summary>
        /// <typeparam name="TValue">The value type.</typeparam>
        /// <param name="value">The value to visit.</param>
        public delegate void ValueVisitor<in TValue>(TValue value)
            where TValue : Value;

        /// <summary>
        /// Represents a value reference within a single basic block.
        /// </summary>
        public readonly struct ValueEntry
        {
            /// <summary>
            /// Converts a new value entry.
            /// </summary>
            /// <param name="index">The index within the block.</param>
            /// <param name="valueReference">The actual value reference.</param>
            public ValueEntry(int index, ValueReference valueReference)
            {
                Index = index;
                ValueReference = valueReference;
            }

            /// <summary>
            /// The current index of the associated value.
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// The actual value reference.
            /// </summary>
            public ValueReference ValueReference { get; }

            /// <summary>
            /// The resolved value.
            /// </summary>
            public Value Value => ValueReference.Resolve();

            /// <summary>
            /// The direct target.
            /// </summary>
            public Value DirectTarget => ValueReference.DirectTarget;

            /// <summary>
            /// Returns the associated basic block.
            /// </summary>
            public BasicBlock BasicBlock => Value.BasicBlock;

            /// <summary>
            /// Accepts a value visitor.
            /// </summary>
            /// <typeparam name="T">The type of the visitor.</typeparam>
            /// <param name="visitor">The visitor.</param>
            public void Accept<T>(T visitor)
                where T : IValueVisitor =>
                ValueReference.Accept(visitor);

            /// <summary>
            /// Implicitly converts the current value entry to its associated value.
            /// </summary>
            public Value ToValue() => Value;

            /// <summary>
            /// Returns the string representation of the underlying value.
            /// </summary>
            /// <returns>The string representation of the underlying value.</returns>
            public override string ToString() => Value.ToString();

            /// <summary>
            /// Implicitly converts the given value entry to its associated value.
            /// </summary>
            /// <param name="valueEntry">The value entry to convert.</param>
            public static implicit operator Value(ValueEntry valueEntry) =>
                valueEntry.Value;
        }

        /// <summary>
        /// An enumerator for values.
        /// </summary>
        public struct Enumerator : IEnumerator<ValueEntry>
        {
            #region Instance

            private int index;
            private readonly List<ValueReference> values;

            /// <summary>
            /// Constructs a new node enumerator.
            /// </summary>
            /// <param name="basicBlock">The basic block to iterate over.</param>
            internal Enumerator(BasicBlock basicBlock)
            {
                Debug.Assert(basicBlock != null, "Invalid basic block");

                BasicBlock = basicBlock;
                values = basicBlock.values;
                index = -1;
            }

            #endregion

            #region Properties

            /// <summary>
            /// Returns the parent basic block.
            /// </summary>
            public BasicBlock BasicBlock { get; }

            /// <summary>
            /// Returns the current node.
            /// </summary>
            public ValueEntry Current => new ValueEntry(index, values[index]);

            /// <summary cref="IEnumerator.Current"/>
            object IEnumerator.Current => Current;

            #endregion

            #region Methods

            /// <summary cref="IDisposable.Dispose"/>
            void IDisposable.Dispose() { }

            /// <summary cref="IEnumerator.MoveNext"/>
            public bool MoveNext() => ++index < values.Count;

            /// <summary cref="IEnumerator.Reset"/>
            void IEnumerator.Reset() => throw new InvalidOperationException();

            #endregion
        }

        #endregion

        #region Instance

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private List<ValueReference> values = new List<ValueReference>();

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private Builder builder = null;

        /// <summary>
        /// Constructs a new basic block.
        /// </summary>
        /// <param name="method">The parent method.</param>
        /// <param name="location">The current location.</param>
        /// <param name="name">The name of the block (or null).</param>
        internal BasicBlock(Method method, Location location, string name)
            : base(location)
        {
            Method = method;
            Name = name ?? "BB";
        }

        #endregion

        #region Properties

        /// <summary>
        /// Returns the parent IR method.
        /// </summary>
        public Method Method { get; }

        /// <summary>
        /// Returns the (meaningless) name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Returns the current terminator.
        /// </summary>
        public TerminatorValue Terminator { get; private set; }

        /// <summary>
        /// Returns all successor nodes.
        /// </summary>
        public ImmutableArray<BasicBlock> Successors =>
            CompactTerminator().Targets;

        /// <summary>
        /// Returns the number of detected blocks.
        /// </summary>
        public int Count => values.Count;

        /// <summary>
        /// Returns the i-th value.
        /// </summary>
        /// <param name="index">The value index.</param>
        /// <returns>The resolved value reference.</returns>
        public ValueReference this[int index] => values[index];

        /// <summary>
        /// Returns the associated block index that is updated during traversal and can
        /// be used to map blocks to values using fast array lookups.
        /// </summary>
        public int BlockIndex { get; private set; } = -1;

        #endregion

        #region Methods

        /// <summary>
        /// Setups the internal block index.
        /// </summary>
        /// <param name="index">The new block index.</param>
        internal void SetupBlockIndex(int index)
        {
            this.Assert(index >= 0);
            BlockIndex = index;
        }

        /// <summary>
        /// Returns true if the given block is a registered successor.
        /// </summary>
        /// <param name="successor">The potential successor block.</param>
        /// <returns>True, if the given block is a registered successor.</returns>
        public bool HasSuccessor(BasicBlock successor)
        {
            foreach (var succ in Successors)
            {
                if (succ == successor)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Checks whether this block has side effects.
        /// </summary>
        /// <returns>True, if this block has side effects.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasSideEffects()
        {
            foreach (Value value in this)
            {
                if (value is MemoryValue)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Returns the terminator converted to the given type.
        /// </summary>
        /// <typeparam name="T">The target terminator type.</typeparam>
        /// <returns>The converted terminator value.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public T GetTerminatorAs<T>()
            where T : TerminatorValue
        {
            this.Assert(Terminator is T);
            return Terminator as T;
        }

        /// <summary>
        /// Resolves the current builder or creates a new one.
        /// </summary>
        /// <param name="functionBuilder">The current function builder.</param>
        /// <param name="resolvedBuilder">The resolved bloc builder.</param>
        /// <returns>True, if the builder was created.</returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool GetOrCreateBuilder(
            Method.Builder functionBuilder,
            out Builder resolvedBuilder)
        {
            resolvedBuilder = builder;
            if (resolvedBuilder != null)
                return false;
            this.AssertNotNull(functionBuilder);
            this.Assert(functionBuilder.Method == Method);
            resolvedBuilder = builder = new Builder(functionBuilder, this);
            return true;
        }

        /// <summary>
        /// Releases the given builder.
        /// </summary>
        /// <param name="otherBuilder">The builder to release.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void ReleaseBuilder(Builder otherBuilder)
        {
            this.AssertNotNull(otherBuilder);
            this.Assert(otherBuilder == builder);
            this.AssertNotNull(Terminator);
            builder = null;
        }

        /// <summary>
        /// Compacts the terminator.
        /// </summary>
        /// <returns></returns>
        private TerminatorValue CompactTerminator() =>
            Terminator = Terminator?.ResolveAs<TerminatorValue>();

        /// <summary>
        /// Dumps this block to the console output.
        /// </summary>
        public void DumpToConsole() =>
            Dump(Console.Out, false);

        /// <summary>
        /// Dumps this block to the console output.
        /// </summary>
        /// <param name="ignoreDeadValues">
        /// True, if dead values should be ignored.
        /// </param>
        public void DumpToConsole(bool ignoreDeadValues) =>
            Dump(Console.Out, false);

        /// <summary>
        /// Dumps this block to the given text writer.
        /// </summary>
        /// <param name="textWriter">The text writer.</param>
        /// <param name="ignoreDeadValues">
        /// True, if dead values should be ignored.
        /// </param>
        public void Dump(TextWriter textWriter, bool ignoreDeadValues)
        {
            if (textWriter == null)
                throw new ArgumentNullException(nameof(textWriter));

            textWriter.Write(ToString());
            textWriter.WriteLine(":");
            foreach (Value value in this)
            {
                textWriter.Write("\t");
                textWriter.WriteLine(value.ToString());
            }
            textWriter.Write("\t");
            textWriter.WriteLine(Terminator.ToString());
        }

        /// <summary>
        /// Performs a GC run on this block.
        /// </summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal void GC()
        {
            var newTerminator = CompactTerminator();
            newTerminator.GC();

            var newValues = new List<ValueReference>(Count);
            foreach (Value value in values)
            {
                value.GC();
                newValues.Add(value);
            }
            values = newValues;
        }

        /// <summary>
        /// Executes the given visitor for each value in this scope.
        /// </summary>
        /// <typeparam name="TValue">The value to match.</typeparam>
        /// <param name="visitor">The visitor.</param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ForEachValue<TValue>(ValueVisitor<TValue> visitor)
            where TValue : Value
        {
            foreach (Value value in values)
            {
                if (value is TValue matchedValue)
                    visitor(matchedValue);
            }
        }

        /// <summary>
        /// Returns a value enumerator.
        /// </summary>
        /// <returns>The resolved enumerator.</returns>
        public Enumerator GetEnumerator() => new Enumerator(this);

        /// <summary cref="IEnumerable{T}.GetEnumerator"/>
        IEnumerator<ValueEntry> IEnumerable<ValueEntry>.GetEnumerator() =>
            GetEnumerator();

        /// <summary cref="IEnumerable.GetEnumerator"/>
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        #endregion

        #region Object

        /// <summary cref="Node.ToPrefixString"/>
        protected override string ToPrefixString() => Name;

        #endregion
    }
}
