﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cgfMerger
{
    class Chunk
    {
        public ushort type;
        public ushort version;
        public uint chunkId;
        public uint size;
        public uint pos;
        public byte[] content;
    }
}