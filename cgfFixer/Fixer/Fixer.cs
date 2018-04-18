using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using CryEngine;

namespace cgfFixer
{
    class Fixer
    {
        File_ChCr_746 primaryFile = null;
        File_ChCr_746 secondaryFile = null;

        bool isPrimaryFileConverted = false;
        bool isSecondaryFileConverted = false;

        public Fixer(string firstFilePath, string secondFilePath)
        {
            if (File.Exists(firstFilePath) && File.Exists(secondFilePath))
            { 
                while (!FixerHelper.IsFileReady(firstFilePath)) { }
                primaryFile = new File_ChCr_746(firstFilePath);  

                while (!FixerHelper.IsFileReady(secondFilePath)) { }
                secondaryFile = new File_ChCr_746(secondFilePath);

                if (!primaryFile.HasConvertedFlag() && (!primaryFile.HasWrongSignature()))
                {
                    FixPrimaryFile();
                }
                else
                {
                    isPrimaryFileConverted = true;
                    Console.WriteLine(primaryFile.filePath);
                    Console.WriteLine("FILE ALREADY CONVERTED - IGNORING");
                }

                if (!secondaryFile.HasConvertedFlag() && !primaryFile.HasWrongSignature()&&!secondaryFile.HasWrongSignature())
                {
                    FixSecondaryFile();
                }
                else
                {
                    isSecondaryFileConverted = true;
                    Console.WriteLine(secondaryFile.filePath);
                    Console.WriteLine("FILE ALREADY CONVERTED - IGNORING");
                }
            }
        }
        void FixPrimaryFile()
        {
            primaryFile = FixMeshes(primaryFile);   
        }
        void FixSecondaryFile()
        { 
            secondaryFile = FixMeshes(secondaryFile);
        }
        File_ChCr_746 FixMeshes(File_ChCr_746 chCrFile)
        {
            Console.WriteLine(chCrFile.filePath);
            //fix mesh
            for (int i = 0; i < chCrFile.chunks.Count; i++)
            {
                if (chCrFile.chunks[i].type == 0x00001000)
                {
                    Chunk_Mesh_801 chunkMesh = new Chunk_Mesh_801(chCrFile.chunks[i].content);

                    //fix p3s_c4b_t2s
                    int p3s_c4b_t2s_ChunkID = chunkMesh.Get_p3s_c4b_t2s_ChunkID();
                    if (p3s_c4b_t2s_ChunkID != 0)
                    {
                        Console.Write(".");
                        Chunk_DataStream_800 dataStreamChunk = new Chunk_DataStream_800(chCrFile.GetChunkById((uint)p3s_c4b_t2s_ChunkID).content);
                        if (dataStreamChunk.nElementSize == 16)
                        {
                            DataStream_p3s_c4b_t2s p3s_c4b_t2sDataStream = new DataStream_p3s_c4b_t2s(dataStreamChunk.nCount, dataStreamChunk.dataStream);
                            p3s_c4b_t2sDataStream = Fix_p3s_c4b_t2s(p3s_c4b_t2sDataStream, chunkMesh.GetBboxMin(), chunkMesh.GetBboxMax());

                            p3s_c4b_t2sDataStream.Serialize();
                            dataStreamChunk.dataStream = p3s_c4b_t2sDataStream.serialized;

                            dataStreamChunk.Serialize();
                            for (int j = 0; j < chCrFile.chunks.Count; j++)
                            {
                                if (chCrFile.chunks[j].chunkId == (uint)p3s_c4b_t2s_ChunkID)
                                    chCrFile.chunks[j].content = dataStreamChunk.serialized;
                            }
                        }
                        //skins since alpha 3.1 use p3f_c4b_t2s
                        if (dataStreamChunk.nElementSize == 20)
                        {
                            DataStream_p3f_c4b_t2s p3f_c4b_t2sDataStream = new DataStream_p3f_c4b_t2s(dataStreamChunk.nCount, dataStreamChunk.dataStream);
                            DataStream_p3s_c4b_t2s p3s_c4b_t2sDataStream = new DataStream_p3s_c4b_t2s();
                            p3s_c4b_t2sDataStream = Fix_p3f_c4b_t2s(p3f_c4b_t2sDataStream);

                            p3s_c4b_t2sDataStream.Serialize();
                            dataStreamChunk.dataStream = p3s_c4b_t2sDataStream.serialized;
                            dataStreamChunk.nElementSize = p3s_c4b_t2sDataStream.GetElementSize();

                            dataStreamChunk.Serialize();
                            for (int j = 0; j < chCrFile.chunks.Count; j++)
                            {
                                if (chCrFile.chunks[j].chunkId == (uint)p3s_c4b_t2s_ChunkID)
                                {
                                    chCrFile.chunks[j].content = dataStreamChunk.serialized;
                                    chCrFile.chunks[j].size = dataStreamChunk.GetSize();
                                }
                            }
                        } 

                        //fix tangents
                        int tangents_ChunkID = chunkMesh.GetTangentsChunkID();
                        if (tangents_ChunkID != 0)
                        {
                            Console.Write(".");
                            dataStreamChunk = new Chunk_DataStream_800(chCrFile.GetChunkById((uint)tangents_ChunkID).content);
                            DataStream_Tangents_SC tangentsDataStream_SC = new DataStream_Tangents_SC(dataStreamChunk.nCount, dataStreamChunk.dataStream);
                            DataStream_Tangents tangentsDataStream = new DataStream_Tangents();
                            tangentsDataStream = FixTangents(tangentsDataStream_SC);

                            tangentsDataStream.Serialize();
                            dataStreamChunk.dataStream = tangentsDataStream.serialized;

                            dataStreamChunk.nElementSize = tangentsDataStream.GetElementSize();

                            dataStreamChunk.Serialize();
                            for (int j = 0; j < chCrFile.chunks.Count; j++)
                            {
                                if (chCrFile.chunks[j].chunkId == (uint)tangents_ChunkID)
                                {
                                    chCrFile.chunks[j].content = dataStreamChunk.serialized;
                                    chCrFile.chunks[j].size = dataStreamChunk.GetSize();
                                }
                            }
                        }

                        //erase normals 
                        chunkMesh.nStreamChunkID[1] = 0;
                    }

                    chunkMesh.Serialize();
                    chCrFile.chunks[i].content = chunkMesh.serialized;
                }
            }
            //fix datastreams chunks elemsizes
            for (int i = 0; i < chCrFile.chunks.Count; i++)
            {
                if (chCrFile.chunks[i].type == 0x00001016)
                {
                    Chunk_DataStream_800 dataStreamChunk = new Chunk_DataStream_800(chCrFile.chunks[i].content);
                    dataStreamChunk.Serialize();
                    chCrFile.chunks[i].content = dataStreamChunk.serialized;
                }
            }
            Console.WriteLine();
            chCrFile.RecalculateChunksPositions();
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
        DataStream_p3s_c4b_t2s Fix_p3f_c4b_t2s(DataStream_p3f_c4b_t2s p3f_c4b_t2s)
        {
            DataStream_p3s_c4b_t2s p3s_c4b_t2s = new DataStream_p3s_c4b_t2s();
            for (int i = 0; i < p3f_c4b_t2s.p3f_c4b_t2sElem.Count; i++)
            {
                DataStream_p3s_c4b_t2s_Elem elem = new DataStream_p3s_c4b_t2s_Elem();
                elem.bgra = p3f_c4b_t2s.p3f_c4b_t2sElem[i].bgra;
                elem.uv = p3f_c4b_t2s.p3f_c4b_t2sElem[i].uv;

                elem.pos[0] = HalfHelper.SingleToHalf(p3f_c4b_t2s.p3f_c4b_t2sElem[i].pos[0]).value;
                elem.pos[1] = HalfHelper.SingleToHalf(p3f_c4b_t2s.p3f_c4b_t2sElem[i].pos[1]).value;
                elem.pos[2] = HalfHelper.SingleToHalf(p3f_c4b_t2s.p3f_c4b_t2sElem[i].pos[2]).value;
                elem.pos[3] = HalfHelper.SingleToHalf(1.0f).value;

                p3s_c4b_t2s.p3s_c4b_t2sElem.Add(elem);
            }
            return p3s_c4b_t2s;
        }
        DataStream_Tangents FixTangents(DataStream_Tangents_SC tangentsDataStream_SC)
        {
            DataStream_Tangents tangentsDataStream = new DataStream_Tangents();

            for(int i=0;i< tangentsDataStream_SC.tangent.Count;i++)
            {
                TSpace tSpace =  Utils.VSassembly(new Vector3(0,0,0), tangentsDataStream_SC.tangent[i].tangent, tangentsDataStream_SC.tangent[i].bitangent);
                DataStream_Tangents_Elem elem = new DataStream_Tangents_Elem(); 

                elem.tangent_binormal[0] = (ushort)Utils.tPackF2B(tSpace.tangent.x);
                elem.tangent_binormal[1] = (ushort)Utils.tPackF2B(tSpace.tangent.y);
                elem.tangent_binormal[2] = (ushort)Utils.tPackF2B(tSpace.tangent.z);
                elem.tangent_binormal[3] = (ushort)Utils.tPackF2B(tSpace.tangent.w);
                elem.tangent_binormal[4] = (ushort)Utils.tPackF2B(tSpace.bitangent.x);
                elem.tangent_binormal[5] = (ushort)Utils.tPackF2B(tSpace.bitangent.y); 
                elem.tangent_binormal[6] = (ushort)Utils.tPackF2B(tSpace.bitangent.z);
                elem.tangent_binormal[7] = (ushort)Utils.tPackF2B(tSpace.tangent.w);

                tangentsDataStream.tangent.Add(elem);
            }

            return tangentsDataStream;
        }   
        public void RenderAndSaveFixedFile_Primary(string path, bool flagAsConverted=false)
        {
            if (!HasPrimaryFileConvertedFlag())
            {
                while (!FixerHelper.IsFileReady(path)) { }
                if (primaryFile != null)
                    primaryFile.RenderAndSaveFile(path, flagAsConverted);
            }
        }
        public void RenderAndSaveFixedFile_Secondary(string path, bool flagAsConverted = false)
        {
            if (!HasSecondaryFileConvertedFlag())
            {
                while (!FixerHelper.IsFileReady(path)) { }
                if (secondaryFile != null)
                    secondaryFile.RenderAndSaveFile(path, flagAsConverted);
            }
        }
        public bool HasPrimaryFileConvertedFlag()
        {
            return isPrimaryFileConverted;
        }
        public bool HasSecondaryFileConvertedFlag()
        {
            return isSecondaryFileConverted;
        }
    }
}
