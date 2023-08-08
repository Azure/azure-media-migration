
using System.Diagnostics;
using System.Globalization;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// Represents a Media Header Box ('mdhd'). ISO 14496-12 Sec:8.4.2.1.
    /// </summary>
    public class mdhdBox : FullBox, IEquatable<mdhdBox>
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public mdhdBox() :
            base(version: 1, flags: 0, boxtype: MP4BoxType.mdhd)
        {
            Size.Value = ComputeSize(); // Derived class is responsible for updating Size.Value after construction complete
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="box">the box to copy from</param>
        public mdhdBox(Box box) :
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.mdhd);
        }

        /// <summary>
        /// parse the body of box.
        /// </summary>
        protected override void ReadBody()
        {
            base.ReadBody();

            long startPosition = Body!.BaseStream.Position;

            // Check on version - we expect 0 or 1, reject anything else
            if (Version > 1)
            {
                // Reported error offset will point to Version byte in FullBox
                throw new MP4DeserializeException(TypeDescription, 0, BodyInitialOffset,
                    String.Format(CultureInfo.InvariantCulture, "Unexpected version: {0}. Expected 0 or 1!", Version));
            }

            try
            {
                if (1 == Version)
                {
                    _creationTime = Body.ReadUInt64();
                    _modificationTime = Body.ReadUInt64();
                    _timeScale = Body.ReadUInt32();
                    _duration = Body.ReadUInt64();
                }
                else
                {
                    Debug.Assert(0 == Version);
                    _creationTime = Body.ReadUInt32();
                    _modificationTime = Body.ReadUInt32();
                    _timeScale = Body.ReadUInt32();
                    _duration = Body.ReadUInt32();
                    SetDirty(); // We always write Version = 1 for now, TFS #713203
                }

                // If we wanted to, we could check for _timeScale == 0, but I am choosing not
                // to do so at this time just in case it doesn't end up getting used.

                // ISO-639-2/T language code, packed as difference between ASCII value and 0x60,
                // resulting in three lower-case letters.
                UInt16 languageInt = Body.ReadUInt16();
                if (0 != (languageInt & 0x8000))
                {
                    SetDirty(); // The current spec says pad == "0", so we cannot round-trip this - we're dirty.
                }

                var languageChars = new char[3];
                for (int i = languageChars.Length - 1; i >= 0; i--)
                {
                    char languageChar = (char)((languageInt & 0x1F) + 0x60);
                    languageChars[i] = languageChar;

                    // Advance variables
                    languageInt >>= 5;
                }
                _language = new string(languageChars);

                UInt16 pre_defined = Body.ReadUInt16();
                if (0 != pre_defined)
                {
                    SetDirty(); // The current spec says pre_defined == "0", so we cannot round-trip this - we're dirty.
                }
            }
            catch (EndOfStreamException ex)
            {
                // Reported error offset will point to start of box
                throw new MP4DeserializeException(TypeDescription, -BodyPreBytes, BodyInitialOffset,
                    String.Format(CultureInfo.InvariantCulture, "Could not read hdlr fields, only {0} bytes left in reported size, expected {1}",
                        Body.BaseStream.Length - startPosition, ComputeLocalSize()), ex);
            }
        }

        /// <summary>
        /// This function does the actual work of serializing this class to disk.
        /// </summary>
        /// <param name="writer">The MP4Writer to write to.</param>
        protected override void WriteToInternal(MP4Writer writer)
        {
            Version = 1; // We always write Version = 1 for now, TFS #713203

            UInt64 thisSize = ComputeLocalSize();

            // Write out the FullBox first
            base.WriteToInternal(writer);

            long startPosition = writer.BaseStream.Position;

            if (1 == Version)
            {
                writer.WriteUInt64(CreationTime);
                writer.WriteUInt64(ModificationTime);
                writer.WriteUInt32(TimeScale);
                writer.WriteUInt64(Duration);
            }
            else
            {
                Debug.Assert(0 == Version);
                writer.WriteUInt32((UInt32)CreationTime);
                writer.WriteUInt32((UInt32)ModificationTime);
                writer.WriteUInt32(TimeScale);
                writer.WriteUInt32((UInt32)Duration);
            }

            CheckLanguage(Language);
            UInt16 languageInt = 0;
            if (false == String.IsNullOrEmpty(Language))
            {
                for (int i = 0; i < Language.Length; i++)
                {
                    languageInt = (UInt16)((languageInt << 5) | ((Language[i] - 0x60) & 0x1F));
                }
            }
            writer.WriteUInt16(languageInt);

            writer.WriteUInt16(0); // pre_defined

            // Confirm that we wrote exactly the number of bytes we said we would
            Debug.Assert(writer.BaseStream.Position - startPosition == (long)thisSize ||
                         Stream.Null == writer.BaseStream);
        }

        /// <summary>
        /// Calculate the current size of this box.
        /// </summary>
        /// <returns>The current size of this box, if it were to be written to disk now.</returns>
        public override UInt64 ComputeSize()
        {
            UInt64 thisSize = ComputeLocalSize();
            UInt64 baseSize = base.ComputeSize();

            return thisSize + baseSize;
        }

        /// <summary>
        /// Calculates the current size of just this class (base classes excluded). This is called by
        /// ComputeSize().
        /// </summary>
        /// <returns>The current size of just the fields from this box.</returns>
        protected static new UInt64 ComputeLocalSize()
        {
            UInt64 commonSize = 4 +     // timescale
                                2 +     // pad + language
                                2;      // pre_defined

            // We always write Version = 1 for now, TFS #713203
            UInt64 sizeByVersion = 3 * 8; // creation_time, modification_time, duration @ 64-bit

            return commonSize + sizeByVersion;
        }

        /// <summary>
        /// Backing store for public CreationTime property.
        /// </summary>
        private UInt64 _creationTime;

        /// <summary>
        /// An integer that declares the creation time of the media in this track, in seconds since
        /// midnight, Jan 1, 1904 UTC (ISO 14496-12:2012 Sec:8.4.2.3).
        /// </summary>
        public UInt64 CreationTime
        {
            get
            {
                return _creationTime;
            }

            set
            {
                _creationTime = value;
                SetDirty();
            }
        }

        /// <summary>
        /// Backing store for public ModificationTime property.
        /// </summary>
        private UInt64 _modificationTime;

        /// <summary>
        /// An integer that declares the most recent time the media in this track was modified,
        /// in seconds since midnight, Jan 1, 1904 UTC (ISO 14496-12:2012 Sec:8.4.2.3).
        /// </summary>
        public UInt64 ModificationTime
        {
            get
            {
                return _modificationTime;
            }

            set
            {
                _modificationTime = value;
                SetDirty();
            }
        }

        /// <summary>
        /// Backing store for public TimeScale property.
        /// </summary>
        private UInt32 _timeScale = 1;

        /// <summary>
        /// An integer that specifies the time-scale for this media; this is the number of time units
        /// that pass in one second. For example, a time coordinate system that measures time in
        /// sixtieths of a second has a time scale of 60. (ISO 14496-12:2012 Sec:8.4.2.3).
        /// </summary>
        public UInt32 TimeScale
        {
            get
            {
                return _timeScale;
            }

            set
            {
                if (value <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(value),
                        String.Format(CultureInfo.InvariantCulture, "Mdhd.TimeScale must be greater than 0: {0}!", value));
                }

                _timeScale = value;
                SetDirty();
            }
        }

        /// <summary>
        /// Backing store for public Duration property.
        /// </summary>
        private UInt64 _duration;

        /// <summary>
        /// An integer that declares the duration of this media (in the scale of the timescale).
        /// If the duration cannot be determined then duration is set to all 1s.
        /// (ISO 14496-12:2012 Sec:8.4.2.3).
        /// </summary>
        public UInt64 Duration
        {
            get
            {
                return _duration;
            }

            set
            {
                _duration = value;
                SetDirty();
            }
        }

        /// <summary>
        /// Backing store for public Language property.
        /// </summary>
        private string _language = "und";

        /// <summary>
        /// Declares the language code for this media. See ISO 639-2/T for the set of three
        /// character codes. The result should be three lower-case letters.
        /// (ISO 14496-12:2012 Sec:8.4.2.3). This cannot be set to null or \0\0\0 and so
        /// the default value shall be 'und'.
        /// </summary>
        public string Language
        {
            get
            {
                return _language;
            }

            set
            {
                CheckLanguage(value);
                _language = value;
                SetDirty();
            }
        }

        private static void CheckLanguage(string language)
        {
            if (null == language)
            {
                throw new ArgumentException("Language cannot be set to null! Use 'und' instead.",
                    nameof(language));
            }

            if (3 != language.Length)
            {
                throw new ArgumentException(
                    String.Format(CultureInfo.InvariantCulture, "Language must be null or else 3 characters exactly: {0}!", language),
                    nameof(language));
            }

            for (int i = 0; i < language.Length; i++)
            {
                char languageChar = language[i];
                if (languageChar < 0x60)
                {
                    throw new ArgumentException(
                        String.Format(CultureInfo.InvariantCulture, "Language {0} has a character value less than 0x60 (0x{1:X}) at position {2}!",
                            language, (int)languageChar, i), nameof(language));
                }

                if (0 != ((languageChar - 0x60) & ~0x1F))
                {
                    throw new ArgumentException(
                        String.Format(CultureInfo.InvariantCulture, "Language {0} has a character value which cannot be represented in 5 bits (0x{1:X}) at position {2}!",
                            language, (int)languageChar, i), nameof(language));
                }
            }
        }


        #region Equality Methods

        //=====================================================================
        // Equality Methods
        //
        // In order to implement IEquatable<T> the way in which MSDN recommends,
        // it is unfortunately necessary to cut-and-paste this group of functions
        // into each derived class and to do a search-and-replace on the types
        // to match the derived class type. Generic classes do not work because
        // of the special rules around operator overloading and such.
        //
        // In addition to this cut-and-paste, search-and-replace, the derived
        // class should also override the Equals(base) method so that programs
        // which attempt to test equality using pointers to base classes will
        // be seeing results from the fully derived equality implementations.
        //
        // In other words, Box box = new DerivedBox(), box.Equals(box2). If
        // Equals(base) is not overridden, then the base implementation of
        // Equals(base) is used, which will not compare derived fields.
        //=====================================================================

        /// <summary>
        /// Object.Equals override.
        /// </summary>
        /// <param name="obj">The object to test equality against.</param>
        /// <returns>True if the this and the given object are equal.</returns>
        public override bool Equals(Object? obj)
        {
            return this.Equals(obj as mdhdBox);
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
            return this.Equals(other as mdhdBox);
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
            return this.Equals(other as mdhdBox);
        }

        /// <summary>
        /// Implements IEquatable(mdhdBox). This function is virtual and it is expected that
        /// derived classes will override it, so that programs which attempt to test equality
        /// using pointers to base classes will can enjoy results from the fully derived
        /// equality implementation.
        /// </summary>
        /// <param name="obj">The box to test equality against.</param>
        /// <returns>True if the this and the given box are equal.</returns>
        public virtual bool Equals(mdhdBox? other)
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

            // === First, check if base classes are equal ===

            if (false == base.Equals(other))
                return false;

            // === Now, compare the fields which are specific to this class ===

            if (CreationTime != other.CreationTime)
                return false;

            if (ModificationTime != other.ModificationTime)
                return false;

            if (TimeScale != other.TimeScale)
                return false;

            if (Duration != other.Duration)
                return false;

            if (Language != other.Language)
                return false;

            // If we reach this point, the fields all match
            return true;
        }

        /// <summary>
        /// Object.GetHashCode override. This must be done as a consequence of overriding
        /// Object.Equals.
        /// </summary>
        /// <returns>Hash code which will be match the hash code of an object which is equal.</returns>
        public override int GetHashCode()
        {
            return base.GetHashCode(); // All fields of mdhd are mutable
        }

        /// <summary>
        ///  Override == operation (as recommended by MSDN).
        /// </summary>
        /// <param name="lhs">The box on the left-hand side of the ==.</param>
        /// <param name="rhs">The box on the right-hand side of the ==.</param>
        /// <returns>True if the two boxes are equal.</returns>
        public static bool operator ==(mdhdBox? lhs, mdhdBox? rhs)
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
        /// <param name="lhs">The box on the left-hand side of the !=.</param>
        /// <param name="rhs">The box on the right-hand side of the !=.</param>
        /// <returns>True if the two boxes are equal.</returns>
        public static bool operator !=(mdhdBox? lhs, mdhdBox? rhs)
        {
            return !(lhs == rhs);
        }

        #endregion

    }
}
