
using System.Diagnostics;
using System.Globalization;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// Represents a track fragment header box. ISO 14496-12 Sec:8.35.2
    /// </summary>
    public class tfhdBox : FullBox, IEquatable<tfhdBox>
    {
        /// <summary>
        /// Default constructor with a given track id.
        /// </summary>
        /// <param name="trackId"></param>
        public tfhdBox(UInt32 trackId) :
            base(version:0, flags:0, boxtype:MP4BoxType.tfhd)
        {
            _trackId = trackId;
            Size.Value = ComputeSize();
        }

        /// <summary>
        /// Copy constructor (deep copy, all properties are ValueType).
        /// </summary>
        /// <param name="other">The tfhdBox to copy.</param>
        public tfhdBox(tfhdBox other) :
            base (other.Version, other.Flags, boxtype: MP4BoxType.tfhd)
        {
            _trackId = other._trackId;
            _baseOffset = other._baseOffset;
            _sampleDescriptionIndex = other._sampleDescriptionIndex;
            _defaultSampleSize = other._defaultSampleSize;
            _defaultSampleDuration = other._defaultSampleDuration;
            _defaultSampleFlags = other._defaultSampleFlags;
            // Policy is for Dirty to be true, which base should already be doing
        }

        /// <summary>
        /// deserializing constructor.
        /// </summary>
        /// <param name="box"></param>
        public tfhdBox(Box box):
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.tfhd);
        }

        /// <summary>
        /// An enum to defined the various allowed flags for a tfhd box.
        /// </summary>
        [Flags]
        public enum TfhdFlags : uint
        {
            None = 0,
            BaseDataOffsetPresent = 0x1,
            SampleDescriptionIndexPresent = 0x2,
            DefaultSampleDurationPresent = 0x8,
            DefaultSampleSizePresent = 0x10,
            DefaultSampleFlagsPresent = 0x20,
            DurationIsEmpty = 0x010000
        }

        /// <summary>
        /// The track ID of the track present in the fragment.
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
        /// The offset of the moof box for this fragment in the file.
        /// </summary>
        public UInt64? BaseOffset
        {
            get
            {
                return _baseOffset;
            }
            set
            {
                _baseOffset = value;
                SetDirty();
            }
        }

        /// <summary>
        /// Optional sample description index for this track.
        /// </summary>
        public UInt32? SampleDescriptionIndex
        {
            get
            {
                return _sampleDescriptionIndex;
            }
            set
            {
                _sampleDescriptionIndex = value;
                SetDirty();
            }
        }

        /// <summary>
        /// Optional default sample size for this fragment.
        /// </summary>
        public UInt32? DefaultSampleSize
        {
            get
            {
                return _defaultSampleSize;
            }
            set
            {
                _defaultSampleSize = value;
                SetDirty();
            }
        }

        /// <summary>
        /// Optional default sample duration for this fragment.
        /// </summary>
        public UInt32? DefaultSampleDuration
        {
            get
            {
                return _defaultSampleDuration;
            }
            set
            {
                _defaultSampleDuration = value;
                SetDirty();
            }
        }

        /// <summary>
        /// Optional default sample flags for this fragment.
        /// </summary>
        public UInt32? DefaultSampleFlags
        {
            get
            {
                return _defaultSampleFlags;
            }
            set
            {
                _defaultSampleFlags = value;
                SetDirty();
            }
        }

        /// <summary>
        /// Parses the body of the box.
        /// </summary>
        protected override void ReadBody()
        {
            base.ReadBody();

            TfhdFlags flags = (TfhdFlags)(Flags);
            long startPosition = Body!.BaseStream.Position;

            try
            {
                _trackId = Body.ReadUInt32();

                if ((flags & TfhdFlags.BaseDataOffsetPresent) != 0)
                {
                    _baseOffset = Body.ReadUInt64();
                }

                if ((flags & TfhdFlags.SampleDescriptionIndexPresent) != 0)
                {
                    _sampleDescriptionIndex = Body.ReadUInt32();
                }

                if ((flags & TfhdFlags.DefaultSampleDurationPresent) != 0)
                {
                    _defaultSampleDuration = Body.ReadUInt32();
                }

                if ((flags & TfhdFlags.DefaultSampleSizePresent) != 0)
                {
                    _defaultSampleSize = Body.ReadUInt32();
                }

                if ((flags & TfhdFlags.DefaultSampleFlagsPresent) != 0)
                {
                    _defaultSampleFlags = Body.ReadUInt32();
                }
            }
            catch (EndOfStreamException ex)
            {
                // Reported error offset will point to start of box
                throw new MP4DeserializeException(TypeDescription, -BodyPreBytes, BodyInitialOffset,
                    String.Format(CultureInfo.InvariantCulture, "Could not read tfhd fields, only {0} bytes left in reported size, expected {1}",
                        Body.BaseStream.Length - startPosition, ComputeLocalSize()), ex);
            }

        }

        /// <summary>
        /// Serialize the box contents to the writer.
        /// </summary>
        /// <param name="writer">the MP4Writer to write to</param>
        protected override void WriteToInternal(MP4Writer writer)
        {
            if (Dirty)
            {
                //Update the flags.
                Flags = ComputeFlags();
            }

            base.WriteToInternal(writer);

            writer.WriteUInt32(_trackId);


            if (_baseOffset.HasValue)
            {
                writer.WriteUInt64(_baseOffset.Value);
            }

            if (_sampleDescriptionIndex.HasValue)
            {
                writer.WriteUInt32(_sampleDescriptionIndex.Value);
            }

            if (_defaultSampleDuration.HasValue)
            {
                writer.WriteUInt32(_defaultSampleDuration.Value);
            }

            if (_defaultSampleSize.HasValue)
            {
                writer.WriteUInt32(_defaultSampleSize.Value);
            }

            if (_defaultSampleFlags.HasValue)
            {
                writer.WriteUInt32(_defaultSampleFlags.Value);
            }
        }

        /// <summary>
        /// Computes the overall size of the box.
        /// </summary>
        /// <returns></returns>
        public override ulong ComputeSize()
        {
            return base.ComputeSize() + ComputeLocalSize();
        }

        /// <summary>
        /// Compute the size of box specific members.
        /// </summary>
        /// <returns></returns>
        protected new UInt64 ComputeLocalSize()
        {
            UInt64 thisSize = 4; //track id.

            if (_baseOffset.HasValue)
            {
                thisSize += 8;
            }

            if (_sampleDescriptionIndex.HasValue)
            {
                thisSize += 4;
            }

            if (_defaultSampleSize.HasValue)
            {
                thisSize += 4;
            }
            
            if (_defaultSampleDuration.HasValue)
            {
                thisSize += 4;
            }

            if (_defaultSampleFlags.HasValue)
            {
                thisSize += 4;
            }

            return thisSize;
        }

        /// <summary>
        /// Helper method to compute the flags based on values present.
        /// </summary>
        /// <returns>The flags to set.</returns>
        private UInt32 ComputeFlags()
        {
            TfhdFlags flags = TfhdFlags.None;

            if (_baseOffset.HasValue)
            {
                flags |= TfhdFlags.BaseDataOffsetPresent;
            }

            if (_sampleDescriptionIndex.HasValue)
            {
                flags |= TfhdFlags.SampleDescriptionIndexPresent;
            }

            if (_defaultSampleSize.HasValue)
            {
                flags |= TfhdFlags.DefaultSampleSizePresent;
            }

            if (_defaultSampleDuration.HasValue)
            {
                flags |= TfhdFlags.DefaultSampleDurationPresent;
            }

            if (_defaultSampleFlags.HasValue)
            {
                flags |= TfhdFlags.DefaultSampleFlagsPresent;
            }

            return (UInt32)flags;

        }
        /// <summary>
        /// Track id of the track
        /// </summary>
        private UInt32 _trackId;

        /// <summary>
        /// base offset of the moof box in the file.
        /// </summary>
        private UInt64? _baseOffset;

        /// <summary>
        /// The sample description index for this track.
        /// </summary>
        private UInt32? _sampleDescriptionIndex;

        /// <summary>
        /// default sample size.
        /// </summary>
        private UInt32? _defaultSampleSize;

        /// <summary>
        /// default sample duration.
        /// </summary>
        private UInt32? _defaultSampleDuration;

        /// <summary>
        /// default sample flags.
        /// </summary>
        private UInt32? _defaultSampleFlags;

        #region equality methods

        /// <summary>
        /// Compare this object with another object.
        /// </summary>
        /// <param name="other">other object to compare with.</param>
        /// <returns>true if both are equal else false</returns>
        public override bool Equals(Object? other)
        {
            return this.Equals(other as tfhdBox);
        }

        /// <summary>
        /// Returns the hashcode for this object.
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
            return this.Equals(other as tfhdBox);
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
            return this.Equals(other as tfhdBox);
        }

        /// <summary>
        /// Compare this object with another tfhdBox
        /// </summary>
        /// <param name="other">the other box to compare against</param>
        /// <returns>true if both are equal else false</returns>
        public bool Equals(tfhdBox? other)
        {
            if (!base.Equals((FullBox?)other))
            {
                return false;
            }

            //check the class specific members.
            if (_trackId != other._trackId ||
                _baseOffset != other._baseOffset ||
                _sampleDescriptionIndex != other._sampleDescriptionIndex ||
                _defaultSampleSize != other._defaultSampleSize ||
                _defaultSampleDuration != other._defaultSampleDuration ||
                _defaultSampleFlags != other._defaultSampleFlags
                )
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Compare two tfhdBox objects for equality.
        /// </summary>
        /// <param name="lhs">left side of ==</param>
        /// <param name="rhs">right side of ==</param>
        /// <returns>true if the two boxes are equal else false.</returns>
        public static bool operator ==(tfhdBox? lhs, tfhdBox? rhs)
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
        /// compares two entries using the Equals() method for equality.
        /// </summary>
        /// <param name="lhs">left side of !=</param>
        /// <param name="rhs">right side of !=</param>
        /// <returns>return true if two boxes are not equal else false.</returns>
        public static bool operator !=(tfhdBox? lhs, tfhdBox? rhs)
        {
            return !(lhs == rhs);
        }

        #endregion
    }
}
