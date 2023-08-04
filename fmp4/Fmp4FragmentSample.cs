

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// The data needed to interpret a sample in Fmp4Fragment are scattered across a number of boxes.
    /// The sample size/duration/flags can be explicitly set in trunEntry, or defaulted in tfhd/trun (first sample flags only).
    /// The purpose of this class is to handle these complexities so that the caller can query sample
    /// size or duration without worrying about the details. For sample modification, this class returns
    /// properties which, if modified, modify the underlying Fmp4Fragment entries directly, such that
    /// serializing the Fmp4Fragment will contain the modifications. Because of this intuitive arrangement,
    /// tradeoffs had to be made which restrict the range of modifications. It is not possible to resize
    /// the sample data, and it is not possible to add SdtpEntry flags if the sdtp box was not already present,
    /// because such changes cannot be immediately propagated to Fmp4Fragment. Such changes are therefore
    /// only possible by creating the desired Fmp4FragmentSample enumeration with resized sample data and/or
    /// sdtp flags, and calling Fmp4Fragment.SetSamples.
    /// </summary>
    public class Fmp4FragmentSample : IEquatable<Fmp4FragmentSample>
    {
        /// <summary>
        /// Public constructor.
        /// </summary>
        /// <param name="tfhd">Pointer to tfhd (required). Used to read default values. The tfhd will not be modified.</param>
        /// <param name="trun">Pointer to trunEntry (required). Used to read and write explicit values.
        /// If this sample reflects an existing sample in Fmp4Fragment, this trunEntry should point to
        /// the underlying trunEntry in Fmp4Fragment.Header.Track.TrackRun.Entries[i] so that user writes
        /// go directly to the fragment and will be written on the next Fmp4Fragment.WriteTo.</param>
        /// <param name="sampleData">Byte array containing the sample data. If this sample reflects an
        /// existing sample in Fmp4Fragment, this should be Fmp4Fragment.Data.SampleData so that user
        /// writes go directly into the fragment and will be written on next Fmp4Fragment.WriteTo.
        /// The size of the sample is inferred from the given tfhd and trunEntry.</param>
        /// <param name="offset">The offset to start from in sampleData.</param>
        /// <param name="sdtp">Pointer to sdtpEntry</param>
        /// <param name="firstSampleFlags">If this is the first sample, then this should be the value
        /// of trunBox.FirstSampleFlags (which may be null). Otherwise this should be null.</param>
        public Fmp4FragmentSample(tfhdBox tfhd, trunEntry trunEntry, byte[]? sampleData, int offset, sdtpEntry? sdtpEntry, UInt32? firstSampleFlags)
        {
            _tfhd = tfhd;
            _firstSampleFlags = firstSampleFlags;
            _trunEntry = trunEntry;
            _sdtpEntry = sdtpEntry;

            if (sampleData != null)
            { 
                _arraySegment = new ArraySegment<byte>(sampleData, offset, (int)Size);                
            }
        }

        /// <summary>
        /// Default constructor. It is private because this class is immutable.
        /// </summary>
#pragma warning disable CS8618
        private Fmp4FragmentSample()
        {
        }
#pragma warning restore CS8618

        /// <summary>
        /// Deep Copy constructor.
        /// </summary>
        /// <param name="other"></param>
        public Fmp4FragmentSample(Fmp4FragmentSample other)
        {
            _tfhd = new tfhdBox(other._tfhd);
            _firstSampleFlags = other._firstSampleFlags;
            _trunEntry = new trunEntry(other._trunEntry);
            if (other._arraySegment != null)
            {
                _arraySegment = new ArraySegment<byte>(other._arraySegment.Value.ToArray());
            }

            if (other._sdtpEntry != null)
            {
                _sdtpEntry = new sdtpEntry(other._sdtpEntry.Value);
            }
        }

        /// <summary>
        /// Private pointer to track header, for obtaining sample defaults
        /// during a read (it is never written to).
        /// </summary>
        private tfhdBox _tfhd;

        /// <summary>
        /// If this is the first sample, then this the value of trunBox.FirstSampleFlags (which can be null).
        /// </summary>
        private UInt32? _firstSampleFlags;

        /// <summary>
        /// Private backing store for public TrunEntry property.
        /// </summary>
        private trunEntry _trunEntry;

        /// <summary>
        /// The trunEntry for this sample. This property may be freely written to,
        /// but when reading sample attributes (size, duration, flags), it is best
        /// to use the corresponding property, which will substitute the default value
        /// if trunEntry does not have the attribute.
        /// </summary>
        public trunEntry TrunEntry
        {
            get
            {
                return _trunEntry;
            }
        }

        /// <summary>
        /// Private backing store for public SdtpEntry property.
        /// </summary>
        private sdtpEntry? _sdtpEntry;

        /// <summary>
        /// The sdtpEntry for this sample. This is optional, and can be null. If null, and caller
        /// wishes to add, then caller must resort to the Fmp4Fragment.SetSamples method and create
        /// sdtpEntry for all samples.
        /// </summary>
        public sdtpEntry? SdtpEntry => _sdtpEntry;

        /// <summary>
        /// An ArraySegment of the original larger mdatBox.SampleData. Using ArraySegment allows us to cheaply offer
        /// in-place write capabilities which will modify mdatBox.SampleData directly, without allowing changes
        /// to the number of bytes used in this sample.
        /// </summary>
        private ArraySegment<byte>? _arraySegment;

        /// <summary>
        /// The sample data. The bytes may be modified and will change Fmp4Fragment.Data.SampleData directly,
        /// however, the number of bytes may not be changed. To change that, the caller must call Fmp4Fragment.SetSamples.
        /// </summary>
        public IList<byte>? Data => _arraySegment as IList<byte>;

        /// <summary>
        /// The sample duration, either from trunEntry or the default from tfhd.
        /// </summary>
        public UInt32 Duration
        {
            get
            {
                if (TrunEntry.SampleDuration.HasValue) // TrunEntry is not permitted to be null
                {
                    return TrunEntry.SampleDuration.Value;
                }

                if (_tfhd.DefaultSampleDuration.HasValue) // _tfhd is not permitted to be null
                {
                    return _tfhd.DefaultSampleDuration.Value;
                }

                throw new InvalidOperationException("Sample has no duration (neither explicit nor default)!");
            }
        }

        /// <summary>
        /// The sample size, either from trunEntry or the default from tfhd.
        /// </summary>
        public UInt32 Size
        {
            get
            {
                if (TrunEntry.SampleSize.HasValue) // TrunEntry is not permitted to be null
                {
                    return TrunEntry.SampleSize.Value;
                }

                if (_tfhd.DefaultSampleSize.HasValue) // _tfhd is not permitted to be null
                {
                    return _tfhd.DefaultSampleSize.Value;
                }

                throw new InvalidOperationException("Sample has no size (neither explicit nor default)!");
            }
        }

        /// <summary>
        /// The sample flags, either from trunEntry, trun.FirstSampleFlags, or tfhd.
        /// </summary>
        public UInt32 Flags
        {
            get
            {
                if (TrunEntry.SampleFlags.HasValue) // TrunEntry is not permitted to be null
                {
                    return TrunEntry.SampleFlags.Value;
                }

                if (_firstSampleFlags.HasValue)
                {
                    return _firstSampleFlags.Value;
                }

                if (_tfhd.DefaultSampleFlags.HasValue) // _tfhd is not permitted to be null
                {
                    return _tfhd.DefaultSampleFlags.Value;
                }

                // Some content, such as BigBuckFragBlob, have no flags (neither explicit nor default).
                // Rather than throw an exception, as earlier versions did, just return 0.
                return 0;
            }
        }

        #region Equality Methods

        //=====================================================================
        // Equality Methods
        //
        //=====================================================================

        /// <summary>
        /// Object.Equals override.
        /// </summary>
        /// <param name="obj">The object to test equality against.</param>
        /// <returns>True if the this and the given object are equal.</returns>
        public override bool Equals(Object? obj)
        {
            return this.Equals(obj as Fmp4FragmentSample);
        }

        /// <summary>
        /// Implements IEquatable(Fmp4FragmentSample). This function is virtual and it is expected that
        /// derived classes will override it, so that programs which attempt to test equality
        /// using pointers to base classes will can enjoy results from the fully derived
        /// equality implementation.
        /// </summary>
        /// <param name="obj">The entry to test equality against.</param>
        /// <returns>True if the this and the given entry are equal.</returns>
        public virtual bool Equals(Fmp4FragmentSample? other)
        {
            // If parameter is null, return false. 
            if (Object.ReferenceEquals(other, null))
            {
                return false;
            }

            // Optimization for a common success case. 
            if (Object.ReferenceEquals(this, other))
            {
                return true;
            }

            // If run-time types are not exactly the same, return false. 
            if (this.GetType() != other.GetType())
                return false;

            // Compare fields
            if (_tfhd != other._tfhd)
                return false;

            if (_firstSampleFlags != other._firstSampleFlags)
                return false;

            if (_trunEntry != other._trunEntry)
                return false;

            if ((Data == null && other.Data != null) || (Data != null && other.Data == null))
            {
                return false;
            }

            if (Data != null && other.Data != null && false == Enumerable.SequenceEqual(Data, other.Data))
                return false;

            if (_sdtpEntry != other._sdtpEntry)
                return false;

            // If we reach this point, the fields all match
            return true;
        }

        /// <summary>
        /// Object.GetHashCode override. This must be done as a consequence of overridding
        /// Object.Equals.
        /// </summary>
        /// <returns>Hash code which will be match the hash code of an object which is equal.</returns>
        public override int GetHashCode()
        {
            return GetType().GetHashCode();
        }

        /// <summary>
        ///  Override == operation (as recommended by MSDN).
        /// </summary>
        /// <param name="lhs">The entry on the left-hand side of the ==.</param>
        /// <param name="rhs">The entry on the right-hand side of the ==.</param>
        /// <returns>True if the two entries are equal.</returns>
        public static bool operator ==(Fmp4FragmentSample? lhs, Fmp4FragmentSample? rhs)
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
            // Equals handles case of null on right side. 
            return lhs.Equals(rhs);
        }

        /// <summary>
        ///  Override != operation (as recommended by MSDN).
        /// </summary>
        /// <param name="lhs">The entry on the left-hand side of the !=.</param>
        /// <param name="rhs">The entry on the right-hand side of the !=.</param>
        /// <returns>True if the two entries are equal.</returns>
        public static bool operator !=(Fmp4FragmentSample? lhs, Fmp4FragmentSample? rhs)
        {
            return !(lhs == rhs);
        }

        #endregion

    }
}
