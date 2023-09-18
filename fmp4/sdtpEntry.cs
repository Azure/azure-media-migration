namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// An enumeration to describe whether the sample depends on other samples.
    /// </summary>
    public enum SampleDependsOn : byte
    {
        Unknown = 0,
        DependsOnOthers = 1,
        NotDependsOnOthers = 2
    };

    /// <summary>
    /// An enumeration to describe whether other samples depends on this sample.
    /// </summary>
    public enum SampleIsDependedOn : byte
    {
        Unknown = 0,
        NotDisposable = 1,
        Disposable = 2
    };

    /// <summary>
    /// An enumeration to describe whether the sample has redundancy.
    /// </summary>
    public enum SampleRedundancy : byte
    {
        Unknown = 0, //it is unknown whether there is redundant coding in this sample;
        Redundant = 1, //there is redundant coding in this sample;
        NotRedundant = 2 //there is no redundant coding in this sample;
    };

    /// <summary>
    /// An enumeration to describe whether the sample is leading.
    /// </summary>
    public enum SampleIsLeading : byte
    {
        Unknown = 0,
        Leading = 1,
        NotLeading = 2,
        LeadingButDecodable = 3
    };

    /// <summary>
    /// A value type to represent a sample dependency info;
    /// </summary>
    public class sdtpEntry : IEquatable<sdtpEntry>
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public sdtpEntry()
        {
        }

        /// <summary>
        /// Public Constructor.
        /// </summary>
        /// <param name="dependencyValue"></param>
        public sdtpEntry(byte dependencyValue)
        {
            _value = dependencyValue;
        }

        /// <summary>
        /// Deep copy constructor (only contains value type, so everything is deep).
        /// </summary>
        /// <param name="other">The instance to copy from.</param>
        public sdtpEntry(sdtpEntry other)
        {
            _value = other._value;
        }

        /// <summary>
        /// The redundancy information for this entry.
        /// </summary>
        public SampleRedundancy Redundancy
        {
            get
            {
                return (SampleRedundancy)(_value & 0x3);
            }
            set
            {
                //clear the old bits and set the new bits.
                _value &= 0xFC;
                _value |= (byte)value;
            }
        }

        /// <summary>
        /// Indicates whether other samples depend on this sample.
        /// </summary>
        public SampleIsDependedOn DependedOn
        {
            get
            {
                return (SampleIsDependedOn)((_value & 0xC) >> 2);
            }
            set
            {
                _value &= 0XF3;
                _value |= (byte)((byte)value << 2);
            }
        }

        /// <summary>
        /// Indicates whether this sample depends on others or not.
        /// </summary>
        public SampleDependsOn DependsOn
        {
            get
            {
                return (SampleDependsOn)((_value & 0x30) >> 4);
            }
            set
            {
                _value &= 0XCF;
                _value |= (byte)((byte)value << 4);
            }
        }


        /// <summary>
        /// Indicates whether this sample is leading or not.
        /// </summary>
        public SampleIsLeading Leading
        {
            get
            {
                return (SampleIsLeading)((_value & 0xC0) >> 6);
            }
            set
            {
                _value &= 0X3F;
                _value |= (byte)((byte)value << 6);
            }
        }
        /// <summary>
        /// Returns the byte value of dependency.
        /// </summary>
        public byte Value
        {
            get
            {
                return _value;
            }
        }


        /// <summary>
        /// The value of the dependency information.
        /// </summary>
        private byte _value;

        #region equality methods

        /// <summary>
        /// Compares this entry with another object.
        /// </summary>
        /// <param name="obj"></param>
        /// <returns></returns>
        public override bool Equals(Object? obj)
        {
            return this.Equals(obj as sdtpEntry);
        }

        /// <summary>
        /// Compares this sdtpEntry with another sdtpEntry.
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public bool Equals(sdtpEntry? other)
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

            return (_value == other._value);
        }

        /// <summary>
        /// Returns the hash code for this object.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return _value;
        }

        /// <summary>
        /// Compare two sdtpEntries for equality.
        /// </summary>
        /// <param name="lhs">left side of ==</param>
        /// <param name="rhs">right side of ==</param>
        /// <returns>true if the two entries are equal else false.</returns>
        public static bool operator ==(sdtpEntry? lhs, sdtpEntry? rhs)
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
        /// compares two entries using the Equals() method for equality.
        /// </summary>
        /// <param name="lhs">left side of !=</param>
        /// <param name="rhs">right side of !=</param>
        /// <returns>return true if two entries are not equal else false.</returns>
        public static bool operator !=(sdtpEntry? lhs, sdtpEntry? rhs)
        {
            return !(lhs == rhs);
        }

        #endregion

    };

}
