using System.Globalization;
using System.Diagnostics;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// Implements the uuid box with extended type tfxd ("6d1d9b05-42d5-44e6-80e2-141daff757b2")
    /// </summary>
    public class tfxdBox : Box, IEquatable<tfxdBox>
    {
        /// <summary>
        /// Default constructor with media time and duration.
        /// </summary>
        /// <param name="fragTime"></param>
        /// <param name="fragDuration"></param>
        public tfxdBox(UInt64 fragTime, UInt64 fragDuration) :
            base(MP4BoxType.tfxd)
        {
            _fragTime = fragTime;
            _fragDuration = fragDuration;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="box">the box to copy from</param>
        public tfxdBox(Box box) :
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.uuid && box.ExtendedType == MP4BoxType.tfxd);
        }

        /// <summary>
        /// Parse the body which includes the version, flags, media time and duration.
        /// </summary>
        protected override void ReadBody()
        {
            base.ReadBody();
            long startPosition = Body!.BaseStream.Position;
            try
            {
                UInt32 dw = Body.ReadUInt32();

                var version = (Byte)(dw >> 24);

                if (version == 1)
                {
                    _fragTime = Body.ReadUInt64();
                    _fragDuration = Body.ReadUInt64();
                }
                else if (version == 0)
                {
                    _fragTime = Body.ReadUInt32();
                    _fragDuration = Body.ReadUInt32();
                }
                else
                {
                    throw new MP4DeserializeException(
                        TypeDescription,
                        -BodyPreBytes,
                        BodyInitialOffset,
                        String.Format(CultureInfo.InvariantCulture, "The version field for a tfxd box must be either 0 or 1. Instead found {0}",
                        version));
                }
            }
            catch (EndOfStreamException ex)
            {
                // Reported error offset will point to start of box
                throw new MP4DeserializeException(TypeDescription, -BodyPreBytes, BodyInitialOffset,
                    String.Format(CultureInfo.InvariantCulture, "Could not read fields for tfxd uuid box, only {0} bytes left in reported size, expected {1}",
                        Body.BaseStream.Length - startPosition, ComputeLocalSize()), ex);
            }

        }

        /// <summary>
        /// Calculate the current size of this box.
        /// </summary>
        /// <returns>The current size of this box, if it were to be written to disk now.</returns>
        public override UInt64 ComputeSize()
        {
            return base.ComputeSize() + ComputeLocalSize();
        }

        /// <summary>
        /// Calculates the current size of just this class (base classes excluded). This is called by
        /// ComputeSize().
        /// </summary>
        /// <returns>The current size of just the fields from this box.</returns>
        protected static new UInt64 ComputeLocalSize()
        {
            return 4   // bVersion + dwFlags(24bit)
                 + 8   // Media time in 64 bits
                 + 8;  // Duration in 64 bits
        }

        /// <summary>
        /// This function does the actual work of serializing this class to disk.
        /// </summary>
        /// <param name="writer">The MP4Writer to write to.</param>
        protected override void WriteToInternal(MP4Writer writer)
        {
            base.WriteToInternal(writer);

            // Always use version 1 and flag 0 when saving to output stream.
            UInt32 versionAndFlags = (UInt32)1 << 24;

            writer.WriteUInt32(versionAndFlags);

            writer.WriteUInt64(_fragTime);
            writer.WriteUInt64(_fragDuration);
        }

        public UInt64 _fragTime;

        /// <summary>
        /// Get or set the media time for the fragment.
        /// </summary>
        public UInt64 FragmentTime
        {
            get
            {
                return _fragTime;
            }
            set
            {
                _fragTime = value;
                SetDirty();
            }
        }


        public UInt64 _fragDuration;

        /// <summary>
        /// Get or set the fragment duration.
        /// </summary>
        public UInt64 FragmentDuration
        {
            get
            {
                return _fragDuration;
            }
            set
            {
                _fragDuration = value;
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
            return this.Equals(other as tfxdBox);
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
            return this.Equals(other as tfxdBox);
        }

        /// <summary>
        /// Implements IEquatable(tfxdBox). This function is virtual and it is expected that
        /// derived classes will override it, so that programs which attempt to test equality
        /// using pointers to base classes will can enjoy results from the fully derived
        /// equality implementation.
        /// </summary>
        /// <param name="obj">The box to test equality against.</param>
        /// <returns>True if the this and the given box are equal.</returns>
        public virtual bool Equals(tfxdBox? other)
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

            if (FragmentTime != other.FragmentTime)
                return false;

            if (FragmentDuration != other.FragmentDuration)
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
            return base.GetHashCode(); // All tfxdBox fields are mutable
        }

        /// <summary>
        ///  Override == operation (as recommended by MSDN).
        /// </summary>
        /// <param name="lhs">The box on the left-hand side of the ==.</param>
        /// <param name="rhs">The box on the right-hand side of the ==.</param>
        /// <returns>True if the two boxes are equal.</returns>
        public static bool operator ==(tfxdBox? lhs, tfxdBox? rhs)
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
        public static bool operator !=(tfxdBox? lhs, tfxdBox? rhs)
        {
            return !(lhs == rhs);
        }

        #endregion
    }
}
