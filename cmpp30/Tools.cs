using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace cmpp30
{
    public class MyMD5
    {
        //md5
        static System.Security.Cryptography.MD5CryptoServiceProvider md5 = new System.Security.Cryptography.MD5CryptoServiceProvider();

        public static string GetMD5_16(string value)
        {
            return GetMD5Str_32(value).Substring(8, 16);
        }
        public static string GetMD5Str_32(string value)
        {
            return BitConverter.ToString(GetMD5Byts_32(value)).Replace("-", "");
        }
        public static byte[] GetMD5Byts_32(string value)
        {
            var tempByts = md5.ComputeHash(ASCIIEncoding.UTF8.GetBytes(value));

            return tempByts;
        }
    }

    /// <summary>
    /// 反转 int uint long ulong 等数值型的字节序
    /// java网络字节序高位在前
    /// c#            低位在前
    /// </summary>
    public class ReverseByteOrder
    {
        public static UInt32 ReverseBytes(UInt32 value)
        {
            return (value & 0x000000FFU) << 24 | (value & 0x0000FF00U) << 8 |
                   (value & 0x00FF0000U) >> 8 | (value & 0xFF000000U) >> 24;
        }

        public static UInt16 ReverseBytes(UInt16 value)
        {
            return (UInt16)((value & 0xFFU) << 8 | (value & 0xFF00U) >> 8);
        }

        public static UInt64 ReverseBytes(UInt64 value)
        {
            return (value & 0x00000000000000FFUL) << 56 | (value & 0x000000000000FF00UL) << 40 |
                   (value & 0x0000000000FF0000UL) << 24 | (value & 0x00000000FF000000UL) << 8 |
                   (value & 0x000000FF00000000UL) >> 8 | (value & 0x0000FF0000000000UL) >> 24 |
                   (value & 0x00FF000000000000UL) >> 40 | (value & 0xFF00000000000000UL) >> 56;
        }
    }

    public class Tools
    {
        public static byte[] StringToBytes(string strValue)
        {
            return ascii.GetBytes(strValue);
        }

        public static string BytesToString(byte[] byts, int index, int count)
        {
            string temp = ascii.GetString(byts, index, count);

            int endCharIndex = temp.IndexOf('\0');

            return endCharIndex >= 0 ? temp.Remove(endCharIndex) : temp;
        }

        public static void CopyAll(int startIndex, byte[] source, ref byte[] target, int copyCount = 0)
        {
            if (copyCount == 0)
            {
                copyCount = source.Length;
            }
            else
            {
                copyCount = Math.Min(copyCount, source.Length);
            }
            Buffer.BlockCopy(source, 0, target, startIndex, copyCount);
        }

        static System.Text.Encoding gb18030 = System.Text.Encoding.GetEncoding("GB18030");
        static System.Text.Encoding ascii = System.Text.Encoding.ASCII;
        static System.Text.Encoding unicode = System.Text.Encoding.Unicode;
        static System.Text.Encoding gb2312 = System.Text.Encoding.GetEncoding("gb2312");
        /// <summary>
        /// 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="coding"></param>
        /// <returns></returns>
        public static string decText(byte[] buffer, uint coding)
        {
            switch (coding)
            {
                case 0://ASCII
                    return ascii.GetString(buffer).Replace("\0", "");
                case 8://UCS2
                    byte[] temp = new byte[buffer.Length];
                    for (int i = 0; i < buffer.Length; )
                    {
                        // 高低位字节对调
                        if ((i + 1) < buffer.Length)
                        {
                            temp[i] = buffer[i + 1];
                            temp[i + 1] = buffer[i];
                        }
                        i = i + 2;
                    }
                    return unicode.GetString(temp).Replace("\0", "");
                case 15://GBK
                default:
                    return gb18030.GetString(buffer).Replace("\0", "");
            }
        }
        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="str"></param>
        ///// <returns></returns>
        //public static byte[] GetGBKBytes(string str)
        //{
        //    byte[] result = gb18030.GetBytes(str);
        //    return result;
        //}
        public static byte[] GetUSCBytes(string str)
        {
            //byte[] buffer = unicode.GetBytes(str);
            //byte[] temp = new byte[buffer.Length];
            //for (int i = 0; i < buffer.Length; )
            //{
            //    // 高低位字节对调
            //    if ((i + 1) < buffer.Length)
            //    {
            //        temp[i] = buffer[i + 1];
            //        temp[i + 1] = buffer[i];
            //    }
            //    i = i + 2;
            //}
            //return temp;

            return Encoding.BigEndianUnicode.GetBytes(str);
        }

    }
}
