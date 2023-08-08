
namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// This is the object model presentation of each entry in the 'trun' box
    /// as defined in ISO 14496-12.
    /// </summary>
    public class trunEntry : IEquatable<trunEntry>
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public trunEntry()
        {
            Dirty = true;
        }

        /// <summary>
        /// Deep Copy constructor (all members are value types, so there is no shallow copy).
        /// </summary>
        /// <param name="other">The instance to copy from.</param>
        public trunEntry(trunEntry other)
        {
            _sampleDuration = other._sampleDuration;
            _sampleSize = other._sampleSize;
            _sampleFlags = other._sampleFlags;
            _sampleCompositionOffset = other._sampleCompositionOffset;
            Dirty = true;
        }

        /// <summary>
        /// Optional sample duration of this entry.
        /// </summary>
        public UInt32? SampleDuration
        {
            get
            {
                return _sampleDuration;
            }
            set
            {
                _sampleDuration = value;
                Dirty = true;
            }
        }

        /// <summary>
        /// backing field for SampleDuration property.
        /// </summary>
        private UInt32? _sampleDuration;

        /// <summary>
        /// optional sample size of this entry.
        /// </summary>
        public UInt32? SampleSize
        {
            get
            {
                return _sampleSize;
            }
            set
            {
                _sampleSize = value;
                Dirty = true;
            }
        }

        /// <summary>
        /// backing field for SampleSize property.
        /// </summary>
        private UInt32? _sampleSize;

        /// <summary>
        /// Optional sample flags for this entry.
        /// </summary>
        public UInt32? SampleFlags
        {
            get
            {
                return _sampleFlags;
            }
            set
            {
                _sampleFlags = value;
                Dirty = true;
            }
        }
        
        /// <summary>
        /// backing field for SampleFlags property.
        /// </summary>
        private UInt32? _sampleFlags;


        /// <summary>
        /// optional sample composition offset for this entry.
        /// Note that spec says it is UInt32 if version = 0 and Int32 if version = 1 or higher.
        /// but we have content that uses it as int32 with version = 0. and offset > Int32.MaxValue is not 
        /// possible anyways. so always keep it Int32.
        /// </summary>
        public Int32? SampleCompositionOffset
        {
            get
            {
                return _sampleCompositionOffset;
            }
            set
            {
                _sampleCompositionOffset = value;
                Dirty = true;
            }
        }

        /// <summary>
        /// backing field for SampleCompositionOffset property.
        /// </summary>
        private Int32? _sampleCompositionOffset;

        /// <summary>
        /// True if this entry matches the on-disk representation, either because we deserialized and
        /// no further changes were made, or because we just saved to disk and no further changes were made.
        /// </summary>
        public bool Dirty { get; set; }


        /// <summary>
        /// Serialize the entry to the mp4 writer.
        /// </summary>
        /// <param name="writer">the MP4Writer to write to</param>
        public void WriteTo(MP4Writer writer)
        {
            if (null == writer)
            {
                throw new ArgumentNullException(nameof(writer));
            }

            if (_sampleDuration.HasValue)
            {
                writer.WriteUInt32(_sampleDuration.Value);
            }

            if (_sampleSize.HasValue)
            {
                writer.WriteUInt32(_sampleSize.Value);
            }

            if (_sampleFlags.HasValue)
            {
                writer.WriteUInt32(_sampleFlags.Value);
            }

            if (_sampleCompositionOffset.HasValue)
            {
                writer.WriteInt32(_sampleCompositionOffset.Value);
            }

            Dirty = false;
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
            return this.Equals(obj as trunEntry);
        }

        /// <summary>
        /// Implements IEquatable(trunEntry). This function is virtual and it is expected that
        /// derived classes will override it, so that programs which attempt to test equality
        /// using pointers to base classes will can enjoy results from the fully derived
        /// equality implementation.
        /// </summary>
        /// <param name="obj">The entry to test equality against.</param>
        /// <returns>True if the this and the given entry are equal.</returns>
        public virtual bool Equals(trunEntry? other)
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
            if (_sampleSize != other._sampleSize)
                return false;

            if (_sampleDuration != other._sampleDuration)
                return false;

            if (_sampleCompositionOffset != other._sampleCompositionOffset)
                return false;

            if (_sampleFlags != other._sampleFlags)
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
        public static bool operator ==(trunEntry? lhs, trunEntry? rhs)
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
        public static bool operator !=(trunEntry? lhs, trunEntry? rhs)
        {
            return !(lhs == rhs);
        }

        #endregion

    }
}
