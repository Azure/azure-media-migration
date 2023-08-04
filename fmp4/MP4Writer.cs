
using System.Diagnostics;
using System.Net;
using System.Text;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// Implements a writer class that writes data in big endian format for MP4 boxes.
    /// </summary>
    public class MP4Writer : BinaryWriter
    {
        /// <summary>
        /// Construct an MP4 writer that writes to the given stream.
        /// </summary>
        /// <param name="stream">the stream to write to</param>
        public MP4Writer(Stream stream) :
            base(stream)
        {
        }

        /// <summary>
        /// Construct an MP4 writer that writes to the given stream.
        /// </summary>
        /// <param name="stream">the stream to write to</param>
        /// <param name="encoding">The character encoding to use.</param>
        /// <param name="leaveOpen">True to leave the stream open after MP4Reader object is disposed. Otherwise, false.</param>
        public MP4Writer(Stream stream, Encoding encoding, bool leaveOpen) :
            base(stream, encoding, leaveOpen)
        {
        }

        #region signed writes

        /// <summary>
        /// Write an 16 bit integer in big endian format to the stream.
        /// </summary>
        /// <param name="value">the value to write.</param>
        public void WriteInt16(Int16 value)
        {
            base.Write(IPAddress.HostToNetworkOrder(value));
        }

        /// <summary>
        /// Writes a 32 bit integer in big endian format to the stream
        /// </summary>
        /// <param name="value">the value to write.</param>
        public void WriteInt32(Int32 value)
        {
            base.Write(IPAddress.HostToNetworkOrder(value));
        }

        /// <summary>
        /// Writes a 64 bit signed integer in big endian format to the stream
        /// </summary>
        /// <param name="value">The value to write.</param>
        public void WriteInt64(Int64 value)
        {
            base.Write(IPAddress.HostToNetworkOrder(value));
        }

        #endregion

        #region unsigned writes

        /// <summary>
        /// Writes a unsigned 16 bit integer
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteUInt16(UInt16 value)
        {
            this.WriteInt16((Int16)value);
        }

        /// <summary>
        /// Writes an unsigned 32 bit integer in big endian format.
        /// </summary>
        /// <param name="value">The value to write</param>
        public void WriteUInt32(UInt32 value)
        {
            this.WriteInt32((Int32)value);
        }

        /// <summary>
        /// Writes an unsigned 64 bit integer in big endian format.
        /// </summary>
        /// <param name="value">The value to write.</param>
        public void WriteUInt64(UInt64 value)
        {
            this.WriteInt64((Int64)value);
        }

        #endregion


        /// <summary>
        /// This function writes an 8/16/24/32-bit big-endian field to disk.
        /// </summary>
        /// <param name="writer">The MP4Writer to write to.</param>
        /// <param name="value">The value to write.</param>
        /// <param name="bitDepth">The size of the field on disk (8/16/24/32).</param>
        public void WriteVariableLengthField(UInt64 value, int bitDepth)
        {
            if (bitDepth % 8 != 0)
            {
                throw new ArgumentException("bitDepth must be a multiple of 8");
            }

            if (bitDepth > 32)
            {
                throw new ArgumentException("bitDepth must be less than or equal to 32");
            }

            for (int i = 0; i < bitDepth; i += 8)
            {
                Byte output = (Byte)(value >> (bitDepth - 8 - i));
                Write(output);
            }
        }

        /// <summary>
        /// Write a GUID Value in the big endian order.
        /// </summary>
        /// <param name="value"></param>
        public void Write(Guid value)
        {
            Byte[] guid = value.ToByteArray();
            Debug.Assert(16 == guid.Length);
            WriteInt32(BitConverter.ToInt32(guid, 0));
            WriteInt16(BitConverter.ToInt16(guid, 4));
            WriteInt16(BitConverter.ToInt16(guid, 6));
            Write(guid, 8, 8);
        }

    }
}
