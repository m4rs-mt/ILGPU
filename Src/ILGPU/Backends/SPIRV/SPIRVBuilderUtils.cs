using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ILGPU.Backends.SPIRV
{
    public static class SPIRVBuilderUtils
    {
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
                    // Infinite loop is impossible because all spirv structs have primitive bases
                    list.AddRange(ToUintList(field));
                }

                return list;
            }

            switch (obj)
            {
                case uint u:
                    return new List<uint> {u};
                case string s:
                    var bytes = System.Text.Encoding.Unicode.GetBytes(s + "\0");
                    uint[] uints = new uint[bytes.Length / 4];
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
    }
}
