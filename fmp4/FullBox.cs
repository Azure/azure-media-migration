
using System.Globalization;
using System.Diagnostics;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// Implements the FullBox object as defined in 14496-12.
    /// </summary>
    public class FullBox : Box, IEquatable<FullBox>
    {
        /// <summary>
        /// Constructor for compact type. Only used by derived classes.
        /// </summary>
        /// <param name="version">The version of this FullBox.</param>
        /// <param name="flags">The flags for this FullBox.</param>
        /// <param name="boxtype">The compact type.</param>
        protected FullBox(Byte version,
                          UInt32 flags,
                          UInt32 boxtype)
            : base(boxtype)
        {
            Version = version;
            Flags = flags;
        }

        /// <summary>
        /// Constructor for extended type ('uuid' box). Only used by derived classes.
        /// </summary>
        /// <param name="version">The version of this FullBox.</param>
        /// <param name="flags">The flags for this FullBox.</param>
        /// <param name="extendedType">The extended type.</param>
        protected FullBox(Byte version,
                          UInt32 flags,
                          Guid extendedType)
            : base(extendedType)
        {
            Version = version;
            Flags = flags;
        }

        /// <summary>
        /// Deserializing constructor.
        /// </summary>
        /// <param name="box">The box which contains the bytes we should deserialize from.</param>
        protected FullBox(Box box)
            : base(box)
        {
        }

        /// <summary>
        /// Parse the body which includes the version and flags.
        /// </summary>
        protected override void ReadBody()
        {
            base.ReadBody();
            long startPosition = Body!.BaseStream.Position;
            try
            {
                UInt32 dw = Body.ReadUInt32();
                _version = (Byte)(dw >> 24);
                _flags = (dw & 0x00FFFFFF);
            }
            catch (EndOfStreamException ex)
            {
                // Reported error offset will point to start of box
                throw new MP4DeserializeException(TypeDescription, -BodyPreBytes, BodyInitialOffset,
                    String.Format(CultureInfo.InvariantCulture, "Could not read Version and Flags for FullBox, only {0} bytes left in reported size, expected {1}",
                        Body.BaseStream.Length - startPosition, ComputeLocalSize()), ex);
            }

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
            return 4; // bVersion + dwFlags(24bit)
        }

        /// <summary>
        /// This function does the actual work of serializing this class to disk.
        /// </summary>
        /// <param name="writer">The MP4Writer to write to.</param>
        protected override void WriteToInternal(MP4Writer writer)
        {
            base.WriteToInternal(writer);

            UInt64 thisSize = ComputeLocalSize();
            long startPosition = writer.BaseStream.Position;
            UInt32 versionAndFlags = ((UInt32)Version << 24) | (Flags & 0x00FFFFFF);
            writer.WriteUInt32(versionAndFlags);

            // Confirm that we wrote exactly the number of bytes we said we would
            Debug.Assert(writer.BaseStream.Position - startPosition == (long)thisSize ||
                         Stream.Null == writer.BaseStream);
        }


        /// <summary>
        /// Backing store for Version.
        /// </summary>
        private Byte _version;

        /// <summary>
        /// The version field of this FullBox, as per 14496-12.
        /// </summary>
        public Byte Version
        {
            get
            {
                return _version;
            }
            set
            {
                _version = value;
                SetDirty();
            }
        }

        /// <summary>
        /// Backing store for Flags.
        /// </summary>
        private UInt32 _flags;

        /// <summary>
        /// The flags field of this FullBox, as per 14496-12.
        /// </summary>
        public UInt32 Flags
        {
            get
            {
                return _flags;
            }
            protected set
            {
                // Check if flags fit within 24 bits
                if (0 != (value & ~0x00FFFFFF))
                {
                    throw new ArgumentOutOfRangeException(nameof(value), "FullBox flags must fit within 24 bits");
                }

                _flags = value;
                SetDirty();
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
        /// <param name="other">The object to test equality against.</param>
        /// <returns>True if the this and the given object are equal.</returns>
        public override bool Equals(Object? other)
        {
            return this.Equals(other as FullBox);
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
            return this.Equals(other as FullBox);
        }

        /// <summary>
        /// Implements IEquatable(FullBox). This function is virtual and it is expected that
        /// derived classes will override it, so that programs which attempt to test equality
        /// using pointers to base classes will can enjoy results from the fully derived
        /// equality implementation.
        /// </summary>
        /// <param name="obj">The box to test equality against.</param>
        /// <returns>True if the this and the given box are equal.</returns>
        public virtual bool Equals(FullBox? other)
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

            if (Version != other.Version)
                return false;

            if (Flags != other.Flags)
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
            return base.GetHashCode(); // All FullBox fields are mutable
        }

        /// <summary>
        ///  Override == operation (as recommended by MSDN).
        /// </summary>
        /// <param name="lhs">The box on the left-hand side of the ==.</param>
        /// <param name="rhs">The box on the right-hand side of the ==.</param>
        /// <returns>True if the two boxes are equal.</returns>
        public static bool operator ==(FullBox? lhs, FullBox? rhs)
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
        public static bool operator !=(FullBox? lhs, FullBox? rhs)
        {
            return !(lhs == rhs);
        }

        #endregion

    }
}
