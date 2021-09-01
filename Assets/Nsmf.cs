#nullable enable

namespace Nsmf
{
    public class ByteFunc
    {
        public static ushort BytesWithOffsetToUInt16(in byte[] bytes, in ulong offset)
        {
            ushort value0 = bytes[offset];
            ushort value1 = bytes[offset + 1];
            return (ushort)((value0 << 8) + value1);

        }

        public static uint BytesWithOffsetToUInt32(in byte[] bytes, in ulong offset)
        {
            uint value0 = bytes[offset];
            uint value1 = bytes[offset + 1];
            uint value2 = bytes[offset + 2];
            uint value3 = bytes[offset + 3];
            return (value0 << 24) + (value1 << 16) + (value2 << 8) + value3;
        }

        public static (ulong, ulong) BytesWithOffsetToULongVariableLengthQuantity(in byte[] bytes, in ulong offset)
        {
            ulong value = 0;
            for (ulong index = 0; (ulong)bytes.Length < index; index += 1)
            {
                byte b = bytes[offset + index];
                value = (value << 8) + (ulong)(b & 0x7f);
                if ((b & 0x8000) != 0)
                {
                    return (value, index);
                }
            }
            throw new System.Exception("可変長のデルタタイムを取得時に最後まで, 先頭ビットが0(これで終了)のものが見つからずにバイトの最後まで読み取ってしまった");
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

        public static (Header, ulong) FromBytes(in byte[] bytes)
        {
            ulong offset = ValidationMagic(bytes);
            offset += ValidationLength(bytes, offset);

            (Format format, ulong formatBytesLength) = ParseFormat(bytes, offset);
            offset += formatBytesLength;

            (ushort trackLength, ulong trackBytesLength) = ParseTrackLength(bytes, offset);
            offset += trackBytesLength;

            (ushort divition, ulong divitionBytesLength) = ParseDivision(bytes, offset);
            offset += divitionBytesLength;

            return (new Header(format, trackLength, divition), offset);
        }

        private static ulong ValidationMagic(in byte[] bytes)
        {
            if (bytes.Length < 4)
            {
                throw new System.Exception("SMFのバイト数が足りません");
            }
            return (bytes[0], bytes[1], bytes[2], bytes[3]) switch
            {
                (0x4d, 0x54, 0x68, 0x64) => 4,
                _ => throw new System.Exception("バイナリの先頭は 0x4D546884 (MThd) でない")
            };
        }

        private static ulong ValidationLength(in byte[] bytes, in ulong offset)
        {
            return ByteFunc.BytesWithOffsetToUInt32(bytes, offset) switch
            {
                6 => 4,
                _ => throw new System.Exception("ヘッダーの長さの指定が 6 ではない")
            };
        }

        private static (Format, ulong) ParseFormat(in byte[] bytes, in ulong offset)
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

        /// <summary>
        /// トラック数を読み取る
        /// </summary>
        /// <param name="bytes">バイナリ</param>
        /// <param name="offset">読み取り位置</param>
        /// <returns>(トラック数, 読み取ったbyte数)</returns>
        private static (ushort, ulong) ParseTrackLength(in byte[] bytes, in ulong offset)
        {
            return (ByteFunc.BytesWithOffsetToUInt16(bytes, offset), 2);
        }

        /// <summary>
        /// 分解能 時間単位を読み取る
        /// </summary>
        /// <param name="bytes">バイナリ</param>
        /// <param name="offset">読み取り位置</param>
        /// <returns>(分解能, 読み取ったbyte数)</returns>
        private static (ushort, ulong) ParseDivision(in byte[] bytes, in ulong offset)
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

    public class Track
    {
        public Track()
        {

        }

        public static (Track, ulong) FromBytes(in byte[] bytes, in ulong offset)
        {
            ulong magicLength = ValidationMagic(bytes, offset);
            return (new Track(), magicLength);
            (uint length, ulong bytesLength) = ParseLength(bytes, offset);

        }

        private static ulong ValidationMagic(in byte[] bytes, in ulong offset)
        {
            return (bytes[offset + 0], bytes[offset + 1], bytes[offset + 2], bytes[offset + 3]) switch
            {
                (0x4d, 0x54, 0x72, 0x6b) => 4,
                _ => throw new System.Exception("トラックの先頭は 0x4D54726B (MThd) でない")
            };
        }

        private static (uint, ulong) ParseLength(in byte[] bytes, in ulong offset)
        {
            return (ByteFunc.BytesWithOffsetToUInt32(bytes, offset), 4);
        }
    }

    public class TimeAndEvent
    {
        public readonly ulong deltaTime;
        public readonly Event @event;

        public TimeAndEvent(in ulong deltaTime, in Event @event)
        {
            this.deltaTime = deltaTime;
            this.@event = @event;
        }

        public static (TimeAndEvent, ulong) FromBytes(in byte[] bytes, in ulong offset)
        {
            (ulong deltaTime, ulong deltaTimeBytesLength) = ParseDeltaTime(bytes, offset);

            (Event @event, ulong eventBytesLength) = Event.FromBytes(bytes, offset + deltaTimeBytesLength);

            return (new TimeAndEvent(deltaTime, @event), deltaTimeBytesLength + eventBytesLength);

        }

        public static (ulong, ulong) ParseDeltaTime(in byte[] bytes, in ulong offset)
        {
            return ByteFunc.BytesWithOffsetToULongVariableLengthQuantity(bytes, offset);
        }
    }

    public class Event
    {
        public static (Event, ulong) FromBytes(in byte[] bytes, in ulong offset)
        {
            switch (bytes[offset])
            {
                case 0xff:
                    {
                        (MetaEvent metaEvent, ulong metaEventBytesLength) = MetaEvent.FromBytesOrNotMatch(bytes, offset + 1);
                        return (metaEvent, 1 + metaEventBytesLength);
                    }
                case 0xf0 or 0xf7:
                    {
                        (SysExEvent sysEx, ulong sysExBytesLength) = SysExEvent.FromBytesOrNotMatch(bytes, offset);
                        return (sysEx, 1 + sysExBytesLength);
                    }
            }

            throw new System.NotImplementedException("メタイベント, SysEx以外のイベントの解析は未実装です");
        }
    }

    public class MidiEvent : Event
    {
        public static (MidiEvent, ulong)? FromBytesOrNotMatch(in byte[] bytes, in ulong offset)
        {
            return null;
        }
    }

    public class SysExEvent : Event
    {
        public static (SysExEvent, ulong) FromBytesOrNotMatch(in byte[] bytes, in ulong offset)
        {
            (ulong length, ulong lengthBytesLength) = ByteFunc.BytesWithOffsetToULongVariableLengthQuantity(bytes, offset + 1);
            return (new SysExEvent(), lengthBytesLength + length);
        }
    }

    public class MetaEvent : Event
    {
        public static (MetaEvent, ulong) FromBytesOrNotMatch(in byte[] bytes, in ulong offset)
        {
            byte metaEventType = bytes[offset];

            (ulong length, ulong lengthBytesLength) = ByteFunc.BytesWithOffsetToULongVariableLengthQuantity(bytes, offset + 1);

            return (
                metaEventType switch
                {
                    0x00 =>
                        throw new System.NotImplementedException("シーケンス番号は未実装です"),
                    0x01 =>
                        throw new System.NotImplementedException("テキストイベントは未実装です"),
                    0x02 =>
                        throw new System.NotImplementedException("著作権表示は未実装です"),

                    0x03 =>
                       throw new System.NotImplementedException("シーケンス名またはトラック名は未実装です"),

                    0x04 =>
                       throw new System.NotImplementedException("楽器名は未実装です"),

                    0x05 =>
                       throw new System.NotImplementedException("歌詞は未実装です"),

                    0x06 =>
                      throw new System.NotImplementedException("マーカーは未実装です"),

                    0x07 =>
                      throw new System.NotImplementedException("キュー・ポイントは未実装です"),

                    0x2f =>
                        new EndOfTrack(),

                    0x51 =>
                        new Tempo(bytes[offset + 0], bytes[offset + 1], bytes[offset + 2]),

                    0x54 =>
                        throw new System.NotImplementedException("SMPTE オフセットは未実装です"),

                    0x58 =>
                        throw new System.NotImplementedException("拍子記号は未実装です"),
                    0x7f =>
                        throw new System.NotImplementedException("シーケンサー特定メタ・イベントは未実装です"),
                    _ =>
                        throw new System.Exception("謎のメタイベントを受け取った eventType" + metaEventType)

                },
                1 + lengthBytesLength + length
            );
        }
    }

    public class EndOfTrack : MetaEvent
    {

    }

    public class Tempo : MetaEvent
    {
        public readonly float tempo;

        public Tempo(in byte tempoByte0, in byte tempoByte1, in byte tempoByte2)
        {
            int micro = (tempoByte0 << 16) + (tempoByte1 << 8) + tempoByte2;
            this.tempo = 60 * 1000 * 1000 / micro;
        }
    }
}