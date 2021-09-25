#nullable enable
using System.Collections.Generic;

namespace Nsmf
{
    /// <summary>
    /// 読み取り専用にして, 読める範囲を制限した byte[]
    /// </summary>
    public class ReadonlyBytes
    {
        private readonly byte[] bytes;
        private readonly uint readableAbusoluteIndexStart;
        private readonly uint readableAbusoluteIndexEnd;

        public ReadonlyBytes(in byte[] bytes)
        {
            this.bytes = bytes;
            this.readableAbusoluteIndexStart = 0;
            this.readableAbusoluteIndexEnd = (uint)bytes.Length;
        }

        private ReadonlyBytes(in byte[] bytes, in uint readableAbusoluteIndexStart, in uint readableAbusoluteIndexEnd)
        {
            this.bytes = bytes;
            this.readableAbusoluteIndexStart = readableAbusoluteIndexStart;
            this.readableAbusoluteIndexEnd = readableAbusoluteIndexEnd;
        }

        public (byte, ReadonlyBytes) GetUInt8()
        {
            return (
                this.bytes[this.readableAbusoluteIndexStart],
                this.CreateWithStartIndex(1)
            );
        }

        public (ushort, ReadonlyBytes) GetUInt16()
        {
            (ushort value0, ReadonlyBytes bytes0) = this.GetUInt8();
            (ushort value1, ReadonlyBytes bytes1) = bytes0.GetUInt8();
            return ((ushort)((value0 << 8) + value1), bytes1);
        }

        public (uint, ReadonlyBytes) GetUInt32()
        {
            (uint value0, ReadonlyBytes bytes0) = this.GetUInt8();
            (uint value1, ReadonlyBytes bytes1) = bytes0.GetUInt8();
            (uint value2, ReadonlyBytes bytes2) = bytes1.GetUInt8();
            (uint value3, ReadonlyBytes bytes3) = bytes2.GetUInt8();
            return ((value0 << 24) + (value1 << 16) + (value2 << 8) + value3, bytes3);
        }

        public (ulong, ReadonlyBytes) GetULongVariableLengthQuantity()
        {
            ulong value = 0;
            ReadonlyBytes bytes = this;
            for (uint index = this.readableAbusoluteIndexStart; this.readableAbusoluteIndexEnd < index; index += 1)
            {
                (byte, ReadonlyBytes) tuple = bytes.GetUInt8();
                byte b = tuple.Item1;
                bytes = tuple.Item2;
                value = (value << 8) + (ulong)(b & 0x7f);
                if ((b & 0x8000) != 0)
                {
                    return (value, bytes);
                }
            }
            throw new System.Exception("可変長のデルタタイムを取得時に最後まで, 先頭ビットが0(これで終了)のものが見つからずにバイトの最後まで読み取ってしまった");
        }

        /// <summary>
        /// 開始地点を指定してさらに範囲を絞った 新しい ReadonlyBytesAndRange を生成する
        /// </summary>
        /// <param name="relativeReadableStartIndex"></param>
        /// <returns></returns>
        public ReadonlyBytes CreateWithStartIndex(in uint relativeReadableStartIndex)
        {
            uint newReadableAbusoluteIndexStart = this.readableAbusoluteIndexStart + relativeReadableStartIndex;
            if (this.readableAbusoluteIndexEnd < newReadableAbusoluteIndexStart)
            {
                throw new System.Exception("読み取り開始位置が前回の終了位置を超えている");
            }
            return new ReadonlyBytes(this.bytes, newReadableAbusoluteIndexStart, this.readableAbusoluteIndexEnd);
        }

        /// <summary>
        /// 開始地点と長さを指定してさらに範囲を絞った 新しい ReadonlyBytesAndRange を生成する
        /// </summary>
        /// <param name="relativeReadableStartIndex"></param>
        /// <returns></returns>
        public ReadonlyBytes CreateWithStartAndEndIndex(in uint relativeReadableStartIndex, in uint length)
        {
            uint newReadableAbusoluteIndexStart = this.readableAbusoluteIndexStart + relativeReadableStartIndex;
            uint newReadableAbusoluteIndexEnd = newReadableAbusoluteIndexStart + length;
            if (this.readableAbusoluteIndexEnd < newReadableAbusoluteIndexEnd)
            {
                throw new System.Exception("読み取り終了位置が前回の終了位置を超えている");
            }
            return new ReadonlyBytes(this.bytes, newReadableAbusoluteIndexStart, newReadableAbusoluteIndexEnd);
        }
    }
    public class ByteFunc
    {

    }

    public class Smf
    {
        public readonly Header header;
        public readonly List<Track> tracks;

        public Smf(in Header header, in List<Track> tracks)
        {
            this.header = header;
            this.tracks = tracks;
        }

        /// <summary>
        /// SMFのバイナリを解析する
        /// </summary>
        /// <param name="bytes">バイナリ</param>
        /// <returns>解析した結果</returns>
        public static Smf FromBytes(in byte[] bytes)
        {
            (Header header, ReadonlyBytes readonlyBytes) = Header.FromBytes(new ReadonlyBytes(bytes));
            List<Track> tracks = new List<Track>();
            ReadonlyBytes bytesInLoop = readonlyBytes;
            for (uint trackIndex = 0; trackIndex < header.trackLength; trackIndex += 1)
            {
                (Track, ReadonlyBytes) tuple = Track.FromBytes(bytesInLoop);
                tracks.Add(tuple.Item1);
                bytesInLoop = tuple.Item2;
            }
            return new Smf(header, tracks);
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

        public static (Header, ReadonlyBytes) FromBytes(in ReadonlyBytes bytes)
        {
            ReadonlyBytes bytesAfterValidationMagic = ValidationMagic(bytes);
            ReadonlyBytes bytesAfterValidationLength = ValidationLength(bytesAfterValidationMagic);

            (Format format, ReadonlyBytes bytesAfterParseFormat) = ParseFormat(bytesAfterValidationLength);

            (ushort trackLength, ReadonlyBytes bytesAfterParseTrackLength) = ParseTrackLength(bytesAfterParseFormat);

            (ushort divition, ReadonlyBytes bytesAfterParseDivision) = ParseDivision(bytesAfterParseTrackLength);

            return (new Header(format, trackLength, divition), bytesAfterParseDivision);
        }

        private static ReadonlyBytes ValidationMagic(in ReadonlyBytes bytes)
        {
            return bytes.GetUInt32() switch
            {
                (0x4d546884, var newBytes) => newBytes,
                (_, _) => throw new System.Exception("バイナリの先頭は 0x4D546884 (MThd) でない")
            };
        }

        private static ReadonlyBytes ValidationLength(in ReadonlyBytes bytes)
        {
            return bytes.GetUInt32() switch
            {
                (6, var newBytes) => newBytes,
                _ => throw new System.Exception("ヘッダーの長さの指定が 6 ではない")
            };
        }

        private static (Format, ReadonlyBytes) ParseFormat(in ReadonlyBytes bytes)
        {
            (ushort formatAsNumber, ReadonlyBytes newBytes) = bytes.GetUInt16();
            return (
                formatAsNumber switch
                {
                    0 => Format.Format0,
                    1 => Format.Format1,
                    _ => throw new System.Exception("サポートされていないフォーマットです ")
                },
                newBytes
            );
        }

        /// <summary>
        /// トラック数を読み取る
        /// </summary>
        private static (ushort, ReadonlyBytes) ParseTrackLength(in ReadonlyBytes bytes)
        {
            return bytes.GetUInt16();
        }

        /// <summary>
        /// 分解能 時間単位を読み取る
        /// </summary>
        private static (ushort, ReadonlyBytes) ParseDivision(in ReadonlyBytes bytes)
        {
            (ushort division, ReadonlyBytes newBytes) = bytes.GetUInt16();
            if ((division & 0x8000) != 0)
            {
                throw new System.Exception("分解能を 何分何秒何フレーム という形式で指定したものは未サポートです");
            }
            return (division, newBytes);
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
        public readonly List<TimeAndEvent> timeAndEvents;
        public Track(in List<TimeAndEvent> timeAndEvents)
        {
            this.timeAndEvents = timeAndEvents;
        }

        public static (Track, ReadonlyBytes) FromBytes(in ReadonlyBytes bytes)
        {
            ReadonlyBytes bytesAftrerValidationMagic = ValidationMagic(bytes);
            List<TimeAndEvent> timeAndEvents = new List<TimeAndEvent>();

            (ulong length, ReadonlyBytes bytesAfterParseLength) = ParseLength(bytesAftrerValidationMagic);

            ReadonlyBytes bytesInLoop = bytesAfterParseLength;
            for (uint timeAndEventIndex = 0; timeAndEventIndex < length; timeAndEventIndex += 1)
            {
                switch (TimeAndEvent.FromBytes(bytesInLoop))
                {
                    case (TimeAndEvent timeAndEvent, ReadonlyBytes newBytes):
                        {
                            timeAndEvents.Add(timeAndEvent);
                            bytesInLoop = newBytes;
                            break;
                        }
                };
            }
            return (new Track(timeAndEvents), bytesInLoop);
        }

        private static ReadonlyBytes ValidationMagic(in ReadonlyBytes bytes)
        {
            return bytes.GetUInt32() switch
            {
                (0x4d54726b, var newBytes) => newBytes,
                (_, _) => throw new System.Exception("トラックの先頭は 0x4D54726B (MThd) でない")
            };
        }

        private static (ulong, ReadonlyBytes) ParseLength(in ReadonlyBytes bytes)
        {
            return bytes.GetULongVariableLengthQuantity();
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

        public static (TimeAndEvent, ReadonlyBytes)? FromBytes(in ReadonlyBytes bytes)
        {
            (ulong deltaTime, ReadonlyBytes bytesAfterParseDeltaTime) = ParseDeltaTime(bytes);

            (Event, ReadonlyBytes)? eventTuple = Event.FromBytes(bytesAfterParseDeltaTime);

            return eventTuple switch
            {
                (Event @event, ReadonlyBytes bytesAfterEvent) =>
                    (new TimeAndEvent(deltaTime, @event), bytesAfterEvent),
                null =>
                    null
            };
        }

        public static (ulong, ReadonlyBytes) ParseDeltaTime(in ReadonlyBytes bytes)
        {
            return bytes.GetULongVariableLengthQuantity();
        }
    }

    public class Event
    {
        public static (Event, ReadonlyBytes)? FromBytes(in ReadonlyBytes bytes)
        {
            (byte eventFirstByte, ReadonlyBytes bytesByFirstBytes) = bytes.GetUInt8();
            byte firstLeft = (byte)(eventFirstByte >> 4);
            byte firstRight = (byte)(eventFirstByte & 0b00001111);
            switch (firstLeft, firstRight)
            {
                case (0x8, var channel):
                    {
                        (byte noteNumber, ReadonlyBytes bytesAfterNoteNumber) = bytesByFirstBytes.GetUInt8();
                        return (
                            new NoteOnOrOff(channel, noteNumber, 0),
                            bytesAfterNoteNumber.CreateWithStartIndex(1)
                        );
                    }
                case (0x9, var channel):
                    {
                        (byte noteNumber, ReadonlyBytes bytesAfterNoteNumber) = bytesByFirstBytes.GetUInt8();
                        (byte velocity, ReadonlyBytes bytesAfterBelocity) = bytesAfterNoteNumber.GetUInt8();
                        return (
                            new NoteOnOrOff(channel, noteNumber, velocity),
                            bytesAfterBelocity
                        );
                    }
                case (0xf, 0xf):
                    {
                        return MetaEvent.MetaEventFromBytes(bytesByFirstBytes);
                    };
                case (0xf, 0x0):
                case (0xf, 0x7):
                    {
                        return SysExEvent.SysExEventFromBytes(bytesByFirstBytes);
                    };

                default:
                    {
                        UnityEngine.Debug.Log("未実装のイベントです");
                        return null;
                    }
            };
        }
    }

    public class MidiEvent : Event
    {
    }

    public class NoteOnOrOff : MidiEvent
    {
        public readonly byte channel;
        public readonly byte noteNumber;
        public readonly byte velocity;

        public NoteOnOrOff(in byte channel, in byte noteNumber, in byte velocity)
        {
            if (15 < channel)
            {
                throw new System.Exception("channel 番号が 15 を超えることはできません");
            }
            this.channel = channel;
            this.noteNumber = noteNumber;
            this.velocity = velocity;
        }

        public bool IsOff()
        {
            return this.velocity == 0;
        }
    }

    public class SysExEvent : Event
    {
        public static (SysExEvent, ReadonlyBytes) SysExEventFromBytes(in ReadonlyBytes bytes)
        {
            (ulong length, ReadonlyBytes newBytes) =
                bytes.GetULongVariableLengthQuantity();
            return (new SysExEvent(), newBytes.CreateWithStartIndex((uint)length));
        }
    }

    public class MetaEvent : Event
    {
        public static (MetaEvent, ReadonlyBytes) MetaEventFromBytes(in ReadonlyBytes bytes)
        {
            (byte metaEventType, ReadonlyBytes bytesAfterMetaEventType) = bytes.GetUInt8();

            (ulong length, ReadonlyBytes bytesAfterLength) = bytesAfterMetaEventType.GetULongVariableLengthQuantity();
            ReadonlyBytes bodyBytes = bytesAfterLength.CreateWithStartAndEndIndex(0, (uint)length);

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
                     Tempo.FromTempoBytes(bodyBytes),

                    0x54 =>
                        throw new System.NotImplementedException("SMPTE オフセットは未実装です"),

                    0x58 =>
                        throw new System.NotImplementedException("拍子記号は未実装です"),
                    0x7f =>
                        throw new System.NotImplementedException("シーケンサー特定メタ・イベントは未実装です"),
                    _ =>
                        throw new System.Exception("謎のメタイベントを受け取った eventType" + metaEventType)

                },
                bytesAfterLength.CreateWithStartIndex((uint)length)
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

        public static Tempo FromTempoBytes(in ReadonlyBytes bytes)
        {
            (byte tempoByte0, ReadonlyBytes bytes0) = bytes.GetUInt8();
            (byte tempoByte1, ReadonlyBytes bytes1) = bytes0.GetUInt8();
            (byte tempoByte2, _) = bytes1.GetUInt8();
            return new Tempo(tempoByte0, tempoByte1, tempoByte2);
        }
    }
}