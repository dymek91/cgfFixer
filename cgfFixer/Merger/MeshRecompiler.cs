using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace cgfMerger
{
    class MeshRecompiler
    {
        Mesh mesh;
        uint nCount;

        public MeshRecompiler(uint vertsCount)
        {
            nCount = vertsCount;
        }
        public MeshRecompiler(uint vertsCount, DataStream_p3s_c4b_t2s p3s_c4b_t2s)
        {
            nCount = vertsCount;
            LoadFrom_p3s_c4b_t2s(p3s_c4b_t2s);
        }
        public void LoadFrom_p3s_c4b_t2s(DataStream_p3s_c4b_t2s p3s_c4b_t2sList)
        {
            mesh = new Mesh(nCount);
            foreach (DataStream_p3s_c4b_t2s_Elem p3s_c4b_t2s in p3s_c4b_t2sList.p3s_c4b_t2s)
            {
                DataStream_Positions_Elem position = new DataStream_Positions_Elem();
                for (int i = 0; i < 3; i++)
                {
                    Half halfEl = new Half();
                    halfEl.value = p3s_c4b_t2s.pos[i];
                    position.pos[i] = HalfHelper.HalfToSingle(halfEl);
                }
                mesh.positions.position.Add(position);

                DataStream_Colors_Elem color = new DataStream_Colors_Elem();
                for (int i = 0; i < 4; i++)
                {
                    color.rgba[i] = p3s_c4b_t2s.bgra[i];
                }
                mesh.colors.color.Add(color);

                DataStream_Texcoords_Elem texoord = new DataStream_Texcoords_Elem();
                for (int i = 0; i < 2; i++)
                {
                    Half halfEl = new Half();
                    halfEl.value = p3s_c4b_t2s.uv[i];
                    texoord.uv[i] = HalfHelper.HalfToSingle(halfEl);
                }
                mesh.texcoords.texcoord.Add(texoord);
            }
        }
        public Chunk_DataStream_800 GetPositionsChunk()
        {
            Chunk_DataStream_800 dataStreamChunkPositions = new Chunk_DataStream_800();
            dataStreamChunkPositions.nFlags = 0;
            dataStreamChunkPositions.nStreamType = 0;
            dataStreamChunkPositions.nCount = nCount;
            dataStreamChunkPositions.nElementSize = 12;
            dataStreamChunkPositions.reserved[0] = 0;
            dataStreamChunkPositions.reserved[1] = 0;
            mesh.SerializePositions();
            dataStreamChunkPositions.dataStream = mesh.serializedPositions;
            return dataStreamChunkPositions;
        }
        public Chunk_DataStream_800 GetTexcoordsChunk()
        {
            Chunk_DataStream_800 dataStreamChunkTexcoords = new Chunk_DataStream_800();
            dataStreamChunkTexcoords.nFlags = 0;
            dataStreamChunkTexcoords.nStreamType = 2;
            dataStreamChunkTexcoords.nCount = nCount;
            dataStreamChunkTexcoords.nElementSize = 8;
            dataStreamChunkTexcoords.reserved[0] = 0;
            dataStreamChunkTexcoords.reserved[1] = 0;
            mesh.SerializeTexcoords();
            dataStreamChunkTexcoords.dataStream = mesh.serializedTexcoords;
            return dataStreamChunkTexcoords;
        }
        public Chunk_DataStream_800 GetColorsChunk()
        {
            Chunk_DataStream_800 dataStreamChunkColors = new Chunk_DataStream_800();
            dataStreamChunkColors.nFlags = 0;
            dataStreamChunkColors.nStreamType = 3;
            dataStreamChunkColors.nCount = nCount;
            dataStreamChunkColors.nElementSize = 4;
            dataStreamChunkColors.reserved[0] = 0;
            dataStreamChunkColors.reserved[1] = 0;
            mesh.SerializeColors();
            dataStreamChunkColors.dataStream = mesh.serializedColors;
            return dataStreamChunkColors;
        }
    }
    class Mesh
    {
        public DataStream_Positions positions = new DataStream_Positions();
        public DataStream_Normals normals = new DataStream_Normals();
        public DataStream_Texcoords texcoords = new DataStream_Texcoords();
        public DataStream_Colors colors = new DataStream_Colors();
        uint nCount; 
        public byte[] serializedPositions;
        public byte[] serializedTexcoords;
        public byte[] serializedColors;

        public Mesh(uint vertsCount)
        {
            nCount = vertsCount;
        }

        public void SerializePositions()
        {
            serializedPositions = new byte[nCount*DataStream_Positions.elementSize];
            using (MemoryStream stream = new MemoryStream(serializedPositions))
            {
                using (BinaryWriter bw = new BinaryWriter(stream))
                {
                    for (int i = 0; i < nCount; i++)
                    {

                        bw.Write(positions.position[i].pos[0]);
                        bw.Write(positions.position[i].pos[1]);
                        bw.Write(positions.position[i].pos[2]);
                    }
                }
            }
        }
        public void SerializeTexcoords()
        {
            serializedTexcoords = new byte[nCount * DataStream_Texcoords.elementSize];
            using (MemoryStream stream = new MemoryStream(serializedTexcoords))
            {
                using (BinaryWriter bw = new BinaryWriter(stream))
                {
                    for (int i = 0; i < nCount; i++)
                    {
                        bw.Write(texcoords.texcoord[i].uv[0]);
                        bw.Write(texcoords.texcoord[i].uv[1]); 
                    }
                }
            }
        }
        public void SerializeColors()
        {
            serializedColors = new byte[nCount * DataStream_Colors.elementSize];
            using (MemoryStream stream = new MemoryStream(serializedColors))
            {
                using (BinaryWriter bw = new BinaryWriter(stream))
                {
                    for (int i = 0; i < nCount; i++)
                    {
                        bw.Write(colors.color[i].rgba[0]);
                        bw.Write(colors.color[i].rgba[1]);
                        bw.Write(colors.color[i].rgba[2]);
                        bw.Write(colors.color[i].rgba[3]); 
                    }
                }
            }
        }
    }
}
