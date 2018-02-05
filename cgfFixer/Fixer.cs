using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CryEngine;
using System.IO;

namespace cgfFixer
{
    static class Fixer
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

        static void overwriteFile(Stream st,string path)
        {
            using (var newFileStram = File.Create(path))
            {
                st.Seek(0, SeekOrigin.Begin);
                st.CopyTo(newFileStram); 
            }
        }

        static UInt16 SingleToHalf(Int32 si32)
        {
            // Our floating point number, F, is represented by the bit pattern in integer i.
            // Disassemble that bit pattern into the sign, S, the exponent, E, and the significand, M.
            // Shift S into the position where it will go in in the resulting half number.
            // Adjust E, accounting for the different exponent bias of float and half (127 versus 15).

            Int32 sign = (si32 >> 16) & 0x00008000;
            Int32 exponent = ((si32 >> 23) & 0x000000ff) - (127 - 15);
            Int32 mantissa = si32 & 0x007fffff;

            // Now reassemble S, E and M into a half:

            if (exponent <= 0)
            {
                if (exponent < -10)
                {
                    // E is less than -10. The absolute value of F is less than Half.MinValue
                    // (F may be a small normalized float, a denormalized float or a zero).
                    //
                    // We convert F to a half zero with the same sign as F.

                    return (UInt16)sign;
                }

                // E is between -10 and 0. F is a normalized float whose magnitude is less than Half.MinNormalizedValue.
                //
                // We convert F to a denormalized half.

                // Add an explicit leading 1 to the significand.

                mantissa = mantissa | 0x00800000;

                // Round to M to the nearest (10+E)-bit value (with E between -10 and 0); in case of a tie, round to the nearest even value.
                //
                // Rounding may cause the significand to overflow and make our number normalized. Because of the way a half's bits
                // are laid out, we don't have to treat this case separately; the code below will handle it correctly.

                Int32 t = 14 - exponent;
                Int32 a = (1 << (t - 1)) - 1;
                Int32 b = (mantissa >> t) & 1;

                mantissa = (mantissa + a + b) >> t;

                // Assemble the half from S, E (==zero) and M.

                return (UInt16)(sign | mantissa);
            }
            else if (exponent == 0xff - (127 - 15))
            {
                if (mantissa == 0)
                {
                    // F is an infinity; convert F to a half infinity with the same sign as F.

                    return (UInt16)(sign | 0x7c00);
                }
                else
                {
                    // F is a NAN; we produce a half NAN that preserves the sign bit and the 10 leftmost bits of the
                    // significand of F, with one exception: If the 10 leftmost bits are all zero, the NAN would turn 
                    // into an infinity, so we have to set at least one bit in the significand.

                    mantissa >>= 13;
                    return (UInt16)(sign | 0x7c00 | mantissa | ((mantissa == 0) ? 1 : 0));
                }
            }
            else
            {
                // E is greater than zero.  F is a normalized float. We try to convert F to a normalized half.

                // Round to M to the nearest 10-bit value. In case of a tie, round to the nearest even value.

                mantissa = mantissa + 0x00000fff + ((mantissa >> 13) & 1);

                if ((mantissa & 0x00800000) == 1)
                {
                    mantissa = 0;        // overflow in significand,
                    exponent += 1;        // adjust exponent
                }

                // exponent overflow
                if (exponent > 30) throw new ArithmeticException("Half: Hardware floating-point overflow.");

                // Assemble the half from S, E and M.

                return (UInt16)(sign | (exponent << 10) | (mantissa >> 13));
            }
        }

        private static bool Replace(string path, uint seek, uint patch)
        {
            long position = 0;
            while (!IsFileReady(path)) { }
            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));
            try
            {
                uint buf = 0; bool found = false;
                while (!found)
                {
                    buf = br.ReadUInt32();
                    if (buf == seek)
                    {
                        position = br.BaseStream.Position - 4;
                        Console.Write(".");
                        found = true;
                    }
                }
            }
            catch (Exception exp) { br.Close(); return false; }

            br.Close();
            while (!IsFileReady(path)) { }
            if (position > 0)
            {
                BinaryWriter bw = new BinaryWriter(File.Open(
                    path, FileMode.Open, FileAccess.ReadWrite));

                //
                // write byte (character 'b') at offset 1234
                // and close the file
                //
                bw.BaseStream.Seek(position, SeekOrigin.Begin);
                bw.Write(patch);
                bw.Close();
                return true;
            }

            return false;
        }

        static int ReverseToInt(ushort uInt16)
        {
            var bytes = BitConverter.GetBytes(uInt16);
            Array.Reverse(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }
        static int toInt(ushort uInt16)
        {
            var bytes = BitConverter.GetBytes(uInt16);
            //Array.Reverse(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }
        static int byteToInt(byte uInt8)
        {
            var bytes = BitConverter.GetBytes(uInt8);
            //Array.Reverse(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }

        static UInt16 fixVert(ushort uInt16)
        {
            //float normFloat = toInt(uInt16) / 256f / 128;
            //var bytes = BitConverter.GetBytes(normFloat);
            //return SingleToHalf(BitConverter.ToInt32(bytes,0));
            float normFloat = byte2hexIntFracToFloat3(uInt16);
            Half halfFloat;
            halfFloat = HalfHelper.SingleToHalf(normFloat);
            return halfFloat.value;
        }

        static ushort fixSmallVert(byte Int8)
        {
            //float normFloat = (sbyte)(uInt8) / 16f / 7;
            //float normFloat = (sbyte)(uInt8) / 16f / 8;
            // Console.WriteLine("normFloat {0}", normFloat);
            //var bytes = BitConverter.GetBytes(normFloat);
            //Console.WriteLine("shortnormFloat {0}", normFloat*256*128);
            //Console.WriteLine("ushortnormFloat {0}",(ushort)( normFloat * 256 * 128));
            //return (ushort)(normFloat * 256 * 128);
            //return BitConverter.ToUInt16(bytes,0);
            //var bytes = BitConverter.GetBytes(normFloat);
            //return SingleToHalf(BitConverter.ToInt32(bytes, 0));
            //Console.WriteLine("sbyte / 128  {0}", Int8 / 128f);
            //Console.WriteLine("short        {0}", Int8 * 256  );
            //string normString = byte1hexToFloat( Int8).ToString("0.0");
            //float normFloat = Convert.ToSingle(normString);
            // Console.WriteLine(normFloat * 256 * 128);
            //return (ushort)(normFloat * 256 * 128);
            return byte1hexToUshort(Int8);
            //return (ushort)(Int8 * 256);
        }
        static int byte1hexToIntType2(string hexString)
        {
            int value = Convert.ToSByte(hexString, 16);
            return value;
        }

        static float byte2hexIntFracToFloat2(ushort uInt16)
        {
            var bytes = BitConverter.GetBytes(uInt16);

            int intPart = byte1hexToIntType2(Convert.ToString(bytes[1], 16));

            string binary = Convert.ToString(bytes[0], 2).PadLeft(8, '0');
            string binaryFracPart = binary;


            //convert Fractional Part
            float dec = 0;
            for (int i = 0; i < binaryFracPart.Length; i++)
            {
                if (binaryFracPart[i] == '0') continue;
                dec += (float)Math.Pow(2, (i + 1) * (-1));
            }
            float number = 0;
            number = (float)intPart + dec;
            /*if (intPart > 0) { number = (float)intPart + dec; }
            if (intPart < 0) { number = (float)intPart - dec; }
            if (intPart == 0) { number =  dec; }*/
            return number;
        }
        static float byte2hexIntFracToFloat3(ushort uInt16)
        {
            var bytes = BitConverter.GetBytes(uInt16);

            string binary1 = Convert.ToString(bytes[0], 2).PadLeft(8, '0');
            string binary2 = Convert.ToString(bytes[1], 2).PadLeft(8, '0');
            string binary = binary2 + binary1;
            string sign = binary.Substring(0, 1);
            //string binaryIntPart = binary.Substring(1, 2);
            string binaryFracPart = binary.Substring(1, 15); ;

            //convert int part
            int intPart = 0;
            /*for (int i = 0; i < binaryIntPart.Length; i++)
            {
                if (binaryIntPart[i] == '0') continue;
                intPart += (int)Math.Pow(2, i);
            }
            */
            if (sign == "1") intPart = -1;
            //convert Fractional Part
            float dec = 0;
            for (int i = 0; i < binaryFracPart.Length; i++)
            {
                if (binaryFracPart[i] == '0') continue;
                dec += (float)Math.Pow(2, (i + 1) * (-1));
            }
            float number = 0;
            number = (float)intPart + dec; //+ (float)0.000030517578125;
            if (number != 0) number = number + (float)0.000030517578125;
            /*if (intPart > 0) { number = (float)intPart + dec; }
            if (intPart < 0) { number = (float)intPart - dec; }
            if (intPart == 0) { number =  dec; }*/
            return number;


        }
        static float byte1hexIntFracToFloat(byte uInt8)
        {

            string binary = Convert.ToString(uInt8, 2).PadLeft(8, '0');
            //binary = Reverse(binary);
            string sign = binary.Substring(0, 1);
            string binaryintPart = binary.Substring(1, 3);
            string binaryFracPart = binary.Substring(4, 4);

            //convert int part
            int intPart = 0;
            for (int i = 0; i < binaryintPart.Length; i++)
            {
                if (binaryintPart[i] == '0') continue;
                intPart += (int)Math.Pow(2, i);
            }
            if (sign == "1") intPart = intPart * (-1);

            //convert Fractional Part
            float dec = 0;
            for (int i = 0; i < binaryFracPart.Length; i++)
            {
                if (binaryFracPart[i] == '0') continue;
                dec += (float)Math.Pow(2, (i + 1) * (-1));
            }
            float number = 0;
            number = (float)intPart + dec;
            /*if (intPart > 0) { number = (float)intPart + dec; }
            if (intPart < 0) { number = (float)intPart - dec; }
            if (intPart == 0) { number =  dec; }*/
            return number;
        }
        static float byte1hexToFloat(byte uInt8)
        {
            //return byte1hexTo8bitIEE(uInt8);
            string binary = Convert.ToString(uInt8, 2).PadLeft(8, '0');
            //binary = Reverse(binary);
            //string part1 = binary.Substring(0, 4);
            //string part2 = binary.Substring(4, 4);
            //binary = part2 + part1;

            string sign = binary.Substring(0, 1);
            string binaryFracPart = binary.Substring(1, 7);
            int intPart = 0;
            //double intPart = -1 * Convert.ToInt32(sign) * Math.Pow(2,0);
            if (sign == "1") intPart = -1;

            //convert Fractional Part
            //binaryFracPart = Reverse(binaryFracPart);
            float dec = 0;
            for (int i = 0; i < binaryFracPart.Length; i++)
            {
                if (binaryFracPart[i] == '0') continue;
                dec += (float)Math.Pow(2, (i + 1) * (-1));
            }
            double number = 0;
            number = intPart + dec + Math.Pow(2, -7);
            /*if (intPart > 0) { number = (float)intPart + dec; }
            if (intPart < 0) { number = (float)intPart - dec; }
            if (intPart == 0) { number =  dec; }*/
            return (float)number;
        }
        static float byte1hexTo8bitIEE(byte uInt8)
        {
            string binary = Convert.ToString(uInt8, 2).PadLeft(8, '0');
            string sign = binary.Substring(0, 1);
            string exponentBits = binary.Substring(1, 3);
            string fractionBits = binary.Substring(4, 4);
            int bias = 3;

            //convert fractionBits Part
            fractionBits = Reverse(fractionBits);
            int intFraction = 0;
            for (int i = 0; i < fractionBits.Length; i++)
            {
                if (fractionBits[i] == '0') continue;
                intFraction += (int)Math.Pow(2, i);
            }
            float floatFraction = intFraction / 16f + 1;

            //convert exponentBits part
            exponentBits = Reverse(exponentBits);
            int intExponent = 0;
            for (int i = 0; i < exponentBits.Length; i++)
            {
                if (exponentBits[i] == '0') continue;
                intExponent += (int)Math.Pow(2, i);
            }
            intExponent = intExponent - bias;



            double value = floatFraction * Math.Pow(2, intExponent);
            if (sign == "1") value = value * (-1);
            Console.WriteLine("{0}", value);
            return (float)value;
        }
        static ushort byte1hexToUshort(byte uInt8)
        {
            string binary = Convert.ToString(uInt8, 2).PadLeft(8, '0');
            binary = binary.PadRight(16, '0');
            return Convert.ToUInt16(binary, 2);
        }
        static void copy(string oldPath, string newPath)
        {
            FileStream input = null;
            FileStream output = null;
            try
            {
                input = new FileStream(oldPath, FileMode.Open);
                output = new FileStream(newPath, FileMode.Create, FileAccess.ReadWrite);

                byte[] buffer = new byte[32768];
                int read;
                while ((read = input.Read(buffer, 0, buffer.Length)) > 0)
                {
                    output.Write(buffer, 0, read);
                }
                input.Close();
                input.Dispose();
                output.Close();
                output.Dispose();
            }
            catch (Exception exp)
            {
            }
            finally
            {

            }
        }
        static string Reverse(string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }
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
                    streamTempFile.Write( BitConverter.GetBytes(currSize + delta),0,sizeof(uint));
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
        private static bool IsFileReady(String sFilename)
        {
            //return true;
            // System.Threading.Thread.Sleep(50);
            // If the file can be opened for exclusive access it means that the file
            // is no longer locked by another process.
            try
            {
                using (FileStream inputStream = File.Open(sFilename, FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    if (inputStream.Length > 0)
                    {
                        return true;
                    }
                    else
                    {
                        return false;
                    }

                }
            }
            catch (Exception)
            {
                return false;
            }
        }
        static void appendFooter(string path , long position, long positionWrite)
        {
            //temp, will do better handling later
            while (!IsFileReady(path)) { }
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
                    streamTempFile.Write(BitConverter.GetBytes( buffer),0,sizeof(uint));
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
            while (!IsFileReady(path)) { }
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

            while (!IsFileReady(path)) { }
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

                    positions[i, j].X = toInt(verticlesValues[i, j, 0]) / 256f / 128;
                    positions[i, j].Y = toInt(verticlesValues[i, j, 1]) / 256f / 128;
                    positions[i, j].Z = toInt(verticlesValues[i, j, 2]) / 256f / 128;
                }

            }
            br.Close();
            
            BinaryWriter bw = new BinaryWriter(File.Open(
                    path, FileMode.Open, FileAccess.ReadWrite));
            for (int i = 0; i < p3s_c4b_t2sCount; i++)
            {
                Console.Write(".");
                Vector3 maxCoord = new Vector3(byte2hexIntFracToFloat3(verticlesValues[i, 0, 0]), byte2hexIntFracToFloat3(verticlesValues[i, 0, 1]), byte2hexIntFracToFloat3(verticlesValues[i, 0, 2]));
                Vector3 minCoord = new Vector3(byte2hexIntFracToFloat3(verticlesValues[i, 0, 0]), byte2hexIntFracToFloat3(verticlesValues[i, 0, 1]), byte2hexIntFracToFloat3(verticlesValues[i, 0, 2]));

                uint offset = p3s_c4b_t2sChunksOffsets[i, 0];
                int vertsCount = (int)p3s_c4b_t2sChunksOffsets[i, 1];
                bw.BaseStream.Position = offset;
                bw.BaseStream.Position = bw.BaseStream.Position + 24;
                for (int j = 0; j < vertsCount; j++)
                {
                    float tempX = byte2hexIntFracToFloat3(verticlesValues[i, j, 0]);
                    float tempY = byte2hexIntFracToFloat3(verticlesValues[i, j, 1]);
                    float tempZ = byte2hexIntFracToFloat3(verticlesValues[i, j, 2]);
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
                    tempX.value = fixVert(verticlesValues[i, j, 0]);
                    tempY.value = fixVert(verticlesValues[i, j, 1]);
                    tempZ.value = fixVert(verticlesValues[i, j, 2]);

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
                    bw.Write(fixVert(verticlesValues[i, j, 3]));
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
            while (!IsFileReady(path)) { }
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
                    editTempFileHeaderOffsets( (int)dataStreamChunksOffsets[i, 1], delta, br);
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

                    streamTempFile.Write(BitConverter.GetBytes(Utils.tPackF2B(tspac.tangent.x)), 0,sizeof(short));
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
                
                appendFooter(path,  tangentsChunksOffsets[i, 0] + (vertsCount * 8) + 24, writePos);
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
            while (!IsFileReady(path)) { }
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
                    while (!IsFileReady(path)) { }
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

                    overwriteFile(streamTempFile, path);
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
                Console.WriteLine("File not found: {0}",path);
                Console.WriteLine("Press any key to continue");
                Console.Read();
            }
        }
        public static void fixSkin(string path)
        {
            if(File.Exists(path)) File.Delete(path);
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
        public static void fixElements(string path)
        {
            //load datastream chunk offset
            while (!IsFileReady(path)) { }
            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));
            br.ReadUInt32();
            br.ReadUInt32();
            uint headerElements = br.ReadUInt32();
            br.ReadUInt32();
            uint[,] dataStreamChunksOffsets = new uint[headerElements,2];
            int dataStreamsCount = 0;
            for (int i = 0; i < (int)headerElements; i++)
            {
                uint chunkType = br.ReadUInt16();
                //Console.WriteLine("{0:X}", chunkType);
                br.ReadUInt16();
                br.ReadUInt32();
                br.ReadUInt32();
                uint offset = br.ReadUInt32();
                if (chunkType == 0x00001016) {
                    dataStreamChunksOffsets[dataStreamsCount,0] = offset; 
                    dataStreamsCount++;
                }
            }
            for (int i = 0; i < dataStreamsCount; i++)
            {
                uint offset = dataStreamChunksOffsets[i,0];
                uint elementsize = 0;
                offset = offset + 12;
                br.BaseStream.Position = offset;
                elementsize = (uint)br.ReadInt16();
                dataStreamChunksOffsets[i, 1] = elementsize;//element size
                //Console.WriteLine(elementsize);
            }
            br.Close();

            //write zeros on nElementSize position+2 (XX XX 00 00) -> datastream chunk offset + 14
            while (!IsFileReady(path)) { }
            BinaryWriter bw = new BinaryWriter(File.Open(
                    path, FileMode.Open, FileAccess.ReadWrite));

            for (int i = 0; i < dataStreamsCount; i++)
            {
                ushort zeroShort = 0;
                uint offset = dataStreamChunksOffsets[i,0]; 
                if (dataStreamChunksOffsets[i, 1] == 8)
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
        public static void fixElementsOld(string path)
        {
            if (File.Exists(path))
            {
                //skin fix
                uint seek = 0x00100008;
                uint patch = 0x00000008;
                while (Replace(path, seek, patch)) ;

                seek = 0x000D000C;
                patch = 0x0000000C;
                while (Replace(path, seek, patch)) ;

                seek = 0x00210002;
                patch = 0x00000002;
                while (Replace(path, seek, patch)) ;

                seek = 0x01400010;
                patch = 0x00000010;
                while (Replace(path, seek, patch)) ;

                seek = 0x00090004;
                patch = 0x00000004;
                while (Replace(path, seek, patch)) ;

                seek = 0x0160000C;
                patch = 0x0000000C;
                while (Replace(path, seek, patch)) ;

                seek = 0x01000014;
                patch = 0x00000014;
                while (Replace(path, seek, patch)) ;

                // seek = 0x001d0004;
                // patch = 0x00000004;
                // while (Replace(path, seek, patch)) ;

                //cda fix
                seek = 0x001d0004;
                patch = 0x00000004;
                while (Replace(path, seek, patch)) ;

                seek = 0x01420008;
                patch = 0x0000010;
                while (Replace(path, seek, patch)) ;

                seek = 0x01010010;
                patch = 0x00000010;
                while (Replace(path, seek, patch)) ;

                seek = 0x01b00004;
                patch = 0x00000004;
                while (Replace(path, seek, patch)) ;

                //seek = 0x00130004;
                //patch = 0x00000004;
                //while (Replace(path, seek, patch)) ;
            }
        }
        public static String[] GetFiles(String path)
        {
            String[] filenames = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            return filenames;
        }

        static bool GetBit(this byte b, int bitNumber)
        {
            var bit = (b & (1 << bitNumber - 1)) != 0;
            return bit;
        }

        static float CalcAngleBetween(Vector3 invA, Vector3 invB)
        {
            float LengthQ = length(invA) * length(invB);

            if (LengthQ < 0.0001f)
            {
                LengthQ = 0.0001f;                      // to prevent division by zero
            }
            double f = mnozenie(invA, invB) / LengthQ;

            if (f > 1.0d)
            {
                f = 1.0d;                                                   // acos need input in the range [-1..1]
            }
            else if (f < -1.0d)
            {
                f = -1.0d;                                          //
            }
            float fRet = (float)Math.Acos(f);                              // cosf is not avaiable on every plattform

            return (fRet);
        }

        static float length(Vector3 invA)
        {
            return (float)Math.Sqrt(invA.X * invA.X + invA.Y * invA.Y + invA.Z * invA.Z);
        }
        static float mnozenie(Vector3 vec1, Vector3 vec2)
        {
            return (vec1.X * vec2.X + vec1.Y * vec2.Y + vec1.Z * vec2.Z);
        }
    }
}
