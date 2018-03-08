using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace cgfMerger
{
    class DataStream_p3s_c4b_t2s
    {
        public static uint elementSize = 16;
        public List<DataStream_p3s_c4b_t2s_Elem> p3s_c4b_t2s = new List<DataStream_p3s_c4b_t2s_Elem>();

        public DataStream_p3s_c4b_t2s(uint nCount, byte[] dataStream)
        {
            //Stream stream = new MemoryStream(content);
            using (MemoryStream stream = new MemoryStream(dataStream))
            {
                using (BinaryReader br = new BinaryReader(stream))
                {
                    p3s_c4b_t2s = new List<DataStream_p3s_c4b_t2s_Elem>();

                    for (int i=0;i<nCount;i++)
                    {
                        DataStream_p3s_c4b_t2s_Elem elem = new DataStream_p3s_c4b_t2s_Elem();

                        elem.pos[0] = br.ReadUInt16();
                        elem.pos[1] = br.ReadUInt16();
                        elem.pos[2] = br.ReadUInt16();
                        elem.pos[3] = br.ReadUInt16();

                        elem.bgra[0] = br.ReadByte();
                        elem.bgra[1] = br.ReadByte();
                        elem.bgra[2] = br.ReadByte();
                        elem.bgra[3] = br.ReadByte();

                        elem.uv[0] = br.ReadUInt16();
                        elem.uv[1] = br.ReadUInt16();

                        p3s_c4b_t2s.Add(elem);
                    }
                }
            }
        }
    }
    class DataStream_p3s_c4b_t2s_Elem
    {
        public ushort[] pos = new ushort[4];
        public byte[] bgra = new byte[4];
        public ushort[] uv = new ushort[2];
    }
}
