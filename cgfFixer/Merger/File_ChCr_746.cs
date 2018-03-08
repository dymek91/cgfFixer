using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cgfMerger
{
    class File_ChCr_746
    {
        public uint signatureString;
        public uint version = 0x00000746;
        public uint chunkCount;
        public uint chunkTableOffset;
        public List<Chunk> chunks = new List<Chunk>();

        uint fileHeaderSize = 16;
        uint singleChunkHeaderSize = 16;
        

        public void OverwriteChunksIds(uint startNumber)
        {
            uint idOffset = startNumber;
            idOffset++;
            for (int i=0;i<chunkCount;i++)
            {
                chunks[i].chunkId = chunks[i].chunkId+ idOffset; 
            }
            for (int i = 0; i < chunkCount; i++)
            {
                //patch Mesh (compiled) chunk nStreamChunkID
                if (chunks[i].type == 0x00001000 && chunks[i].version == 0x0000802)
                {
                    Chunk_Mesh_802 meshChunk = new Chunk_Mesh_802(chunks[i].content);
                    for (int j = 0; j < 128; j++)
                    {
                        if (meshChunk.nStreamChunkID[j] != 0)
                            meshChunk.nStreamChunkID[j] = meshChunk.nStreamChunkID[j] + idOffset;
                    }
                    meshChunk.nSubsetsChunkId = meshChunk.nSubsetsChunkId + idOffset;

                    meshChunk.Serialize();
                    chunks[i].content = meshChunk.serialized;
                }
                if (chunks[i].type == 0x00001000 && chunks[i].version == 0x0000801)
                {
                    Chunk_Mesh_801 meshChunk = new Chunk_Mesh_801(chunks[i].content);
                    for (int j = 0; j < 16; j++)
                    {
                        if (meshChunk.nStreamChunkID[j] != 0)
                            meshChunk.nStreamChunkID[j] = meshChunk.nStreamChunkID[j] + idOffset;
                    }
                    meshChunk.nSubsetsChunkId = meshChunk.nSubsetsChunkId + idOffset;

                    meshChunk.Serialize();
                    chunks[i].content = meshChunk.serialized;
                }
                //patch Node chunk ObjectID
                if (chunks[i].type == 0x0000100B)
                {
                    Chunk_Node_824 nodeChunk = new Chunk_Node_824(chunks[i].content);
                    nodeChunk.objectID = nodeChunk.objectID + idOffset;
                    if (nodeChunk.parentID != -1)
                        nodeChunk.parentID = nodeChunk.parentID + (int)idOffset;
                    nodeChunk.matID = nodeChunk.matID + idOffset;

                    nodeChunk.pos_cont_id = -1;
                    nodeChunk.rot_cont_id = -1;
                    nodeChunk.scl_cont_id = -1;

                    nodeChunk.Serialize();
                    chunks[i].content = nodeChunk.serialized;
                }
            }
        }
        public uint GetMaxId()
        {
            uint maxId=0;
            for (int i = 0; i < chunkCount; i++)
                if (chunks[i].chunkId > maxId) maxId = chunks[i].chunkId;
            return maxId;
        }
        public void RecalculateChunksPositions()
        {
            uint currentPosition = 0;
            currentPosition = currentPosition + fileHeaderSize;
            for(int i=0;i< chunkCount;i++)
            {
                currentPosition = currentPosition + singleChunkHeaderSize;
            }
            for(int i=0;i<chunkCount;i++)
            {
                chunks[i].pos = currentPosition;
                currentPosition = currentPosition + chunks[i].size;
            }
        }
        public Chunk GetChunkById(uint id)
        {
            Chunk retChunk = null;
            foreach(Chunk chunk in chunks)
            {
                if(chunk.chunkId == id)
                {
                    retChunk = chunk;
                }
            }
            return retChunk;
        }

    }
}
