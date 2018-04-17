using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using CryEngine;

namespace cgfFixer
{
    static class FixerHelper
    { 
    
        public static void overwriteFile(Stream st, string path)
        {
            using (var newFileStram = File.Create(path))
            {
                st.Seek(0, SeekOrigin.Begin);
                st.CopyTo(newFileStram);
            }
        }

        public static UInt16 SingleToHalf(Int32 si32)
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

        public static bool Replace(string path, uint seek, uint patch)
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

        public static int ReverseToInt(ushort uInt16)
        {
            var bytes = BitConverter.GetBytes(uInt16);
            Array.Reverse(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }
        public static int toInt(ushort uInt16)
        {
            var bytes = BitConverter.GetBytes(uInt16);
            //Array.Reverse(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }
        public static int byteToInt(byte uInt8)
        {
            var bytes = BitConverter.GetBytes(uInt8);
            //Array.Reverse(bytes);
            return BitConverter.ToInt16(bytes, 0);
        }

        public static UInt16 fixVert(ushort uInt16)
        {
            //float normFloat = toInt(uInt16) / 256f / 128;
            //var bytes = BitConverter.GetBytes(normFloat);
            //return SingleToHalf(BitConverter.ToInt32(bytes,0));
            float normFloat = byte2hexIntFracToFloat3(uInt16);
            Half halfFloat;
            halfFloat = HalfHelper.SingleToHalf(normFloat);
            return halfFloat.value;
        }

        public static ushort fixSmallVert(byte Int8)
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
        public static int byte1hexToIntType2(string hexString)
        {
            int value = Convert.ToSByte(hexString, 16);
            return value;
        }

        public static float byte2hexIntFracToFloat2(ushort uInt16)
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
        public static float byte2hexIntFracToFloat3(ushort uInt16)
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
        public static float byte1hexIntFracToFloat(byte uInt8)
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
        public static float byte1hexToFloat(byte uInt8)
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
        public static float byte1hexTo8bitIEE(byte uInt8)
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
        public static ushort byte1hexToUshort(byte uInt8)
        {
            string binary = Convert.ToString(uInt8, 2).PadLeft(8, '0');
            binary = binary.PadRight(16, '0');
            return Convert.ToUInt16(binary, 2);
        }
        public static void copy(string oldPath, string newPath)
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
        public static string Reverse(string s)
        {
            char[] charArray = s.ToCharArray();
            Array.Reverse(charArray);
            return new string(charArray);
        }
        
        public static bool IsFileReady(String sFilename)
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
            catch (Exception e)
            { 
                return false;
            }
        }
        public static String[] GetFiles(String path)
        {
            String[] filenames = Directory.GetFiles(path, "*", SearchOption.AllDirectories);
            return filenames;
        }

        public static bool GetBit(this byte b, int bitNumber)
        {
            var bit = (b & (1 << bitNumber - 1)) != 0;
            return bit;
        }

        public static float CalcAngleBetween(Vector3 invA, Vector3 invB)
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

        public static float length(Vector3 invA)
        {
            return (float)Math.Sqrt(invA.X * invA.X + invA.Y * invA.Y + invA.Z * invA.Z);
        }
        public static float mnozenie(Vector3 vec1, Vector3 vec2)
        {
            return (vec1.X * vec2.X + vec1.Y * vec2.Y + vec1.Z * vec2.Z);
        }

    }
}
