using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cgfMerger
{
    class NodePairs
    {
        List<Chunk_Node_824> primaryFileNodes;
        List<Chunk_Node_824> secondaryFileNodes;
        List<Chunk_Node_824> mergedNodes = null;
        List<NodePair> nodePairs = null;

        public void LoadPrimaryFileNodes(List<Chunk> chunks)
        {
            primaryFileNodes = new List<Chunk_Node_824>();
            foreach (Chunk chunk in chunks)
            {
                if(chunk.type == 0x0000100B)
                {
                    Chunk_Node_824 node = new Chunk_Node_824(chunk.content);
                    primaryFileNodes.Add(node);
                }
            }
        }
        public void LoadSecondaryFileNodes(List<Chunk> chunks)
        {
            secondaryFileNodes = new List<Chunk_Node_824>();
            foreach (Chunk chunk in chunks)
            {
                if (chunk.type == 0x0000100B)
                {
                    Chunk_Node_824 node = new Chunk_Node_824(chunk.content);
                    secondaryFileNodes.Add(node);
                }
            }
        }
        public void MergeNodes()
        {
            mergedNodes = new List<Chunk_Node_824>();
            nodePairs = new List<NodePair>();
            int pairsFound = 0;
            for(int i=0;i<primaryFileNodes.Count;i++)
            {
                string primaryFileNodeName = Encoding.UTF8.GetString(primaryFileNodes[i].name);
                Chunk_Node_824 secondaryFileNode = GetSecondaryFileNodeByName(primaryFileNodeName);
                if(secondaryFileNode!=null)
                {
                    pairsFound++; 
                    Chunk_Node_824 mergedNode = primaryFileNodes[i];
                    mergedNode.objectID = secondaryFileNode.objectID;
                    mergedNodes.Add(mergedNode);

                    NodePair nodePair = new NodePair(primaryFileNodes[i], secondaryFileNode);
                    nodePairs.Add(nodePair);
                } 
            }
            if(pairsFound==0)
            {
                Console.WriteLine("Node Pairs Not Found");
            }
        }
        Chunk_Node_824 GetSecondaryFileNodeByName(string name)
        {
            Chunk_Node_824 node = null;
            foreach(Chunk_Node_824 chunk in secondaryFileNodes)
            {
                string chunkName = Encoding.UTF8.GetString(chunk.name);
                if (chunkName == name)
                {
                    node = chunk;
                }
            }
            return node;
        }
        public Chunk_Node_824 GetMergedNodeByName(string name)
        {
            Chunk_Node_824 node = null;
            foreach (Chunk_Node_824 chunk in mergedNodes)
            {
                string chunkName = Encoding.UTF8.GetString(chunk.name);
                if (chunkName == name)
                {
                    node = chunk;
                }
            }
            return node;
        }
    }
    class NodePair
    {
        public Chunk_Node_824 primaryFileNode;
        public Chunk_Node_824 secondaryFileNode;

        public NodePair()
        {

        }
        public NodePair(Chunk_Node_824 firstNode, Chunk_Node_824 secondNode)
        {
            primaryFileNode = firstNode;
            secondaryFileNode = secondNode;
        }
    }
}
