#nullable enable

namespace Nsmf
{
    public class ByteFunc
    {
        public static ushort BytesWithOffsetToUInt16(in byte[] bytes, in uint offset)
        {
            int value0 = bytes[offset];
            int value1 = bytes[offset + 1];
            return (ushort)((value0 << 8) + value1);

        }
    }

    public class Smf
    {
        public readonly Header header;

        public Smf(in Header header)
        {
            this.header = header;
        }

        /// <summary>
        /// SMFのバイナリを解析する
        /// </summary>
        /// <param name="bytes">バイナリ</param>
        /// <returns>解析した結果</returns>
        public static Smf FromBytes(in byte[] bytes)
        {
            return new Smf(Header.FromBytes(bytes).Item1);
        }
    }

    public class Header
    {
        public readonly Format format;
        public readonly ushort trackLength;
        public readonly ushort division;

        public Header(in Format format, in ushort trackLength, in ushort division)
        {
            this.format = format;
            this.trackLength = trackLength;
            this.division = division;
        }

        public static (Header, uint) FromBytes(in byte[] bytes)
        {
            uint offset = ValidationMagic(bytes);
            offset += ValidationHeaderLength(bytes, offset);

            (Format format, uint formatBytesLength) = ParseFormat(bytes, offset);
            offset += formatBytesLength;

            (ushort trackLength, uint trackBytesLength) = ParseTrackLength(bytes, offset);
            offset += trackBytesLength;

            (ushort divition, uint divitionBytesLength) = ParseDivision(bytes, offset);
            offset += divitionBytesLength;

            return (new Header(format, trackLength, divition), offset);
        }

        private static uint ValidationMagic(in byte[] bytes)
        {
            if (bytes.Length < 4)
            {
                throw new System.Exception("SMFのバイト数が足りません");
            }
            byte magic0 = bytes[0];
            byte magic1 = bytes[1];
            byte magic2 = bytes[2];
            byte magic3 = bytes[3];
            if (
                magic0 != 0x4d ||
                magic1 != 0x54 ||
                magic2 != 0x68 ||
                magic3 != 0x64)
            {
                throw new System.Exception("バイナリの先頭は 4D546884 (MThd) でない");
            }
            return 4;
        }

        private static uint ValidationHeaderLength(in byte[] bytes, in uint offset)
        {
            byte length0 = bytes[offset + 0];
            byte length1 = bytes[offset + 1];
            byte length2 = bytes[offset + 2];
            byte length3 = bytes[offset + 3];
            if (
                length0 != 0x00 ||
                length1 != 0x00 ||
                length2 != 0x00 ||
                length3 != 0x06)
            {
                throw new System.Exception("ヘッダーの長さの指定が 6 ではない");
            }
            return 4;
        }

        private static (Format, uint) ParseFormat(in byte[] bytes, in uint offset)
        {
            return (
                ByteFunc.BytesWithOffsetToUInt16(bytes, offset) switch
                {
                    0 => Format.Format0,
                    1 => Format.Format1,
                    var e => throw new System.Exception("サポートされていないフォーマットです " + offset + " ," + e)
                },
                2
            );
        }

        private static (ushort, uint) ParseTrackLength(in byte[] bytes, in uint offset)
        {
            return (ByteFunc.BytesWithOffsetToUInt16(bytes, offset), 2);
        }

        /// <summary>
        /// 分解能 時間単位を取得する
        /// </summary>
        /// <param name="bytes"></param>
        /// <param name="offset"></param>
        /// <returns>(分解能, 読み取ったbyte数)</returns>
        private static (ushort, uint) ParseDivision(in byte[] bytes, in uint offset)
        {
            ushort division = ByteFunc.BytesWithOffsetToUInt16(bytes, offset);
            if ((division & 0x8000) != 0)
            {
                throw new System.Exception("分解能を 何分何秒何フレーム という形式で指定したものは未サポートです");
            }
            return (division, 2);
        }
    }

    /// <summary>
    /// フォーマット. SMF のバージョンのようなもの
    /// </summary>
    public enum Format
    {
        /// <summary>
        /// トラック数が1つの形式
        /// </summary>
        Format0,
        /// <summary>
        /// トラック数が1つ以上持つことができる形式
        /// </summary>
        Format1
    }
}