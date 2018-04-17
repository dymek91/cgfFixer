using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryEngine;
using System.IO;

namespace cgfFixer
{
    class Fixer_CE_5_4OLD
    {
        static Stream streamTempFile = new MemoryStream();

        static bool useQTan = false;

        static Vector3[,] positions;
        static Vector2[,] texcoords;
        static Vector3[,] normals;
        static Vector3[,] tangents;
        static Vector3[,] biTangents;
        static Quaternion[,] quaternions;
        static Vector3[] bboxMin;
        static Vector3[] bboxMax;
        static int[,] indices;
        static int[] indicesCounts;
        static int[] positionsCounts;
        static int meshesCount;
        static uint[,] qtangentsChunksOffsets = new uint[128, 3];//offset,chunkid

        static void editTempFileHeaderOffsets(int afterElement, uint delta, BinaryReader br)
        {
            //streamTempFile.Position = 0;
            //streamTempFile.Close();
            // BinaryWriter bw = new BinaryWriter(streamTempFile); 
            br.BaseStream.Position = 0;
            br.ReadUInt32();
            br.ReadUInt32();
            int headerElements = br.ReadInt32();
            br.ReadUInt32();
            for (int i = 0; i < headerElements; i++)
            {
                br.ReadUInt32();
                int id = br.ReadInt32();
                if (id == afterElement)
                {
                    long position = br.BaseStream.Position;
                    uint currSize = br.ReadUInt32();
                    //  bw.BaseStream.Position = position;
                    //  bw.Write(currSize + delta);
                    streamTempFile.Position = position;
                    streamTempFile.Write(BitConverter.GetBytes(currSize + delta), 0, sizeof(uint));
                }
                else
                {
                    br.ReadUInt32();
                }

                if (id > afterElement)
                {
                    long position = br.BaseStream.Position;
                    uint currOffset = br.ReadUInt32();
                    // bw.BaseStream.Position = position;
                    // bw.Write(currOffset + delta);
                    streamTempFile.Position = position;
                    streamTempFile.Write(BitConverter.GetBytes(currOffset + delta), 0, sizeof(uint));
                }
                else
                {
                    br.ReadUInt32();
                }

            }
            // bw.Close();
        }
        //static void editTempFileHeaderOffsets(int afterElement, uint delta, BinaryReader br)
        //{
        //    BinaryWriter bw = new BinaryWriter(streamTempFile);
        //    br.BaseStream.Position = 0;
        //    br.ReadUInt32();
        //    br.ReadUInt32();
        //    int headerElements = br.ReadInt32();
        //    br.ReadUInt32();
        //    for (int i = 0; i < headerElements; i++)
        //    {
        //        br.ReadUInt32();
        //        int id = br.ReadInt32();
        //        if (id == afterElement)
        //        {
        //            long position = br.BaseStream.Position;
        //            uint currSize = br.ReadUInt32();
        //            bw.BaseStream.Position = position;
        //            bw.Write(currSize + delta);
        //        }
        //        else
        //        {
        //            br.ReadUInt32();
        //        }

        //        if (id > afterElement)
        //        {
        //            long position = br.BaseStream.Position;
        //            uint currOffset = br.ReadUInt32();
        //            bw.BaseStream.Position = position;
        //            bw.Write(currOffset + delta);
        //        }
        //        else
        //        {
        //            br.ReadUInt32();
        //        }

        //    }
        //    bw.Close();
        //}
        static void appendFooter(string path, long position, long positionWrite)
        {
            //temp, will do better handling later
            while (!FixerHelper.IsFileReady(path)) { }
            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));

            //BinaryWriter bw = new BinaryWriter(streamFile);

            br.BaseStream.Position = position;
            streamTempFile.Position = positionWrite;
            uint buffer;
            int licz = 0;
            try
            {
                while (true)
                {
                    buffer = br.ReadUInt32();
                    streamTempFile.Write(BitConverter.GetBytes(buffer), 0, sizeof(uint));
                    licz = licz + 1;
                }
            }
            catch (Exception exp) { }
            //Console.WriteLine("wrote {0} uints",licz);
            br.Close();
            //bw.Close();

        }
        //static void appendFooter(string path, Stream streamFile, long position, long positionWrite)
        //{
        //    //temp, will do better handling later
        //    while (!IsFileReady(path)) { }
        //    BinaryReader br = new BinaryReader(File.Open(
        //            path, FileMode.Open, FileAccess.Read));

        //    BinaryWriter bw = new BinaryWriter(streamFile);

        //    br.BaseStream.Position = position;
        //    bw.BaseStream.Position = positionWrite;
        //    uint buffer;
        //    int licz = 0;
        //    try
        //    {
        //        while (true)
        //        {
        //            buffer = br.ReadUInt32();
        //            bw.Write(buffer);
        //            licz = licz + 1;
        //        }
        //    }
        //    catch (Exception exp) { }
        //    //Console.WriteLine("wrote {0} uints",licz);
        //    br.Close();
        //    bw.Close();

        //}
        private static void fixVerts(string path)
        {
            while (!FixerHelper.IsFileReady(path)) { }
            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));
            br.ReadUInt32();
            br.ReadUInt32();
            uint headerElements = br.ReadUInt32();
            br.ReadUInt32();
            uint[] dataStreamChunksOffsets = new uint[headerElements];
            int dataStreamsCount = 0;
            for (int i = 0; i < (int)headerElements; i++)
            {
                uint chunkType = br.ReadUInt16();
                //Console.WriteLine("{0:X}", chunkType);
                br.ReadUInt16();
                br.ReadUInt32();
                br.ReadUInt32();
                uint offset = br.ReadUInt32();
                if (chunkType == 0x00001016) { dataStreamChunksOffsets[dataStreamsCount] = offset; dataStreamsCount++; }
            }

            uint[,] p3s_c4b_t2sChunksOffsets = new uint[dataStreamsCount, 2];
            int p3s_c4b_t2sCount = 0;
            for (int i = 0; i < dataStreamsCount; i++)
            {
                br.BaseStream.Position = dataStreamChunksOffsets[i];
                br.ReadUInt32();
                uint nStreamType = br.ReadUInt32();
                if (nStreamType == 0x0000000F)
                {
                    p3s_c4b_t2sChunksOffsets[p3s_c4b_t2sCount, 0] = dataStreamChunksOffsets[i]; //offset
                    p3s_c4b_t2sChunksOffsets[p3s_c4b_t2sCount, 1] = br.ReadUInt32();//verts count
                    p3s_c4b_t2sCount++;
                }
            }
            int maxVertsCount = 0;
            for (int i = 0; i < p3s_c4b_t2sCount; i++)
            {
                int buf = (int)p3s_c4b_t2sChunksOffsets[i, 1];
                if (buf > maxVertsCount) maxVertsCount = buf;
            }
            //uint[,,] verticlesValues = new uint[p3s_c4b_t2sCount, maxVertsCount, 4];
            short[,,] verticlesValues = new short[p3s_c4b_t2sCount, maxVertsCount, 4];
            positions = new Vector3[p3s_c4b_t2sCount, maxVertsCount];
            texcoords = new Vector2[p3s_c4b_t2sCount, maxVertsCount];
            normals = new Vector3[p3s_c4b_t2sCount, maxVertsCount];
            tangents = new Vector3[p3s_c4b_t2sCount, maxVertsCount];
            biTangents = new Vector3[p3s_c4b_t2sCount, maxVertsCount];
            positionsCounts = new int[p3s_c4b_t2sCount];
            quaternions = new CryEngine.Quaternion[p3s_c4b_t2sCount, maxVertsCount];
            meshesCount = p3s_c4b_t2sCount;

            for (int i = 0; i < p3s_c4b_t2sCount; i++)
            {
                uint offset = p3s_c4b_t2sChunksOffsets[i, 0];
                br.BaseStream.Position = offset;
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                uint vertsCount = p3s_c4b_t2sChunksOffsets[i, 1];

                positionsCounts[i] = (int)vertsCount;


                for (int j = 0; j < (int)vertsCount; j++)
                {
                    //verticlesValues[i, j, 0] = br.ReadUInt16();
                    //verticlesValues[i, j, 1] = br.ReadUInt16();
                    //verticlesValues[i, j, 2] = br.ReadUInt16();
                    //verticlesValues[i, j, 3] = br.ReadUInt16();
                    verticlesValues[i, j, 0] = br.ReadInt16();
                    verticlesValues[i, j, 1] = br.ReadInt16();
                    verticlesValues[i, j, 2] = br.ReadInt16();
                    verticlesValues[i, j, 3] = br.ReadInt16();
                    br.ReadUInt32();
                    Half UVx = new Half();
                    UVx.value = br.ReadUInt16();
                    Half UVy = new Half();
                    UVy.value = br.ReadUInt16();

                    texcoords[i, j].X = HalfHelper.HalfToSingle(UVx);
                    texcoords[i, j].Y = HalfHelper.HalfToSingle(UVy);

                }

            }
            br.Close();

            while (!FixerHelper.IsFileReady(path)) { }
            BinaryWriter bw = new BinaryWriter(File.Open(
                    path, FileMode.Open, FileAccess.ReadWrite));

            for (int i = 0; i < p3s_c4b_t2sCount; i++)
            {
                Console.Write(".");
                Vector3 maxCoord = new Vector3(Utils.tPackB2F(verticlesValues[i, 0, 0]), Utils.tPackB2F(verticlesValues[i, 0, 1]), Utils.tPackB2F(verticlesValues[i, 0, 2]));
                Vector3 minCoord = new Vector3(Utils.tPackB2F(verticlesValues[i, 0, 0]), Utils.tPackB2F(verticlesValues[i, 0, 1]), Utils.tPackB2F(verticlesValues[i, 0, 2]));

                uint offset = p3s_c4b_t2sChunksOffsets[i, 0];
                int vertsCount = (int)p3s_c4b_t2sChunksOffsets[i, 1];
                bw.BaseStream.Position = offset;
                bw.BaseStream.Position = bw.BaseStream.Position + 24;
                for (int j = 0; j < vertsCount; j++)
                {
                    float tempX = Utils.tPackB2F(verticlesValues[i, j, 0]);
                    float tempY = Utils.tPackB2F(verticlesValues[i, j, 1]);
                    float tempZ = Utils.tPackB2F(verticlesValues[i, j, 2]);
                    if (tempX > maxCoord.X) maxCoord.X = tempX;
                    if (tempY > maxCoord.Y) maxCoord.Y = tempY;
                    if (tempZ > maxCoord.Z) maxCoord.Z = tempZ;
                    if (tempX < minCoord.X) minCoord.X = tempX;
                    if (tempY < minCoord.Y) minCoord.Y = tempY;
                    if (tempZ < minCoord.Z) minCoord.Z = tempZ;
                }
                for (int j = 0; j < vertsCount; j++)
                {
                    float multiplerX = Math.Abs(bboxMin[i].X - bboxMax[i].X) / 2f;//Math.Abs(minCoord.X - maxCoord.X);
                    float multiplerY = Math.Abs(bboxMin[i].Y - bboxMax[i].Y) / 2f;//Math.Abs(minCoord.Y - maxCoord.Y);
                    float multiplerZ = Math.Abs(bboxMin[i].Z - bboxMax[i].Z) / 2f;//Math.Abs(minCoord.Z - maxCoord.Z);
                    if (multiplerX < 1) { multiplerX = 1; }//bboxMax[i].X = 0; bboxMin[i].X = 0;  } 
                    if (multiplerY < 1) { multiplerY = 1; }//bboxMax[i].Y = 0; bboxMin[i].Y = 0;  }
                    if (multiplerZ < 1) { multiplerZ = 1; }//bboxMax[i].Z = 0; bboxMin[i].Z = 0;  } 



                    bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 0]) * multiplerX + (bboxMax[i].X + bboxMin[i].X) / 2).value);
                    bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 1]) * multiplerY + (bboxMax[i].Y + bboxMin[i].Y) / 2).value);
                    bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 2]) * multiplerZ + (bboxMax[i].Z + bboxMin[i].Z) / 2).value);
                    positions[i, j].X = Utils.tPackB2F(verticlesValues[i, j, 0]) * multiplerX + (bboxMax[i].X + bboxMin[i].X) / 2;
                    positions[i, j].Y = Utils.tPackB2F(verticlesValues[i, j, 1]) * multiplerY + (bboxMax[i].Y + bboxMin[i].Y) / 2;
                    positions[i, j].Z = Utils.tPackB2F(verticlesValues[i, j, 2]) * multiplerZ + (bboxMax[i].Z + bboxMin[i].Z) / 2;



                    bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 3])).value);
                    //ushort w = 15360;
                    //bw.Write(w);
                    bw.BaseStream.Position = bw.BaseStream.Position + 8;
                }

            }

            // bw.BaseStream.Seek(position, SeekOrigin.Begin);
            // bw.Write(patch);

            bw.Close();


        }
        private static void fixSkinVerts(string path)
        {
            while (!FixerHelper.IsFileReady(path)) { }
            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));
            br.ReadUInt32();
            br.ReadUInt32();
            uint headerElements = br.ReadUInt32();
            br.ReadUInt32();
            uint[] dataStreamChunksOffsets = new uint[headerElements];
            int dataStreamsCount = 0;
            for (int i = 0; i < (int)headerElements; i++)
            {
                uint chunkType = br.ReadUInt16();
                //Console.WriteLine("{0:X}", chunkType);
                br.ReadUInt16();
                br.ReadUInt32();
                br.ReadUInt32();
                uint offset = br.ReadUInt32();
                if (chunkType == 0x00001016) { dataStreamChunksOffsets[dataStreamsCount] = offset; dataStreamsCount++; }
            }

            uint[,] p3s_c4b_t2sChunksOffsets = new uint[dataStreamsCount, 2];
            int p3s_c4b_t2sCount = 0;
            for (int i = 0; i < dataStreamsCount; i++)
            {
                br.BaseStream.Position = dataStreamChunksOffsets[i];
                br.ReadUInt32();
                uint nStreamType = br.ReadUInt32();
                if (nStreamType == 0x0000000F)
                {
                    p3s_c4b_t2sChunksOffsets[p3s_c4b_t2sCount, 0] = dataStreamChunksOffsets[i]; //offset
                    p3s_c4b_t2sChunksOffsets[p3s_c4b_t2sCount, 1] = br.ReadUInt32();//verts count
                    p3s_c4b_t2sCount++;
                }
            }
            int maxVertsCount = 0;
            for (int i = 0; i < p3s_c4b_t2sCount; i++)
            {
                int buf = (int)p3s_c4b_t2sChunksOffsets[i, 1];
                if (buf > maxVertsCount) maxVertsCount = buf;
            }
            //uint[,,] verticlesValues = new uint[p3s_c4b_t2sCount, maxVertsCount, 4];
            float[,,] verticlesValues = new float[p3s_c4b_t2sCount, maxVertsCount, 4];
            uint[,] colors = new uint[p3s_c4b_t2sCount, maxVertsCount];
            ushort[,,] texcoordsUint16 = new ushort[p3s_c4b_t2sCount, maxVertsCount, 2];
            positions = new Vector3[p3s_c4b_t2sCount, maxVertsCount];
            texcoords = new Vector2[p3s_c4b_t2sCount, maxVertsCount];
            normals = new Vector3[p3s_c4b_t2sCount, maxVertsCount];
            tangents = new Vector3[p3s_c4b_t2sCount, maxVertsCount];
            biTangents = new Vector3[p3s_c4b_t2sCount, maxVertsCount];
            positionsCounts = new int[p3s_c4b_t2sCount];
            quaternions = new CryEngine.Quaternion[p3s_c4b_t2sCount, maxVertsCount];
            meshesCount = p3s_c4b_t2sCount;

            for (int i = 0; i < p3s_c4b_t2sCount; i++)
            {
                uint offset = p3s_c4b_t2sChunksOffsets[i, 0];
                br.BaseStream.Position = offset;
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                uint vertsCount = p3s_c4b_t2sChunksOffsets[i, 1];

                positionsCounts[i] = (int)vertsCount;


                for (int j = 0; j < (int)vertsCount; j++)
                {
                    //verticlesValues[i, j, 0] = br.ReadUInt16();
                    //verticlesValues[i, j, 1] = br.ReadUInt16();
                    //verticlesValues[i, j, 2] = br.ReadUInt16();
                    //verticlesValues[i, j, 3] = br.ReadUInt16();
                    verticlesValues[i, j, 0] = br.ReadSingle();
                    verticlesValues[i, j, 1] = br.ReadSingle();
                    verticlesValues[i, j, 2] = br.ReadSingle();
                    verticlesValues[i, j, 3] = 1.0f;
                    colors[i, j] = br.ReadUInt32();
                    Half UVx = new Half();
                    UVx.value = br.ReadUInt16();
                    Half UVy = new Half();
                    UVy.value = br.ReadUInt16();

                    texcoordsUint16[i, j, 0] = UVx.value;
                    texcoordsUint16[i, j, 1] = UVy.value;

                    texcoords[i, j].X = HalfHelper.HalfToSingle(UVx);
                    texcoords[i, j].Y = HalfHelper.HalfToSingle(UVy);

                    //positions[i, j].X = toInt(verticlesValues[i, j, 0]) / 256f / 128;
                    //positions[i, j].Y = toInt(verticlesValues[i, j, 1]) / 256f / 128;
                    //positions[i, j].Z = toInt(verticlesValues[i, j, 2]) / 256f / 128;
                }

            }
            br.Close();



            BinaryWriter bw = new BinaryWriter(File.Open(
                    path, FileMode.Open, FileAccess.ReadWrite));

            for (int i = 0; i < p3s_c4b_t2sCount; i++)
            {
                Console.Write(".");
                //Vector3 maxCoord = new Vector3(Utils.tPackB2F(verticlesValues[i, 0, 0]), Utils.tPackB2F(verticlesValues[i, 0, 1]), Utils.tPackB2F(verticlesValues[i, 0, 2]));
                //Vector3 minCoord = new Vector3(Utils.tPackB2F(verticlesValues[i, 0, 0]), Utils.tPackB2F(verticlesValues[i, 0, 1]), Utils.tPackB2F(verticlesValues[i, 0, 2]));

                uint offset = p3s_c4b_t2sChunksOffsets[i, 0];
                int vertsCount = (int)p3s_c4b_t2sChunksOffsets[i, 1];

                //ushort zeroShort = 0;
                //ushort patch16 = 16;
                //bw.BaseStream.Position = offset + 12;
                //bw.Write(patch16);
                //bw.Write(zeroShort);

                bw.BaseStream.Position = offset;
                bw.BaseStream.Position = bw.BaseStream.Position + 24;

                for (int j = 0; j < vertsCount; j++)
                {

                    bw.Write(HalfHelper.SingleToHalf(verticlesValues[i, j, 0]).value);
                    bw.Write(HalfHelper.SingleToHalf(verticlesValues[i, j, 1]).value);
                    bw.Write(HalfHelper.SingleToHalf(verticlesValues[i, j, 2]).value);
                    bw.Write(HalfHelper.SingleToHalf(verticlesValues[i, j, 3]).value);
                    bw.Write(colors[i, j]);
                    bw.Write(texcoordsUint16[i, j, 0]);
                    bw.Write(texcoordsUint16[i, j, 1]);



                }

            }

            // bw.BaseStream.Seek(position, SeekOrigin.Begin);
            // bw.Write(patch);

            bw.Close();

            //Console.WriteLine("{0} {1} {2}", fixVert(verticlesValues[0, 1, 0]), fixVert(verticlesValues[0, 1, 1]), fixVert(verticlesValues[0, 1, 2]));
            //Console.WriteLine("{0} {1} {2} {3}", byte2hexIntFracToFloat2(verticlesValues[0, 0, 0]), byte2hexIntFracToFloat2(verticlesValues[0, 0, 1]), byte2hexIntFracToFloat2(verticlesValues[0, 0, 2]), byte2hexIntFracToFloat2(verticlesValues[0, 0, 3]));
            //Console.WriteLine("{0} {1} {2} {3}", toInt(verticlesValues[0, 0, 0]) / 256f / 128, toInt(verticlesValues[0, 0, 1]) / 256f / 128, toInt(verticlesValues[0, 0, 2]) / 256f / 128, toInt(verticlesValues[0, 0, 3]) / 256f / 128);

            //Console.WriteLine("{0} elements in header, {1} dataStreams, {2} p3s_c4b_t2sChunks", headerElements, dataStreamsCount, p3s_c4b_t2sCount);
            //Console.WriteLine("{0} vericles", maxVertsCount);

        }

        private static void fixVertsSkin(string path)
        {
            //skiny czasami maja pozycje jako p3f_c4b_t2s, ale oznaczone jako p3s_c4b_t2s
            //p3f_c4b_t2s - 3 floaty, 4 bajty, 2 half floaty
            //p3s_c4b_t2s - 3 half floaty, 4 bajty, 2 half floaty
            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));
            br.ReadUInt32();
            br.ReadUInt32();
            uint headerElements = br.ReadUInt32();
            br.ReadUInt32();
            uint[] dataStreamChunksOffsets = new uint[headerElements];
            int dataStreamsCount = 0;
            for (int i = 0; i < (int)headerElements; i++)
            {
                uint chunkType = br.ReadUInt16();
                //Console.WriteLine("{0:X}", chunkType);
                br.ReadUInt16();
                br.ReadUInt32();
                br.ReadUInt32();
                uint offset = br.ReadUInt32();
                if (chunkType == 0x00001016) { dataStreamChunksOffsets[dataStreamsCount] = offset; dataStreamsCount++; }
            }

            uint[,] p3s_c4b_t2sChunksOffsets = new uint[dataStreamsCount, 2];
            int p3s_c4b_t2sCount = 0;
            for (int i = 0; i < dataStreamsCount; i++)
            {
                br.BaseStream.Position = dataStreamChunksOffsets[i];
                br.ReadUInt32();
                uint nStreamType = br.ReadUInt32();
                if (nStreamType == 0x0000000F)
                {
                    br.ReadUInt32();
                    uint elementSize = br.ReadUInt32();
                    if (elementSize == 20)
                    {
                        p3s_c4b_t2sChunksOffsets[p3s_c4b_t2sCount, 0] = dataStreamChunksOffsets[i]; //offset
                        p3s_c4b_t2sChunksOffsets[p3s_c4b_t2sCount, 1] = br.ReadUInt32();//verts count
                        p3s_c4b_t2sCount++;
                    }
                }
            }
            int maxVertsCount = 0;
            for (int i = 0; i < p3s_c4b_t2sCount; i++)
            {
                int buf = (int)p3s_c4b_t2sChunksOffsets[i, 1];
                if (buf > maxVertsCount) maxVertsCount = buf;
            }
            ushort[,,] verticlesValues = new ushort[p3s_c4b_t2sCount, maxVertsCount, 4];
            positions = new Vector3[p3s_c4b_t2sCount, maxVertsCount];
            texcoords = new Vector2[p3s_c4b_t2sCount, maxVertsCount];

            for (int i = 0; i < p3s_c4b_t2sCount; i++)
            {
                uint offset = p3s_c4b_t2sChunksOffsets[i, 0];
                br.BaseStream.Position = offset;
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                uint vertsCount = p3s_c4b_t2sChunksOffsets[i, 1];

                for (int j = 0; j < (int)vertsCount; j++)
                {
                    verticlesValues[i, j, 0] = br.ReadUInt16();
                    verticlesValues[i, j, 1] = br.ReadUInt16();
                    verticlesValues[i, j, 2] = br.ReadUInt16();
                    verticlesValues[i, j, 3] = br.ReadUInt16();
                    br.ReadUInt32();
                    Half UVx = new Half();
                    UVx.value = br.ReadUInt16();
                    Half UVy = new Half();
                    UVy.value = br.ReadUInt16();

                    texcoords[i, j].X = HalfHelper.HalfToSingle(UVx);
                    texcoords[i, j].Y = HalfHelper.HalfToSingle(UVy);

                    positions[i, j].X = FixerHelper.toInt(verticlesValues[i, j, 0]) / 256f / 128;
                    positions[i, j].Y = FixerHelper.toInt(verticlesValues[i, j, 1]) / 256f / 128;
                    positions[i, j].Z = FixerHelper.toInt(verticlesValues[i, j, 2]) / 256f / 128;
                }

            }
            br.Close();

            BinaryWriter bw = new BinaryWriter(File.Open(
                    path, FileMode.Open, FileAccess.ReadWrite));
            for (int i = 0; i < p3s_c4b_t2sCount; i++)
            {
                Console.Write(".");
                Vector3 maxCoord = new Vector3(FixerHelper.byte2hexIntFracToFloat3(verticlesValues[i, 0, 0]), FixerHelper.byte2hexIntFracToFloat3(verticlesValues[i, 0, 1]), FixerHelper.byte2hexIntFracToFloat3(verticlesValues[i, 0, 2]));
                Vector3 minCoord = new Vector3(FixerHelper.byte2hexIntFracToFloat3(verticlesValues[i, 0, 0]), FixerHelper.byte2hexIntFracToFloat3(verticlesValues[i, 0, 1]), FixerHelper.byte2hexIntFracToFloat3(verticlesValues[i, 0, 2]));

                uint offset = p3s_c4b_t2sChunksOffsets[i, 0];
                int vertsCount = (int)p3s_c4b_t2sChunksOffsets[i, 1];
                bw.BaseStream.Position = offset;
                bw.BaseStream.Position = bw.BaseStream.Position + 24;
                for (int j = 0; j < vertsCount; j++)
                {
                    float tempX = FixerHelper.byte2hexIntFracToFloat3(verticlesValues[i, j, 0]);
                    float tempY = FixerHelper.byte2hexIntFracToFloat3(verticlesValues[i, j, 1]);
                    float tempZ = FixerHelper.byte2hexIntFracToFloat3(verticlesValues[i, j, 2]);
                    if (tempX > maxCoord.X) maxCoord.X = tempX;
                    if (tempY > maxCoord.Y) maxCoord.Y = tempY;
                    if (tempZ > maxCoord.Z) maxCoord.Z = tempZ;
                    if (tempX < minCoord.X) minCoord.X = tempX;
                    if (tempY < minCoord.Y) minCoord.Y = tempY;
                    if (tempZ < minCoord.Z) minCoord.Z = tempZ;
                }
                for (int j = 0; j < vertsCount; j++)
                {
                    //float multiplerX = Math.Abs(bboxMin[i].X - bboxMax[i].X) / 2;
                    //float multiplerY = Math.Abs(bboxMin[i].Y - bboxMax[i].Y) / 2;
                    //float multiplerZ = Math.Abs(bboxMin[i].Z - bboxMax[i].Z) / 2;
                    float multiplerX = Math.Abs(bboxMin[i].X - bboxMax[i].X) / 2f;//Math.Abs(minCoord.X - maxCoord.X);
                    float multiplerY = Math.Abs(bboxMin[i].Y - bboxMax[i].Y) / 2f;//Math.Abs(minCoord.Y - maxCoord.Y);
                    float multiplerZ = Math.Abs(bboxMin[i].Z - bboxMax[i].Z) / 2f;//Math.Abs(minCoord.Z - maxCoord.Z);
                    if (multiplerX < 1) { multiplerX = 1; }
                    if (multiplerY < 1) { multiplerY = 1; }
                    if (multiplerZ < 1) { multiplerZ = 1; }
                    Half tempX = new Half();
                    Half tempY = new Half();
                    Half tempZ = new Half();
                    tempX.value = FixerHelper.fixVert(verticlesValues[i, j, 0]);
                    tempY.value = FixerHelper.fixVert(verticlesValues[i, j, 1]);
                    tempZ.value = FixerHelper.fixVert(verticlesValues[i, j, 2]);

                    //if (HalfHelper.HalfToSingle(tempX) > 0 && Math.Abs(bboxMax[i].X) >= 1) { multiplerX = Math.Abs(bboxMax[i].X); } else if (HalfHelper.HalfToSingle(tempX) < 0 && Math.Abs(bboxMin[i].X) >= 1) { multiplerX = Math.Abs(bboxMin[i].X); }
                    //if (HalfHelper.HalfToSingle(tempY) > 0 && Math.Abs(bboxMax[i].Y) >= 1) { multiplerY = Math.Abs(bboxMax[i].Y); } else if (HalfHelper.HalfToSingle(tempY) < 0 && Math.Abs(bboxMin[i].Y) >= 1) { multiplerY = Math.Abs(bboxMin[i].Y); }
                    //if (HalfHelper.HalfToSingle(tempZ) > 0 && Math.Abs(bboxMax[i].Z) >= 1) { multiplerZ = Math.Abs(bboxMax[i].Z); } else if (HalfHelper.HalfToSingle(tempZ) < 0 && Math.Abs(bboxMin[i].Z) >= 1) { multiplerZ = Math.Abs(bboxMin[i].Z); }

                    //bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempX) * multiplerX + (bboxMax[i].X + bboxMin[i].X) / 2).value);
                    //bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempY) * multiplerY + (bboxMax[i].Y + bboxMin[i].Y) / 2).value);
                    //bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempZ) * multiplerZ + (bboxMax[i].Z + bboxMin[i].Z) / 2).value);
                    bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempX) * multiplerX + (bboxMax[i].X + bboxMin[i].X) / 2).value);
                    bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempY) * multiplerY + (bboxMax[i].Y + bboxMin[i].Y) / 2).value);
                    bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempZ) * multiplerZ + (bboxMax[i].Z + bboxMin[i].Z) / 2).value);

                    //bw.Write(fixVert(verticlesValues[i, j, 0]));
                    //bw.Write(fixVert(verticlesValues[i, j, 1]));
                    //bw.Write(fixVert(verticlesValues[i, j, 2]));
                    bw.Write(FixerHelper.fixVert(verticlesValues[i, j, 3]));
                    //ushort w = 15360;
                    //bw.Write(w);
                    bw.BaseStream.Position = bw.BaseStream.Position + 8;
                }
            }

            // bw.BaseStream.Seek(position, SeekOrigin.Begin);
            // bw.Write(patch);

            bw.Close();

            //Console.WriteLine("{0} {1} {2}", fixVert(verticlesValues[0, 1, 0]), fixVert(verticlesValues[0, 1, 1]), fixVert(verticlesValues[0, 1, 2]));
            //Console.WriteLine("{0} {1} {2} {3}", byte2hexIntFracToFloat2(verticlesValues[0, 0, 0]), byte2hexIntFracToFloat2(verticlesValues[0, 0, 1]), byte2hexIntFracToFloat2(verticlesValues[0, 0, 2]), byte2hexIntFracToFloat2(verticlesValues[0, 0, 3]));
            //Console.WriteLine("{0} {1} {2} {3}", toInt(verticlesValues[0, 0, 0]) / 256f / 128, toInt(verticlesValues[0, 0, 1]) / 256f / 128, toInt(verticlesValues[0, 0, 2]) / 256f / 128, toInt(verticlesValues[0, 0, 3]) / 256f / 128);

            //Console.WriteLine("{0} elements in header, {1} dataStreams, {2} p3s_c4b_t2sChunks", headerElements, dataStreamsCount, p3s_c4b_t2sCount);
            //Console.WriteLine("{0} vericles", maxVertsCount);

        }

        private static void fixTangents7(string path)
        {
            while (!FixerHelper.IsFileReady(path)) { }
            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));
            br.ReadUInt32();
            br.ReadUInt32();
            uint headerElements = br.ReadUInt32();
            br.ReadUInt32();
            uint[,] dataStreamChunksOffsets = new uint[headerElements, 2];
            int dataStreamsCount = 0;
            for (int i = 0; i < (int)headerElements; i++)
            {
                uint chunkType = br.ReadUInt16();
                //Console.WriteLine("{0:X}", chunkType);
                br.ReadUInt16();
                uint id = br.ReadUInt32();
                br.ReadUInt32();
                uint offset = br.ReadUInt32();
                if (chunkType == 0x00001016) { dataStreamChunksOffsets[dataStreamsCount, 0] = offset; dataStreamChunksOffsets[dataStreamsCount, 1] = id; dataStreamsCount++; }
            }

            uint[,] tangentsChunksOffsets = new uint[dataStreamsCount, 2];
            int tangentsCount = 0;
            uint delta = 0;
            for (int i = 0; i < dataStreamsCount; i++)
            {
                br.BaseStream.Position = dataStreamChunksOffsets[i, 0];
                br.ReadUInt32();
                uint nStreamType = br.ReadUInt32();
                if (nStreamType == 0x00000006)
                {
                    tangentsChunksOffsets[tangentsCount, 0] = dataStreamChunksOffsets[i, 0]; //offset
                    tangentsChunksOffsets[tangentsCount, 1] = br.ReadUInt32();//verts count
                    long temppos = br.BaseStream.Position;
                    delta = delta + tangentsChunksOffsets[tangentsCount, 1] * 8;
                    editTempFileHeaderOffsets((int)dataStreamChunksOffsets[i, 1], delta, br);
                    br.BaseStream.Position = temppos;
                    tangentsCount++;
                }
            }
            int maxVertsCount = 0;
            for (int i = 0; i < tangentsCount; i++)
            {
                int buf = (int)tangentsChunksOffsets[i, 1];
                if (buf > maxVertsCount) maxVertsCount = buf;
            }
            sbyte[,,] verticlesValues = new sbyte[tangentsCount, maxVertsCount, 8];
            uint[,] tangentHex = new uint[tangentsCount, maxVertsCount];
            uint[,] bitangentHex = new uint[tangentsCount, maxVertsCount];

            for (int i = 0; i < tangentsCount; i++)
            {
                uint offset = tangentsChunksOffsets[i, 0];
                br.BaseStream.Position = offset;
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                uint vertsCount = tangentsChunksOffsets[i, 1];
                for (int j = 0; j < (int)vertsCount; j++)
                {
                    tangentHex[i, j] = br.ReadUInt32();
                    bitangentHex[i, j] = br.ReadUInt32();
                }

            }
            br.Close();

            uint delta2 = 0;
            for (int i = 0; i < tangentsCount; i++)
            {
                Console.Write(".");
                //Console.WriteLine("");
                //while (!IsFileReady(path)) { }
                //BinaryWriter bw = new BinaryWriter(streamTempFile);

                uint offset = tangentsChunksOffsets[i, 0];// +(uint)i* tangentsChunksOffsets[i, 1] * 8;
                if (i > 0) delta2 = delta2 + tangentsChunksOffsets[i - 1, 1] * 8;
                offset = offset + delta2;
                int vertsCount = (int)tangentsChunksOffsets[i, 1];




                streamTempFile.Position = offset;
                streamTempFile.Position = streamTempFile.Position + 24;
                //bw.BaseStream.Position = offset;
                //bw.BaseStream.Position = bw.BaseStream.Position + 24;
                for (int j = 0; j < vertsCount; j++)
                {
                    CryEngine.Vector3 pos = new CryEngine.Vector3(positions[i, j].X, positions[i, j].Y, positions[i, j].Z);
                    CryEngine.Vector2 texcoor = new CryEngine.Vector2(texcoords[i, j].X, texcoords[i, j].Y);
                    TSpace tspac = new TSpace();
                    //if (i == 0 && (j == 0 || j == 7 || j == 10 || j == 9774)) { Console.WriteLine("[{0}]",j);  tspac = Utils.VSassembly(pos, texcoor, tangentHex[i, j], bitangentHex[i, j],true); }
                    //else {  tspac = Utils.VSassembly(pos, texcoor, tangentHex[i, j], bitangentHex[i, j]);  }
                    tspac = Utils.VSassembly(pos, tangentHex[i, j], bitangentHex[i, j]);

                    //bw.Write(Utils.tPackF2B(tspac.tangent.x));
                    //bw.Write(Utils.tPackF2B(tspac.tangent.y));
                    //bw.Write(Utils.tPackF2B(tspac.tangent.z));
                    //bw.Write(Utils.tPackF2B(tspac.tangent.w));
                    //bw.Write(Utils.tPackF2B(tspac.bitangent.x));
                    //bw.Write(Utils.tPackF2B(tspac.bitangent.y));
                    //bw.Write(Utils.tPackF2B(tspac.bitangent.z));
                    //bw.Write(Utils.tPackF2B(tspac.tangent.w));

                    streamTempFile.Write(BitConverter.GetBytes(Utils.tPackF2B(tspac.tangent.x)), 0, sizeof(short));
                    streamTempFile.Write(BitConverter.GetBytes(Utils.tPackF2B(tspac.tangent.y)), 0, sizeof(short));
                    streamTempFile.Write(BitConverter.GetBytes(Utils.tPackF2B(tspac.tangent.z)), 0, sizeof(short));
                    streamTempFile.Write(BitConverter.GetBytes(Utils.tPackF2B(tspac.tangent.w)), 0, sizeof(short));
                    streamTempFile.Write(BitConverter.GetBytes(Utils.tPackF2B(tspac.bitangent.x)), 0, sizeof(short));
                    streamTempFile.Write(BitConverter.GetBytes(Utils.tPackF2B(tspac.bitangent.y)), 0, sizeof(short));
                    streamTempFile.Write(BitConverter.GetBytes(Utils.tPackF2B(tspac.bitangent.z)), 0, sizeof(short));
                    streamTempFile.Write(BitConverter.GetBytes(Utils.tPackF2B(tspac.tangent.w)), 0, sizeof(short));


                }
                long writePos = streamTempFile.Position;
                // bw.Close();

                appendFooter(path, tangentsChunksOffsets[i, 0] + (vertsCount * 8) + 24, writePos);
            }
        }


        private static void loadIndices(string path)
        {
            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));
            br.ReadUInt32();
            br.ReadUInt32();
            uint headerElements = br.ReadUInt32();
            br.ReadUInt32();
            uint[] dataStreamChunksOffsets = new uint[headerElements];
            int dataStreamsCount = 0;
            for (int i = 0; i < (int)headerElements; i++)
            {
                uint chunkType = br.ReadUInt16();
                //Console.WriteLine("{0:X}", chunkType);
                br.ReadUInt16();
                br.ReadUInt32();
                br.ReadUInt32();
                uint offset = br.ReadUInt32();
                if (chunkType == 0x00001016) { dataStreamChunksOffsets[dataStreamsCount] = offset; dataStreamsCount++; }
            }

            uint[,] indicesChunksOffsets = new uint[dataStreamsCount, 2];
            int indicesChunksCount = 0;
            for (int i = 0; i < dataStreamsCount; i++)
            {
                br.BaseStream.Position = dataStreamChunksOffsets[i];
                br.ReadUInt32();
                uint nStreamType = br.ReadUInt32();
                if (nStreamType == 0x00000005)
                {
                    indicesChunksOffsets[indicesChunksCount, 0] = dataStreamChunksOffsets[i]; //offset
                    indicesChunksOffsets[indicesChunksCount, 1] = br.ReadUInt32();//verts count
                    indicesChunksCount++;
                }
            }
            int maxIndexesCount = 0;
            for (int i = 0; i < indicesChunksCount; i++)
            {
                int buf = (int)indicesChunksOffsets[i, 1];
                if (buf > maxIndexesCount) maxIndexesCount = buf;
            }
            //ushort[,] verticlesValues = new ushort[indicesChunksCount, maxVertsCount, 4];
            indicesCounts = new int[indicesChunksCount];
            indices = new int[indicesChunksCount, maxIndexesCount];

            for (int i = 0; i < indicesChunksCount; i++)
            {
                Console.Write(".");
                uint offset = indicesChunksOffsets[i, 0];
                br.BaseStream.Position = offset;
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                br.ReadUInt32();
                uint indexesCount = indicesChunksOffsets[i, 1];
                for (int j = 0; j < (int)indexesCount; j++)
                {
                    indices[i, j] = br.ReadInt32();
                    //Console.WriteLine("index[{0}] = {1}",j, indices[i, j]);
                }
                indicesCounts[i] = (int)indexesCount;
                //Console.WriteLine("loaded {0} indexes",indexesCount);

            }
            br.Close();
        }
        private static void modifyTransforms(string path)
        {
            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));
            br.ReadUInt32();
            br.ReadUInt32();
            uint headerElements = br.ReadUInt32();
            br.ReadUInt32();
            uint[] nodeChunksOffsets = new uint[headerElements];
            int nodesCount = 0;
            for (int i = 0; i < (int)headerElements; i++)
            {
                uint chunkType = br.ReadUInt16();
                //Console.WriteLine("{0:X}", chunkType);
                br.ReadUInt16();
                br.ReadUInt32();
                br.ReadUInt32();
                uint offset = br.ReadUInt32();
                if (chunkType == 0x0000100B) { nodeChunksOffsets[nodesCount] = offset; nodesCount++; }
            }
            //Console.WriteLine("found {0} nodes",nodesCount);
            Vector3[] tm1 = new Vector3[nodesCount];
            Vector3[] tm2 = new Vector3[nodesCount];
            Vector3[] tm3 = new Vector3[nodesCount];
            Vector3[] tm4 = new Vector3[nodesCount];
            for (int i = 0; i < nodesCount; i++)
            {
                uint offset = nodeChunksOffsets[i];
                br.BaseStream.Position = offset;
                br.BaseStream.Position = br.BaseStream.Position + 84;
                tm1[i].X = br.ReadSingle();
                tm1[i].Y = br.ReadSingle();
                tm1[i].Z = br.ReadSingle();
                br.ReadSingle();
                tm2[i].X = br.ReadSingle();
                tm2[i].Y = br.ReadSingle();
                tm2[i].Z = br.ReadSingle();
                br.ReadSingle();
                tm3[i].X = br.ReadSingle();
                tm3[i].Y = br.ReadSingle();
                tm3[i].Z = br.ReadSingle();
                br.ReadSingle();
                tm4[i].X = br.ReadSingle();
                tm4[i].Y = br.ReadSingle();
                tm4[i].Z = br.ReadSingle();
            }

            br.Close();

            BinaryWriter bw = new BinaryWriter(File.Open(
                    path, FileMode.Open, FileAccess.ReadWrite));
            for (int i = 0; i < nodesCount; i++)
            {
                uint offset = nodeChunksOffsets[i];
                bw.BaseStream.Position = offset;
                bw.BaseStream.Position = bw.BaseStream.Position + 84;
                float zero = 0;
                bw.Write(tm1[i].X);
                bw.Write(tm1[i].Y);
                bw.Write(tm1[i].Z);
                bw.BaseStream.Position = bw.BaseStream.Position + 4;
                bw.Write(tm2[i].X);
                bw.Write(tm2[i].Y);
                bw.Write(tm2[i].Z);
                bw.BaseStream.Position = bw.BaseStream.Position + 4;
                bw.Write(tm3[i].X);
                bw.Write(tm3[i].Y);
                bw.Write(tm3[i].Z);
                bw.BaseStream.Position = bw.BaseStream.Position + 4;
                //bw.Write(1f);
                //bw.Write(zero);
                //bw.Write(zero);
                //bw.BaseStream.Position = bw.BaseStream.Position + 4;
                //bw.Write(zero);
                //bw.Write(1f);
                //bw.Write(zero);
                //bw.BaseStream.Position = bw.BaseStream.Position + 4;
                //bw.Write(zero);
                //bw.Write(zero);
                //bw.Write(1f);
                //bw.BaseStream.Position = bw.BaseStream.Position + 4;
                bw.Write(tm4[i].X);
                bw.Write(tm4[i].Y);
                bw.Write(tm4[i].Z);

            }
            bw.Close();
        }
        private static void loadBboxes(string path)
        {
            while (!FixerHelper.IsFileReady(path)) { }
            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));
            br.ReadUInt32();
            br.ReadUInt32();
            uint headerElements = br.ReadUInt32();
            br.ReadUInt32();
            uint[] meshCompiledChunksOffsets = new uint[headerElements];
            int meshCompiledCount = 0;
            for (int i = 0; i < (int)headerElements; i++)
            {
                uint chunkType = br.ReadUInt16();
                //Console.WriteLine("{0:X}", chunkType);
                br.ReadUInt16();
                br.ReadUInt32();
                br.ReadUInt32();
                uint offset = br.ReadUInt32();
                if (chunkType == 0x00001000) { meshCompiledChunksOffsets[meshCompiledCount] = offset; meshCompiledCount++; }
            }
            uint[] truemeshCompiledChunksOffsets = new uint[meshCompiledCount];
            int truemeshCompiledCount = 0;
            for (int i = 0; i < meshCompiledCount; i++)
            {
                uint offset = meshCompiledChunksOffsets[i];
                br.BaseStream.Position = offset;
                br.BaseStream.Position = br.BaseStream.Position + 88;
                int p3schunkID = br.ReadInt32();
                if (p3schunkID != 0) { truemeshCompiledChunksOffsets[truemeshCompiledCount] = offset; truemeshCompiledCount++; }
            }
            //Console.WriteLine("found {0} meshcompiled", truemeshCompiledCount);
            bboxMin = new Vector3[truemeshCompiledCount];
            bboxMax = new Vector3[truemeshCompiledCount];
            for (int i = 0; i < truemeshCompiledCount; i++)
            {
                Console.Write(".");
                uint offset = truemeshCompiledChunksOffsets[i];
                br.BaseStream.Position = offset;
                br.BaseStream.Position = br.BaseStream.Position + 108;
                bboxMin[i].X = br.ReadSingle();
                bboxMin[i].Y = br.ReadSingle();
                bboxMin[i].Z = br.ReadSingle();
                bboxMax[i].X = br.ReadSingle();
                bboxMax[i].Y = br.ReadSingle();
                bboxMax[i].Z = br.ReadSingle();
            }

            br.Close();

        }
        private static void fixMesh(string path)
        {
            //erase normals id from nStreamChunkID 
            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));
            br.ReadUInt32();
            br.ReadUInt32();
            uint headerElements = br.ReadUInt32();
            br.ReadUInt32();
            uint[,] meshChunksOffsets = new uint[headerElements, 2];
            int meshChunksCount = 0;
            for (int i = 0; i < (int)headerElements; i++)
            {
                uint chunkType = br.ReadUInt16();
                //Console.WriteLine("{0:X}", chunkType);
                br.ReadUInt16();
                uint id = br.ReadUInt32();
                br.ReadUInt32();
                uint offset = br.ReadUInt32();
                if (chunkType == 0x00001000) { meshChunksOffsets[meshChunksCount, 0] = offset; meshChunksOffsets[meshChunksCount, 1] = id; meshChunksCount++; }
            }
            br.Close();
            BinaryWriter bw = new BinaryWriter(File.Open(
                    path, FileMode.Open, FileAccess.ReadWrite));
            for (int i = 0; i < meshChunksCount; i++)
            {
                uint offset = meshChunksOffsets[i, 0];
                bw.BaseStream.Position = offset;
                bw.BaseStream.Position = bw.BaseStream.Position + 32;
                uint zero = 0;
                bw.Write(zero);
                if (useQTan)
                {
                    bw.BaseStream.Position = offset;
                    bw.BaseStream.Position = bw.BaseStream.Position + 28 + (4 * 6);
                    bw.Write(zero);
                    bw.BaseStream.Position = offset;
                    bw.BaseStream.Position = bw.BaseStream.Position + 28 + (4 * 12);
                    int chunkId = (int)qtangentsChunksOffsets[i, 1];
                    bw.Write(chunkId);
                }
            }
            bw.Close();
        }


        public static void fixCga(string path)
        {
            if (File.Exists(path))
            {
                //Console.Write("Loading indices");
                //loadIndices(path); Console.Write("DONE\n");

                Console.Write("Loading Bboxes");
                loadBboxes(path); Console.Write("DONE\n");

                //modifyTransforms(path);
                Console.Write("Fixing verts");
                fixVerts(path); Console.Write("DONE\n");
                if (useQTan)
                {
                    Console.Write("Fixing Tangent Space");
                    //fixQTangents(path); Console.Write("DONE\n");
                    //fixQTangents3(path); Console.Write("DONE\n");
                }
                fixMesh(path);

                if (!useQTan)
                {
                    //copy(path, path + "_new");
                    while (!FixerHelper.IsFileReady(path)) { }
                    using (FileStream fs = File.Open(path, FileMode.Open, FileAccess.Read))
                    {
                        streamTempFile = new MemoryStream();
                        fs.CopyTo(streamTempFile);
                    }
                    streamTempFile.Position = 0;
                    streamTempFile.Flush();

                    Console.Write("Fixing Tangent Space");
                    fixTangents7(path); Console.Write("DONE\n");
                    //fixTangents2(path); Console.Write("DONE\n");

                    FixerHelper.overwriteFile(streamTempFile, path);
                    //File.Delete(path);
                    //copy(path + "_new", path);
                    //File.Delete(path + "_new");
                }

                //if (!useQTan)
                //{
                //    copy(path, path + "_new");

                //    Console.Write("Fixing Tangent Space");
                //    fixTangents7(path); Console.Write("DONE\n");
                //    //fixTangents2(path); Console.Write("DONE\n");

                //    File.Delete(path);
                //    copy(path + "_new", path);
                //    File.Delete(path + "_new");
                //}
            }
            else
            {
                Console.WriteLine("File not found: {0}", path);
                Console.WriteLine("Press any key to continue");
                Console.Read();
            }
        }
        public static bool ContainsGeomStream(string path)
        {
            bool containsGeomStream = false;

            while (!FixerHelper.IsFileReady(path)) { }
            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));
            br.ReadUInt32();
            br.ReadUInt32();
            uint headerElements = br.ReadUInt32();
            br.ReadUInt32();
            uint[] dataStreamChunksOffsets = new uint[headerElements];
            //int dataStreamsCount = 0;
            for (int i = 0; i < (int)headerElements; i++)
            {
                uint chunkType = br.ReadUInt16();
                //Console.WriteLine("{0:X}", chunkType);
                br.ReadUInt16();
                br.ReadUInt32();
                br.ReadUInt32();
                uint offset = br.ReadUInt32();
                if (chunkType == 0x00001016) { containsGeomStream = true; break; }
            }
            br.Close();

            return containsGeomStream;
        }
        public static void fixSkin(string path)
        {
            if (File.Exists(path))
            {
                if (ContainsGeomStream(path))
                {
                    if (File.Exists(path + "m")) File.Delete(path + "m");
                }
                else
                {
                    if (File.Exists(path + "m"))
                    {
                        fixMesh(path + "m");
                        fixSkinVerts(path + "m");
                        if (!useQTan)
                        {
                            //copy(path, path + "_new");
                            while (!FixerHelper.IsFileReady(path + "m")) { }
                            using (FileStream fs = File.Open(path + "m", FileMode.Open, FileAccess.Read))
                            {
                                streamTempFile = new MemoryStream();
                                fs.CopyTo(streamTempFile);
                            }
                            streamTempFile.Position = 0;
                            streamTempFile.Flush();

                            Console.Write("Fixing Tangent Space");
                            fixTangents7(path + "m"); Console.Write("DONE\n");
                            //fixTangents2(path); Console.Write("DONE\n");

                            FixerHelper.overwriteFile(streamTempFile, path + "m");
                            //File.Delete(path);
                            //copy(path + "_new", path);
                            //File.Delete(path + "_new");
                        }
                    }
                }
            }
            //if(File.Exists(path)) File.Delete(path);
            //Console.Write("Loading indices");
            //loadIndices(path); Console.Write("DONE\n");

            //Console.Write("Loading Bboxes");
            //loadBboxes(path); Console.Write("DONE\n");

            ////modifyTransforms(path);
            //Console.Write("Fixing verts");
            //fixSkinVerts(path); Console.Write("DONE\n");
            //if (useQTan)
            //{
            //    Console.Write("Fixing Tangent Space");
            //    //fixQTangents(path); Console.Write("DONE\n");
            //    //fixQTangents3(path); Console.Write("DONE\n");
            //}
            //fixMesh(path);

            //if (!useQTan)
            //{
            //    copy(path, path + "_new");

            //    Console.Write("Fixing Tangent Space");
            //    fixTangents7(path); Console.Write("DONE\n");
            //    //fixTangents2(path); Console.Write("DONE\n");

            //    File.Delete(path);
            //    copy(path + "_new", path);
            //    File.Delete(path + "_new");
            //}

        }
        //TEMP for testing 
        //TODO: read file header then fix sizes by reading each chunk. - DONE
        public static void fixElements(string path, bool isSKIN = false)
        {
            //load datastream chunk offset
            while (!FixerHelper.IsFileReady(path)) { if (!File.Exists(path)) return; }
            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));
            //br.ReadUInt32();
            string signature = new string(br.ReadChars(4));
            if (signature != "CryT")
            {
                br.ReadUInt32();
                uint headerElements = br.ReadUInt32();
                br.ReadUInt32();
                uint[,] dataStreamChunksOffsets = new uint[headerElements, 3];
                //[x,0] - offset
                //[x,1] - elementsize
                //[x,2] - nStreamType
                int dataStreamsCount = 0;
                for (int i = 0; i < (int)headerElements; i++)
                {
                    uint chunkType = br.ReadUInt16();
                    //Console.WriteLine("{0:X}", chunkType);
                    br.ReadUInt16();
                    br.ReadUInt32();
                    br.ReadUInt32();
                    uint offset = br.ReadUInt32();
                    if (chunkType == 0x00001016)
                    {
                        dataStreamChunksOffsets[dataStreamsCount, 0] = offset;
                        dataStreamsCount++;
                    }
                }
                for (int i = 0; i < dataStreamsCount; i++)
                {
                    uint offset = dataStreamChunksOffsets[i, 0];
                    uint elementsize = 0;
                    uint nStreamType = 0;
                    br.BaseStream.Position = offset + 4;
                    nStreamType = (uint)br.ReadInt16();
                    br.BaseStream.Position = offset + 12;
                    elementsize = (uint)br.ReadInt16();
                    dataStreamChunksOffsets[i, 1] = elementsize;//element size //Console.WriteLine(elementsize);
                    dataStreamChunksOffsets[i, 2] = nStreamType;
                }
                br.Close();

                //write zeros on nElementSize position+2 (XX XX 00 00) -> datastream chunk offset + 14
                while (!FixerHelper.IsFileReady(path)) { }
                BinaryWriter bw = new BinaryWriter(File.Open(
                        path, FileMode.Open, FileAccess.ReadWrite));

                for (int i = 0; i < dataStreamsCount; i++)
                {
                    ushort zeroShort = 0;
                    uint offset = dataStreamChunksOffsets[i, 0];
                    //if (dataStreamChunksOffsets[i, 1] == 8 && !isSKIN)
                    //{
                    //    ushort patch16 = 16;
                    //    bw.BaseStream.Position = offset + 12;
                    //    bw.Write(patch16);
                    //    bw.Write(zeroShort);
                    //}
                    //nStreamType = 15 0xf(CGF_STREAM_P3S_C4B_T2S)
                    if (dataStreamChunksOffsets[i, 2] == 15)
                    {
                        ushort patch16 = 16;
                        bw.BaseStream.Position = offset + 12;
                        bw.Write(patch16);
                        bw.Write(zeroShort);
                    }
                    //nStreamType = 6 0x6 (CGF_STREAM_TANGENTS)
                    if (dataStreamChunksOffsets[i, 2] == 6)
                    {
                        ushort patch16 = 16;
                        bw.BaseStream.Position = offset + 12;
                        bw.Write(patch16);
                        bw.Write(zeroShort);
                    }
                    bw.BaseStream.Position = offset;
                    bw.BaseStream.Position = bw.BaseStream.Position + 14;
                    bw.Write(zeroShort);
                }
                bw.Close();
            }
            else
            {
                br.Close();
            }
        }
        public static void fixElementsOld(string path)
        {
            if (File.Exists(path))
            {
                //skin fix
                uint seek = 0x00100008;
                uint patch = 0x00000008;
                while (FixerHelper.Replace(path, seek, patch)) ;

                seek = 0x000D000C;
                patch = 0x0000000C;
                while (FixerHelper.Replace(path, seek, patch)) ;

                seek = 0x00210002;
                patch = 0x00000002;
                while (FixerHelper.Replace(path, seek, patch)) ;

                seek = 0x01400010;
                patch = 0x00000010;
                while (FixerHelper.Replace(path, seek, patch)) ;

                seek = 0x00090004;
                patch = 0x00000004;
                while (FixerHelper.Replace(path, seek, patch)) ;

                seek = 0x0160000C;
                patch = 0x0000000C;
                while (FixerHelper.Replace(path, seek, patch)) ;

                seek = 0x01000014;
                patch = 0x00000014;
                while (FixerHelper.Replace(path, seek, patch)) ;

                // seek = 0x001d0004;
                // patch = 0x00000004;
                // while (Replace(path, seek, patch)) ;

                //cda fix
                seek = 0x001d0004;
                patch = 0x00000004;
                while (FixerHelper.Replace(path, seek, patch)) ;

                seek = 0x01420008;
                patch = 0x0000010;
                while (FixerHelper.Replace(path, seek, patch)) ;

                seek = 0x01010010;
                patch = 0x00000010;
                while (FixerHelper.Replace(path, seek, patch)) ;

                seek = 0x01b00004;
                patch = 0x00000004;
                while (FixerHelper.Replace(path, seek, patch)) ;

                //seek = 0x00130004;
                //patch = 0x00000004;
                //while (Replace(path, seek, patch)) ;
            }
        }
    }
}
