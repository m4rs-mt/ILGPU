using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace ILGPU.Backends.SPIRV
{
    /// <summary>
    /// Utilities for generated functions in SPIRVBuilder.cs
    /// </summary>
    public static class SPIRVBuilderUtils
    {
        /// <summary>
        /// Attempts to convert an object to a SPIRV compatible representation
        /// </summary>
        /// <param name="obj">The object to convert</param>
        /// <returns>A list of words (uints) representing the object</returns>
        /// <exception cref="NotImplementedException"></exception>
        public static List<uint> ToUintList(object obj)
        {
            if (obj == null)
            {
                return new List<uint>();
            }

            var source = obj.GetType();
            if (source.IsValueType && !source.IsPrimitive && !source.IsEnum)
            {
                var list = new List<uint>();
                //TODO: No reflection or cache lookups?
                foreach (var field in source.GetFields(BindingFlags.Instance |
                    BindingFlags.NonPublic |
                    BindingFlags.Public))
                {
                    // Infinite loop is impossible because all
                    // SPIRV structs have primitive bases
                    list.AddRange(ToUintList(field.GetValue(obj)));
                }

                return list;
            }

            switch (obj)
            {
                case uint u:
                    return new List<uint> {u};
                case string s:
                    var bytes = Encoding.UTF8.GetBytes(s + "\0");
                    int roundedUp = (bytes.Length - 1) / 4 + 1;
                    uint[] uints = new uint[roundedUp];
                    Buffer.BlockCopy(bytes, 0, uints, 0, bytes.Length);
                    return uints.ToList();
                case Array a:
                    var list = new List<uint>();
                    foreach (var element in a)
                    {
                        list.AddRange(ToUintList(element));
                    }

                    return list;
                case Enum e:
                    return new List<uint>() { Convert.ToUInt32(e) };
                default:
                    throw new NotImplementedException();
            }
        }

        /// <summary>
        /// Joins an Opcode and word count into one word
        /// </summary>
        /// <param name="opCode"></param>
        /// <param name="wordCount"></param>
        /// <returns></returns>
        public static uint JoinOpCodeWordCount(ushort opCode, ushort wordCount)
        {
            uint opCodeUint = opCode;
            uint wordCountUint = wordCount;

            uint shiftedWordCount = wordCountUint << 16;

            return shiftedWordCount | opCodeUint;
        }
    }
}
