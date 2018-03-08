using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cgfMerger
{
    class DataStream_Tangents
    {
        public static uint elementSize = 16;
        public List<DataStream_Tangents_Elem> tangent = new List<DataStream_Tangents_Elem>();
    }
    class DataStream_Tangents_Elem
    {
        ushort[] tangent_binormal = new ushort[8];
        public static uint elementSize = 16;
    }
}
