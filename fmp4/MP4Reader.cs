
using System.Net;
using System.Text;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// A utility class the extends BinaryReader and writes values in big endian format for MP4.
    /// </summary>
    public class MP4Reader : BinaryReader
    {
        /// <summary>
        /// Construct with a given stream.
        /// </summary>
        /// <param name="stream">the stream to read from</param>
        public MP4Reader(Stream stream) :
            base(stream)
        {
        }

        /// <summary>
        /// Construct with a given stream.
        /// </summary>
        /// <param name="stream">the stream to read from</param>
        /// <param name="encoding">The character encoding to use.</param>
        /// <param name="leaveOpen">True to leave the stream open after MP4Reader object is disposed. Otherwise, false.</param>
        public MP4Reader(Stream stream, Encoding encoding, bool leaveOpen) :
            base(stream, encoding, leaveOpen)
        {
        }

        /// <summary>
        /// Reads a signed integer as big endian from the stream.
        /// </summary>
        /// <returns>16 bit integer value</returns>
        public override Int16 ReadInt16()
        {
            return IPAddress.NetworkToHostOrder(base.ReadInt16());
        }

        /// <summary>
        /// Reads an unsigned 16 bit integer in big endian format from the stream.
        /// </summary>
        /// <returns></returns>
        public override UInt16 ReadUInt16()
        {
            return (UInt16)this.ReadInt16();
        }

        /// <summary>
        /// Reads a singed 32 bit integer value in big endian format from stream.
        /// </summary>
        /// <returns>A big-endian signed 32-bit integer from bit stream</returns>
        public override Int32 ReadInt32()
        {
            return IPAddress.NetworkToHostOrder(base.ReadInt32());
        }

        /// <summary>
        /// Helper function to read unsigned 32-bit integer in BIG-ENDIAN format from a stream.
        /// </summary>
        /// <returns>A big-endian unsigned 32-integer from the bitstream.</returns>
        public override UInt32 ReadUInt32()
        {
            return (UInt32)this.ReadInt32();
        }

        /// <summary>
        /// Reads a 64-bit signed integer in big endian format from a stream.
        /// </summary>
        /// <returns>A big endian 64 bit integer</returns>
        public override Int64 ReadInt64()
        {
            return IPAddress.NetworkToHostOrder(base.ReadInt64());
        }

        /// <summary>
        /// Helper function to read unsigned 64-bit integer in BIG-ENDIAN format from a stream.
        /// </summary>
        /// <returns>An big-endian unsigned 64-integer from the bitstream.</returns>
        public override UInt64 ReadUInt64()
        {
            return (UInt64)this.ReadInt64();
        }


        /// <summary>
        /// Read a GUID value from the mp4 byte stream.
        /// </summary>
        /// <returns>A Guid object</returns>
        public Guid ReadGuid()
        {
            Int32 data1 = ReadInt32();
            Int16 data2 = ReadInt16();
            Int16 data3 = ReadInt16();
            Byte[] data4 = ReadBytes(8);

            return new Guid(data1, data2, data3, data4);
        }


        /// <summary>
        /// This function reads an 8/16/24/32-bit big-endian field from disk.
        /// </summary>
        /// <param name="value">Returns the value here.</param>
        /// <param name="bitDepth">The size of the field (8/16/24/32).</param>
        public UInt32 ReadVariableLengthField(int bitDepth)
        {
            UInt32 value = 0;

            if (bitDepth % 8 != 0)
            {
                throw new ArgumentException("bitDepth must be multiple of 8");
            }

            if (bitDepth > 32)
            {
                throw new ArgumentException("bitDepth must be 8/16/24/32 only");
            }

            for (int i = 0; i < bitDepth; i += 8)
            {
                value <<= 8;
                value |= ReadByte();
            }

            return value;
        }


    }
}
