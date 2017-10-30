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
        static void editHeaderOffsets(string tempFilePath, int afterElement, uint delta, BinaryReader br)
        {
            BinaryWriter bw = new BinaryWriter(File.Open(
                    tempFilePath, FileMode.Open, FileAccess.ReadWrite));
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
                    bw.BaseStream.Position = position;
                    bw.Write(currSize + delta);
                }
                else
                {
                    br.ReadUInt32();
                }

                if (id > afterElement)
                {
                    long position = br.BaseStream.Position;
                    uint currOffset = br.ReadUInt32();
                    bw.BaseStream.Position = position;
                    bw.Write(currOffset + delta);
                }
                else
                {
                    br.ReadUInt32();
                }

            }
            bw.Close();
        }
        static void appendFooter(string path, string tempFilePath, long position, long positionWrite)
        {
            BinaryReader br = new BinaryReader(File.Open(
                    path, FileMode.Open, FileAccess.Read));
            BinaryWriter bw = new BinaryWriter(File.Open(
                    tempFilePath, FileMode.Open, FileAccess.ReadWrite));

            br.BaseStream.Position = position;
            bw.BaseStream.Position = positionWrite;
            uint buffer;
            int licz = 0;
            try
            {
                while (true)
                {
                    buffer = br.ReadUInt32();
                    bw.Write(buffer);
                    licz = licz + 1;
                }
            }
            catch (Exception exp) { }
            //Console.WriteLine("wrote {0} uints",licz);
            br.Close();
            bw.Close();

        }
        private static void fixVerts(string path)
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

                    //positions[i, j].X = toInt(verticlesValues[i, j, 0]) / 256f / 128;
                    //positions[i, j].Y = toInt(verticlesValues[i, j, 1]) / 256f / 128;
                    //positions[i, j].Z = toInt(verticlesValues[i, j, 2]) / 256f / 128;
                }

            }
            br.Close();
            //Console.WriteLine("texcords[0] {0} {1}", texcoords[0, 0].X, texcoords[0, 0].Y);

            //write fixed verts
            //File.Copy(path, path + "_new", true);
            //System.IO.FileStream fs = System.IO.File.Create(path + "_new");
            //fs.Close();
            BinaryWriter bw = new BinaryWriter(File.Open(
                    path, FileMode.Open, FileAccess.ReadWrite));
            //for(int i=0;i< p3s_c4b_t2sCount; i++)
            //{
            //    Console.Write(".");
            //    Vector3 maxCoord = new Vector3(byte2hexIntFracToFloat3(verticlesValues[i, 0, 0]), byte2hexIntFracToFloat3(verticlesValues[i, 0, 1]), byte2hexIntFracToFloat3(verticlesValues[i, 0, 2]));
            //    Vector3 minCoord = new Vector3(byte2hexIntFracToFloat3(verticlesValues[i, 0, 0]), byte2hexIntFracToFloat3(verticlesValues[i, 0, 1]), byte2hexIntFracToFloat3(verticlesValues[i, 0, 2]));

            //    uint offset = p3s_c4b_t2sChunksOffsets[i, 0];
            //    int vertsCount = (int)p3s_c4b_t2sChunksOffsets[i, 1];
            //    bw.BaseStream.Position = offset;
            //    bw.BaseStream.Position = bw.BaseStream.Position + 24;
            //    for (int j = 0; j < vertsCount; j++)
            //    {
            //        float tempX = byte2hexIntFracToFloat3(verticlesValues[i, j, 0]);
            //        float tempY = byte2hexIntFracToFloat3(verticlesValues[i, j, 1]);
            //        float tempZ = byte2hexIntFracToFloat3(verticlesValues[i, j, 2]);
            //        if (tempX > maxCoord.X) maxCoord.X = tempX;
            //        if (tempY > maxCoord.Y) maxCoord.Y = tempY;
            //        if (tempZ > maxCoord.Z) maxCoord.Z = tempZ;
            //        if (tempX < minCoord.X) minCoord.X = tempX;
            //        if (tempY < minCoord.Y) minCoord.Y = tempY;
            //        if (tempZ < minCoord.Z) minCoord.Z = tempZ;
            //    }
            //    for(int j=0;j< vertsCount;j++)
            //    {
            //        //float multiplerX = Math.Abs(bboxMin[i].X - bboxMax[i].X) / 2;
            //        //float multiplerY = Math.Abs(bboxMin[i].Y - bboxMax[i].Y) / 2;
            //        //float multiplerZ = Math.Abs(bboxMin[i].Z - bboxMax[i].Z) / 2;
            //        float multiplerX = Math.Abs(bboxMin[i].X - bboxMax[i].X) / 2f;//Math.Abs(minCoord.X - maxCoord.X);
            //        float multiplerY = Math.Abs(bboxMin[i].Y - bboxMax[i].Y) / 2f;//Math.Abs(minCoord.Y - maxCoord.Y);
            //        float multiplerZ = Math.Abs(bboxMin[i].Z - bboxMax[i].Z) / 2f;//Math.Abs(minCoord.Z - maxCoord.Z);
            //        if (multiplerX < 1) { multiplerX = 1; }//bboxMax[i].X = 0; bboxMin[i].X = 0;  } 
            //        if (multiplerY < 1) { multiplerY = 1; }//bboxMax[i].Y = 0; bboxMin[i].Y = 0;  }
            //        if (multiplerZ < 1) { multiplerZ = 1; }//bboxMax[i].Z = 0; bboxMin[i].Z = 0;  }
            //        Half tempX = new Half();
            //        Half tempY = new Half();
            //        Half tempZ = new Half();
            //        tempX.value = fixVert(verticlesValues[i, j, 0]);
            //        tempY.value = fixVert(verticlesValues[i, j, 1]);
            //        tempZ.value = fixVert(verticlesValues[i, j, 2]);

            //        //if (HalfHelper.HalfToSingle(tempX) > 0 && Math.Abs(bboxMax[i].X) >= 1) { multiplerX = Math.Abs(bboxMax[i].X); } else if (HalfHelper.HalfToSingle(tempX) < 0 && Math.Abs(bboxMin[i].X) >= 1) { multiplerX = Math.Abs(bboxMin[i].X); }
            //        //if (HalfHelper.HalfToSingle(tempY) > 0 && Math.Abs(bboxMax[i].Y) >= 1) { multiplerY = Math.Abs(bboxMax[i].Y); } else if (HalfHelper.HalfToSingle(tempY) < 0 && Math.Abs(bboxMin[i].Y) >= 1) { multiplerY = Math.Abs(bboxMin[i].Y); }
            //        //if (HalfHelper.HalfToSingle(tempZ) > 0 && Math.Abs(bboxMax[i].Z) >= 1) { multiplerZ = Math.Abs(bboxMax[i].Z); } else if (HalfHelper.HalfToSingle(tempZ) < 0 && Math.Abs(bboxMin[i].Z) >= 1) { multiplerZ = Math.Abs(bboxMin[i].Z); }

            //        //bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempX) * multiplerX + (bboxMax[i].X + bboxMin[i].X) / 2).value);
            //        //bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempY) * multiplerY + (bboxMax[i].Y + bboxMin[i].Y) / 2).value);
            //        //bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempZ) * multiplerZ + (bboxMax[i].Z + bboxMin[i].Z) / 2).value);
            //        bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempX) * multiplerX + (bboxMax[i].X + bboxMin[i].X) / 2).value);
            //        bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempY) * multiplerY + (bboxMax[i].Y + bboxMin[i].Y) / 2).value);
            //        bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempZ) * multiplerZ + (bboxMax[i].Z + bboxMin[i].Z) / 2).value);
            //        positions[i, j].X = HalfHelper.HalfToSingle(tempX) * multiplerX + (bboxMax[i].X + bboxMin[i].X) / 2;
            //        positions[i, j].Y = HalfHelper.HalfToSingle(tempY) * multiplerY + (bboxMax[i].Y + bboxMin[i].Y) / 2;
            //        positions[i, j].Z = HalfHelper.HalfToSingle(tempZ) * multiplerZ + (bboxMax[i].Z + bboxMin[i].Z) / 2;

            //        //bw.Write(fixVert(verticlesValues[i, j, 0]));
            //        //bw.Write(fixVert(verticlesValues[i, j, 1]));
            //        //bw.Write(fixVert(verticlesValues[i, j, 2]));
            //        bw.Write(fixVert(verticlesValues[i, j, 3]));
            //        //ushort w = 15360;
            //        //bw.Write(w);
            //        bw.BaseStream.Position = bw.BaseStream.Position + 8;
            //    }

            //}
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

                    //bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 0])).value);
                    //bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 1])).value);
                    //bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 2])).value);
                    //positions[i, j].X = Utils.tPackB2F(verticlesValues[i, j, 0]);
                    //positions[i, j].Y = Utils.tPackB2F(verticlesValues[i, j, 1]);
                    //positions[i, j].Z = Utils.tPackB2F(verticlesValues[i, j, 2]);

                    bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 0]) * multiplerX + (bboxMax[i].X + bboxMin[i].X) / 2).value);
                    bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 1]) * multiplerY + (bboxMax[i].Y + bboxMin[i].Y) / 2).value);
                    bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 2]) * multiplerZ + (bboxMax[i].Z + bboxMin[i].Z) / 2).value);
                    positions[i, j].X = Utils.tPackB2F(verticlesValues[i, j, 0]) * multiplerX + (bboxMax[i].X + bboxMin[i].X) / 2;
                    positions[i, j].Y = Utils.tPackB2F(verticlesValues[i, j, 1]) * multiplerY + (bboxMax[i].Y + bboxMin[i].Y) / 2;
                    positions[i, j].Z = Utils.tPackB2F(verticlesValues[i, j, 2]) * multiplerZ + (bboxMax[i].Z + bboxMin[i].Z) / 2;


                    //bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 0]) * multiplerX).value);
                    //bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 1]) * multiplerY).value);
                    //bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 2]) * multiplerZ).value);
                    //positions[i, j].X = Utils.tPackB2F(verticlesValues[i, j, 0]) * multiplerX;
                    //positions[i, j].Y = Utils.tPackB2F(verticlesValues[i, j, 1]) * multiplerY;
                    //positions[i, j].Z = Utils.tPackB2F(verticlesValues[i, j, 2]) * multiplerZ;

                    //bw.Write(fixVert(verticlesValues[i, j, 0]));
                    //bw.Write(fixVert(verticlesValues[i, j, 1]));
                    //bw.Write(fixVert(verticlesValues[i, j, 2]));
                    bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 3])).value);
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
            //Console.WriteLine("texcords[0] {0} {1}", texcoords[0, 0].X, texcoords[0, 0].Y);

            //write fixed verts
            //File.Copy(path, path + "_new", true);
            //System.IO.FileStream fs = System.IO.File.Create(path + "_new");
            //fs.Close();
            BinaryWriter bw = new BinaryWriter(File.Open(
                    path, FileMode.Open, FileAccess.ReadWrite));
            //for(int i=0;i< p3s_c4b_t2sCount; i++)
            //{
            //    Console.Write(".");
            //    Vector3 maxCoord = new Vector3(byte2hexIntFracToFloat3(verticlesValues[i, 0, 0]), byte2hexIntFracToFloat3(verticlesValues[i, 0, 1]), byte2hexIntFracToFloat3(verticlesValues[i, 0, 2]));
            //    Vector3 minCoord = new Vector3(byte2hexIntFracToFloat3(verticlesValues[i, 0, 0]), byte2hexIntFracToFloat3(verticlesValues[i, 0, 1]), byte2hexIntFracToFloat3(verticlesValues[i, 0, 2]));

            //    uint offset = p3s_c4b_t2sChunksOffsets[i, 0];
            //    int vertsCount = (int)p3s_c4b_t2sChunksOffsets[i, 1];
            //    bw.BaseStream.Position = offset;
            //    bw.BaseStream.Position = bw.BaseStream.Position + 24;
            //    for (int j = 0; j < vertsCount; j++)
            //    {
            //        float tempX = byte2hexIntFracToFloat3(verticlesValues[i, j, 0]);
            //        float tempY = byte2hexIntFracToFloat3(verticlesValues[i, j, 1]);
            //        float tempZ = byte2hexIntFracToFloat3(verticlesValues[i, j, 2]);
            //        if (tempX > maxCoord.X) maxCoord.X = tempX;
            //        if (tempY > maxCoord.Y) maxCoord.Y = tempY;
            //        if (tempZ > maxCoord.Z) maxCoord.Z = tempZ;
            //        if (tempX < minCoord.X) minCoord.X = tempX;
            //        if (tempY < minCoord.Y) minCoord.Y = tempY;
            //        if (tempZ < minCoord.Z) minCoord.Z = tempZ;
            //    }
            //    for(int j=0;j< vertsCount;j++)
            //    {
            //        //float multiplerX = Math.Abs(bboxMin[i].X - bboxMax[i].X) / 2;
            //        //float multiplerY = Math.Abs(bboxMin[i].Y - bboxMax[i].Y) / 2;
            //        //float multiplerZ = Math.Abs(bboxMin[i].Z - bboxMax[i].Z) / 2;
            //        float multiplerX = Math.Abs(bboxMin[i].X - bboxMax[i].X) / 2f;//Math.Abs(minCoord.X - maxCoord.X);
            //        float multiplerY = Math.Abs(bboxMin[i].Y - bboxMax[i].Y) / 2f;//Math.Abs(minCoord.Y - maxCoord.Y);
            //        float multiplerZ = Math.Abs(bboxMin[i].Z - bboxMax[i].Z) / 2f;//Math.Abs(minCoord.Z - maxCoord.Z);
            //        if (multiplerX < 1) { multiplerX = 1; }//bboxMax[i].X = 0; bboxMin[i].X = 0;  } 
            //        if (multiplerY < 1) { multiplerY = 1; }//bboxMax[i].Y = 0; bboxMin[i].Y = 0;  }
            //        if (multiplerZ < 1) { multiplerZ = 1; }//bboxMax[i].Z = 0; bboxMin[i].Z = 0;  }
            //        Half tempX = new Half();
            //        Half tempY = new Half();
            //        Half tempZ = new Half();
            //        tempX.value = fixVert(verticlesValues[i, j, 0]);
            //        tempY.value = fixVert(verticlesValues[i, j, 1]);
            //        tempZ.value = fixVert(verticlesValues[i, j, 2]);

            //        //if (HalfHelper.HalfToSingle(tempX) > 0 && Math.Abs(bboxMax[i].X) >= 1) { multiplerX = Math.Abs(bboxMax[i].X); } else if (HalfHelper.HalfToSingle(tempX) < 0 && Math.Abs(bboxMin[i].X) >= 1) { multiplerX = Math.Abs(bboxMin[i].X); }
            //        //if (HalfHelper.HalfToSingle(tempY) > 0 && Math.Abs(bboxMax[i].Y) >= 1) { multiplerY = Math.Abs(bboxMax[i].Y); } else if (HalfHelper.HalfToSingle(tempY) < 0 && Math.Abs(bboxMin[i].Y) >= 1) { multiplerY = Math.Abs(bboxMin[i].Y); }
            //        //if (HalfHelper.HalfToSingle(tempZ) > 0 && Math.Abs(bboxMax[i].Z) >= 1) { multiplerZ = Math.Abs(bboxMax[i].Z); } else if (HalfHelper.HalfToSingle(tempZ) < 0 && Math.Abs(bboxMin[i].Z) >= 1) { multiplerZ = Math.Abs(bboxMin[i].Z); }

            //        //bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempX) * multiplerX + (bboxMax[i].X + bboxMin[i].X) / 2).value);
            //        //bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempY) * multiplerY + (bboxMax[i].Y + bboxMin[i].Y) / 2).value);
            //        //bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempZ) * multiplerZ + (bboxMax[i].Z + bboxMin[i].Z) / 2).value);
            //        bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempX) * multiplerX + (bboxMax[i].X + bboxMin[i].X) / 2).value);
            //        bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempY) * multiplerY + (bboxMax[i].Y + bboxMin[i].Y) / 2).value);
            //        bw.Write(HalfHelper.SingleToHalf(HalfHelper.HalfToSingle(tempZ) * multiplerZ + (bboxMax[i].Z + bboxMin[i].Z) / 2).value);
            //        positions[i, j].X = HalfHelper.HalfToSingle(tempX) * multiplerX + (bboxMax[i].X + bboxMin[i].X) / 2;
            //        positions[i, j].Y = HalfHelper.HalfToSingle(tempY) * multiplerY + (bboxMax[i].Y + bboxMin[i].Y) / 2;
            //        positions[i, j].Z = HalfHelper.HalfToSingle(tempZ) * multiplerZ + (bboxMax[i].Z + bboxMin[i].Z) / 2;

            //        //bw.Write(fixVert(verticlesValues[i, j, 0]));
            //        //bw.Write(fixVert(verticlesValues[i, j, 1]));
            //        //bw.Write(fixVert(verticlesValues[i, j, 2]));
            //        bw.Write(fixVert(verticlesValues[i, j, 3]));
            //        //ushort w = 15360;
            //        //bw.Write(w);
            //        bw.BaseStream.Position = bw.BaseStream.Position + 8;
            //    }

            //}
            for (int i = 0; i < p3s_c4b_t2sCount; i++)
            {
                Console.Write(".");
                //Vector3 maxCoord = new Vector3(Utils.tPackB2F(verticlesValues[i, 0, 0]), Utils.tPackB2F(verticlesValues[i, 0, 1]), Utils.tPackB2F(verticlesValues[i, 0, 2]));
                //Vector3 minCoord = new Vector3(Utils.tPackB2F(verticlesValues[i, 0, 0]), Utils.tPackB2F(verticlesValues[i, 0, 1]), Utils.tPackB2F(verticlesValues[i, 0, 2]));

                uint offset = p3s_c4b_t2sChunksOffsets[i, 0];
                int vertsCount = (int)p3s_c4b_t2sChunksOffsets[i, 1];
                bw.BaseStream.Position = offset;
                bw.BaseStream.Position = bw.BaseStream.Position + 24;
                //for (int j = 0; j < vertsCount; j++)
                //{
                //    float tempX = Utils.tPackB2F(verticlesValues[i, j, 0]);
                //    float tempY = Utils.tPackB2F(verticlesValues[i, j, 1]);
                //    float tempZ = Utils.tPackB2F(verticlesValues[i, j, 2]);
                //    if (tempX > maxCoord.X) maxCoord.X = tempX;
                //    if (tempY > maxCoord.Y) maxCoord.Y = tempY;
                //    if (tempZ > maxCoord.Z) maxCoord.Z = tempZ;
                //    if (tempX < minCoord.X) minCoord.X = tempX;
                //    if (tempY < minCoord.Y) minCoord.Y = tempY;
                //    if (tempZ < minCoord.Z) minCoord.Z = tempZ;
                //}
                for (int j = 0; j < vertsCount; j++)
                {
                    //float multiplerX = Math.Abs(bboxMin[i].X - bboxMax[i].X) / 2f;//Math.Abs(minCoord.X - maxCoord.X);
                    //float multiplerY = Math.Abs(bboxMin[i].Y - bboxMax[i].Y) / 2f;//Math.Abs(minCoord.Y - maxCoord.Y);
                    //float multiplerZ = Math.Abs(bboxMin[i].Z - bboxMax[i].Z) / 2f;//Math.Abs(minCoord.Z - maxCoord.Z);
                    //if (multiplerX < 1) { multiplerX = 1; }//bboxMax[i].X = 0; bboxMin[i].X = 0;  } 
                    //if (multiplerY < 1) { multiplerY = 1; }//bboxMax[i].Y = 0; bboxMin[i].Y = 0;  }
                    //if (multiplerZ < 1) { multiplerZ = 1; }//bboxMax[i].Z = 0; bboxMin[i].Z = 0;  } 

                    //bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 0])).value);
                    //bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 1])).value);
                    //bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 2])).value);
                    //positions[i, j].X = Utils.tPackB2F(verticlesValues[i, j, 0]);
                    //positions[i, j].Y = Utils.tPackB2F(verticlesValues[i, j, 1]);
                    //positions[i, j].Z = Utils.tPackB2F(verticlesValues[i, j, 2]);

                    //bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 0]) * multiplerX + (bboxMax[i].X + bboxMin[i].X) / 2).value);
                    //bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 1]) * multiplerY + (bboxMax[i].Y + bboxMin[i].Y) / 2).value);
                    //bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 2]) * multiplerZ + (bboxMax[i].Z + bboxMin[i].Z) / 2).value);
                    //positions[i, j].X = Utils.tPackB2F(verticlesValues[i, j, 0]) * multiplerX + (bboxMax[i].X + bboxMin[i].X) / 2;
                    //positions[i, j].Y = Utils.tPackB2F(verticlesValues[i, j, 1]) * multiplerY + (bboxMax[i].Y + bboxMin[i].Y) / 2;
                    //positions[i, j].Z = Utils.tPackB2F(verticlesValues[i, j, 2]) * multiplerZ + (bboxMax[i].Z + bboxMin[i].Z) / 2;

                    bw.Write(HalfHelper.SingleToHalf(verticlesValues[i, j, 0]).value);
                    bw.Write(HalfHelper.SingleToHalf(verticlesValues[i, j, 1]).value);
                    bw.Write(HalfHelper.SingleToHalf(verticlesValues[i, j, 2]).value);
                    bw.Write(HalfHelper.SingleToHalf(verticlesValues[i, j, 3]).value);
                    bw.Write(colors[i, j]);
                    bw.Write(texcoordsUint16[i, j, 0]);
                    bw.Write(texcoordsUint16[i, j, 1]);


                    //bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 0]) * multiplerX).value);
                    //bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 1]) * multiplerY).value);
                    //bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 2]) * multiplerZ).value);
                    //positions[i, j].X = Utils.tPackB2F(verticlesValues[i, j, 0]) * multiplerX;
                    //positions[i, j].Y = Utils.tPackB2F(verticlesValues[i, j, 1]) * multiplerY;
                    //positions[i, j].Z = Utils.tPackB2F(verticlesValues[i, j, 2]) * multiplerZ;

                    //bw.Write(fixVert(verticlesValues[i, j, 0]));
                    //bw.Write(fixVert(verticlesValues[i, j, 1]));
                    //bw.Write(fixVert(verticlesValues[i, j, 2]));
                    //bw.Write(HalfHelper.SingleToHalf(Utils.tPackB2F(verticlesValues[i, j, 3])).value);
                    //ushort w = 15360;
                    //bw.Write(w);
                    //bw.BaseStream.Position = bw.BaseStream.Position + 8;
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
            //Console.WriteLine("texcords[0] {0} {1}", texcoords[0, 0].X, texcoords[0, 0].Y);

            //write fixed verts
            //File.Copy(path, path + "_new", true);
            //System.IO.FileStream fs = System.IO.File.Create(path + "_new");
            //fs.Close();
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

        //private static void fixTangents(string path)
        //{
        //    BinaryReader br = new BinaryReader(File.Open(
        //            path, FileMode.Open, FileAccess.Read));
        //    br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint headerElements = br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint[,] dataStreamChunksOffsets = new uint[headerElements,2];
        //    int dataStreamsCount = 0;
        //    for (int i = 0; i < (int)headerElements; i++)
        //    {
        //        uint chunkType = br.ReadUInt16();
        //        //Console.WriteLine("{0:X}", chunkType);
        //        br.ReadUInt16();
        //        uint id = br.ReadUInt32();
        //        br.ReadUInt32();
        //        uint offset = br.ReadUInt32();
        //        if (chunkType == 0x00001016) { dataStreamChunksOffsets[dataStreamsCount,0] = offset; dataStreamChunksOffsets[dataStreamsCount, 1] = id; dataStreamsCount++; }
        //    }

        //    uint[,] tangentsChunksOffsets = new uint[dataStreamsCount, 2];
        //    int tangentsCount = 0;
        //    uint delta = 0;
        //    for (int i = 0; i < dataStreamsCount; i++)
        //    {
        //        br.BaseStream.Position = dataStreamChunksOffsets[i,0];
        //        br.ReadUInt32();
        //        uint nStreamType = br.ReadUInt32();
        //        if (nStreamType == 0x00000006)
        //        {
        //            tangentsChunksOffsets[tangentsCount, 0] = dataStreamChunksOffsets[i,0]; //offset
        //            tangentsChunksOffsets[tangentsCount, 1] = br.ReadUInt32();//verts count
        //            long temppos = br.BaseStream.Position;
        //            delta = delta + tangentsChunksOffsets[tangentsCount, 1] * 8;
        //            editHeaderOffsets(path+"_new", (int)dataStreamChunksOffsets[i, 1], delta,br);
        //            br.BaseStream.Position = temppos;
        //            tangentsCount++;
        //        }
        //    }
        //    int maxVertsCount = 0;
        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        int buf = (int)tangentsChunksOffsets[i, 1];
        //        if (buf > maxVertsCount) maxVertsCount = buf;
        //    }
        //    sbyte[,,] verticlesValues = new sbyte[tangentsCount, maxVertsCount, 8];

        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        uint offset = tangentsChunksOffsets[i, 0];
        //        br.BaseStream.Position = offset;
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        uint vertsCount = tangentsChunksOffsets[i, 1];
        //        for (int j = 0; j < (int)vertsCount; j++)
        //        {
        //            verticlesValues[i, j, 0] = br.ReadSByte();
        //            verticlesValues[i, j, 1] = br.ReadSByte();
        //            verticlesValues[i, j, 2] = br.ReadSByte();
        //            verticlesValues[i, j, 3] = br.ReadSByte();
        //            verticlesValues[i, j, 4] = br.ReadSByte();
        //            verticlesValues[i, j, 5] = br.ReadSByte();
        //            verticlesValues[i, j, 6] = br.ReadSByte();
        //            verticlesValues[i, j, 7] = br.ReadSByte();

        //        }

        //    }
        //    br.Close();

        //    //save positions to file
        //    //xml
        //    //XDocument xmlDoc = new XDocument();
        //    //XElement xmlMeshes = new XElement("Meshes");

        //    //for (int i = 0; i < tangentsCount; i++)
        //    //{
        //    //    XElement xmlMesh = new XElement("Mesh");
        //    //    XElement xmlPositions = new XElement("Positions");
        //    //    for (int j = 0; j < positionsCounts[i]; j++)
        //    //    {
        //    //        XElement xmlPosition = new XElement("Position");
        //    //        xmlPosition.Add(new XElement("X", positions[i, j].X));
        //    //        xmlPosition.Add(new XElement("Y", positions[i, j].Y));
        //    //        xmlPosition.Add(new XElement("Z", positions[i, j].Z));
        //    //        xmlPositions.Add(xmlPosition);
        //    //    }
        //    //    xmlMesh.Add(xmlPositions);
        //    //    xmlMeshes.Add(xmlMesh);

        //    //}
        //    //xmlDoc.Add(xmlMeshes);
        //    //xmlDoc.Save("C:\\Users\\Damian\\Desktop\\Nowy folder (3)\\Nowy folder\\verts.xml");
        //    //xml/

        //    ///////////binarywriter
        //    //FileStream writeStream = new FileStream(path+"_verts", FileMode.Create); 
        //    //BinaryWriter bw = new BinaryWriter(writeStream);

        //    ////verts
        //    //bw.Write(meshesCount);//int

        //    //for(int i=0;i<meshesCount;i++)
        //    //{
        //    //    bw.Write(positionsCounts[i]);//int
        //    //}

        //    //for (int i = 0; i < meshesCount; i++)
        //    //{
        //    //    for(int j=0;j<positionsCounts[i];j++)
        //    //    {
        //    //        bw.Write(positions[i, j].X);//float
        //    //        bw.Write(positions[i, j].Y);
        //    //        bw.Write(positions[i, j].Z);
        //    //        bw.Write(texcoords[i, j].X);
        //    //        bw.Write(texcoords[i, j].Y);
        //    //    }
        //    //}
        //    ////-/
        //    ////indices
        //    //bw.Write(meshesCount);//int

        //    //for (int i = 0; i < meshesCount; i++)
        //    //{
        //    //    bw.Write(indicesCounts[i]);//int
        //    //}

        //    //for (int i = 0; i < meshesCount; i++)
        //    //{
        //    //    for (int j = 0; j < indicesCounts[i]; j++)
        //    //    {
        //    //        bw.Write(indices[i, j]);//int 
        //    //    }
        //    //}
        //    ////-/

        //    //bw.Close();
        //    ///////////-/
        //    //////////execute tangent calc
        //    //System.Diagnostics.ProcessStartInfo proc = new System.Diagnostics.ProcessStartInfo();
        //    //proc.FileName = @"C:\Users\Damian\Documents\Visual Studio 2015\Projects\TangentSpaceCalculator\Debug\TangentSpaceCalculator.exe";
        //    //proc.Arguments = "\""+path + "_verts"+"\""; 
        //    //var process = System.Diagnostics.Process.Start(proc); 
        //    //process.WaitForExit();
        //    //////////-/
        //    //////////binaryreader
        //    //br = new BinaryReader(File.Open(
        //    //        path + "_verts_tspace", FileMode.Open, FileAccess.Read));

        //    //int tspaceMeshes = br.ReadInt32();
        //    //int[] tspaceVertsCounts = new int[128];
        //    //for(int i=0;i< tspaceMeshes; i++)
        //    //{
        //    //    tspaceVertsCounts[i] = br.ReadInt32();
        //    //}

        //    //for (int i = 0; i < tspaceMeshes; i++)
        //    //{
        //    //    for(int j=0;j<tspaceVertsCounts[i];j++)
        //    //    {
        //    //        tangents[i, j].X = br.ReadSingle();
        //    //        tangents[i, j].Y = br.ReadSingle();
        //    //        tangents[i, j].Z = br.ReadSingle();
        //    //        biTangents[i, j].X = br.ReadSingle();
        //    //        biTangents[i, j].Y = br.ReadSingle();
        //    //        biTangents[i, j].Z = br.ReadSingle();
        //    //    }
        //    //}
        //    //////////-/

        //    //write fixed verts
        //    //File.Copy(path, path + "_new", true);
        //    //System.IO.FileStream fs = System.IO.File.Create(path + "_new");
        //    //fs.Close();
        //    //Console.WriteLine("dlugosci prostopadle");
        //    uint delta2 = 0;
        //    Console.WriteLine("");
        //    //for (int i = 0; i < tangentsCount; i++)
        //    //{
        //    //    for (int j = 0; j < (int)tangentsChunksOffsets[i, 1]; j++)
        //    //    {
        //    //        tangents[i, j] = new Vector3(0);
        //    //        biTangents[i, j] = new Vector3(0);
        //    //        normals[i, j] = new Vector3(0);
        //    //    }
        //    //} 

        //    for (int i = 0; i < tangentsCount; i++) 
        //    {
        //        //Console.Write(".");
        //        BinaryWriter bw = new BinaryWriter(File.Open(
        //            path + "_new", FileMode.Open, FileAccess.ReadWrite));

        //        uint offset = tangentsChunksOffsets[i, 0];// +(uint)i* tangentsChunksOffsets[i, 1] * 8;
        //        if(i>0)delta2 = delta2 + tangentsChunksOffsets[i-1, 1] * 8;
        //        offset = offset + delta2;
        //        int vertsCount = (int)tangentsChunksOffsets[i, 1];



        //        /////////////////////XNA
        //        //MeshBuilder meshBuilder = MeshBuilder.StartMesh("siatka");
        //        //meshBuilder.CreateVertexChannel<Vector2>("TextureCoordinate0");
        //        //meshBuilder.CreateVertexChannel<Vector3>("Normal0");
        //        //for (int j = 0; j < vertsCount; j++)
        //        //{
        //        //    meshBuilder.CreatePosition(positions[i, j]);
        //        //}
        //        //for (int j = 0; j < indicesCounts[i]; j++)
        //        //{
        //        //    meshBuilder.AddTriangleVertex(indices[i, j]);
        //        //    // meshBuilder.
        //        //}

        //        //calculate normals
        //        //for (int j = 0; j < indicesCounts[i]; j += 3)
        //        //{
        //        //    Triangle triangle = new Triangle();
        //        //    triangle.a = positions[i, indices[i, j]];
        //        //    triangle.b = positions[i, indices[i, j + 1]];
        //        //    triangle.c = positions[i, indices[i, j + 2]];

        //        //    Vector3 normal = Vector3.Normalize(triangle.Normal);  //This is the normal of the triangle if that's all you're interested in.

        //        //    //normal.X = (float)Math.Round(normal.X, 1);
        //        //    // normal.Y = (float)Math.Round(normal.Y, 1);
        //        //    //normal.Z = (float)Math.Round(normal.Z, 1);

        //        //    for (int e = 0; e < 3; e++)
        //        //    {
        //        //        normals[i, indices[i, j + e]] = normal;
        //        //        normals[i, indices[i, j + e]] = Vector3.Normalize(normals[i, indices[i, j + e]]);
        //        //        //if(e>2)
        //        //        //{
        //        //        //    normals[i, indices[i, j + e]] = normal;
        //        //        //    normals[i, indices[i, j + e]] = Vector3.Normalize(normals[i, indices[i, j + e]]);
        //        //        //}
        //        //    }
        //        //}

        //        ////calculate tangents
        //        int fixNullVecCounter = 0;
        //        int divZeroCounter = 0;

        //        for (int j = 0; j < indicesCounts[i]; j += 3)
        //        {
        //            Vector3[] vPos = new Vector3[3];
        //            Vector2[] vUV = new Vector2[3];

        //            for (int e = 0; e < 3; e++)
        //            {
        //                vPos[e] = positions[i, indices[i, j + e]];
        //                vUV[e] = texcoords[i, indices[i, j + e]];
        //            }

        //            Vector3 vA = vPos[1] - vPos[0];
        //            Vector3 vB = vPos[2] - vPos[0];
        //            Vector3 vC = vPos[2] - vPos[1];
        //            Vector3 vU, vV, vN = Vector3.Normalize(Vector3.Cross(vA, vB));
        //            Vector3 vecZero = new Vector3(0);
        //            Vector3 vecSmall = new Vector3(0.0001f);

        //            if (Double.IsNaN(vN.X) && Double.IsNaN(vN.Y) && Double.IsNaN(vN.Z))
        //            {
        //                //friend CVec3 cross(const CVec3& vec1, const CVec3& vec2) { return CVec3(vec1.y * vec2.z - vec1.z * vec2.y, vec1.z * vec2.x - vec1.x * vec2.z, vec1.x * vec2.y - vec1.y * vec2.x); }
        //                Console.WriteLine("Vector3.Normalize(Vector3.Cross(vA, vB)) produced nan | j = {0}", j);
        //                Console.WriteLine("vA: {0} {1} {2} ", vA.X, vA.Y, vA.Z);
        //                Console.WriteLine("vB: {0} {1} {2} ", vB.X, vB.Y, vB.Z);
        //                Console.WriteLine("vPos[0]: {0} {1} {2} ", vPos[0].X, vPos[0].Y, vPos[0].Z);
        //                Console.WriteLine("vPos[1]: {0} {1} {2} ", vPos[1].X, vPos[1].Y, vPos[1].Z);
        //                Console.WriteLine("vPos[2]: {0} {1} {2} ", vPos[2].X, vPos[2].Y, vPos[2].Z);
        //                Vector3 tempCross = Vector3.Cross(vA, vB);
        //                Console.WriteLine("Vector3.Cross(vA, vB): {0} {1} {2} ", tempCross.X, tempCross.Y, tempCross.Z);
        //                Console.WriteLine("");
        //                //vN = new Vector3(0, 0, 0);
        //                vU = new Vector3(0, 0, 0);
        //                vV = new Vector3(0, 0, 0);
        //                vN = new Vector3(0, 0, 0);
        //                for (int e = 0; e < 3; e++)
        //                {
        //                    tangents[i, indices[i, j + e]] = vU;
        //                    biTangents[i, indices[i, j + e]] = vV;
        //                    normals[i, indices[i, j + e]] = vN;
        //                }
        //                continue;
        //            }

        //            if (vA == vecZero || vB == vecZero || vC == vecZero)
        //            { 
        //                Console.WriteLine(i+ " takie same wspólrzędne | j = {0}", j);
        //                Console.WriteLine("Vertices 2 and 1 have the same coordinate: ({0} : {1} : {2})", vPos[1].X, vPos[1].Y, vPos[1].Z);
        //                Console.WriteLine("{0} , {1} ,{2}", indices[i, j], indices[i, j+1], indices[i, j+2]);
        //                Console.WriteLine("indexes {0} , {1} , {2}", j, j+1,j+2);
        //                Console.WriteLine("");
        //                vU = new Vector3(0, 0, 0);
        //                vV = new Vector3(0, 0, 0);
        //                //vN = new Vector3(0, 0, 0);
        //                for (int e = 0; e < 3; e++)
        //                {
        //                    tangents[i, indices[i, j + e]] = vU;
        //                    biTangents[i, indices[i, j + e]] = vV;
        //                    normals[i, indices[i, j + e]] = vN;
        //                }
        //                continue;
        //            }

        //            if(vN == vecZero)
        //            {
        //                Console.WriteLine("vN == vecZero | j = {0}", j);
        //                Console.WriteLine("");
        //            }

        //            float fDeltaU1 = vUV[1].X - vUV[0].X;
        //            float fDeltaU2 = vUV[2].X - vUV[0].X;
        //            float fDeltaV1 = vUV[1].Y - vUV[0].Y;
        //            float fDeltaV2 = vUV[2].Y - vUV[0].Y;

        //            //for(int e=0;e<3;e++)
        //            //{
        //            //    if(vUV[e].X>1|| vUV[e].Y > 1)
        //            //    {
        //            //        Console.WriteLine("vUV[{0}] > 1",e);
        //            //        Console.WriteLine("i{0} j{1}", i, indices[i, j + e]);
        //            //        Console.WriteLine("vUV[{0}]: {1} {2}", e, vUV[e].X, vUV[e].Y);
        //            //        Console.WriteLine("");
        //            //    }
        //            //}

        //            float div = (fDeltaU1 * fDeltaV2 - fDeltaU2 * fDeltaV1);



        //            if (div != 0.0)
        //            {
        //                //  area(u1*v2-u2*v1)/2
        //                float fAreaMul2 = Math.Abs(fDeltaU1 * fDeltaV2 - fDeltaU2 * fDeltaV1);  // weight the tangent vectors by the UV triangles area size (fix problems with base UV assignment)
        //                                                                                        //fAreaMul2 = 1;

        //                float a = fDeltaV2 / div;
        //                float b = -fDeltaV1 / div;
        //                float c = -fDeltaU2 / div;
        //                float d = fDeltaU1 / div;
        //                //float a = fDeltaV2;
        //                //float b = -fDeltaV1;
        //                //float c = -fDeltaU2;
        //                //float d = fDeltaU1;

        //                vU = Vector3.Normalize(vA * a + vB * b) * fAreaMul2;
        //                vV = Vector3.Normalize(vA * c + vB * d) * fAreaMul2;
        //                //vU = (vA * a + vB * b) * Math.Sign(div);
        //                //vV = (vA * c + vB * d) * Math.Sign(div);

        //                if (vU  == vecZero)
        //                {
        //                    Console.WriteLine("vU  == vecZero | j = {0}", j);
        //                }
        //                //if (fAreaMul2 > 1)
        //                //{
        //                //    vU = new Vector3(0, 0, 0);
        //                //    vV = new Vector3(0, 0, 0);
        //                //   // vN = new Vector3(0, 0, 0);
        //                //    for (int e = 0; e < 3; e++)
        //                //    {
        //                //        tangents[i, indices[i, j + e]] = vU;
        //                //        biTangents[i, indices[i, j + e]] = vV;
        //                //        normals[i, indices[i, j + e]] = vN;
        //                //    }
        //                //    continue;
        //                //    //Console.WriteLine("fAreaMul2 {0}", fAreaMul2);
        //                //    //Console.WriteLine("div {0}", div);
        //                //    //Console.WriteLine("fDeltaV1 {0} {1}", fDeltaV1, fDeltaV1 / div);
        //                //    //Console.WriteLine("fDeltaV2 {0} {1}", fDeltaV2, fDeltaV2 / div);
        //                //    //Console.WriteLine("fDeltaU1 {0} {1}", fDeltaU1, fDeltaU1 / div);
        //                //    //Console.WriteLine("fDeltaU2 {0} {1}", fDeltaU2, fDeltaU2 / div);
        //                //    //Console.WriteLine("");
        //                //}

        //            }
        //            else
        //            {
        //                divZeroCounter++;
        //                //float a = fDeltaV2 ;
        //                //float b = -fDeltaV1 ;
        //                //float c = -fDeltaU2 ;
        //                //float d = fDeltaU1 ;
        //                //vU = Vector3.Normalize(vA * a + vB * b) * (-1.0f);
        //                //vV = Vector3.Normalize(vA * c + vB * d) * (-1.0f);

        //                //Console.WriteLine(i + "div = 0");
        //                //vU = new Vector3(0, 0, 0);
        //                //vV = new Vector3(0, 0, 0);
        //                //vV = new Vector3(1, 0, 0);
        //                //vU = Vector3.Normalize(Vector3.Cross(vV, vN));
        //                //if (Double.IsNaN(vU.X) && Double.IsNaN(vU.Y) && Double.IsNaN(vU.Z))
        //                //{
        //                //    Console.WriteLine("in else nan");
        //                //    vV = new Vector3(0, 1, 0);
        //                //    vU = Vector3.Normalize(Vector3.Cross(vV, vN));
        //                //    Console.WriteLine("vN: {0} {1} {2} ", vN.X, vN.Y, vN.Z);
        //                //}

        //                //float a = fDeltaV2;
        //                //float b = -fDeltaV1;
        //                //float c = -fDeltaU2;
        //                //float d = fDeltaU1;
        //                //vU = (vA * a + vB * b) * (-1.0f);
        //                //vV = (vA * c + vB * d) * (-1.0f);

        //                //Console.WriteLine("vU: {0} {1} {2} ", vU.X, vU.Y, vU.Z);
        //                //Console.WriteLine("vV: {0} {1} {2} ", vV.X, vV.Y, vV.Z);
        //                //Console.WriteLine("vN: {0} {1} {2} ", vN.X, vN.Y, vN.Z);
        //                //Console.WriteLine("fDeltaV1: {0}", fDeltaV1);
        //                //Console.WriteLine("fDeltaV2: {0}", fDeltaV2);
        //                //Console.WriteLine("fDeltaU1: {0}", fDeltaU1);
        //                //Console.WriteLine("fDeltaU2: {0}", fDeltaU2);

        //                //for (int e = 0; e < 3; e++)
        //                //{
        //                //    Console.WriteLine("i{0} j{1}", i, indices[i, j + e]);
        //                //    Console.WriteLine("vPos[{0}]: {1} {2} {3}", e, vPos[e].X, vPos[e].Y, vPos[e].Z);
        //                //    Console.WriteLine("vUV[{0}]: {1} {2}", e, vUV[e].X, vUV[e].Y);

        //                //}
        //                //Console.WriteLine("");

        //                //if (vU == vecZero)
        //                //{
        //                //    Console.WriteLine("vU  == vecZero in else");
        //                //}
        //                vU = new Vector3(0, 0, 0);
        //                vV = new Vector3(0, 0, 0);
        //                vN = new Vector3(0, 0, 0);
        //                for (int e = 0; e < 3; e++)
        //                {
        //                    tangents[i, indices[i, j + e]] = vU;
        //                    biTangents[i, indices[i, j + e]] = vV;
        //                    normals[i, indices[i, j + e]] = vN;
        //                }
        //                continue;
        //            }
        //            //if (div == 0.0)
        //            //{
        //            //    Console.WriteLine(i + "div = 0"); 
        //            //    vU = (vA * a + vB * b) * (-1.0f);
        //            //    vV = (vA * c + vB * d) * (-1.0f);
        //            //}


        //            for (int e = 0; e < 3; e++)
        //            {
        //                float fWeight = CalcAngleBetween(vPos[(e + 2) % 3] - vPos[e], vPos[(e + 1) % 3] - vPos[e]);
        //                if (Double.IsNaN(fWeight) )
        //                {
        //                    Console.WriteLine("fWeight produced nan | j = {0}", j);
        //                    Console.WriteLine("");
        //                }
        //                if (fWeight <= 0.0f)
        //                {
        //                    fWeight = 0.0001f;
        //                }
        //                if (Double.IsNaN(tangents[i, indices[i, j + e]].X) && Double.IsNaN(tangents[i, indices[i, j + e]].Y) && Double.IsNaN(tangents[i, indices[i, j + e]].Z))
        //                {
        //                    Console.WriteLine("tangents is nan | j = {0}", j);
        //                    Console.WriteLine("");
        //                }
        //                if (Double.IsNaN(normals[i, indices[i, j + e]].X) && Double.IsNaN(normals[i, indices[i, j + e]].Y) && Double.IsNaN(normals[i, indices[i, j + e]].Z))
        //                {
        //                    Console.WriteLine("normals is nan | j = {0}", j);
        //                    Console.WriteLine("");
        //                }

        //                //normals[i, indices[i, j + e]] += (vN * fWeight);
        //                //tangents[i, indices[i, j + e]] += (vU * fWeight);
        //                //biTangents[i, indices[i, j + e]] += (vV * fWeight);

        //                fWeight = 1;
        //                normals[i, indices[i, j + e]] += (vN * fWeight);
        //                tangents[i, indices[i, j + e]] += (vU * fWeight);
        //                biTangents[i, indices[i, j + e]] += (vV * fWeight);

        //                if (Double.IsNaN(tangents[i, indices[i, j + e]].X) && Double.IsNaN(tangents[i, indices[i, j + e]].Y) && Double.IsNaN(tangents[i, indices[i, j + e]].Z))
        //                {
        //                    Console.WriteLine("+= (vU * fWeight) produced nan | j = {0}",j);
        //                    Console.WriteLine("");
        //                }
        //                if (Double.IsNaN(normals[i, indices[i, j + e]].X) && Double.IsNaN(normals[i, indices[i, j + e]].Y) && Double.IsNaN(normals[i, indices[i, j + e]].Z))
        //                {
        //                    Console.WriteLine("+= (vN * fWeight) produced nan | j = {0}", j);
        //                    Console.WriteLine("vN: {0} {1} {2} ", vN.X, vN.Y, vN.Z);
        //                    Console.WriteLine("fWeight: {0} ", fWeight);
        //                    Console.WriteLine("");
        //                }

        //                Vector3 vUout, vVout, vNout;

        //                vNout = Vector3.Normalize(normals[i, indices[i, j + e]]); 

        //                if (Double.IsNaN(vNout.X) && Double.IsNaN(vNout.Y) && Double.IsNaN(vNout.Z))
        //                {
        //                    Console.WriteLine("Vector3.Normalize(normals[i, indices[i, j + e]]) produced nan | j = {0}", j);
        //                    Console.WriteLine("");
        //                }

        //                vUout = tangents[i, indices[i, j + e]] - vNout * (vNout * tangents[i, indices[i, j + e]]);                      // project u in n plane
        //                vVout = biTangents[i, indices[i, j + e]] - vNout * (vNout * biTangents[i, indices[i, j + e]]);                      // project v in n plane

        //                if (vUout==vecZero)
        //                {
        //                    Console.WriteLine(i+" vUout==vecZero | j = {0}", j); 
        //                    //Console.WriteLine("vUout: {0} {1} {2} ", vUout.X, vUout.Y, vUout.Z);
        //                    Console.WriteLine("tangents[i, indices[i, j + e]]: {0} {1} {2} ", tangents[i, indices[i, j + e]].X, tangents[i, indices[i, j + e]].Y, tangents[i, indices[i, j + e]].Z);
        //                    Console.WriteLine("vVout: {0} {1} {2} ", vVout.X, vVout.Y, vVout.Z);
        //                    Console.WriteLine("vNout: {0} {1} {2} ", vNout.X, vNout.Y, vNout.Z);
        //                    //vNout = Vector3.Normalize(Vector3.Cross(biTangents[i, indices[i, j + e]], Vector3.Normalize(vVout)));
        //                    //vUout = tangents[i, indices[i, j + e]] - vNout * (vNout * tangents[i, indices[i, j + e]]);
        //                    //vVout = biTangents[i, indices[i, j + e]] - vNout * (vNout * biTangents[i, indices[i, j + e]]);
        //                    tangents[i, indices[i, j + e]]= Vector3.Normalize(Vector3.Cross(vNout, Vector3.Normalize(vVout)));
        //                    vUout = tangents[i, indices[i, j + e]] - vNout * (vNout * tangents[i, indices[i, j + e]]);
        //                    Console.WriteLine("new vUout: {0} {1} {2} ", vUout.X, vUout.Y, vUout.Z);
        //                    Console.WriteLine("new vVout: {0} {1} {2} ", vVout.X, vVout.Y, vVout.Z);
        //                    Console.WriteLine("new vNout: {0} {1} {2} ", vNout.X, vNout.Y, vNout.Z); 
        //                    Console.WriteLine("");
        //                }

        //                tangents[i, indices[i, j + e]] = Vector3.Normalize(vUout);
        //                biTangents[i, indices[i, j + e]] = Vector3.Normalize(vVout);
        //                normals[i, indices[i, j + e]] = vNout;
        //                if (Double.IsNaN(tangents[i, indices[i, j + e]].X) && Double.IsNaN(tangents[i, indices[i, j + e]].Y) && Double.IsNaN(tangents[i, indices[i, j + e]].Z))
        //                {
        //                    Console.WriteLine("tangents produced nan | j = {0}", j);
        //                    Console.WriteLine("");
        //                    tangents[i, indices[i, j + e]] = new Vector3(0);

        //                }
        //                if (Double.IsNaN(biTangents[i, indices[i, j + e]].X) && Double.IsNaN(biTangents[i, indices[i, j + e]].Y) && Double.IsNaN(biTangents[i, indices[i, j + e]].Z))
        //                {
        //                    Console.WriteLine("biTangents produced nan");
        //                    Console.WriteLine("");
        //                    biTangents[i, indices[i, j + e]] = new Vector3(0);
        //                }
        //                //if (Double.IsNaN(normals[i, indices[i, j + e]].X) && Double.IsNaN(normals[i, indices[i, j + e]].Y) && Double.IsNaN(normals[i, indices[i, j + e]].Z))
        //                //{
        //                //    Console.WriteLine("normals produced nan");
        //                //}

        //                //if(vUout == vecZero || vVout == vecZero)
        //                //{
        //                //    fixNullVecCounter++;
        //                //    //Console.WriteLine(i+"fixing null vec");
        //                //    tangents[i, indices[i, j + e]] +=    new Vector3(1, 0, 0);
        //                //    biTangents[i, indices[i, j + e]] +=  new Vector3(0, 1, 0);
        //                //    normals[i, indices[i, j + e]] +=     new Vector3(0, 0, 1);
        //                //}
        //                //vUout = Vector3.Normalize(vUout);
        //                //vVout = Vector3.Normalize(vVout); 
        //                //if ((vUout.X < 0.0001f && vUout.Y < 0.0001f && vUout.Z < 0.0001f) || (vVout.X < 0.0001f && vVout.Y < 0.0001f && vVout.Z < 0.0001f))
        //                //{
        //                //    Console.WriteLine("fixing null vec2");
        //                //    tangents[i, indices[i, j + e]] = new Vector3(1, 0, 0);
        //                //    biTangents[i, indices[i, j + e]] = new Vector3(0, 1, 0);
        //                //    normals[i, indices[i, j + e]] = new Vector3(0, 0, 1);
        //                //}

        //                //normals[i, indices[i, j + e]] = Vector3.Normalize(normals[i, indices[i, j + e]]);
        //                //tangents[i, indices[i, j + e]] = Vector3.Normalize(tangents[i, indices[i, j + e]]);
        //                //biTangents[i, indices[i, j + e]] = Vector3.Normalize(biTangents[i, indices[i, j + e]]);
        //            }
        //        }

        //        //fix nan vecs
        //        int fixnancounter = 0;
        //        for (int j = 0; j < vertsCount; j++)
        //        {
        //            Vector3 vecZero = new Vector3(0);

        //            if (Double.IsNaN(tangents[i, j].X) && Double.IsNaN(tangents[i, j].Y) && Double.IsNaN(tangents[i, j].Z))
        //            {
        //                fixnancounter++;
        //                //Console.WriteLine(i + "fixing NaN vec");
        //                tangents[i, j] = new Vector3(1, 0, 0);
        //                biTangents[i, j] = new Vector3(0, 1, 0);
        //                normals[i, j] = new Vector3(0, 0, 1);
        //            }
        //            if (tangents[i, j] == vecZero || biTangents[i, j] == vecZero)
        //            {
        //                fixNullVecCounter++;
        //                //Console.WriteLine(i + "fixing null vec");
        //                tangents[i, j] = new Vector3(1, 0, 0);
        //                biTangents[i, j] = new Vector3(0, 1, 0);
        //                normals[i, j] = new Vector3(0, 0, 1);
        //            }
        //            if (i == 0 && j == 1667)
        //            {
        //                //Console.WriteLine("[T:1667]" + tangents[i, j].X + " " + tangents[i, j].Y + " " + tangents[i, j].Z);
        //            }
        //        }

        //        Console.WriteLine(i+ " fixNaNcounter: " + fixnancounter);
        //        Console.WriteLine(i + " fixNullVecCounter: " + fixNullVecCounter);
        //        Console.WriteLine(i + " devZeroCounter: " + divZeroCounter);
        //        //-/
        //        //for (int j = 0; j < 6; j++)
        //        //{
        //        //    Console.WriteLine("");
        //        //    Console.WriteLine("[" + j + "]");
        //        //    Console.WriteLine("[P] " + positions[i, j].X + " " + positions[i, j].Y + " " + positions[i, j].Z);
        //        //    Console.WriteLine("[N] " + normals[i, j].X + " " + normals[i, j].Y + " " + normals[i, j].Z);
        //        //    Console.WriteLine("[T] " + tangents[i, j].X + " " + tangents[i, j].Y + " " + tangents[i, j].Z);
        //        //    Console.WriteLine("[B] " + biTangents[i, j].X + " " + biTangents[i, j].Y + " " + biTangents[i, j].Z);
        //        //    Console.WriteLine("[UV] " + texcoords[i, j].X + " " + texcoords[i, j].Y);

        //        //}

        //        ////

        //        // MeshContent meshContent = new MeshContent();
        //        // meshContent = meshBuilder.FinishMesh();
        //        //// MeshHelper.CalculateNormals(meshContent,true);
        //        // for (int j = 0; j < vertsCount; j++)
        //        // {
        //        //     meshContent.Geometry[0].Vertices.Channels.Get<Vector3>(VertexChannelNames.Normal(0))[j] = normals[i, j];
        //        //     meshContent.Geometry[0].Vertices.Channels.Get<Vector2>(VertexChannelNames.TextureCoordinate(0))[j] = texcoords[i, j];
        //        // }
        //        // //var normals = meshContent.Geometry[0].Vertices.Channels.Get<Vector3>(VertexChannelNames.Normal(0));
        //        // MeshHelper.CalculateTangentFrames(meshContent, "TextureCoordinate0", "Tangent0", "Binormal0");
        //        // var channels = meshContent.Geometry[0].Vertices.Channels;
        //        // var tangents = channels.Get<Vector3>(VertexChannelNames.Tangent(0));
        //        // var biTangents = channels.Get<Vector3>(VertexChannelNames.Binormal(0));
        //        // //Console.WriteLine("meshconent count {0}", meshContent.Geometry[0].Vertices.VertexCount);
        //        // //Console.WriteLine("meshconent texcoord {0}", channels.Get<Vector2>(VertexChannelNames.TextureCoordinate(0))[0].X);
        //        // //Console.WriteLine("meshconent tangents {0} {1} {2}", channels.Get<Vector3>(VertexChannelNames.Tangent(0))[0].X, channels.Get<Vector3>(VertexChannelNames.Tangent(0))[0].Y, channels.Get<Vector3>(VertexChannelNames.Tangent(0))[0].Z);
        //        // ///////////////////////


        //        bw.BaseStream.Position = offset;
        //        bw.BaseStream.Position = bw.BaseStream.Position + 24;
        //        for (int j = 0; j < vertsCount; j++)
        //        {

        //            //copy sign
        //            //tangents[i, j].X = Utils.copySign(tangents[i, j].X, verticlesValues[i, j, 0]);
        //            //tangents[i, j].Y = Utils.copySign(tangents[i, j].Y, verticlesValues[i, j, 1]);
        //            //tangents[i, j].Z = Utils.copySign(tangents[i, j].Z, verticlesValues[i, j, 2]);
        //            //biTangents[i, j].X = Utils.copySign(biTangents[i, j].X, verticlesValues[i, j, 4]);
        //            //biTangents[i, j].Y = Utils.copySign(biTangents[i, j].Y, verticlesValues[i, j, 5]);
        //            //biTangents[i, j].Z = Utils.copySign(biTangents[i, j].Z, verticlesValues[i, j, 6]);

        //            //CryEngine.Quaternion quat = new CryEngine.Quaternion(12,32,34,54); 
        //            /*
        //            bw.Write(fixSmallVert(verticlesValues[i, j, 0]));
        //            bw.Write(fixSmallVert(verticlesValues[i, j, 1]));
        //            bw.Write(fixSmallVert(verticlesValues[i, j, 2]));
        //            bw.Write(fixSmallVert(verticlesValues[i, j, 3]));
        //            //if (verticlesValues[i, j, 3] >= 0) { bw.Write((short)(32767)); } else { bw.Write((short)(-32767)); }
        //            bw.Write(fixSmallVert(verticlesValues[i, j, 4]));
        //            bw.Write(fixSmallVert(verticlesValues[i, j, 5]));
        //            bw.Write(fixSmallVert(verticlesValues[i, j, 6]));
        //            bw.Write(fixSmallVert(verticlesValues[i, j, 7]));
        //            //if (verticlesValues[i, j, 7] >= 0) { bw.Write((short)(32767)); } else { bw.Write((short)(-32767)); }
        //            */
        //            float mnoznik = 256 * 128 - 1;
        //            //bw.Write((short)(tangents[j].X * mnoznik));
        //            //bw.Write((short)(tangents[j].Y * mnoznik));
        //            //bw.Write((short)(tangents[j].Z * mnoznik));//Console.WriteLine(j + " " + tangents[j].X + " " + tangents[j].Y + " " + tangents[j].Z);
        //            //if (Math.Sign(tangents[i, j].X) == Math.Sign(verticlesValues[i, j, 0])) { bw.Write((short)(tangents[i, j].X * mnoznik)); } else { bw.Write((short)((-1.0f) * tangents[i, j].X * mnoznik)); }
        //            //if (Math.Sign(tangents[i, j].Y) == Math.Sign(verticlesValues[i, j, 1])) { bw.Write((short)(tangents[i, j].Y * mnoznik)); } else { bw.Write((short)((-1.0f) * tangents[i, j].Y * mnoznik)); }
        //            //if (Math.Sign(tangents[i, j].Z) == Math.Sign(verticlesValues[i, j, 2])) { bw.Write((short)(tangents[i, j].Z * mnoznik)); } else { bw.Write((short)((-1.0f) * tangents[i, j].Z * mnoznik)); }
        //            //bw.Write((short)(tangents[i, j].X * mnoznik));
        //            //bw.Write((short)(tangents[i, j].Y * mnoznik));
        //            //bw.Write((short)(tangents[i, j].Z * mnoznik));
        //            bw.Write((short)(tangents[i, j].X * mnoznik));
        //            bw.Write((short)(tangents[i, j].Y * mnoznik));
        //            bw.Write((short)(tangents[i, j].Z * mnoznik));
        //            //bw.Write((short)(tangents[i, j].Z * mnoznik));//Console.WriteLine(j + " " + tangents[j].X + " " + tangents[j].Y + " " + tangents[j].Z);
        //            //bw.Write(Utils.tPackF2B((-1.0f)*Utils.tSmallPackUB2F(verticlesValues[i, j, 3])- 0.001f));
        //            //bw.Write((short)(32767));
        //            //Console.WriteLine(verticlesValues[i, j, 3]);
        //            if ((verticlesValues[i, j, 3]) >= 0) { bw.Write((short)(-32767)); } else { bw.Write((short)(-32767)); }
        //            //if (!GetBit(verticlesValues[i, j, 3], 8)) { bw.Write((short)(32767)); } else { bw.Write((short)(-32767)); }
        //            //bw.Write((short)(biTangents[j].X * mnoznik));
        //            //bw.Write((short)(biTangents[j].Y * mnoznik));
        //            //bw.Write((short)(biTangents[j].Z * mnoznik));
        //            //if (Math.Sign(biTangents[i, j].X) == Math.Sign(verticlesValues[i, j, 4])) { bw.Write((short)(biTangents[i, j].X * mnoznik)); } else { bw.Write((short)((-1.0f) * biTangents[i, j].X * mnoznik)); }
        //            //if (Math.Sign(biTangents[i, j].Y) == Math.Sign(verticlesValues[i, j, 5])) { bw.Write((short)(biTangents[i, j].Y * mnoznik)); } else { bw.Write((short)((-1.0f) * biTangents[i, j].Y * mnoznik)); }
        //            //if (Math.Sign(biTangents[i, j].Z) == Math.Sign(verticlesValues[i, j, 6])) { bw.Write((short)(biTangents[i, j].Z * mnoznik)); } else { bw.Write((short)((-1.0f) * biTangents[i, j].Z * mnoznik)); }
        //            bw.Write((short)(biTangents[i, j].X * mnoznik));
        //            bw.Write((short)(biTangents[i, j].Y * mnoznik));
        //            bw.Write((short)(biTangents[i, j].Z * mnoznik));
        //            //bw.Write(Utils.tPackF2B((-1.0f) * Utils.tSmallPackUB2F(verticlesValues[i, j, 7])- 0.001f));
        //            //bw.Write((short)(32767));
        //            //Console.WriteLine(verticlesValues[i, j, 7]); ;
        //            if ((verticlesValues[i, j, 7]) >= 0) { bw.Write((short)(-32767)); } else { bw.Write((short)(-32767)); }
        //            //if (!GetBit(verticlesValues[i, j, 7], 8)) { bw.Write((short)(32767)); } else { bw.Write((short)(-32767)); }

        //            int dzielna = 256*256;
        //            // float prostopad = byte1hexToFloat(verticlesValues[i, j, 0]) / dzielna * verticlesValues[i, j, 4] / dzielna + (float)verticlesValues[i, j, 1] / dzielna * verticlesValues[i, j, 5] / dzielna + (float)verticlesValues[i, j, 3] / dzielna * verticlesValues[i, j, 7] / dzielna;
        //            /*float prostopad = byte1hexToFloat(verticlesValues[i, j, 0]) * byte1hexToFloat(verticlesValues[i, j, 4]) + byte1hexToFloat(verticlesValues[i, j, 1]) * byte1hexToFloat(verticlesValues[i, j, 5]) + byte1hexToFloat(verticlesValues[i, j, 3]) * byte1hexToFloat(verticlesValues[i, j, 7]);
        //            // Console.WriteLine("[{0}] {1:F10}", j, prostopad);
        //            double pot1x = Math.Pow(byte1hexToFloat(verticlesValues[i, j, 0]), 2);
        //            double pot1y = Math.Pow(byte1hexToFloat(verticlesValues[i, j, 1]), 2);
        //            double pot1z = Math.Pow(byte1hexToFloat(verticlesValues[i, j, 2]), 2);
        //            double pot1w = Math.Pow(byte1hexToFloat(verticlesValues[i, j, 3]), 2);
        //            double pot2x = Math.Pow(byte1hexToFloat(verticlesValues[i, j, 4]), 2);
        //            double pot2y = Math.Pow(byte1hexToFloat(verticlesValues[i, j, 5]), 2);
        //            double pot2z = Math.Pow(byte1hexToFloat(verticlesValues[i, j, 6]), 2);
        //            double pot2w = Math.Pow(byte1hexToFloat(verticlesValues[i, j, 7]), 2);
        //            double lengthVec1 = Math.Sqrt(pot1x + pot1y + pot1z);
        //            double lengthVec2 = Math.Sqrt(pot2x + pot2y + pot2z);*/
        //            //Console.WriteLine("{0:F10}  {1:F10} {2:F10} {3:F10}", byte1hexToFloat(verticlesValues[i, j, 0]), byte1hexToFloat(verticlesValues[i, j, 1]), byte1hexToFloat(verticlesValues[i, j, 2]), byte1hexToFloat(verticlesValues[i, j, 3]));
        //            //Console.WriteLine("{0:F10}  {1:F10} {2:F10} {3:F10}", byte1hexToFloat(verticlesValues[i, j, 4]), byte1hexToFloat(verticlesValues[i, j, 5]), byte1hexToFloat(verticlesValues[i, j, 6]), byte1hexToFloat(verticlesValues[i, j, 7]));
        //            //Console.WriteLine("[{0}]    {1:F10}", j, lengthVec1);
        //            //Console.WriteLine("[{0}]    {1:F10}", j, lengthVec2);
        //            //Console.WriteLine("[{0}]    {1:F10}", j, prostopad);
        //            //Console.WriteLine("tangents[{3}] {0} {1} {2}",tangents[j].X, tangents[j].Y, tangents[j].Z,j);
        //            // Console.WriteLine("tangents[{3}] {0} {1} {2}", tangents[j].X * (256 * 128), tangents[j].Y * (256 * 128), tangents[j].Z * (256 * 128), j);

        //        }
        //        long writePos = bw.BaseStream.Position; 
        //        bw.Close();
        //        appendFooter(path, path + "_new", tangentsChunksOffsets[i, 0] + (vertsCount * 8) + 24, writePos);
        //    }

        //    // bw.BaseStream.Seek(position, SeekOrigin.Begin);
        //    // bw.Write(patch);



        //    //Console.WriteLine("{0}  {1} {2} {3}", byte1hexToFloat(verticlesValues[0, 1152, 0]), byte1hexToFloat(verticlesValues[0, 1152, 1]), byte1hexToFloat(verticlesValues[0, 1152, 2]), byte1hexToFloat(verticlesValues[0, 1152, 3]));
        //    //Console.WriteLine("{0} {1} {2}", byte2hexIntFracToFloat2(verticlesValues[0, 1, 0]), byte2hexIntFracToFloat2(verticlesValues[0, 1, 1]), byte2hexIntFracToFloat2(verticlesValues[0, 1, 2]));

        //    //Console.WriteLine("{0} elements in header, {1} dataStreams, {2} p3s_c4b_t2sChunks", headerElements, dataStreamsCount, tangentsCount);
        //    //Console.WriteLine("{0} vericles", maxVertsCount);
        //}
        //private static void fixQTangents(string path)
        //{
        //    BinaryReader br = new BinaryReader(File.Open(
        //            path, FileMode.Open, FileAccess.Read));
        //    br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint headerElements = br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint[,] dataStreamChunksOffsets = new uint[headerElements, 2];
        //    int dataStreamsCount = 0;
        //    for (int i = 0; i < (int)headerElements; i++)
        //    {
        //        uint chunkType = br.ReadUInt16();
        //        //Console.WriteLine("{0:X}", chunkType);
        //        br.ReadUInt16();
        //        uint id = br.ReadUInt32();
        //        br.ReadUInt32();
        //        uint offset = br.ReadUInt32();
        //        if (chunkType == 0x00001016) { dataStreamChunksOffsets[dataStreamsCount, 0] = offset; dataStreamChunksOffsets[dataStreamsCount, 1] = id; dataStreamsCount++; }
        //    }

        //    //uint[,] tangentsChunksOffsets = new uint[dataStreamsCount, 2];
        //    int tangentsCount = 0;
        //    uint delta = 0;
        //    for (int i = 0; i < dataStreamsCount; i++)
        //    {
        //        br.BaseStream.Position = dataStreamChunksOffsets[i, 0];
        //        br.ReadUInt32();
        //        uint nStreamType = br.ReadUInt32();
        //        if (nStreamType == 0x00000006)
        //        {
        //            qtangentsChunksOffsets[tangentsCount, 0] = dataStreamChunksOffsets[i, 0]; //offset
        //            qtangentsChunksOffsets[tangentsCount, 1] = dataStreamChunksOffsets[i, 1];//chunk id
        //            long temppos = br.BaseStream.Position;
        //            br.BaseStream.Position = temppos;
        //            tangentsCount++;
        //        }
        //    }
        //    br.Close();

        //    BinaryWriter bw = new BinaryWriter(File.Open(
        //            path, FileMode.Open, FileAccess.ReadWrite));

        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        uint offset = qtangentsChunksOffsets[i, 0]+4;
        //        bw.BaseStream.Position = offset;
        //        int qtan = 12;
        //        bw.Write(qtan);
        //        bw.BaseStream.Position += 4;
        //        int elSize = 8;
        //        bw.Write(elSize);
        //    }
        //    bw.Close();
        //    //for (int i = 0; i < tangentsCount; i++)
        //    //{
        //    //    uint offset = tangentsChunksOffsets[i, 0];
        //    //    br.BaseStream.Position = offset;
        //    //    br.ReadUInt32();
        //    //    br.ReadUInt32();
        //    //    br.ReadUInt32();
        //    //    br.ReadUInt32();
        //    //    br.ReadUInt32();
        //    //    br.ReadUInt32();
        //    //    uint vertsCount = tangentsChunksOffsets[i, 1];
        //    //    for (int j = 0; j < (int)vertsCount; j++)
        //    //    {
        //    //        verticlesValues[i, j, 0] = br.ReadByte();
        //    //        verticlesValues[i, j, 1] = br.ReadByte();
        //    //        verticlesValues[i, j, 2] = br.ReadByte();
        //    //        verticlesValues[i, j, 3] = br.ReadByte();
        //    //        verticlesValues[i, j, 4] = br.ReadByte();
        //    //        verticlesValues[i, j, 5] = br.ReadByte();
        //    //        verticlesValues[i, j, 6] = br.ReadByte();
        //    //        verticlesValues[i, j, 7] = br.ReadByte();
        //    //    }

        //    //}

        //}
        //private static void fixQTangents2(string path)
        //{
        //    BinaryReader br = new BinaryReader(File.Open(
        //            path, FileMode.Open, FileAccess.Read));
        //    br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint headerElements = br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint[,] dataStreamChunksOffsets = new uint[headerElements, 2];
        //    int dataStreamsCount = 0;
        //    for (int i = 0; i < (int)headerElements; i++)
        //    {
        //        uint chunkType = br.ReadUInt16();
        //        //Console.WriteLine("{0:X}", chunkType);
        //        br.ReadUInt16();
        //        uint id = br.ReadUInt32();
        //        br.ReadUInt32();
        //        uint offset = br.ReadUInt32();
        //        if (chunkType == 0x00001016) { dataStreamChunksOffsets[dataStreamsCount, 0] = offset; dataStreamChunksOffsets[dataStreamsCount, 1] = id; dataStreamsCount++; }
        //    }

        //    //uint[,] tangentsChunksOffsets = new uint[dataStreamsCount, 2];
        //    int tangentsCount = 0;
        //    uint delta = 0;
        //    for (int i = 0; i < dataStreamsCount; i++)
        //    {
        //        br.BaseStream.Position = dataStreamChunksOffsets[i, 0];
        //        br.ReadUInt32();
        //        uint nStreamType = br.ReadUInt32();
        //        if (nStreamType == 0x00000006)
        //        {
        //            qtangentsChunksOffsets[tangentsCount, 0] = dataStreamChunksOffsets[i, 0]; //offset
        //            qtangentsChunksOffsets[tangentsCount, 1] = dataStreamChunksOffsets[i, 1];//chunk id
        //            qtangentsChunksOffsets[tangentsCount, 2]   = br.ReadUInt32(); ;//count
        //            long temppos = br.BaseStream.Position;
        //            br.BaseStream.Position = temppos;
        //            tangentsCount++;
        //        }
        //    }
        //    int maxVertsCount = 0;
        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        int buf = (int)qtangentsChunksOffsets[i, 2];
        //        if (buf > maxVertsCount) maxVertsCount = buf;
        //    }
        //    sbyte[,,] scalarValues = new sbyte[tangentsCount, maxVertsCount, 2];
        //    Half [,,] verticlesValues = new Half[tangentsCount, maxVertsCount, 8];

        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        uint offset = qtangentsChunksOffsets[i, 0];
        //        br.BaseStream.Position = offset;
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        uint vertsCount = qtangentsChunksOffsets[i, 2];
        //        for (int j = 0; j < (int)vertsCount; j++)
        //        {
        //            verticlesValues[i, j, 0].value = br.ReadUInt16();//br.ReadUInt16BE
        //            verticlesValues[i, j, 1].value = br.ReadUInt16();
        //            verticlesValues[i, j, 2].value = br.ReadUInt16();
        //            verticlesValues[i, j, 3].value = br.ReadUInt16();

        //            //scalarValues[i, j, 0] = br.ReadSByte();//br.ReadUInt16BE
        //            //scalarValues[i, j, 1] = br.ReadSByte();
        //            //verticlesValues[i, j, 0] = br.ReadInt16BE();
        //            //verticlesValues[i, j, 1] = br.ReadInt16BE();
        //            //verticlesValues[i, j, 2] = br.ReadInt16BE();
        //        }

        //    }
        //    br.Close();

        //    BinaryWriter bw = new BinaryWriter(File.Open(
        //            path, FileMode.Open, FileAccess.ReadWrite));

        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        uint offset = qtangentsChunksOffsets[i, 0] + 4;
        //        bw.BaseStream.Position = offset;
        //        int qtan = 12;
        //        bw.Write(qtan);
        //        bw.BaseStream.Position += 4;
        //        int elSize = 8;
        //        bw.Write(elSize); 
        //    }
        //    bw.Close();

        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        //Console.Write(".");
        //          bw = new BinaryWriter(File.Open(
        //            path, FileMode.Open, FileAccess.ReadWrite));

        //        uint offset = qtangentsChunksOffsets[i, 0];// +(uint)i* tangentsChunksOffsets[i, 1] * 8; 
        //        int vertsCount = (int)qtangentsChunksOffsets[i, 2];
        //        Console.WriteLine("verts: "+ vertsCount);

        //        bw.BaseStream.Position = offset;
        //        bw.BaseStream.Position = bw.BaseStream.Position + 24;
        //        for (int j = 0; j < vertsCount; j++)
        //        {
        //            //short zero = 0;
        //            //bw.Write(zero);
        //            //bw.Write(zero);
        //            //bw.Write(zero);
        //            //bw.Write(zero);

        //            Vector4 orgVec = new Vector4(
        //                HalfHelper.HalfToSingle(verticlesValues[i, j, 0]),
        //                HalfHelper.HalfToSingle(verticlesValues[i, j, 1]),
        //                HalfHelper.HalfToSingle(verticlesValues[i, j, 2]),
        //                HalfHelper.HalfToSingle(verticlesValues[i, j, 3]));
        //            Vector4 norm = Vector4.Normalize(orgVec); 
        //            bw.Write(Utils.tPackF2B(norm.X));
        //            bw.Write(Utils.tPackF2B(norm.Y * (-1.0f)));
        //            bw.Write(Utils.tPackF2B(norm.Z));
        //            bw.Write(Utils.tPackF2B(norm.W));



        //            //bw.Write(Utils.tPackF2B((Utils.tPackB2F(verticlesValues[i, j, 0]))));
        //            //bw.Write(Utils.tPackF2B((Utils.tPackB2F(verticlesValues[i, j, 1]))));
        //            //bw.Write(Utils.tPackF2B((Utils.tPackB2F(verticlesValues[i, j, 2]))));
        //            ////bw.Write(Utils.tPackF2B((Utils.tSmallPackB2F(scalarValues[i, j, 1]))));(-1.0f) *

        //            //bw.Write(Utils.tPackF2B((-1.0f)*Utils.tPackB2F(verticlesValues[i, j, 3])));
        //            ////if (!GetBit(verticlesValues[i, j, 3], 8)) { bw.Write((short)(32767)); } else { bw.Write((short)(-32767)); }  


        //        }
        //        bw.Close(); 
        //    }

        //}
        //private static void fixQTangents3(string path)
        //{
        //    BinaryReader br = new BinaryReader(File.Open(
        //            path, FileMode.Open, FileAccess.Read));
        //    br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint headerElements = br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint[,] dataStreamChunksOffsets = new uint[headerElements, 2];
        //    int dataStreamsCount = 0;
        //    for (int i = 0; i < (int)headerElements; i++)
        //    {
        //        uint chunkType = br.ReadUInt16();
        //        //Console.WriteLine("{0:X}", chunkType);
        //        br.ReadUInt16();
        //        uint id = br.ReadUInt32();
        //        br.ReadUInt32();
        //        uint offset = br.ReadUInt32();
        //        if (chunkType == 0x00001016) { dataStreamChunksOffsets[dataStreamsCount, 0] = offset; dataStreamChunksOffsets[dataStreamsCount, 1] = id; dataStreamsCount++; }
        //    }

        //    //uint[,] tangentsChunksOffsets = new uint[dataStreamsCount, 2];
        //    int tangentsCount = 0;
        //    uint delta = 0;
        //    for (int i = 0; i < dataStreamsCount; i++)
        //    {
        //        br.BaseStream.Position = dataStreamChunksOffsets[i, 0];
        //        br.ReadUInt32();
        //        uint nStreamType = br.ReadUInt32();
        //        if (nStreamType == 0x00000006)
        //        {
        //            qtangentsChunksOffsets[tangentsCount, 0] = dataStreamChunksOffsets[i, 0]; //offset
        //            qtangentsChunksOffsets[tangentsCount, 1] = dataStreamChunksOffsets[i, 1];//chunk id
        //            qtangentsChunksOffsets[tangentsCount, 2] = br.ReadUInt32(); ;//count
        //            long temppos = br.BaseStream.Position;
        //            br.BaseStream.Position = temppos;
        //            tangentsCount++;
        //        }
        //    }
        //    int maxVertsCount = 0;
        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        int buf = (int)qtangentsChunksOffsets[i, 2];
        //        if (buf > maxVertsCount) maxVertsCount = buf;
        //    }
        //    sbyte[,,] scalarValues = new sbyte[tangentsCount, maxVertsCount, 2];
        //    sbyte[,,] verticlesValues = new sbyte[tangentsCount, maxVertsCount, 8];

        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        uint offset = qtangentsChunksOffsets[i, 0];
        //        br.BaseStream.Position = offset;
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        uint vertsCount = qtangentsChunksOffsets[i, 2];
        //        for (int j = 0; j < (int)vertsCount; j++)
        //        {
        //            verticlesValues[i, j, 0] = br.ReadSByte(); 
        //            verticlesValues[i, j, 1] = br.ReadSByte();
        //            verticlesValues[i, j, 2] = br.ReadSByte();
        //            verticlesValues[i, j, 3] = br.ReadSByte();
        //            verticlesValues[i, j, 4] = br.ReadSByte();
        //            verticlesValues[i, j, 5] = br.ReadSByte();
        //            verticlesValues[i, j, 6] = br.ReadSByte();
        //            verticlesValues[i, j, 7] = br.ReadSByte();

        //            tangents[i, j]   = new Vector3(verticlesValues[i, j, 0] / 128.0f, verticlesValues[i, j, 1] / 128.0f, verticlesValues[i, j, 2] / 128.0f);
        //            biTangents[i, j] = new Vector3(verticlesValues[i, j, 4] / 128.0f, verticlesValues[i, j, 5] / 128.0f, verticlesValues[i, j, 6] / 128.0f);
        //        }

        //    }
        //    br.Close();

        //    BinaryWriter bw = new BinaryWriter(File.Open(
        //            path, FileMode.Open, FileAccess.ReadWrite));

        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        uint offset = qtangentsChunksOffsets[i, 0] + 4;
        //        bw.BaseStream.Position = offset;
        //        int qtan = 12;
        //        bw.Write(qtan);
        //        bw.BaseStream.Position += 4;
        //        int elSize = 8;
        //        bw.Write(elSize);
        //    }
        //    bw.Close();

        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        //Console.Write(".");
        //        bw = new BinaryWriter(File.Open(
        //          path, FileMode.Open, FileAccess.ReadWrite));

        //        uint offset = qtangentsChunksOffsets[i, 0];// +(uint)i* tangentsChunksOffsets[i, 1] * 8; 
        //        int vertsCount = (int)qtangentsChunksOffsets[i, 2];
        //        Console.WriteLine("verts: " + vertsCount);

        //        bw.BaseStream.Position = offset;
        //        bw.BaseStream.Position = bw.BaseStream.Position + 24;
        //        for (int j = 0; j < vertsCount; j++)
        //        {
        //            CryEngine.Angles3 anglesTan = new CryEngine.Angles3(tangents[i,j].X * 3.14f, tangents[i, j].Y * 3.14f, tangents[i, j].Z * 3.14f);
        //            CryEngine.Quaternion quatTan = new CryEngine.Quaternion(anglesTan);
        //            CryEngine.Vector3 newTan = quatTan.v;
        //            float newTanScalar = quatTan.w;

        //            CryEngine.Angles3 anglesBTan = new CryEngine.Angles3(biTangents[i, j].X * 3.14f, biTangents[i, j].Y * 3.14f, biTangents[i, j].Z * 3.14f);
        //            CryEngine.Quaternion quatBTan = new CryEngine.Quaternion(anglesBTan);
        //            CryEngine.Vector4 newBTan = quatBTan.v;
        //            float newBTanScalar = quatBTan.w;

        //            if (i == 0 && j < 5)
        //            {
        //                Console.WriteLine("{0} {1} {2} {3}", newTan.x, newTan.y, newTan.z, newTanScalar);
        //                Console.WriteLine("{0} {1} {2} {3}", newBTan.x, newBTan.y, newBTan.z, newBTanScalar);
        //                Console.WriteLine("");
        //            }

        //            //bw.Write(Utils.tPackF2B(norm.X));
        //            //bw.Write(Utils.tPackF2B(norm.Y));
        //            //bw.Write(Utils.tPackF2B(norm.Z));
        //            //bw.Write(Utils.tPackF2B(norm.W));

        //        }
        //        bw.Close();
        //    }

        //}

        //private static void fixTangents2(string path)
        //{
        //    BinaryReader br = new BinaryReader(File.Open(
        //            path, FileMode.Open, FileAccess.Read));
        //    br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint headerElements = br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint[,] dataStreamChunksOffsets = new uint[headerElements, 2];
        //    int dataStreamsCount = 0;
        //    for (int i = 0; i < (int)headerElements; i++)
        //    {
        //        uint chunkType = br.ReadUInt16();
        //        //Console.WriteLine("{0:X}", chunkType);
        //        br.ReadUInt16();
        //        uint id = br.ReadUInt32();
        //        br.ReadUInt32();
        //        uint offset = br.ReadUInt32();
        //        if (chunkType == 0x00001016) { dataStreamChunksOffsets[dataStreamsCount, 0] = offset; dataStreamChunksOffsets[dataStreamsCount, 1] = id; dataStreamsCount++; }
        //    }

        //    uint[,] tangentsChunksOffsets = new uint[dataStreamsCount, 2];
        //    int tangentsCount = 0;
        //    uint delta = 0;
        //    for (int i = 0; i < dataStreamsCount; i++)
        //    {
        //        br.BaseStream.Position = dataStreamChunksOffsets[i, 0];
        //        br.ReadUInt32();
        //        uint nStreamType = br.ReadUInt32();
        //        if (nStreamType == 0x00000006)
        //        {
        //            tangentsChunksOffsets[tangentsCount, 0] = dataStreamChunksOffsets[i, 0]; //offset
        //            tangentsChunksOffsets[tangentsCount, 1] = br.ReadUInt32();//verts count
        //            long temppos = br.BaseStream.Position;
        //            delta = delta + tangentsChunksOffsets[tangentsCount, 1] * 8;
        //            editHeaderOffsets(path + "_new", (int)dataStreamChunksOffsets[i, 1], delta, br);
        //            br.BaseStream.Position = temppos;
        //            tangentsCount++;
        //        }
        //    }
        //    int maxVertsCount = 0;
        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        int buf = (int)tangentsChunksOffsets[i, 1];
        //        if (buf > maxVertsCount) maxVertsCount = buf;
        //    }
        //    sbyte[,,] verticlesValues = new sbyte[tangentsCount, maxVertsCount, 8];

        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        uint offset = tangentsChunksOffsets[i, 0];
        //        br.BaseStream.Position = offset;
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        uint vertsCount = tangentsChunksOffsets[i, 1];
        //        for (int j = 0; j < (int)vertsCount; j++)
        //        {
        //            verticlesValues[i, j, 0] = br.ReadSByte();
        //            verticlesValues[i, j, 1] = br.ReadSByte();
        //            verticlesValues[i, j, 2] = br.ReadSByte();
        //            verticlesValues[i, j, 3] = br.ReadSByte();
        //            verticlesValues[i, j, 4] = br.ReadSByte();
        //            verticlesValues[i, j, 5] = br.ReadSByte();
        //            verticlesValues[i, j, 6] = br.ReadSByte();
        //            verticlesValues[i, j, 7] = br.ReadSByte();

        //            tangents[i, j] =   new Vector3(
        //                Utils.tSmallPackB2F(verticlesValues[i, j, 0]),
        //                Utils.tSmallPackB2F(verticlesValues[i, j, 1]), 
        //                Utils.tSmallPackB2F(verticlesValues[i, j, 2]));
        //            biTangents[i, j] = new Vector3(
        //                Utils.tSmallPackB2F(verticlesValues[i, j, 4]), 
        //                Utils.tSmallPackB2F(verticlesValues[i, j, 5]), 
        //                Utils.tSmallPackB2F(verticlesValues[i, j, 6]));
        //        }

        //    }
        //    br.Close();

        //    uint delta2 = 0;
        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        Console.Write(".");
        //        BinaryWriter bw = new BinaryWriter(File.Open(
        //            path + "_new", FileMode.Open, FileAccess.ReadWrite));

        //        uint offset = tangentsChunksOffsets[i, 0];// +(uint)i* tangentsChunksOffsets[i, 1] * 8;
        //        if (i > 0) delta2 = delta2 + tangentsChunksOffsets[i - 1, 1] * 8;
        //        offset = offset + delta2;
        //        int vertsCount = (int)tangentsChunksOffsets[i, 1];





        //        bw.BaseStream.Position = offset;
        //        bw.BaseStream.Position = bw.BaseStream.Position + 24;
        //        for (int j = 0; j < vertsCount; j++)
        //        {

        //            tangents[i, j].Normalize();
        //            biTangents[i, j].Normalize();

        //            bw.Write(Utils.tPackF2B(tangents[i, j].X));
        //            bw.Write(Utils.tPackF2B(tangents[i, j].Y));
        //            bw.Write(Utils.tPackF2B(tangents[i, j].Z));
        //            //bw.Write(Utils.tPackF2B(Utils.tSmallPackB2F(verticlesValues[i, j, 3])));
        //            if (verticlesValues[i, j, 3]>0) { bw.Write((short)(-32767)); } else { bw.Write((short)(-32767)); } 
        //            bw.Write(Utils.tPackF2B(biTangents[i, j].X));
        //            bw.Write(Utils.tPackF2B(biTangents[i, j].Y));
        //            bw.Write(Utils.tPackF2B(biTangents[i, j].Z));
        //            //bw.Write(Utils.tPackF2B(Utils.tSmallPackB2F(verticlesValues[i, j, 7])));
        //            if (verticlesValues[i, j, 7] > 0) { bw.Write((short)(-32767)); } else { bw.Write((short)(-32767)); }
        //            //if (!GetBit(verticlesValues[i, j, 7], 8)) { bw.Write((short)(32767)); } else { bw.Write((short)(-32767)); }

        //            if (i == 0 && j > vertsCount-20)
        //            {
        //                CryEngine.Vector3 newTan = new CryEngine.Vector3(tangents[i, j].X, tangents[i, j].Y, tangents[i, j].Z);
        //                CryEngine.Vector3 newBTan = new CryEngine.Vector3(biTangents[i, j].X, biTangents[i, j].Y, biTangents[i, j].Z);

        //                Console.WriteLine("[{0}] Deg: {1} Rad: {2}",j, CryEngine.Vector3.AngleInDeg(newTan, newBTan), CryEngine.Vector3.AngleInRad(newTan, newBTan));
        //                Console.WriteLine("");
        //            }


        //        }
        //        //Console.WriteLine(Utils.tSmallPackUB2F(verticlesValues[0, 0, 0]) + " "+ Utils.tSmallPackUB2F(verticlesValues[0, 0, 1]) + " " + Utils.tSmallPackUB2F(verticlesValues[0, 0, 2]));
        //        //Console.WriteLine(Utils.tSmallPackB2F(verticlesValues[0, 0, 0]) + " " + Utils.tSmallPackB2F(verticlesValues[0, 0, 1]) + " " + Utils.tSmallPackB2F(verticlesValues[0, 0, 2]));
        //        long writePos = bw.BaseStream.Position;
        //        bw.Close();
        //        appendFooter(path, path + "_new", tangentsChunksOffsets[i, 0] + (vertsCount * 8) + 24, writePos);
        //    }
        //}
        //private static void fixTangents3(string path)
        //{
        //    BinaryReader br = new BinaryReader(File.Open(
        //            path, FileMode.Open, FileAccess.Read));
        //    br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint headerElements = br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint[,] dataStreamChunksOffsets = new uint[headerElements, 2];
        //    int dataStreamsCount = 0;
        //    for (int i = 0; i < (int)headerElements; i++)
        //    {
        //        uint chunkType = br.ReadUInt16();
        //        //Console.WriteLine("{0:X}", chunkType);
        //        br.ReadUInt16();
        //        uint id = br.ReadUInt32();
        //        br.ReadUInt32();
        //        uint offset = br.ReadUInt32();
        //        if (chunkType == 0x00001016) { dataStreamChunksOffsets[dataStreamsCount, 0] = offset; dataStreamChunksOffsets[dataStreamsCount, 1] = id; dataStreamsCount++; }
        //    }

        //    uint[,] tangentsChunksOffsets = new uint[dataStreamsCount, 2];
        //    int tangentsCount = 0;
        //    uint delta = 0;
        //    for (int i = 0; i < dataStreamsCount; i++)
        //    {
        //        br.BaseStream.Position = dataStreamChunksOffsets[i, 0];
        //        br.ReadUInt32();
        //        uint nStreamType = br.ReadUInt32();
        //        if (nStreamType == 0x00000006)
        //        {
        //            tangentsChunksOffsets[tangentsCount, 0] = dataStreamChunksOffsets[i, 0]; //offset
        //            tangentsChunksOffsets[tangentsCount, 1] = br.ReadUInt32();//verts count
        //            long temppos = br.BaseStream.Position;
        //            delta = delta + tangentsChunksOffsets[tangentsCount, 1] * 8;
        //            editHeaderOffsets(path + "_new", (int)dataStreamChunksOffsets[i, 1], delta, br);
        //            br.BaseStream.Position = temppos;
        //            tangentsCount++;
        //        }
        //    }
        //    int maxVertsCount = 0;
        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        int buf = (int)tangentsChunksOffsets[i, 1];
        //        if (buf > maxVertsCount) maxVertsCount = buf;
        //    }
        //    sbyte[,,] verticlesValues = new sbyte[tangentsCount, maxVertsCount, 8];

        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        uint offset = tangentsChunksOffsets[i, 0];
        //        br.BaseStream.Position = offset;
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        uint vertsCount = tangentsChunksOffsets[i, 1];
        //        for (int j = 0; j < (int)vertsCount; j++)
        //        {
        //            //verticlesValues[i, j, 0] = br.ReadSByte();
        //            //verticlesValues[i, j, 1] = br.ReadSByte();
        //            //verticlesValues[i, j, 2] = br.ReadSByte();
        //            //verticlesValues[i, j, 3] = br.ReadSByte();
        //            //verticlesValues[i, j, 4] = br.ReadSByte();
        //            //verticlesValues[i, j, 5] = br.ReadSByte();
        //            //verticlesValues[i, j, 6] = br.ReadSByte();
        //            //verticlesValues[i, j, 7] = br.ReadSByte();
        //            Int16 quatX = br.ReadInt16();
        //            Int16 quatY = br.ReadInt16();
        //            Int16 quatZ = br.ReadInt16();
        //            Int16 quatW = br.ReadInt16();
        //            float floatX = Utils.tPackB2F(quatX);
        //            float floatY = Utils.tPackB2F(quatY);
        //            float floatZ = Utils.tPackB2F(quatZ);
        //            float floatW = Utils.tPackB2F(quatW);
        //            quaternions[i, j] = new CryEngine.Quaternion(floatX, floatY, floatZ, floatW);

        //        }

        //    }
        //    br.Close();


        //    uint delta2 = 0;
        //    Console.WriteLine("");
        //    //for (int i = 0; i < tangentsCount; i++)
        //    //{
        //    //    for (int j = 0; j < (int)tangentsChunksOffsets[i, 1]; j++)
        //    //    {
        //    //        tangents[i, j] = new Vector3(0);
        //    //        biTangents[i, j] = new Vector3(0);
        //    //        normals[i, j] = new Vector3(0);
        //    //    }
        //    //} 
        //    //Console.WriteLine("{0} {1} {2} {3}", quaternions[0, 0].x, quaternions[0, 0].y, quaternions[0, 0].z, quaternions[0, 0].w);
        //    //Console.WriteLine("{0} {1} {2}", quaternions[0, 0].Forward.x, quaternions[0, 0].Forward.y, quaternions[0, 0].Forward.z);
        //    //Console.WriteLine("{0} {1} {2}", quaternions[0, 0].Right.x, quaternions[0, 0].Right.y, quaternions[0, 0].Right.z);
        //    //Console.WriteLine("{0} {1} {2}", quaternions[0, 0].Up.x, quaternions[0, 0].Up.y, quaternions[0, 0].Up.z);

        //    for (int i = 0; i < tangentsCount; i++)
        //    {

        //        //Console.Write(".");
        //        BinaryWriter bw = new BinaryWriter(File.Open(
        //            path + "_new", FileMode.Open, FileAccess.ReadWrite));

        //        uint offset = tangentsChunksOffsets[i, 0];// +(uint)i* tangentsChunksOffsets[i, 1] * 8;
        //        if (i > 0) delta2 = delta2 + tangentsChunksOffsets[i - 1, 1] * 8;
        //        offset = offset + delta2;
        //        int vertsCount = (int)tangentsChunksOffsets[i, 1];





        //        bw.BaseStream.Position = offset;
        //        bw.BaseStream.Position = bw.BaseStream.Position + 24;
        //        for (int j = 0; j < vertsCount; j++)
        //        {


        //            if (i == 0 && j<10)
        //            {
        //                Console.WriteLine("{0} {1} {2} {3}", quaternions[i, j].x, quaternions[i, j].y, quaternions[i, j].z, quaternions[i, j].w);
        //                Console.WriteLine("{0} {1} {2}", quaternions[i, j].Forward.x, quaternions[i, j].Forward.y, quaternions[i, j].Forward.z);
        //                Console.WriteLine("{0} {1} {2}", quaternions[i, j].Right.x, quaternions[i, j].Right.y, quaternions[i, j].Right.z);
        //                Console.WriteLine("{0} {1} {2}", quaternions[i, j].Up.x, quaternions[i, j].Up.y, quaternions[i, j].Up.z);
        //                Console.WriteLine("");
        //            }

        //            //copy sign
        //            //tangents[i, j].X = Utils.copySign(tangents[i, j].X, verticlesValues[i, j, 0]);
        //            //tangents[i, j].Y = Utils.copySign(tangents[i, j].Y, verticlesValues[i, j, 1]);
        //            //tangents[i, j].Z = Utils.copySign(tangents[i, j].Z, verticlesValues[i, j, 2]);
        //            //biTangents[i, j].X = Utils.copySign(biTangents[i, j].X, verticlesValues[i, j, 4]);
        //            //biTangents[i, j].Y = Utils.copySign(biTangents[i, j].Y, verticlesValues[i, j, 5]);
        //            //biTangents[i, j].Z = Utils.copySign(biTangents[i, j].Z, verticlesValues[i, j, 6]);

        //            //CryEngine.Quaternion quat = new CryEngine.Quaternion(12,32,34,54); 
        //            /*
        //            bw.Write(fixSmallVert(verticlesValues[i, j, 0]));
        //            bw.Write(fixSmallVert(verticlesValues[i, j, 1]));
        //            bw.Write(fixSmallVert(verticlesValues[i, j, 2]));
        //            bw.Write(fixSmallVert(verticlesValues[i, j, 3]));
        //            //if (verticlesValues[i, j, 3] >= 0) { bw.Write((short)(32767)); } else { bw.Write((short)(-32767)); }
        //            bw.Write(fixSmallVert(verticlesValues[i, j, 4]));
        //            bw.Write(fixSmallVert(verticlesValues[i, j, 5]));
        //            bw.Write(fixSmallVert(verticlesValues[i, j, 6]));
        //            bw.Write(fixSmallVert(verticlesValues[i, j, 7]));
        //            //if (verticlesValues[i, j, 7] >= 0) { bw.Write((short)(32767)); } else { bw.Write((short)(-32767)); }
        //            */
        //            float mnoznik = 256 * 128;
        //            //bw.Write((short)(tangents[j].X * mnoznik));
        //            //bw.Write((short)(tangents[j].Y * mnoznik));
        //            //bw.Write((short)(tangents[j].Z * mnoznik));//Console.WriteLine(j + " " + tangents[j].X + " " + tangents[j].Y + " " + tangents[j].Z);
        //            //if (Math.Sign(tangents[i, j].X) == Math.Sign(verticlesValues[i, j, 0])) { bw.Write((short)(tangents[i, j].X * mnoznik)); } else { bw.Write((short)((-1.0f) * tangents[i, j].X * mnoznik)); }
        //            //if (Math.Sign(tangents[i, j].Y) == Math.Sign(verticlesValues[i, j, 1])) { bw.Write((short)(tangents[i, j].Y * mnoznik)); } else { bw.Write((short)((-1.0f) * tangents[i, j].Y * mnoznik)); }
        //            //if (Math.Sign(tangents[i, j].Z) == Math.Sign(verticlesValues[i, j, 2])) { bw.Write((short)(tangents[i, j].Z * mnoznik)); } else { bw.Write((short)((-1.0f) * tangents[i, j].Z * mnoznik)); }
        //            //bw.Write((short)(tangents[i, j].X * mnoznik));
        //            //bw.Write((short)(tangents[i, j].Y * mnoznik));
        //            //bw.Write((short)(tangents[i, j].Z * mnoznik));
        //            bw.Write((short)(quaternions[i, j].Right.X * mnoznik));
        //            bw.Write((short)(quaternions[i, j].Right.Y * mnoznik));
        //            bw.Write((short)(quaternions[i, j].Right.Z * mnoznik));
        //            //bw.Write((short)(tangents[i, j].Z * mnoznik));//Console.WriteLine(j + " " + tangents[j].X + " " + tangents[j].Y + " " + tangents[j].Z);
        //            //bw.Write(Utils.tPackF2B((-1.0f)*Utils.tSmallPackUB2F(verticlesValues[i, j, 3])- 0.001f));
        //            //bw.Write((short)(32767));
        //            //Console.WriteLine(verticlesValues[i, j, 3]);
        //            if ((verticlesValues[i, j, 3]) >= 0) { bw.Write((short)(-32767)); } else { bw.Write((short)(-32767)); }
        //            //if (!GetBit(verticlesValues[i, j, 3], 8)) { bw.Write((short)(32767)); } else { bw.Write((short)(-32767)); }
        //            //bw.Write((short)(biTangents[j].X * mnoznik));
        //            //bw.Write((short)(biTangents[j].Y * mnoznik));
        //            //bw.Write((short)(biTangents[j].Z * mnoznik));
        //            //if (Math.Sign(biTangents[i, j].X) == Math.Sign(verticlesValues[i, j, 4])) { bw.Write((short)(biTangents[i, j].X * mnoznik)); } else { bw.Write((short)((-1.0f) * biTangents[i, j].X * mnoznik)); }
        //            //if (Math.Sign(biTangents[i, j].Y) == Math.Sign(verticlesValues[i, j, 5])) { bw.Write((short)(biTangents[i, j].Y * mnoznik)); } else { bw.Write((short)((-1.0f) * biTangents[i, j].Y * mnoznik)); }
        //            //if (Math.Sign(biTangents[i, j].Z) == Math.Sign(verticlesValues[i, j, 6])) { bw.Write((short)(biTangents[i, j].Z * mnoznik)); } else { bw.Write((short)((-1.0f) * biTangents[i, j].Z * mnoznik)); }
        //            //bw.Write((short)(biTangents[i, j].X * mnoznik));
        //            //bw.Write((short)(biTangents[i, j].Y * mnoznik));
        //            //bw.Write((short)(biTangents[i, j].Z * mnoznik));
        //            bw.Write((short)(quaternions[i, j].Up.X * mnoznik));
        //            bw.Write((short)(quaternions[i, j].Up.Y * mnoznik));
        //            bw.Write((short)(quaternions[i, j].Up.Z * mnoznik));
        //            //bw.Write(Utils.tPackF2B((-1.0f) * Utils.tSmallPackUB2F(verticlesValues[i, j, 7])- 0.001f));
        //            //bw.Write((short)(32767));
        //            //Console.WriteLine(verticlesValues[i, j, 7]); ;
        //            if ((verticlesValues[i, j, 7]) >= 0) { bw.Write((short)(-32767)); } else { bw.Write((short)(-32767)); }
        //            //if (!GetBit(verticlesValues[i, j, 7], 8)) { bw.Write((short)(32767)); } else { bw.Write((short)(-32767)); }

        //            int dzielna = 256 * 256;
        //            // float prostopad = byte1hexToFloat(verticlesValues[i, j, 0]) / dzielna * verticlesValues[i, j, 4] / dzielna + (float)verticlesValues[i, j, 1] / dzielna * verticlesValues[i, j, 5] / dzielna + (float)verticlesValues[i, j, 3] / dzielna * verticlesValues[i, j, 7] / dzielna;
        //            /*float prostopad = byte1hexToFloat(verticlesValues[i, j, 0]) * byte1hexToFloat(verticlesValues[i, j, 4]) + byte1hexToFloat(verticlesValues[i, j, 1]) * byte1hexToFloat(verticlesValues[i, j, 5]) + byte1hexToFloat(verticlesValues[i, j, 3]) * byte1hexToFloat(verticlesValues[i, j, 7]);
        //            // Console.WriteLine("[{0}] {1:F10}", j, prostopad);
        //            double pot1x = Math.Pow(byte1hexToFloat(verticlesValues[i, j, 0]), 2);
        //            double pot1y = Math.Pow(byte1hexToFloat(verticlesValues[i, j, 1]), 2);
        //            double pot1z = Math.Pow(byte1hexToFloat(verticlesValues[i, j, 2]), 2);
        //            double pot1w = Math.Pow(byte1hexToFloat(verticlesValues[i, j, 3]), 2);
        //            double pot2x = Math.Pow(byte1hexToFloat(verticlesValues[i, j, 4]), 2);
        //            double pot2y = Math.Pow(byte1hexToFloat(verticlesValues[i, j, 5]), 2);
        //            double pot2z = Math.Pow(byte1hexToFloat(verticlesValues[i, j, 6]), 2);
        //            double pot2w = Math.Pow(byte1hexToFloat(verticlesValues[i, j, 7]), 2);
        //            double lengthVec1 = Math.Sqrt(pot1x + pot1y + pot1z);
        //            double lengthVec2 = Math.Sqrt(pot2x + pot2y + pot2z);*/
        //            //Console.WriteLine("{0:F10}  {1:F10} {2:F10} {3:F10}", byte1hexToFloat(verticlesValues[i, j, 0]), byte1hexToFloat(verticlesValues[i, j, 1]), byte1hexToFloat(verticlesValues[i, j, 2]), byte1hexToFloat(verticlesValues[i, j, 3]));
        //            //Console.WriteLine("{0:F10}  {1:F10} {2:F10} {3:F10}", byte1hexToFloat(verticlesValues[i, j, 4]), byte1hexToFloat(verticlesValues[i, j, 5]), byte1hexToFloat(verticlesValues[i, j, 6]), byte1hexToFloat(verticlesValues[i, j, 7]));
        //            //Console.WriteLine("[{0}]    {1:F10}", j, lengthVec1);
        //            //Console.WriteLine("[{0}]    {1:F10}", j, lengthVec2);
        //            //Console.WriteLine("[{0}]    {1:F10}", j, prostopad);
        //            //Console.WriteLine("tangents[{3}] {0} {1} {2}",tangents[j].X, tangents[j].Y, tangents[j].Z,j);
        //            // Console.WriteLine("tangents[{3}] {0} {1} {2}", tangents[j].X * (256 * 128), tangents[j].Y * (256 * 128), tangents[j].Z * (256 * 128), j);

        //        }
        //        long writePos = bw.BaseStream.Position;
        //        bw.Close();
        //        appendFooter(path, path + "_new", tangentsChunksOffsets[i, 0] + (vertsCount * 8) + 24, writePos);
        //    }

        //    // bw.BaseStream.Seek(position, SeekOrigin.Begin);
        //    // bw.Write(patch);



        //    //Console.WriteLine("{0}  {1} {2} {3}", byte1hexToFloat(verticlesValues[0, 1152, 0]), byte1hexToFloat(verticlesValues[0, 1152, 1]), byte1hexToFloat(verticlesValues[0, 1152, 2]), byte1hexToFloat(verticlesValues[0, 1152, 3]));
        //    //Console.WriteLine("{0} {1} {2}", byte2hexIntFracToFloat2(verticlesValues[0, 1, 0]), byte2hexIntFracToFloat2(verticlesValues[0, 1, 1]), byte2hexIntFracToFloat2(verticlesValues[0, 1, 2]));

        //    //Console.WriteLine("{0} elements in header, {1} dataStreams, {2} p3s_c4b_t2sChunks", headerElements, dataStreamsCount, tangentsCount);
        //    //Console.WriteLine("{0} vericles", maxVertsCount);
        //}
        //private static void fixTangents4(string path)
        //{
        //    BinaryReader br = new BinaryReader(File.Open(
        //            path, FileMode.Open, FileAccess.Read));
        //    br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint headerElements = br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint[,] dataStreamChunksOffsets = new uint[headerElements, 2];
        //    int dataStreamsCount = 0;
        //    for (int i = 0; i < (int)headerElements; i++)
        //    {
        //        uint chunkType = br.ReadUInt16();
        //        //Console.WriteLine("{0:X}", chunkType);
        //        br.ReadUInt16();
        //        uint id = br.ReadUInt32();
        //        br.ReadUInt32();
        //        uint offset = br.ReadUInt32();
        //        if (chunkType == 0x00001016) { dataStreamChunksOffsets[dataStreamsCount, 0] = offset; dataStreamChunksOffsets[dataStreamsCount, 1] = id; dataStreamsCount++; }
        //    }

        //    uint[,] tangentsChunksOffsets = new uint[dataStreamsCount, 2];
        //    int tangentsCount = 0;
        //    uint delta = 0;
        //    for (int i = 0; i < dataStreamsCount; i++)
        //    {
        //        br.BaseStream.Position = dataStreamChunksOffsets[i, 0];
        //        br.ReadUInt32();
        //        uint nStreamType = br.ReadUInt32();
        //        if (nStreamType == 0x00000006)
        //        {
        //            tangentsChunksOffsets[tangentsCount, 0] = dataStreamChunksOffsets[i, 0]; //offset
        //            tangentsChunksOffsets[tangentsCount, 1] = br.ReadUInt32();//verts count
        //            long temppos = br.BaseStream.Position;
        //            delta = delta + tangentsChunksOffsets[tangentsCount, 1] * 8;
        //            editHeaderOffsets(path + "_new", (int)dataStreamChunksOffsets[i, 1], delta, br);
        //            br.BaseStream.Position = temppos;
        //            tangentsCount++;
        //        }
        //    }
        //    int maxVertsCount = 0;
        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        int buf = (int)tangentsChunksOffsets[i, 1];
        //        if (buf > maxVertsCount) maxVertsCount = buf;
        //    }
        //    sbyte[,,] verticlesValues = new sbyte[tangentsCount, maxVertsCount, 8];

        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        uint offset = tangentsChunksOffsets[i, 0];
        //        br.BaseStream.Position = offset;
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        uint vertsCount = tangentsChunksOffsets[i, 1];
        //        for (int j = 0; j < (int)vertsCount; j++)
        //        {
        //            verticlesValues[i, j, 0] = br.ReadSByte();
        //            verticlesValues[i, j, 1] = br.ReadSByte();
        //            verticlesValues[i, j, 2] = br.ReadSByte();
        //            verticlesValues[i, j, 3] = br.ReadSByte();
        //            verticlesValues[i, j, 4] = br.ReadSByte();
        //            verticlesValues[i, j, 5] = br.ReadSByte();
        //            verticlesValues[i, j, 6] = br.ReadSByte();
        //            verticlesValues[i, j, 7] = br.ReadSByte();

        //            tangents[i, j] = new Vector3(
        //                verticlesValues[i, j, 0] / 128.0f, 
        //                verticlesValues[i, j, 1] / 128.0f, 
        //                verticlesValues[i, j, 2] / 128.0f);
        //            biTangents[i, j] = new Vector3(
        //                verticlesValues[i, j, 4] / 128.0f, 
        //                verticlesValues[i, j, 5] / 128.0f, 
        //                verticlesValues[i, j, 6] / 128.0f);
        //        }

        //    }
        //    br.Close();

        //    uint delta2 = 0;
        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        //Console.Write(".");
        //        Console.WriteLine("");
        //        BinaryWriter bw = new BinaryWriter(File.Open(
        //            path + "_new", FileMode.Open, FileAccess.ReadWrite));

        //        uint offset = tangentsChunksOffsets[i, 0];// +(uint)i* tangentsChunksOffsets[i, 1] * 8;
        //        if (i > 0) delta2 = delta2 + tangentsChunksOffsets[i - 1, 1] * 8;
        //        offset = offset + delta2;
        //        int vertsCount = (int)tangentsChunksOffsets[i, 1];





        //        bw.BaseStream.Position = offset;
        //        bw.BaseStream.Position = bw.BaseStream.Position + 24;
        //        for (int j = 0; j < vertsCount; j++)
        //        {
        //            tangents[i, j] = tangents[i, j] * (float)Math.PI * 2.0f;
        //            biTangents[i, j] = biTangents[i, j] * (float)Math.PI * 2.0f;

        //            CryEngine.Angles3 anglesTan = new CryEngine.Angles3(tangents[i, j].X, tangents[i, j].Y, tangents[i, j].Z);
        //            CryEngine.Quaternion quatTan = new CryEngine.Quaternion(anglesTan);
        //            float newTanScalar = quatTan.w;

        //            CryEngine.Angles3 anglesBTan = new CryEngine.Angles3(biTangents[i, j].X, biTangents[i, j].Y, biTangents[i, j].Z);
        //            CryEngine.Quaternion quatBTan = new CryEngine.Quaternion(anglesBTan);
        //            float newBTanScalar = quatBTan.w;

        //            CryEngine.Vector3 rootVec = new CryEngine.Vector3(1, 0, 0);
        //            CryEngine.Vector3 newTan = rootVec.rotate_vector_by_quaternion(quatTan);
        //            rootVec = new CryEngine.Vector3(1, 0, 0);
        //            CryEngine.Vector3 newBTan = rootVec.rotate_vector_by_quaternion(quatBTan);

        //            bw.Write(Utils.tPackF2B(newTan.x));
        //            bw.Write(Utils.tPackF2B(newTan.y));
        //            bw.Write(Utils.tPackF2B(newTan.z));
        //            if ((verticlesValues[i, j, 4]) >= 0) { bw.Write((short)(32767)); } else { bw.Write((short)(-32767)); }

        //            bw.Write(Utils.tPackF2B(newBTan.x));
        //            bw.Write(Utils.tPackF2B(newBTan.y));
        //            bw.Write(Utils.tPackF2B(newBTan.z));
        //            if ((verticlesValues[i, j, 7]) >= 0) { bw.Write((short)(32767)); } else { bw.Write((short)(-32767)); }

        //            if (i == 0 && j > vertsCount-20)
        //            {
        //                Console.WriteLine("[{0}]", j);
        //                Console.WriteLine("{0} {1} {2}", tangents[i, j].X, tangents[i, j].Y, tangents[i, j].Z);
        //                Console.WriteLine("{0} {1} {2}", biTangents[i, j].X, biTangents[i, j].Y, biTangents[i, j].Z);
        //                Console.WriteLine("{0} {1} {2} {3}", quatTan.v.x, quatTan.v.y, quatTan.v.z, quatTan.w);
        //                Console.WriteLine("{0} {1} {2} {3}", quatBTan.v.x, quatBTan.v.y, quatBTan.v.z, quatBTan.w);
        //                Console.WriteLine("{0} {1} {2}", newTan.x, newTan.y, newTan.z);
        //                Console.WriteLine("{0} {1} {2}", newBTan.x, newBTan.y, newBTan.z);
        //                Console.WriteLine("Deg: {0} Rad: {1}",CryEngine.Vector3.AngleInDeg(newTan,newBTan), CryEngine.Vector3.AngleInRad(newTan, newBTan));
        //                Console.WriteLine(""); 
        //            } 
        //        }
        //        //Console.WriteLine(Utils.tSmallPackUB2F(verticlesValues[0, 0, 0]) + " "+ Utils.tSmallPackUB2F(verticlesValues[0, 0, 1]) + " " + Utils.tSmallPackUB2F(verticlesValues[0, 0, 2]));
        //        //Console.WriteLine(Utils.tSmallPackB2F(verticlesValues[0, 0, 0]) + " " + Utils.tSmallPackB2F(verticlesValues[0, 0, 1]) + " " + Utils.tSmallPackB2F(verticlesValues[0, 0, 2]));
        //        long writePos = bw.BaseStream.Position;
        //        bw.Close();
        //        appendFooter(path, path + "_new", tangentsChunksOffsets[i, 0] + (vertsCount * 8) + 24, writePos);
        //    }
        //}
        //private static void fixTangents5(string path)
        //{
        //    BinaryReader br = new BinaryReader(File.Open(
        //            path, FileMode.Open, FileAccess.Read));
        //    br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint headerElements = br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint[,] dataStreamChunksOffsets = new uint[headerElements, 2];
        //    int dataStreamsCount = 0;
        //    for (int i = 0; i < (int)headerElements; i++)
        //    {
        //        uint chunkType = br.ReadUInt16();
        //        //Console.WriteLine("{0:X}", chunkType);
        //        br.ReadUInt16();
        //        uint id = br.ReadUInt32();
        //        br.ReadUInt32();
        //        uint offset = br.ReadUInt32();
        //        if (chunkType == 0x00001016) { dataStreamChunksOffsets[dataStreamsCount, 0] = offset; dataStreamChunksOffsets[dataStreamsCount, 1] = id; dataStreamsCount++; }
        //    }

        //    uint[,] tangentsChunksOffsets = new uint[dataStreamsCount, 2];
        //    int tangentsCount = 0;
        //    uint delta = 0;
        //    for (int i = 0; i < dataStreamsCount; i++)
        //    {
        //        br.BaseStream.Position = dataStreamChunksOffsets[i, 0];
        //        br.ReadUInt32();
        //        uint nStreamType = br.ReadUInt32();
        //        if (nStreamType == 0x00000006)
        //        {
        //            tangentsChunksOffsets[tangentsCount, 0] = dataStreamChunksOffsets[i, 0]; //offset
        //            tangentsChunksOffsets[tangentsCount, 1] = br.ReadUInt32();//verts count
        //            long temppos = br.BaseStream.Position;
        //            delta = delta + tangentsChunksOffsets[tangentsCount, 1] * 8;
        //            editHeaderOffsets(path + "_new", (int)dataStreamChunksOffsets[i, 1], delta, br);
        //            br.BaseStream.Position = temppos;
        //            tangentsCount++;
        //        }
        //    }
        //    int maxVertsCount = 0;
        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        int buf = (int)tangentsChunksOffsets[i, 1];
        //        if (buf > maxVertsCount) maxVertsCount = buf;
        //    }
        //    short[,,] verticlesValues = new short[tangentsCount, maxVertsCount, 8];

        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        uint offset = tangentsChunksOffsets[i, 0];
        //        br.BaseStream.Position = offset;
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        uint vertsCount = tangentsChunksOffsets[i, 1];
        //        for (int j = 0; j < (int)vertsCount; j++)
        //        {
        //            verticlesValues[i, j, 0] = br.ReadInt16();
        //            verticlesValues[i, j, 1] = br.ReadInt16();
        //            verticlesValues[i, j, 2] = br.ReadInt16();
        //            verticlesValues[i, j, 3] = br.ReadInt16(); 


        //            quaternions[i, j] = new CryEngine.Quaternion(
        //                verticlesValues[i, j, 0] / 32768.0f,
        //                verticlesValues[i, j, 1] / 32768.0f,
        //                verticlesValues[i, j, 2] / 32768.0f,
        //                verticlesValues[i, j, 3] / 32768.0f); 
        //        }

        //    }
        //    br.Close();

        //    uint delta2 = 0;
        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        //Console.Write(".");
        //        Console.WriteLine("");
        //        BinaryWriter bw = new BinaryWriter(File.Open(
        //            path + "_new", FileMode.Open, FileAccess.ReadWrite));

        //        uint offset = tangentsChunksOffsets[i, 0];// +(uint)i* tangentsChunksOffsets[i, 1] * 8;
        //        if (i > 0) delta2 = delta2 + tangentsChunksOffsets[i - 1, 1] * 8;
        //        offset = offset + delta2;
        //        int vertsCount = (int)tangentsChunksOffsets[i, 1];





        //        bw.BaseStream.Position = offset;
        //        bw.BaseStream.Position = bw.BaseStream.Position + 24;
        //        for (int j = 0; j < vertsCount; j++)
        //        {
        //            CryEngine.Angles3 anglesTan = new CryEngine.Angles3(quaternions[i, j].X * (float)Math.PI, quaternions[i, j].Y * (float)Math.PI, quaternions[i, j].Z * (float)Math.PI);
        //            CryEngine.Quaternion quat = new CryEngine.Quaternion(anglesTan); 

        //            CryEngine.Vector3 rootVec = new CryEngine.Vector3(1, 0, 0);
        //            CryEngine.Vector3 newTan = quat.QuaternionToTangent();
        //            CryEngine.Vector3 newBTan = quat.QuaternionToBitangent();

        //            bw.Write(Utils.tPackF2B(newTan.x));
        //            bw.Write(Utils.tPackF2B(newTan.y));
        //            bw.Write(Utils.tPackF2B(newTan.z));
        //            if ((verticlesValues[i, j, 0]) >= 0) { bw.Write((short)(-32767)); } else { bw.Write((short)(-32767)); }

        //            bw.Write(Utils.tPackF2B(newBTan.x));
        //            bw.Write(Utils.tPackF2B(newBTan.y));
        //            bw.Write(Utils.tPackF2B(newBTan.z));
        //            if ((verticlesValues[i, j, 4]) >= 0) { bw.Write((short)(-32767)); } else { bw.Write((short)(-32767)); }

        //            if (i == 0 && j < 20)
        //            {
        //                Console.WriteLine("[{0}]", j);
        //                Console.WriteLine("{0} {1} {2} {3}", quaternions[i, j].x * (float)Math.PI, quaternions[i, j].y * (float)Math.PI, quaternions[i, j].z * (float)Math.PI, quaternions[i, j].w * (float)Math.PI);
        //                Console.WriteLine("{0} {1} {2} {3}", quat.x, quat.y, quat.z, quat.w);
        //                Console.WriteLine("{0} {1} {2}", newTan.x, newTan.y, newTan.z);
        //                Console.WriteLine("{0} {1} {2}", newBTan.x, newBTan.y, newBTan.z);
        //                Console.WriteLine("Deg: {0} Rad: {1}", CryEngine.Vector3.AngleInDeg(newTan, newBTan), CryEngine.Vector3.AngleInRad(newTan, newBTan));
        //                Console.WriteLine("");
        //            }
        //        }
        //        //Console.WriteLine(Utils.tSmallPackUB2F(verticlesValues[0, 0, 0]) + " "+ Utils.tSmallPackUB2F(verticlesValues[0, 0, 1]) + " " + Utils.tSmallPackUB2F(verticlesValues[0, 0, 2]));
        //        //Console.WriteLine(Utils.tSmallPackB2F(verticlesValues[0, 0, 0]) + " " + Utils.tSmallPackB2F(verticlesValues[0, 0, 1]) + " " + Utils.tSmallPackB2F(verticlesValues[0, 0, 2]));
        //        long writePos = bw.BaseStream.Position;
        //        bw.Close();
        //        appendFooter(path, path + "_new", tangentsChunksOffsets[i, 0] + (vertsCount * 8) + 24, writePos);
        //    }
        //}
        //private static void fixTangents6(string path)
        //{
        //    BinaryReader br = new BinaryReader(File.Open(
        //            path, FileMode.Open, FileAccess.Read));
        //    br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint headerElements = br.ReadUInt32();
        //    br.ReadUInt32();
        //    uint[,] dataStreamChunksOffsets = new uint[headerElements, 2];
        //    int dataStreamsCount = 0;
        //    for (int i = 0; i < (int)headerElements; i++)
        //    {
        //        uint chunkType = br.ReadUInt16();
        //        //Console.WriteLine("{0:X}", chunkType);
        //        br.ReadUInt16();
        //        uint id = br.ReadUInt32();
        //        br.ReadUInt32();
        //        uint offset = br.ReadUInt32();
        //        if (chunkType == 0x00001016) { dataStreamChunksOffsets[dataStreamsCount, 0] = offset; dataStreamChunksOffsets[dataStreamsCount, 1] = id; dataStreamsCount++; }
        //    }

        //    uint[,] tangentsChunksOffsets = new uint[dataStreamsCount, 2];
        //    int tangentsCount = 0;
        //    uint delta = 0;
        //    for (int i = 0; i < dataStreamsCount; i++)
        //    {
        //        br.BaseStream.Position = dataStreamChunksOffsets[i, 0];
        //        br.ReadUInt32();
        //        uint nStreamType = br.ReadUInt32();
        //        if (nStreamType == 0x00000006)
        //        {
        //            tangentsChunksOffsets[tangentsCount, 0] = dataStreamChunksOffsets[i, 0]; //offset
        //            tangentsChunksOffsets[tangentsCount, 1] = br.ReadUInt32();//verts count
        //            long temppos = br.BaseStream.Position;
        //            delta = delta + tangentsChunksOffsets[tangentsCount, 1] * 8;
        //            editHeaderOffsets(path + "_new", (int)dataStreamChunksOffsets[i, 1], delta, br);
        //            br.BaseStream.Position = temppos;
        //            tangentsCount++;
        //        }
        //    }
        //    int maxVertsCount = 0;
        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        int buf = (int)tangentsChunksOffsets[i, 1];
        //        if (buf > maxVertsCount) maxVertsCount = buf;
        //    }
        //    sbyte[,,] verticlesValues = new sbyte[tangentsCount, maxVertsCount, 8];

        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        uint offset = tangentsChunksOffsets[i, 0];
        //        br.BaseStream.Position = offset;
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        br.ReadUInt32();
        //        uint vertsCount = tangentsChunksOffsets[i, 1];
        //        for (int j = 0; j < (int)vertsCount; j++)
        //        {
        //            verticlesValues[i, j, 0] = br.ReadSByte();
        //            verticlesValues[i, j, 1] = br.ReadSByte();
        //            verticlesValues[i, j, 2] = br.ReadSByte();
        //            verticlesValues[i, j, 3] = br.ReadSByte();
        //            verticlesValues[i, j, 4] = br.ReadSByte();
        //            verticlesValues[i, j, 5] = br.ReadSByte();
        //            verticlesValues[i, j, 6] = br.ReadSByte();
        //            verticlesValues[i, j, 7] = br.ReadSByte();

        //        }

        //    }
        //    br.Close();

        //    uint delta2 = 0;
        //    for (int i = 0; i < tangentsCount; i++)
        //    {
        //        Console.Write(".");
        //        BinaryWriter bw = new BinaryWriter(File.Open(
        //            path + "_new", FileMode.Open, FileAccess.ReadWrite));

        //        uint offset = tangentsChunksOffsets[i, 0];// +(uint)i* tangentsChunksOffsets[i, 1] * 8;
        //        if (i > 0) delta2 = delta2 + tangentsChunksOffsets[i - 1, 1] * 8;
        //        offset = offset + delta2;
        //        int vertsCount = (int)tangentsChunksOffsets[i, 1];





        //        bw.BaseStream.Position = offset;
        //        bw.BaseStream.Position = bw.BaseStream.Position + 24;
        //        for (int j = 0; j < vertsCount; j++)
        //        {
        //            CryEngine.Vector3 rootVec = new CryEngine.Vector3(1,0,0);

        //            CryEngine.Quaternion quatTan = new CryEngine.Quaternion(
        //                Utils.tSmallPackB2F(verticlesValues[i, j, 0]),
        //                Utils.tSmallPackB2F(verticlesValues[i, j, 1]),
        //                Utils.tSmallPackB2F(verticlesValues[i, j, 2]),
        //                Utils.tSmallPackB2F(verticlesValues[i, j, 3])
        //                );
        //            //CryEngine.Vector3 newTan = quatTan.QuaternionToTangent();
        //            //CryEngine.Vector3 newTan = quatTan.Forward.Normalized; 
        //            CryEngine.Vector3 newTan = rootVec.rotate_vector_by_quaternion(quatTan);


        //            CryEngine.Quaternion quatBTan = new CryEngine.Quaternion(
        //                Utils.tSmallPackB2F(verticlesValues[i, j, 4]),
        //                Utils.tSmallPackB2F(verticlesValues[i, j, 5]),
        //                Utils.tSmallPackB2F(verticlesValues[i, j, 6]),
        //                Utils.tSmallPackB2F(verticlesValues[i, j, 7])
        //                );
        //            //CryEngine.Vector3 newBTan = quatBTan.QuaternionToBitangent();
        //            //CryEngine.Vector3 newBTan = quatBTan.Right.Normalized;
        //            CryEngine.Vector3 newBTan = rootVec.rotate_vector_by_quaternion(quatBTan);

        //            bw.Write(Utils.tPackF2B(newTan.X));
        //            bw.Write(Utils.tPackF2B(newTan.Y));
        //            bw.Write(Utils.tPackF2B(newTan.Z));
        //            //bw.Write(Utils.tPackF2B(Utils.tSmallPackB2F(verticlesValues[i, j, 3])));
        //            if (verticlesValues[i, j, 3] > 0) { bw.Write((short)(-32767)); } else { bw.Write((short)(-32767)); }
        //            bw.Write(Utils.tPackF2B(newBTan.X));
        //            bw.Write(Utils.tPackF2B(newBTan.Y));
        //            bw.Write(Utils.tPackF2B(newBTan.Z));
        //            //bw.Write(Utils.tPackF2B(Utils.tSmallPackB2F(verticlesValues[i, j, 7])));
        //            if (verticlesValues[i, j, 7] > 0) { bw.Write((short)(-32767)); } else { bw.Write((short)(-32767)); }
        //            //if (!GetBit(verticlesValues[i, j, 7], 8)) { bw.Write((short)(32767)); } else { bw.Write((short)(-32767)); }

        //            if (i == 0 && j > vertsCount - 20)
        //            {
        //                Console.WriteLine("{0} {1} {2} {3}", quatTan.x, quatTan.y, quatTan.z, quatTan.w);
        //                Console.WriteLine("{0} {1} {2} {3}", quatBTan.x, quatBTan.y, quatBTan.z, quatBTan.w);
        //                Console.WriteLine("--");
        //                Console.WriteLine("{0} {1} {2}", quatTan.Up.x, quatTan.Up.y, quatTan.Up.z);
        //                Console.WriteLine("{0} {1} {2}", quatTan.Forward.x, quatTan.Forward.y, quatTan.Forward.z);
        //                Console.WriteLine("{0} {1} {2}", quatTan.Right.x, quatTan.Right.y, quatTan.Right.z);
        //                Console.WriteLine("--");
        //                Console.WriteLine("{0} {1} {2}", quatBTan.Up.x, quatBTan.Up.y, quatBTan.Up.z);
        //                Console.WriteLine("{0} {1} {2}", quatBTan.Forward.x, quatBTan.Forward.y, quatBTan.Forward.z);
        //                Console.WriteLine("{0} {1} {2}", quatBTan.Right.x, quatBTan.Right.y, quatBTan.Right.z);
        //                Console.WriteLine("--");
        //                Console.WriteLine("{0} {1} {2}", newTan.x, newTan.y, newTan.z);
        //                Console.WriteLine("{0} {1} {2}", newBTan.x, newBTan.y, newBTan.z);
        //                Console.WriteLine("--");
        //                Console.WriteLine("[{0}] Deg: {1} Rad: {2}", j, CryEngine.Vector3.AngleInDeg(newTan, newBTan), CryEngine.Vector3.AngleInRad(newTan, newBTan));
        //                Console.WriteLine("");
        //                Console.WriteLine("");
        //            }


        //        }
        //        //Console.WriteLine(Utils.tSmallPackUB2F(verticlesValues[0, 0, 0]) + " "+ Utils.tSmallPackUB2F(verticlesValues[0, 0, 1]) + " " + Utils.tSmallPackUB2F(verticlesValues[0, 0, 2]));
        //        //Console.WriteLine(Utils.tSmallPackB2F(verticlesValues[0, 0, 0]) + " " + Utils.tSmallPackB2F(verticlesValues[0, 0, 1]) + " " + Utils.tSmallPackB2F(verticlesValues[0, 0, 2]));
        //        long writePos = bw.BaseStream.Position;
        //        bw.Close();
        //        appendFooter(path, path + "_new", tangentsChunksOffsets[i, 0] + (vertsCount * 8) + 24, writePos);
        //    }
        //}

        private static void fixTangents7(string path)
        {
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
                    editHeaderOffsets(path + "_new", (int)dataStreamChunksOffsets[i, 1], delta, br);
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
                BinaryWriter bw = new BinaryWriter(File.Open(
                    path + "_new", FileMode.Open, FileAccess.ReadWrite));

                uint offset = tangentsChunksOffsets[i, 0];// +(uint)i* tangentsChunksOffsets[i, 1] * 8;
                if (i > 0) delta2 = delta2 + tangentsChunksOffsets[i - 1, 1] * 8;
                offset = offset + delta2;
                int vertsCount = (int)tangentsChunksOffsets[i, 1];





                bw.BaseStream.Position = offset;
                bw.BaseStream.Position = bw.BaseStream.Position + 24;
                for (int j = 0; j < vertsCount; j++)
                {
                    CryEngine.Vector3 pos = new CryEngine.Vector3(positions[i, j].X, positions[i, j].Y, positions[i, j].Z);
                    CryEngine.Vector2 texcoor = new CryEngine.Vector2(texcoords[i, j].X, texcoords[i, j].Y);
                    TSpace tspac = new TSpace();
                    //if (i == 0 && (j == 0 || j == 7 || j == 10 || j == 9774)) { Console.WriteLine("[{0}]",j);  tspac = Utils.VSassembly(pos, texcoor, tangentHex[i, j], bitangentHex[i, j],true); }
                    //else {  tspac = Utils.VSassembly(pos, texcoor, tangentHex[i, j], bitangentHex[i, j]);  }
                    tspac = Utils.VSassembly(pos, tangentHex[i, j], bitangentHex[i, j]);

                    bw.Write(Utils.tPackF2B(tspac.tangent.x));
                    bw.Write(Utils.tPackF2B(tspac.tangent.y));
                    bw.Write(Utils.tPackF2B(tspac.tangent.z));
                    bw.Write(Utils.tPackF2B(tspac.tangent.w));
                    bw.Write(Utils.tPackF2B(tspac.bitangent.x));
                    bw.Write(Utils.tPackF2B(tspac.bitangent.y));
                    bw.Write(Utils.tPackF2B(tspac.bitangent.z));
                    bw.Write(Utils.tPackF2B(tspac.tangent.w));
                    //bw.Write(Utils.tPackF2B(Utils.tSmallPackB2F(verticlesValues[i, j, 3])));
                    //if (verticlesValues[i, j, 3] > 0) { bw.Write((short)(-32767)); } else { bw.Write((short)(-32767)); }
                    //bw.Write(Utils.tPackF2B(newBTan.X));
                    //bw.Write(Utils.tPackF2B(newBTan.Y));
                    //bw.Write(Utils.tPackF2B(newBTan.Z));
                    ////bw.Write(Utils.tPackF2B(Utils.tSmallPackB2F(verticlesValues[i, j, 7])));
                    //if (verticlesValues[i, j, 7] > 0) { bw.Write((short)(-32767)); } else { bw.Write((short)(-32767)); }
                    ////if (!GetBit(verticlesValues[i, j, 7], 8)) { bw.Write((short)(32767)); } else { bw.Write((short)(-32767)); }

                    //if (i == 0 && j <20)
                    //{
                    //    Console.WriteLine("[{0}]",j);
                    //    Console.WriteLine("{0} {1} {2}", positions[i,j].X, positions[i, j].Y, positions[i, j].Z );
                    //    Console.WriteLine("{0} {1}", texcoords[i,j].X, texcoords[i, j].X);
                    //    Console.WriteLine("{0} {1}", tangentHex[i, j], bitangentHex[i, j]);
                    //    Console.WriteLine("{0} {1} {2} {3}", tspac.tangent.x, tspac.tangent.Y, tspac.tangent.Z, tspac.tangent.w);
                    //    Console.WriteLine("{0} {1} {2}", tspac.bitangent.x, tspac.bitangent.Y, tspac.bitangent.Z);
                    //    //Console.WriteLine("--");
                    //    //Console.WriteLine("[{0}] Deg: {1} Rad: {2}", j, CryEngine.Vector3.AngleInDeg(newTan, newBTan), CryEngine.Vector3.AngleInRad(newTan, newBTan));
                    //    //Console.WriteLine("");
                    //    Console.WriteLine("");
                    //}


                }
                //Console.WriteLine(Utils.tSmallPackUB2F(verticlesValues[0, 0, 0]) + " "+ Utils.tSmallPackUB2F(verticlesValues[0, 0, 1]) + " " + Utils.tSmallPackUB2F(verticlesValues[0, 0, 2]));
                //Console.WriteLine(Utils.tSmallPackB2F(verticlesValues[0, 0, 0]) + " " + Utils.tSmallPackB2F(verticlesValues[0, 0, 1]) + " " + Utils.tSmallPackB2F(verticlesValues[0, 0, 2]));
                long writePos = bw.BaseStream.Position;
                bw.Close();
                appendFooter(path, path + "_new", tangentsChunksOffsets[i, 0] + (vertsCount * 8) + 24, writePos);
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
            Console.Write("Loading indices");
            loadIndices(path); Console.Write("DONE\n");

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
                copy(path, path + "_new");

                Console.Write("Fixing Tangent Space");
                fixTangents7(path); Console.Write("DONE\n");
                //fixTangents2(path); Console.Write("DONE\n");

                File.Delete(path);
                copy(path + "_new", path);
                File.Delete(path + "_new");
            }

        }
        static void fixSkin(string path)
        {
            Console.Write("Loading indices");
            loadIndices(path); Console.Write("DONE\n");

            Console.Write("Loading Bboxes");
            loadBboxes(path); Console.Write("DONE\n");

            //modifyTransforms(path);
            Console.Write("Fixing verts");
            fixSkinVerts(path); Console.Write("DONE\n");
            if (useQTan)
            {
                Console.Write("Fixing Tangent Space");
                //fixQTangents(path); Console.Write("DONE\n");
                //fixQTangents3(path); Console.Write("DONE\n");
            }
            fixMesh(path);

            if (!useQTan)
            {
                copy(path, path + "_new");

                Console.Write("Fixing Tangent Space");
                fixTangents7(path); Console.Write("DONE\n");
                //fixTangents2(path); Console.Write("DONE\n");

                File.Delete(path);
                copy(path + "_new", path);
                File.Delete(path + "_new");
            }

        }
        public static void fixElements(string path)
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
