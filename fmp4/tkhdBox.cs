using System.Diagnostics;
using System.Globalization;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// Represents a movie header box . ISO 14496-12 Sec:8.2.2
    /// </summary>
    public class tkhdBox : FullBox, IEquatable<tkhdBox>
    {
        /// <summary>
        /// Default constructor with a track id
        /// </summary>
        /// <param name="trackId">The track ID of the track..</param>
        public tkhdBox(UInt32 trackId) :
            base(version: 0, flags: 0, boxtype: MP4BoxType.tkhd)
        {
            Matrix = new Int32[9];
            _trackId = trackId;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="box">the box to copy from</param>
        public tkhdBox(Box box) :
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.tkhd);
            Matrix = new Int32[9];
        }

        /// <summary>
        /// Get or set the track ID for a track.
        /// </summary>
        public UInt32 TrackId
        {
            get
            {
                return _trackId;
            }
            set
            {
                _trackId = value;
                SetDirty();
            }
        }

        /// <summary>
        /// Creation time of media as number of seconds since 1st Jan 1904.
        /// </summary>
        public UInt64 CreationTime { get; private set; }

        /// <summary>
        /// Creation time of media as number of seconds since 1st Jan 1904.
        /// </summary>
        public UInt64 ModificationTime { get; private set; }

        /// <summary>
        /// Duration of the track in the timescale.
        /// </summary>
        public UInt64 Duration { get; private set; }

        /// <summary>
        /// The z-axis of the video track.
        /// </summary>
        public UInt16 Layer { get; private set; }

        /// <summary>
        /// The alternate group to which this track belongs.
        /// </summary>
        public UInt16 AlternateGroup { get; private set; }

        /// <summary>
        /// Volume for an audio track.
        /// </summary>
        public UInt16 Volume { get; private set; }

        /// <summary>
        /// The optional transformation matrix to be used for the video.
        /// </summary>
        public Int32[] Matrix { get; private set; }

        /// <summary>
        /// The current track ID.
        /// </summary>
        private UInt32 _trackId;


        /// <summary>
        /// The width of a video track.
        /// </summary>
        public UInt32 Width { get; private set; }

        /// <summary>
        /// The height of a video track.
        /// </summary>
        public UInt32 Height { get; private set; }

        /// <summary>
        /// Parse the body contents of this box.
        /// </summary>
        /// <returns></returns>
        protected override void ReadBody()
        {
            base.ReadBody();

            long startPosition = Body!.BaseStream.Position;

            try
            {
                UInt32 reserved;

                if (Version == 1)
                {
                    CreationTime = Body.ReadUInt64();
                    ModificationTime = Body.ReadUInt64();
                    _trackId = Body.ReadUInt32();
                    reserved = Body.ReadUInt32();
                    Debug.Assert(reserved == 0);
                    Duration = Body.ReadUInt64();
                }
                else
                {
                    CreationTime = Body.ReadUInt32();
                    ModificationTime = Body.ReadUInt32();
                    _trackId = Body.ReadUInt32();
                    reserved = Body.ReadUInt32();
                    Debug.Assert(reserved == 0);
                    Duration = Body.ReadUInt32();
                    SetDirty(); //We can't round trip version 0.
                }

                for (int i = 0; i < 2; ++i)
                {
                    reserved = Body.ReadUInt32();
                    Debug.Assert(reserved == 0);
                }

                Layer = Body.ReadUInt16();
                AlternateGroup = Body.ReadUInt16();
                Volume = Body.ReadUInt16();

                reserved = Body.ReadUInt16();
                Debug.Assert(reserved == 0);

                Matrix = new Int32[9];
                for (int i = 0; i < Matrix.Length; ++i)
                {
                    Matrix[i] = Body.ReadInt32();
                }

                Width = Body.ReadUInt32();
                Height = Body.ReadUInt32();

            }
            catch (EndOfStreamException ex)
            {
                // Reported error offset will point to start of box
                throw new MP4DeserializeException(TypeDescription, -BodyPreBytes, BodyInitialOffset,
                    String.Format(CultureInfo.InvariantCulture, "Could not read tkhd fields, only {0} bytes left in reported size, expected {1}",
                        Body.BaseStream.Length - startPosition, ComputeLocalSize()), ex);
            }

        }


        /// <summary>
        /// serialize the contents of the box to a writer.
        /// </summary>
        /// <param name="writer">MP4Writer to write to</param>
        protected override void WriteToInternal(MP4Writer writer)
        {
            //Version is always 1.
            Version = 1;
            base.WriteToInternal(writer);
            writer.WriteUInt64(CreationTime);
            writer.WriteUInt64(ModificationTime);
            writer.WriteUInt32(TrackId);
            writer.WriteUInt32(0);
            writer.WriteUInt64(Duration);
            for (int i = 0; i < 2; ++i)
            {
                writer.WriteUInt32(0);
            }
            writer.WriteUInt16(Layer);
            writer.WriteUInt16(AlternateGroup);
            writer.WriteUInt16(Volume);
            writer.WriteUInt16(0);

            foreach (Int32 value in Matrix)
            {
                writer.WriteInt32(value);
            }

            writer.WriteUInt32(Width);
            writer.WriteUInt32(Height);
        }

        /// <summary>
        /// Compute the size of the box.
        /// </summary>
        /// <returns>size of the box itself</returns>
        public override UInt64 ComputeSize()
        {
            return base.ComputeSize() + ComputeLocalSize();
        }

        protected static new UInt64 ComputeLocalSize()
        {
            return 8 //creation time 
                + 8 //modification time
                + 4 //track id
                + 4 //reserved
                + 8 //duration
                + 2 * 4 // reserved
                + 2 //layer
                + 2 //alternate group
                + 2 //volume
                + 2 // reserved bits.
                + 9 * 4 // size of matrix
                + 4 //width
                + 4; //height
        }


        #region equality methods
        /// <summary>
        /// Compare this box to another object.
        /// </summary>
        /// <param name="other">the object to compare equality against</param>
        /// <returns></returns>
        public override bool Equals(Object? other)
        {
            return this.Equals(other as tkhdBox);
        }

        /// <summary>
        /// Returns the hashcode of the object.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Box.Equals override. This is done so that programs which attempt to test equality
        /// using pointers to Box will can enjoy results from the fully derived
        /// equality implementation.
        /// </summary>
        /// <param name="other">The box to test equality against.</param>
        /// <returns>True if the this and the given box are equal.</returns>
        public override bool Equals(Box? other)
        {
            return this.Equals(other as tkhdBox);
        }

        /// <summary>
        /// FullBox.Equals override. This is done so that programs which attempt to test equality
        /// using pointers to Box will can enjoy results from the fully derived
        /// equality implementation.
        /// </summary>
        /// <param name="other">The box to test equality against.</param>
        /// <returns>True if the this and the given box are equal.</returns>
        public override bool Equals(FullBox? other)
        {
            return this.Equals(other as tkhdBox);
        }

        /// <summary>
        /// Compare two tkhdBoxes for equality.
        /// </summary>
        /// <param name="other">other tkhdBox to compare against</param>
        /// <returns></returns>
        public bool Equals(tkhdBox? other)
        {
            if (!base.Equals((FullBox?)other))
                return false;

            if (CreationTime != other.CreationTime)
            {
                return false;
            }

            if (ModificationTime != other.ModificationTime)
            {
                return false;
            }

            if (TrackId != other.TrackId)
            {
                return false;
            }

            if (Duration != other.Duration)
            {
                return false;
            }


            return true;
        }

        /// <summary>
        /// Compare two tkhdBox objects for equality.
        /// </summary>
        /// <param name="lhs">left hand side of ==</param>
        /// <param name="rhs">right hand side of ==</param>
        /// <returns></returns>
        public static bool operator ==(tkhdBox? lhs, tkhdBox? rhs)
        {
            // Check for null on left side. 
            if (Object.ReferenceEquals(lhs, null))
            {
                if (Object.ReferenceEquals(rhs, null))
                {
                    // null == null = true. 
                    return true;
                }

                // Only the left side is null. 
                return false;
            }
            return lhs.Equals(rhs);
        }

        /// <summary>
        /// Compare two tkhdBox objects for equality
        /// </summary>
        /// <param name="lhs">left side of !=</param>
        /// <param name="rhs">right side of !=</param>
        /// <returns>true if not equal else false.</returns>
        public static bool operator !=(tkhdBox? lhs, tkhdBox? rhs)
        {
            return !(lhs == rhs);
        }


        #endregion
    }
}
