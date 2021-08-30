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
            throw new System.Exception("�ϒ��̃f���^�^�C�����擾���ɍŌ�܂�, �擪�r�b�g��0(����ŏI��)�̂��̂������炸�Ƀo�C�g�̍Ō�܂œǂݎ���Ă��܂���");
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
        /// SMF�̃o�C�i������͂���
        /// </summary>
        /// <param name="bytes">�o�C�i��</param>
        /// <returns>��͂�������</returns>
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
                throw new System.Exception("SMF�̃o�C�g��������܂���");
            }
            return (bytes[0], bytes[1], bytes[2], bytes[3]) switch
            {
                (0x4d, 0x54, 0x68, 0x64) => 4,
                _ => throw new System.Exception("�o�C�i���̐擪�� 0x4D546884 (MThd) �łȂ�")
            };
        }

        private static ulong ValidationLength(in byte[] bytes, in ulong offset)
        {
            return ByteFunc.BytesWithOffsetToUInt32(bytes, offset) switch
            {
                6 => 4,
                _ => throw new System.Exception("�w�b�_�[�̒����̎w�肪 6 �ł͂Ȃ�")
            };
        }

        private static (Format, ulong) ParseFormat(in byte[] bytes, in ulong offset)
        {
            return (
                ByteFunc.BytesWithOffsetToUInt16(bytes, offset) switch
                {
                    0 => Format.Format0,
                    1 => Format.Format1,
                    var e => throw new System.Exception("�T�|�[�g����Ă��Ȃ��t�H�[�}�b�g�ł� " + offset + " ," + e)
                },
                2
            );
        }

        /// <summary>
        /// �g���b�N����ǂݎ��
        /// </summary>
        /// <param name="bytes">�o�C�i��</param>
        /// <param name="offset">�ǂݎ��ʒu</param>
        /// <returns>(�g���b�N��, �ǂݎ����byte��)</returns>
        private static (ushort, ulong) ParseTrackLength(in byte[] bytes, in ulong offset)
        {
            return (ByteFunc.BytesWithOffsetToUInt16(bytes, offset), 2);
        }

        /// <summary>
        /// ����\ ���ԒP�ʂ�ǂݎ��
        /// </summary>
        /// <param name="bytes">�o�C�i��</param>
        /// <param name="offset">�ǂݎ��ʒu</param>
        /// <returns>(����\, �ǂݎ����byte��)</returns>
        private static (ushort, ulong) ParseDivision(in byte[] bytes, in ulong offset)
        {
            ushort division = ByteFunc.BytesWithOffsetToUInt16(bytes, offset);
            if ((division & 0x8000) != 0)
            {
                throw new System.Exception("����\�� �������b���t���[�� �Ƃ����`���Ŏw�肵�����͖̂��T�|�[�g�ł�");
            }
            return (division, 2);
        }
    }

    /// <summary>
    /// �t�H�[�}�b�g. SMF �̃o�[�W�����̂悤�Ȃ���
    /// </summary>
    public enum Format
    {
        /// <summary>
        /// �g���b�N����1�̌`��
        /// </summary>
        Format0,
        /// <summary>
        /// �g���b�N����1�ȏ㎝���Ƃ��ł���`��
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
                _ => throw new System.Exception("�g���b�N�̐擪�� 0x4D54726B (MThd) �łȂ�")
            };
        }

        private static (uint, ulong) ParseLength(in byte[] bytes, in ulong offset)
        {
            return (ByteFunc.BytesWithOffsetToUInt32(bytes, offset), 4);
        }
    }

    public class TimeAndEvent
    {
        public static (TimeAndEvent, ulong) FromBytes(in byte[] bytes, in ulong offset)
        {
            (ulong deltaTime, ulong deltaTimeBytesLength) = ParseDeltaTime(bytes, offset);
            return (new TimeAndEvent(), 0);

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
            throw new System.NotImplementedException("�C�x���g��͖͂������ł�");
        }
    }

    public class MidiEvent : Event
    {
        public static (MidiEvent, ulong)? FromBytesOrNotMatch(in byte[] bytes, in ulong offset)
        {
            return null;
        }
    }

    public class SysEx : Event
    {
        public static (MidiEvent, ulong)? FromBytesOrNotMatch(in byte[] bytes, in ulong offset)
        {

            return null;
        }
    }

    public class MetaEvent : Event
    {
        public static (MetaEvent, ulong)? FromBytesOrNotMatch(in byte[] bytes, in ulong initOffset)
        {
            ulong offset = initOffset;
            byte metaEventType = bytes[offset];
            offset += 1;

            (ulong length, ulong lengthBytesLength) = ByteFunc.BytesWithOffsetToULongVariableLengthQuantity(bytes, offset);
            offset += lengthBytesLength;

            ulong byteLength = offset + length;

            return metaEventType switch
            {
                0xff =>
                    metaEventType switch
                    {
                        0x00 =>
                            throw new System.NotImplementedException("�V�[�P���X�ԍ��͖������ł�"),
                        0x01 =>
                            throw new System.NotImplementedException("�e�L�X�g�C�x���g�͖������ł�"),
                        0x02 =>
                            throw new System.NotImplementedException("���쌠�\���͖������ł�"),

                        0x03 =>
                           throw new System.NotImplementedException("�V�[�P���X���܂��̓g���b�N���͖������ł�"),

                        0x04 =>
                           throw new System.NotImplementedException("�y�햼�͖������ł�"),

                        0x05 =>
                           throw new System.NotImplementedException("�̎��͖������ł�"),

                        0x06 =>
                          throw new System.NotImplementedException("�}�[�J�[�͖������ł�"),

                        0x07 =>
                          throw new System.NotImplementedException("�L���[�E�|�C���g�͖������ł�"),

                        0x2f =>
                            (new EndOfTrack(), byteLength),

                        0x51 =>
                            (new Tempo(bytes[offset + 0], bytes[offset + 1], bytes[offset + 2]), byteLength),

                        0x54 =>
                            throw new System.NotImplementedException("SMPTE �I�t�Z�b�g�͖������ł�"),

                        0x58 =>
                            throw new System.NotImplementedException("���q�L���͖������ł�"),
                        0x7f =>
                            throw new System.NotImplementedException("�V�[�P���T�[���胁�^�E�C�x���g�͖������ł�"),
                        _ =>
                            throw new System.Exception("��̃��^�C�x���g���󂯎���� eventType" + metaEventType)

                    }
                ,
                _ => null
            };
        }
    }

    public class EndOfTrack : MetaEvent
    {

    }

    public class Tempo: MetaEvent
    {
        public readonly float tempo;

        public Tempo(in byte tempoByte0, in byte tempoByte1, in byte tempoByte2)
        {
            int micro = (tempoByte0 << 16) + (tempoByte1 << 8) + tempoByte2;
            this.tempo = 60 * 1000 * 1000 / micro;
        }
    }
}