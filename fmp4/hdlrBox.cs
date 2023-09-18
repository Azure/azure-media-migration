using System.Globalization;
using System.Diagnostics;
using System.Text;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// Represents a Handler Reference Box ('hdlr'). ISO 14496-12 Sec:8.4.3
    /// </summary>
    public class hdlrBox : FullBox, IEquatable<hdlrBox>
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public hdlrBox() :
            base(version: 0, flags: 0, boxtype: MP4BoxType.hdlr)
        {
            Size.Value = ComputeSize(); // Derived class is responsible for updating Size.Value after construction complete
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="box">the box to copy from</param>
        public hdlrBox(Box box) :
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.hdlr);
        }

        /// <summary>
        /// parse the body of box.
        /// </summary>
        protected override void ReadBody()
        {
            long startOfFullBox = Body!.BaseStream.Position;
            base.ReadBody();
            long startPosition = Body.BaseStream.Position;
            try
            {
                UInt32 pre_defined = Body.ReadUInt32();
                if (0 != pre_defined)
                {
                    SetDirty();
                }

                _handlerType = Body.ReadUInt32();

                // Read past the three reserved ints
                for (int i = 0; i < 3; i++)
                {
                    UInt32 reserved = Body.ReadUInt32();
                    if (0 != reserved)
                    {
                        SetDirty();
                    }
                }

                // Read the remaining bytes into a byte buffer and convert from UTF-8 to string
                long endOfBox = startOfFullBox + (long)Size.Value - BodyPreBytes;
                int cbBytesToRead = (int)(endOfBox - Body.BaseStream.Position);
                if (cbBytesToRead > 0)
                {
                    byte[] strBytes = Body.ReadBytes(cbBytesToRead);

                    // Check if last character was a null-terminator. If so, just trim that one.
                    // It is entirely possible for multiple null-terms to exist in name, we won't touch.
                    int cchString;
                    if ('\0' == strBytes[strBytes.Length - 1])
                    {
                        cchString = strBytes.Length - 1;
                    }
                    else
                    {
                        cchString = strBytes.Length;
                    }
                    _name = Encoding.UTF8.GetString(strBytes, 0, cchString);

                    // Check for roundtrip. Might not round-trip if, for instance, early or multiple null-terminator,
                    // or lack of null-terminator.
                    int cbBytesToWrite = Encoding.UTF8.GetByteCount(_name) + 1; // We will always output *one* null-terminator
                    if (cbBytesToRead != cbBytesToWrite)
                    {
                        SetDirty();
                    }
                }
                else
                {
                    Debug.Assert(null == Name);
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
            UInt64 thisSize = ComputeLocalSize();

            // Write out the FullBox first
            base.WriteToInternal(writer);

            long startPosition = writer.BaseStream.Position;

            writer.WriteUInt32(0);              // pre_defined
            writer.WriteUInt32(_handlerType);   // handler_type
            for (int i = 0; i < 3; i++)
            {
                writer.WriteUInt32(0);          // reserved
            }

            // If Name is null, we won't even write out the null terminator
            if (null != Name)
            {
                byte[] strBytes = Encoding.UTF8.GetBytes(Name);
                writer.Write(strBytes);             // name
                writer.Write((byte)0);              // null-terminator
            }

            // Confirm that we wrote exactly the number of bytes we said we would
            Debug.Assert(writer.BaseStream.Position - startPosition == (long)thisSize ||
                         Stream.Null == writer.BaseStream);
        }

        /// <summary>
        /// Backing store for public HandlerType property.
        /// </summary>
        private UInt32 _handlerType;

        /// <summary>
        /// The handler_type field of this 'hdlr' box.
        /// </summary>
        public UInt32 HandlerType
        {
            get
            {
                return _handlerType;
            }
            set
            {
                _handlerType = value;
                SetDirty();
            }
        }

        /// <summary>
        /// Backing store for the public Name property.
        /// </summary>
        private string? _name;

        /// <summary>
        /// The name field of this 'hdlr' box.
        /// </summary>
        public string? Name
        {
            get
            {
                return _name;
            }
            set
            {
                _name = value;
                SetDirty();
            }
        }

        /// <summary>
        /// A list of known values for HandlerType.
        /// </summary>
        public static class HandlerTypes
        {
            // The following definitions follow spec casing, and thus generate CA1709 - suppress in GlobalSuppressions.cs to retain readability
            public const UInt32 vide = 0x76696465;  // 'vide', Video track
            public const UInt32 soun = 0x736F756E;  // 'soun', Audio track
            public const UInt32 hint = 0x68696E74;  // 'hint', Hint track
            public const UInt32 meta = 0x6D657461;  // 'meta', Timed Metadata track
            public const UInt32 auxv = 0x61757876;  // 'auxv', Auxiliary Video track
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
        protected new UInt64 ComputeLocalSize()
        {
            UInt64 cbName;
            if (null == Name)
            {
                cbName = 0;
            }
            else
            {
                cbName = (UInt64)Encoding.UTF8.GetByteCount(Name) + 1; // Add null-term
            }

            return 4 +      // pre_defined
                   4 +      // handler_type
                   12 +     // reserved
                   cbName;  // name plus null-term
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
            return this.Equals(obj as hdlrBox);
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
            return this.Equals(other as hdlrBox);
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
            return this.Equals(other as hdlrBox);
        }

        /// <summary>
        /// Implements IEquatable(hdlrBox). This function is virtual and it is expected that
        /// derived classes will override it, so that programs which attempt to test equality
        /// using pointers to base classes will can enjoy results from the fully derived
        /// equality implementation.
        /// </summary>
        /// <param name="obj">The box to test equality against.</param>
        /// <returns>True if the this and the given box are equal.</returns>
        public virtual bool Equals(hdlrBox? other)
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

            if (HandlerType != other.HandlerType)
                return false;

            if (Name != other.Name)
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
            return Type.GetHashCode();
        }

        /// <summary>
        ///  Override == operation (as recommended by MSDN).
        /// </summary>
        /// <param name="lhs">The box on the left-hand side of the ==.</param>
        /// <param name="rhs">The box on the right-hand side of the ==.</param>
        /// <returns>True if the two boxes are equal.</returns>
        public static bool operator ==(hdlrBox? lhs, hdlrBox? rhs)
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
        public static bool operator !=(hdlrBox? lhs, hdlrBox? rhs)
        {
            return !(lhs == rhs);
        }

        #endregion


    }
}
