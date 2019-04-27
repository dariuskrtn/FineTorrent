using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FineTorrent.Application.Decoders
{
    public class BDecoder
    {
        private static byte DictionaryStart = System.Text.Encoding.UTF8.GetBytes("d")[0]; // 100
        private static byte DictionaryEnd = System.Text.Encoding.UTF8.GetBytes("e")[0]; // 101
        private static byte ListStart = System.Text.Encoding.UTF8.GetBytes("l")[0]; // 108
        private static byte ListEnd = System.Text.Encoding.UTF8.GetBytes("e")[0]; // 101
        private static byte NumberStart = System.Text.Encoding.UTF8.GetBytes("i")[0]; // 105
        private static byte NumberEnd = System.Text.Encoding.UTF8.GetBytes("e")[0]; // 101
        private static byte ByteArrayDivider = System.Text.Encoding.UTF8.GetBytes(":")[0]; //  58


        public object Decode(IEnumerator<byte> bytes, bool moveNext = true)
        {
            if (moveNext) bytes.MoveNext();
            
            var b = bytes.Current;
            if (b == DictionaryStart) return DecodeDict(bytes);
            if (b == NumberStart) return DecodeNum(bytes);
            if (b == ListStart) return DecodeList(bytes);

            return DecodeByteArray(bytes);
        }

        public long DecodeNum(IEnumerator<byte> bytes)
        {
            List<byte> numBytes = new List<byte>();

            while (bytes.MoveNext())
            {
                if (bytes.Current == NumberEnd)
                    break;

                numBytes.Add(bytes.Current);
            }

            string numAsString = Encoding.UTF8.GetString(numBytes.ToArray());

            return Int64.Parse(numAsString);
        }

        public byte[] DecodeByteArray(IEnumerator<byte> bytes)
        {
            List<byte> lengthBytes = new List<byte>();

            // scan until we get to divider
            do
            {
                if (bytes.Current == ByteArrayDivider)
                    break;

                lengthBytes.Add(bytes.Current);
            }
            while (bytes.MoveNext());

            string lengthString = System.Text.Encoding.UTF8.GetString(lengthBytes.ToArray());

            int length;
            if (!Int32.TryParse(lengthString, out length))
                throw new Exception("unable to parse length of byte array");

            // now read in the actual byte array
            byte[] bytess = new byte[length];

            for (int i = 0; i < length; i++)
            {
                bytes.MoveNext();
                bytess[i] = bytes.Current;
            }

            return bytess;
        }

        public List<object> DecodeList(IEnumerator<byte> bytes)
        {
            List<object> list = new List<object>();

            // keep decoding objects until we hit the end flag
            while (bytes.MoveNext())
            {
                if (bytes.Current == ListEnd)
                    break;

                list.Add(Decode(bytes, false));
            }

            return list;
        }

        public Dictionary<string, object> DecodeDict(IEnumerator<byte> bytes)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            List<string> keys = new List<string>();

            // keep decoding objects until we hit the end flag
            while (bytes.MoveNext())
            {
                if (bytes.Current == DictionaryEnd)
                    break;

                // all keys are valid UTF8 strings
                string key = Encoding.UTF8.GetString(DecodeByteArray(bytes));
                //bytes.MoveNext();
                object val = Decode(bytes);

                keys.Add(key);
                dict.Add(key, val);
            }

            // verify incoming dictionary is sorted correctly
            // we will not be able to create an identical encoding otherwise
            var sortedKeys = keys.OrderBy(x => BitConverter.ToString(Encoding.UTF8.GetBytes(x)));
            if (!keys.SequenceEqual(sortedKeys))
                throw new Exception("error loading dictionary: keys not sorted");

            return dict;
        }
    }
}