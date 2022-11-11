// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                           Copyright (c) 2022 ILGPU Project
//                                    www.ilgpu.net
//
// File: StringExtensions.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace CopyrightUpdateTool.Util
{
    static class StringExtensions
    {
        /// <summary>
        /// Convenience extension to repeat a string.
        /// </summary>
        public static string Repeat(this string value, int count) =>
            string.Concat(Enumerable.Repeat(value, count));

        /// <summary>
        /// Decomposes a path into directory parts.
        /// </summary>
        public static IReadOnlyList<string> DecomposePath(this string path)
        {
            static IEnumerable<string> Decompose(string path)
            {
                var dirName = Path.GetDirectoryName(path);
                var fileName = Path.GetFileName(path);

                if (!string.IsNullOrEmpty(dirName))
                {
                    foreach (var part in Decompose(dirName))
                        yield return part;
                }
                yield return fileName;
            }

            return Decompose(path).ToList();
        }
    }
}
