using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;

namespace cgfMerger
{
    class Merger
    {
        File_ChCr_746 primaryFile;
        File_ChCr_746 secondaryFile;

        File_ChCr_746 mergedFile;

        public Merger(string firstFilePath, string secondFilePath)
        {
            if (File.Exists(firstFilePath) && File.Exists(secondFilePath))
            {
                LoadPrimaryFile(firstFilePath);
                LoadSecondaryFile(secondFilePath);
                Merge();
                RecompileMeshes();
            }
        } 
        public void RenderAndSaveMergedFile(string path)
        {
            while (!MergerHelper.IsFileReady(path)) { }

            BinaryWriter bw = new BinaryWriter(File.Open(
                    path, FileMode.Open, FileAccess.ReadWrite));

            //write file header
            bw.Write(mergedFile.signatureString);
            bw.Write(mergedFile.version);
            bw.Write(mergedFile.chunkCount);
            bw.Write(mergedFile.chunkTableOffset);
            //write chunks headers
            for(int i=0;i< mergedFile.chunkCount;i++)
            {
                bw.Write(mergedFile.chunks[i].type);
                bw.Write(mergedFile.chunks[i].version);
                bw.Write(mergedFile.chunks[i].chunkId);
                bw.Write(mergedFile.chunks[i].size);
                bw.Write(mergedFile.chunks[i].pos);
            }
            //write chunks contents
            for (int i = 0; i < mergedFile.chunkCount; i++)
            {
                for (int j=0; j< mergedFile.chunks[i].size;j++ )
                {
                    bw.Write(mergedFile.chunks[i].content[j]);
                }
            }
            bw.Close();
        }
        void RecompileMeshes()
        {
            File_ChCr_746 newFile = mergedFile; 
            for(int i=0;i<mergedFile.chunkCount;i++)
            {
                //find mesh compiled with p3s_c4b_t2s, nStreamChunkID [15]!=0
                if(mergedFile.chunks[i].type == 0x00001000)
                {
                    Chunk_Mesh_801 mergedFileMeshChunk = new Chunk_Mesh_801(mergedFile.chunks[i].content);
                    if(mergedFileMeshChunk.nStreamChunkID[15] !=0)
                    {
                        uint p3s_c4b_t2sChunkId = mergedFileMeshChunk.nStreamChunkID[15];
                        uint maxChunkId = newFile.GetMaxId();
                        maxChunkId++;
                        Chunk p3s_c4b_t2sChunk = mergedFile.GetChunkById(mergedFileMeshChunk.nStreamChunkID[15]);
                        Chunk_DataStream_800 p3s_c4b_t2sDataStreamChunk = new Chunk_DataStream_800(p3s_c4b_t2sChunk.content);
                        DataStream_p3s_c4b_t2s p3s_c4b_t2sDataStream = new DataStream_p3s_c4b_t2s(p3s_c4b_t2sDataStreamChunk.nCount, p3s_c4b_t2sDataStreamChunk.dataStream); 

                        MeshRecompiler meshRecompiler = new MeshRecompiler(p3s_c4b_t2sDataStreamChunk.nCount, p3s_c4b_t2sDataStream);
                        Chunk_DataStream_800 positionsChunk = meshRecompiler.GetPositionsChunk();
                        Chunk_DataStream_800 texoordChunk = meshRecompiler.GetTexcoordsChunk();
                        Chunk_DataStream_800 colorsChunk = meshRecompiler.GetColorsChunk();
                        //<----------------------------------
                    }
                }
            }
        }
        void Merge()
        {
            RecalculateSecondFileChunksIds(primaryFile.GetMaxId());
            MergeNodes();
            for (int i=0; i<secondaryFile.chunkCount;i++)
            {
                primaryFile.chunks.Add(secondaryFile.chunks[i]);
                primaryFile.chunkCount++;
            }
            primaryFile.RecalculateChunksPositions();
            mergedFile = primaryFile;
        }
        void MergeNodes()
        {
            NodePairs nodePairs = new NodePairs();
            nodePairs.LoadPrimaryFileNodes(primaryFile.chunks);
            nodePairs.LoadSecondaryFileNodes(secondaryFile.chunks);
            nodePairs.MergeNodes();
            for(int i=0; i<primaryFile.chunkCount;i++)
            {
                if(primaryFile.chunks[i].type == 0x0000100B)
                {
                    Chunk_Node_824 primaryFileNode = new Chunk_Node_824(primaryFile.chunks[i].content);
                    string primaryFileNodeName = Encoding.UTF8.GetString(primaryFileNode.name);
                    Chunk_Node_824 mergedNode = nodePairs.GetMergedNodeByName(primaryFileNodeName);
                    if(mergedNode!=null)
                    {
                        mergedNode.Serialize();
                        primaryFile.chunks[i].content = mergedNode.serialized;
                    }
                }
                
            }
        }

        void LoadPrimaryFile(string path)
        {
            while (!MergerHelper.IsFileReady(path)) { } 

            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));

            primaryFile = Load_ChCr_746_File(br);
            br.Close();
        }
        void LoadSecondaryFile(string path)
        {
            while (!MergerHelper.IsFileReady(path)) { }

            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));

            secondaryFile = Load_ChCr_746_File(br);
            br.Close();
        } 
        void RecalculateSecondFileChunksIds(uint startId)
        {
            secondaryFile.OverwriteChunksIds(startId);
        }

        File_ChCr_746 Load_ChCr_746_File( BinaryReader br)
        {
            File_ChCr_746 chCrFile = new File_ChCr_746();

            chCrFile.signatureString = br.ReadUInt32();
            chCrFile.version = br.ReadUInt32();
            chCrFile.chunkCount = br.ReadUInt32();
            chCrFile.chunkTableOffset = br.ReadUInt32();

            br.BaseStream.Position = chCrFile.chunkTableOffset;
            for (int i=0;i< chCrFile.chunkCount;i++)
            {
                Chunk chunk = new Chunk();

                chunk.type = br.ReadUInt16();
                chunk.version = br.ReadUInt16();
                chunk.chunkId = br.ReadUInt32();
                chunk.size = br.ReadUInt32();
                chunk.pos = br.ReadUInt32();

                chCrFile.chunks.Add(chunk);
            }
            for(int i = 0; i<chCrFile.chunkCount; i++)
            {
                br.BaseStream.Position = chCrFile.chunks[i].pos;
                chCrFile.chunks[i].content = new byte[chCrFile.chunks[i].size];
                for (int j = 0; j < chCrFile.chunks[i].size; j++)
                    chCrFile.chunks[i].content[j] = br.ReadByte();
            }

            return chCrFile;
        }
    }
}
