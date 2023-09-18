using System.Diagnostics;
using System.Globalization;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// Represents a movie header box . ISO 14496-12 Sec:8.2.2
    /// </summary>
    public class trexBox : FullBox, IEquatable<trexBox>
    {
        /// <summary>
        /// Default constructor with a sequence number
        /// </summary>
        /// <param name="trackId">The track ID which this box refers to.</param>
        public trexBox(UInt32 trackId) :
            base(version: 0, flags: 0, boxtype: MP4BoxType.trex)
        {
            _trackId = trackId;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="box">the box to copy from</param>
        public trexBox(Box box) :
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.trex);
        }

        /// <summary>
        /// Get or set the track ID.
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
        /// The default sample description index for this track.
        /// </summary>
        public UInt32 DefaultSampleDescriptionIndex { get; private set; }

        /// <summary>
        /// The default sample duration for this track.
        /// </summary>
        public UInt32 DefaultSampleDuration { get; private set; }

        /// <summary>
        /// The default sample size for this track.
        /// </summary>
        public UInt32 DefaultSampleSize { get; private set; }

        /// <summary>
        /// The default sample flags for this track.
        /// </summary>
        public UInt32 DefaultSampleFlags { get; private set; }

        /// <summary>
        /// The track ID for this track.
        /// </summary>
        private UInt32 _trackId;


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
                _trackId = Body.ReadUInt32();
                DefaultSampleDescriptionIndex = Body.ReadUInt32();
                DefaultSampleDuration = Body.ReadUInt32();
                DefaultSampleSize = Body.ReadUInt32();
                DefaultSampleFlags = Body.ReadUInt32();
            }
            catch (EndOfStreamException ex)
            {
                // Reported error offset will point to start of box
                throw new MP4DeserializeException(TypeDescription, -BodyPreBytes, BodyInitialOffset,
                    String.Format(CultureInfo.InvariantCulture, "Could not read trex fields, only {0} bytes left in reported size, expected {1}",
                        Body.BaseStream.Length - startPosition, ComputeLocalSize()), ex);
            }

        }

        protected override void WriteToInternal(MP4Writer writer)
        {
            base.WriteToInternal(writer);

            writer.WriteUInt32(TrackId);
            writer.WriteUInt32(DefaultSampleDescriptionIndex);
            writer.WriteUInt32(DefaultSampleDuration);
            writer.WriteUInt32(DefaultSampleSize);
            writer.WriteUInt32(DefaultSampleFlags);
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
            return
                + 4 //track id
                + 4 //sample description index
                + 4 //duration
                + 4 //size
                + 4; //flags
        }


        #region equality methods
        /// <summary>
        /// Compare this box to another object.
        /// </summary>
        /// <param name="other">the object to compare equality against</param>
        /// <returns></returns>
        public override bool Equals(Object? other)
        {
            return this.Equals(other as trexBox);
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
            return this.Equals(other as trexBox);
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
            return this.Equals(other as trexBox);
        }

        /// <summary>
        /// Compare two trexBoxes for equality.
        /// </summary>
        /// <param name="other">other trexBox to compare against</param>
        /// <returns></returns>
        public bool Equals(trexBox? other)
        {
            if (!base.Equals((FullBox?)other))
                return false;

            if (TrackId != other.TrackId)
            {
                return false;
            }

            if (DefaultSampleDescriptionIndex != other.DefaultSampleDescriptionIndex)
            {
                return false;
            }

            if (DefaultSampleDuration != other.DefaultSampleDuration)
            {
                return false;
            }

            if (DefaultSampleSize != other.DefaultSampleSize)
            {
                return false;
            }

            if (DefaultSampleFlags != other.DefaultSampleFlags)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Compare two trexBox objects for equality.
        /// </summary>
        /// <param name="lhs">left hand side of ==</param>
        /// <param name="rhs">right hand side of ==</param>
        /// <returns></returns>
        public static bool operator ==(trexBox? lhs, trexBox? rhs)
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
        /// Compare two trexBox objects for equality
        /// </summary>
        /// <param name="lhs">left side of !=</param>
        /// <param name="rhs">right side of !=</param>
        /// <returns>true if not equal else false.</returns>
        public static bool operator !=(trexBox? lhs, trexBox? rhs)
        {
            return !(lhs == rhs);
        }


        #endregion
    }
}
