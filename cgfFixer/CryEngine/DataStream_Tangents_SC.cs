using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CryEngine
{
    class DataStream_Tangents_SC
    {
        public static uint elementSize = 8;
        public List<DataStream_Tangents_SC_Elem> tangent = new List<DataStream_Tangents_SC_Elem>();
    }
    class DataStream_Tangents_SC_Elem
    {
        uint tangent;
        uint bitangent;
        public static uint elementSize = 8;
    }
}
