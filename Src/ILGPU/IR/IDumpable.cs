// ---------------------------------------------------------------------------------------
//                                        ILGPU
//                        Copyright (c) 2020-2021 ILGPU Project
//                                    www.ilgpu.net
//
// File: IDumpable.cs
//
// This file is part of ILGPU and is distributed under the University of Illinois Open
// Source License. See LICENSE.txt for details.
// ---------------------------------------------------------------------------------------

using System;
using System.IO;

namespace ILGPU.IR
{
    /// <summary>
    /// A dumpable IR object for debugging purposes.
    /// </summary>
    public interface IDumpable
    {
        /// <summary>
        /// Dumps this object to the given text writer.
        /// </summary>
        void Dump(TextWriter textWriter);
    }

    /// <summary>
    /// Helper methods for <see cref="IDumpable"/> instances.
    /// </summary>
    public static class Dumpable
    {
        #region Methods

        /// <summary>
        /// Dumps the IR object to the console output.
        /// </summary>
        /// <param name="dumpable">The IR object to dump.</param>
        public static void DumpToConsole(this IDumpable dumpable) =>
            dumpable.Dump(Console.Out);

        /// <summary>
        /// Dumps the IR object to the console error output.
        /// </summary>
        /// <param name="dumpable">The IR object to dump.</param>
        public static void DumpToError(this IDumpable dumpable) =>
            dumpable.Dump(Console.Error);

        /// <summary>
        /// Dumps the IR object to a file.
        /// </summary>
        /// <param name="dumpable">The IR object to dump.</param>
        /// <param name="fileName">The target file name to write to.</param>
        public static void DumpToFile(this IDumpable dumpable, string fileName)
        {
            using var stream = new StreamWriter(fileName, false);
            dumpable.Dump(stream);
        }

        #endregion
    }
}
