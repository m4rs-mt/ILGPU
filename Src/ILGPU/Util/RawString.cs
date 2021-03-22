// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2016-2020 Marcel Koester
//                                    www.ilgpu.net
//
// File: RawString.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details
// ---------------------------------------------------------------------------------------

using System;

// disable: max_line_length

namespace ILGPU.Util
{
    /// <summary>
    /// Helper to coerce the C# compiler into preferencing the FormattableString
    /// overload of a function, and to also accept a regular string.
    ///
    /// https://www.damirscorner.com/blog/posts/20180921-FormattableStringAsMethodParameter.html
    /// </summary>
    public struct RawString
    {
        private RawString(char value)
        {
            Value = value.ToString();
        }

        private RawString(string value)
        {
            Value = value;
        }

        /// <summary>
        /// The string value.
        /// </summary>
        public string Value { get; }

        /// <summary>
        /// Implicit conversion from char.
        /// </summary>
        /// <param name="value">The char value.</param>
        public static implicit operator RawString(char value) =>
            new RawString(value);

        /// <summary>
        /// Implicit conversion from string.
        /// </summary>
        /// <param name="value">The string value.</param>
        public static implicit operator RawString(string value) =>
            new RawString(value);

        /// <summary>
        /// Implicit conversion from <see cref="FormattableString"/>.
        /// </summary>
        /// <remarks>
        /// This should not be used, but is necessary to coerce the C# compiler.
        /// </remarks>
        /// <param name="value">The string value.</param>
        public static implicit operator RawString(FormattableString value) =>
            new RawString(value.ToString());
    }
}
