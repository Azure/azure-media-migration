
using System.Diagnostics;
using System.Globalization;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// Represents a movie header box . ISO 14496-12 Sec:8.2.2
    /// </summary>
    public class mvhdBox : FullBox, IEquatable<mvhdBox>
    {
        /// <summary>
        /// Default constructor 
        /// </summary>
        public mvhdBox() :
            base(version:0, flags:0, boxtype:MP4BoxType.mvhd)
        {
            _nextTrackId = UInt32.MaxValue;
            Matrix = new Int32[9];
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="box">the box to copy from</param>
        public mvhdBox(Box box):
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.mvhd);
            Matrix = new Int32[9];
        }

        /// <summary>
        /// Get or set the next available track ID.
        /// </summary>
        public UInt32 NextTrackId
        {
            get
            {
                return _nextTrackId;
            }
            set
            {
                _nextTrackId = value;
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
        /// TimeScale of the track.
        /// </summary>
        public UInt32 TimeScale { get; private set; }

        /// <summary>
        /// Duration of the track in the timescale.
        /// </summary>
        public UInt64 Duration { get; private set; }

        /// <summary>
        /// The rate of video playback.
        /// </summary>
        public UInt32 Rate { get; private set; }

        /// <summary>
        /// Volume for an audio track.
        /// </summary>
        public UInt16 Volume { get; private set; }

        /// <summary>
        /// Transformation matrix to apply to a video.
        /// </summary>
        public Int32[] Matrix { get; private set; }

        /// <summary>
        /// The next available track ID.
        /// </summary>
        private UInt32 _nextTrackId;

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

                if (Version == 1)
                {
                    CreationTime = Body.ReadUInt64();
                    ModificationTime = Body.ReadUInt64();
                    TimeScale = Body.ReadUInt32();
                    Duration = Body.ReadUInt64();
                }
                else if (Version == 0)
                {
                    CreationTime = Body.ReadUInt32();
                    ModificationTime = Body.ReadUInt32();
                    TimeScale = Body.ReadUInt32();
                    Duration = Body.ReadUInt32();
                    SetDirty(); //We can't round trip version 0.
                }
                else
                {
                    throw new MP4DeserializeException(
                        TypeDescription, 
                        -BodyPreBytes,
                        BodyInitialOffset,
                        String.Format(CultureInfo.InvariantCulture, "The version field for an mvhd box must be either 0 or 1. Instead found {0}",
                        Version));
                }

                Rate = Body.ReadUInt32();
                Volume = Body.ReadUInt16();

                UInt32 reserved = Body.ReadUInt16();
                Debug.Assert(reserved == 0);

                for (int i = 0; i < 2; ++i)
                {
                    reserved = Body.ReadUInt32();
                    Debug.Assert(reserved == 0);
                }

                Matrix = new Int32[9];
                for (int i = 0; i < Matrix.Length; ++i)
                {
                    Matrix[i] = Body.ReadInt32();
                }

                for (int i = 0; i < 6; ++i)
                {
                    reserved = Body.ReadUInt32();
                    Debug.Assert(reserved == 0);
                }

                _nextTrackId = Body.ReadUInt32();
            }
            catch (EndOfStreamException ex)
            {
                // Reported error offset will point to start of box
                throw new MP4DeserializeException(TypeDescription, -BodyPreBytes, BodyInitialOffset,
                    String.Format(CultureInfo.InvariantCulture, "Could not read mvhd fields, only {0} bytes left in reported size, expected {1}",
                        Body.BaseStream.Length - startPosition, ComputeLocalSize()), ex);
            }

        }

        /// <summary>
        /// serialize the contents of the box
        /// </summary>
        /// <param name="writer">MP4Writer to write to</param>
        protected override void WriteToInternal(MP4Writer writer)
        {
            //Version is always 1.
            Version = 1;
            base.WriteToInternal(writer);
            writer.WriteUInt64(CreationTime);
            writer.WriteUInt64(ModificationTime);
            writer.WriteUInt32(TimeScale);
            writer.WriteUInt64(Duration);
            writer.WriteUInt32(Rate);
            writer.WriteUInt16(Volume);
            writer.WriteUInt16(0);
            for(int i =0; i < 2; ++i)
            {
                writer.WriteUInt32(0);
            }

            foreach(Int32 value in Matrix)
            {
                writer.WriteInt32(value);
            }

            for (int i = 0; i < 6; ++i)
            {
                writer.WriteUInt32(0);
            }

            writer.WriteUInt32(_nextTrackId);
        }

        /// <summary>
        /// Compute the size of the box.
        /// </summary>
        /// <returns>size of the box itself</returns>
        public override UInt64 ComputeSize()
        {
            return base.ComputeSize() + ComputeLocalSize();
        }

        /// <summary>
        /// computes the size of just box specific content.
        /// </summary>
        /// <returns></returns>
        protected static new UInt64 ComputeLocalSize()
        {
            return 8 //creation time 
                + 8 //modification time
                + 4 //timescale
                + 8 //duration
                + 4 //rate
                + 2 //volume
                + 2 + 4 * 2 // reserved bits.
                + 9 * 4 // size of matrix
                + 4 * 6 //reserved
                + 4;
        }

        #region equality methods
        /// <summary>
        /// Compare this box to another object.
        /// </summary>
        /// <param name="other">the object to compare equality against</param>
        /// <returns></returns>
        public override bool Equals(Object? other)
        {
            return this.Equals(other as mvhdBox);
        }

        /// <summary>
        /// Returns the hash code of the object.
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
            return this.Equals(other as mvhdBox);
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
            return this.Equals(other as mvhdBox);
        }

        /// <summary>
        /// Compare two mvhdBoxes for equality.
        /// </summary>
        /// <param name="other">other mvhdBox to compare against</param>
        /// <returns></returns>
        public bool Equals(mvhdBox? other)
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

            if (TimeScale != other.TimeScale)
            {
                return false;
            }

            if (Duration != other.Duration)
            {
                return false;
            }

            if (Rate != other.Rate)
            {
                return false;
            }

            if (Volume != other.Volume)
            {
                return false;
            }

            if (_nextTrackId != other._nextTrackId)
            {
                return false;
            }

            if (!Matrix.SequenceEqual(other.Matrix))
            {
                return false;
            }

            return true;

        }

        /// <summary>
        /// Compare two mvhdBox objects for equality.
        /// </summary>
        /// <param name="lhs">left hand side of ==</param>
        /// <param name="rhs">right hand side of ==</param>
        /// <returns></returns>
        public static bool operator ==(mvhdBox? lhs, mvhdBox? rhs)
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
        /// Compare two mvhdBox objects for equality
        /// </summary>
        /// <param name="lhs">left side of !=</param>
        /// <param name="rhs">right side of !=</param>
        /// <returns>true if not equal else false.</returns>
        public static bool operator !=(mvhdBox? lhs, mvhdBox? rhs)
        {
            return !(lhs == rhs);
        }


        #endregion
    }
}
