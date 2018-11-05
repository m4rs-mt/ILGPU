// -----------------------------------------------------------------------------
//                                    ILGPU
//                     Copyright (c) 2016-2018 Marcel Koester
//                                www.ilgpu.net
//
// File: IRDumper.cs
//
// This file is part of ILGPU and is distributed under the University of
// Illinois Open Source License. See LICENSE.txt for details
// -----------------------------------------------------------------------------

using ILGPU.IR.Values;
using System;
using System.IO;

namespace ILGPU.IR
{
    /// <summary>
    /// Dumps IR nodes.
    /// </summary>
    public static class IRDumper
    {
        /// <summary>
        /// Dumps the function value to the console output.
        /// </summary>
        /// <param name="functionValue">The function value.</param>
        /// <param name="context">The current context.</param>
        public static void DumpToConsole(
            this FunctionValue functionValue,
            IRContext context)
        {
            Dump(functionValue, context, Console.Out);
        }

        /// <summary>
        /// Dumps the function value to the given text writer.
        /// </summary>
        /// <param name="functionValue">The function value.</param>
        /// <param name="context">The current context.</param>
        /// <param name="textWriter">The target text writer.</param>
        public static void Dump(
            this FunctionValue functionValue,
            IRContext context,
            TextWriter textWriter)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (functionValue == null)
                throw new ArgumentNullException(nameof(functionValue));
            if (textWriter == null)
                throw new ArgumentNullException(nameof(textWriter));

            var scope = Scope.Create(context, functionValue);
            var cfg = CFG.Create(scope);
            var placement = Placement.CreateCSEPlacement(cfg);

            foreach (var cfgNode in cfg)
            {
                textWriter.WriteLine(cfgNode.FunctionValue.ToString());
                using (var placementEnumerator = placement[cfgNode])
                {
                    while (placementEnumerator.MoveNext())
                    {
                        textWriter.Write("\t");
                        textWriter.WriteLine(placementEnumerator.Current.ToString());
                    }
                }
            }
        }

        /// <summary>
        /// Dumps all function values to the console output.
        /// </summary>
        /// <param name="context">The current context.</param>
        public static void DumpToConsole(this IRContext context)
        {
            Dump(context, Console.Out);
        }

        /// <summary>
        /// Dumps all function values to the given text writer.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="textWriter">The target text writer.</param>
        public static void Dump(
            this IRContext context,
            TextWriter textWriter)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            foreach (var function in context.UnsafeTopLevelFunctions)
            {
                Dump(function, context, textWriter);
                textWriter.WriteLine("------------------------------");
            }
        }

        // Raw Dumps

        /// <summary>
        /// Dumps the function value to the console output.
        /// </summary>
        /// <param name="functionValue">The function value.</param>
        /// <param name="context">The current context.</param>
        public static void DumpRawToConsole(
            this FunctionValue functionValue,
            IRContext context)
        {
            DumpRaw(functionValue, context, Console.Out);
        }

        /// <summary>
        /// Dumps the function value to the given text writer.
        /// </summary>
        /// <param name="functionValue">The function value.</param>
        /// <param name="context">The current context.</param>
        /// <param name="textWriter">The target text writer.</param>
        public static void DumpRaw(
            this FunctionValue functionValue,
            IRContext context,
            TextWriter textWriter)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            if (functionValue == null)
                throw new ArgumentNullException(nameof(functionValue));
            if (textWriter == null)
                throw new ArgumentNullException(nameof(textWriter));

            var scope = Scope.Create(context, functionValue);

            foreach (var node in scope)
            {
                switch (node)
                {
                    case Parameter _:
                        continue;
                    case FunctionValue func:
                        textWriter.WriteLine(func.ToString());
                        break;
                    default:
                        textWriter.Write("\t");
                        textWriter.WriteLine(node.ToString());
                        break;
                }
            }
        }

        /// <summary>
        /// Dumps all function values to the console output.
        /// </summary>
        /// <param name="context">The current context.</param>
        public static void DumpRawToConsole(this IRContext context)
        {
            DumpRaw(context, Console.Out);
        }

        /// <summary>
        /// Dumps all function values to the given text writer.
        /// </summary>
        /// <param name="context">The current context.</param>
        /// <param name="textWriter">The target text writer.</param>
        public static void DumpRaw(
            this IRContext context,
            TextWriter textWriter)
        {
            if (context == null)
                throw new ArgumentNullException(nameof(context));
            foreach (var function in context.UnsafeTopLevelFunctions)
            {
                DumpRaw(function, context, textWriter);
                textWriter.WriteLine("------------------------------");
            }
        }
    }
}
