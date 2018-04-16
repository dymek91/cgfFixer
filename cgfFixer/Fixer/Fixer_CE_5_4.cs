using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using CryEngine;

namespace cgfFixer
{
    class Fixer_CE_5_4
    {
        File_ChCr_746 primaryFile;
        File_ChCr_746 secondaryFile;

        public Fixer_CE_5_4(string firstFilePath, string secondFilePath)
        {
            if (File.Exists(firstFilePath) && File.Exists(secondFilePath))
            {
                LoadPrimaryFile(firstFilePath);
                LoadSecondaryFile(secondFilePath);

                Fix();
            }
        }
        void Fix()
        {
            primaryFile = FixMeshes(primaryFile);  
            secondaryFile = FixMeshes(secondaryFile);
        }
        File_ChCr_746 FixMeshes(File_ChCr_746 chCrFile)
        {
            for (int i = 0; i < chCrFile.chunks.Count; i++)
            {
                if (chCrFile.chunks[i].type == 0x00001000)
                {
                    Chunk_Mesh_801 chunkMesh = new Chunk_Mesh_801(chCrFile.chunks[i].content);

                    //fix p3s_c4b_t2s
                    int p3s_c4b_t2s_ChunkID = chunkMesh.Get_p3s_c4b_t2s_ChunkID();
                    if (p3s_c4b_t2s_ChunkID!=0)
                    {
                        Chunk_DataStream_800 dataStreamChunk = new Chunk_DataStream_800(chCrFile.chunks[p3s_c4b_t2s_ChunkID].content);
                        DataStream_p3s_c4b_t2s p3s_c4b_t2s = new DataStream_p3s_c4b_t2s(dataStreamChunk.nCount, dataStreamChunk.dataStream);
                        p3s_c4b_t2s = Fix_p3s_c4b_t2s(p3s_c4b_t2s, chunkMesh.GetBboxMin(), chunkMesh.GetBboxMax());

                        p3s_c4b_t2s.Serialize();
                        dataStreamChunk.dataStream = p3s_c4b_t2s.serialized;

                        dataStreamChunk.Serialize();
                        chCrFile.chunks[i].content = dataStreamChunk.serialized;

                        //fix tangents

                        //erase normals 
                        chunkMesh.nStreamChunkID[1] = 0;
                    }
                }
            }
            return chCrFile;
        }
        DataStream_p3s_c4b_t2s Fix_p3s_c4b_t2s(DataStream_p3s_c4b_t2s p3s_c4b_t2s,Vector3 bboxMin, Vector3 bboxMax)
        { 
            for(int i=0;i<p3s_c4b_t2s.p3s_c4b_t2sElem.Count;i++)
            {
                float multiplerX = Math.Abs(bboxMin.X - bboxMax.X) / 2f;
                float multiplerY = Math.Abs(bboxMin.Y - bboxMax.Y) / 2f;
                float multiplerZ = Math.Abs(bboxMin.Z - bboxMax.Z) / 2f;
                if (multiplerX < 1) { multiplerX = 1; }
                if (multiplerY < 1) { multiplerY = 1; }
                if (multiplerZ < 1) { multiplerZ = 1; }

                short shortPosX = Utils.EvilConverter.Convert(p3s_c4b_t2s.p3s_c4b_t2sElem[i].pos[0]);
                short shortPosY = Utils.EvilConverter.Convert(p3s_c4b_t2s.p3s_c4b_t2sElem[i].pos[1]);
                short shortPosZ = Utils.EvilConverter.Convert(p3s_c4b_t2s.p3s_c4b_t2sElem[i].pos[2]);
                short shortPosW = Utils.EvilConverter.Convert(p3s_c4b_t2s.p3s_c4b_t2sElem[i].pos[3]);

                p3s_c4b_t2s.p3s_c4b_t2sElem[i].pos[0] = HalfHelper.SingleToHalf(Utils.tPackB2F(shortPosX) * multiplerX + (bboxMax.X + bboxMin.X) / 2).value;
                p3s_c4b_t2s.p3s_c4b_t2sElem[i].pos[1] = HalfHelper.SingleToHalf(Utils.tPackB2F(shortPosY) * multiplerY + (bboxMax.Y + bboxMin.Y) / 2).value;
                p3s_c4b_t2s.p3s_c4b_t2sElem[i].pos[2] = HalfHelper.SingleToHalf(Utils.tPackB2F(shortPosZ) * multiplerZ + (bboxMax.Z + bboxMin.Z) / 2).value;
                p3s_c4b_t2s.p3s_c4b_t2sElem[i].pos[3] = HalfHelper.SingleToHalf(Utils.tPackB2F(shortPosW)).value;
            }
            return p3s_c4b_t2s;
        }
        File_ChCr_746 FixTangents(File_ChCr_746 chCrFile)
        {
            return chCrFile;
        } 
        void LoadPrimaryFile(string path)
        {
            while (!FixerHelper.IsFileReady(path)) { }

            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));

            primaryFile = Load_ChCr_746_File(br);
            br.Close();
        }
        void LoadSecondaryFile(string path)
        {
            while (!FixerHelper.IsFileReady(path)) { }

            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));

            secondaryFile = Load_ChCr_746_File(br);
            br.Close();
        }
        File_ChCr_746 Load_ChCr_746_File(BinaryReader br)
        {
            File_ChCr_746 chCrFile = new File_ChCr_746();

            chCrFile.signatureString = br.ReadUInt32();
            chCrFile.version = br.ReadUInt32();
            chCrFile.chunkCount = br.ReadUInt32();
            chCrFile.chunkTableOffset = br.ReadUInt32();

            br.BaseStream.Position = chCrFile.chunkTableOffset;
            for (int i = 0; i < chCrFile.chunkCount; i++)
            {
                Chunk chunk = new Chunk();

                chunk.type = br.ReadUInt16();
                chunk.version = br.ReadUInt16();
                chunk.chunkId = br.ReadUInt32();
                chunk.size = br.ReadUInt32();
                chunk.pos = br.ReadUInt32();

                chCrFile.chunks.Add(chunk);
            }
            for (int i = 0; i < chCrFile.chunkCount; i++)
            {
                br.BaseStream.Position = chCrFile.chunks[i].pos;
                chCrFile.chunks[i].content = new byte[chCrFile.chunks[i].size];
                for (int j = 0; j < chCrFile.chunks[i].size; j++)
                    chCrFile.chunks[i].content[j] = br.ReadByte();
            }

            return chCrFile;
        }
        public void RenderAndSaveFixedFile_Primary(string path)
        {  
            while (!FixerHelper.IsFileReady(path)) { }
            BinaryWriter bw = new BinaryWriter(File.Open(
                    path, FileMode.Open, FileAccess.ReadWrite));

            //write file header
            bw.Write(primaryFile.signatureString);
            bw.Write(primaryFile.version);
            bw.Write(primaryFile.chunkCount);
            bw.Write(primaryFile.chunkTableOffset);
            //write chunks headers
            for (int i = 0; i < primaryFile.chunkCount; i++)
            {
                bw.Write(primaryFile.chunks[i].type);
                bw.Write(primaryFile.chunks[i].version);
                bw.Write(primaryFile.chunks[i].chunkId);
                bw.Write(primaryFile.chunks[i].size);
                bw.Write(primaryFile.chunks[i].pos);
            }
            //write chunks contents
            for (int i = 0; i < primaryFile.chunkCount; i++)
            {
                for (int j = 0; j < primaryFile.chunks[i].size; j++)
                {
                    bw.Write(primaryFile.chunks[i].content[j]);
                }
            }
            bw.Close(); 
        }
        public void RenderAndSaveFixedFile_Secondary(string path)
        {
            while (!FixerHelper.IsFileReady(path)) { }
            BinaryWriter bw = new BinaryWriter(File.Open(
                    path, FileMode.Open, FileAccess.ReadWrite));

            //write file header
            bw.Write(secondaryFile.signatureString);
            bw.Write(secondaryFile.version);
            bw.Write(secondaryFile.chunkCount);
            bw.Write(secondaryFile.chunkTableOffset);
            //write chunks headers
            for (int i = 0; i < secondaryFile.chunkCount; i++)
            {
                bw.Write(secondaryFile.chunks[i].type);
                bw.Write(secondaryFile.chunks[i].version);
                bw.Write(secondaryFile.chunks[i].chunkId);
                bw.Write(secondaryFile.chunks[i].size);
                bw.Write(secondaryFile.chunks[i].pos);
            }
            //write chunks contents
            for (int i = 0; i < secondaryFile.chunkCount; i++)
            {
                for (int j = 0; j < secondaryFile.chunks[i].size; j++)
                {
                    bw.Write(secondaryFile.chunks[i].content[j]);
                }
            }
            bw.Close();
        }
    }
}
