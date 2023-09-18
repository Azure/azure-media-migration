using System.Diagnostics;
using System.Globalization;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// Represents a Track fragment decode time box . ISO 14496-12 Sec:8.8.12
    /// </summary>
    public class tfdtBox : FullBox, IEquatable<tfdtBox>
    {
        /// <summary>
        /// Default constructor with a decode time.
        /// </summary>
        /// <param name="decodeTime">The decode time which this box refers to.</param>
        public tfdtBox(UInt64 decodeTime) :
            base(version: 1, flags: 0, boxtype: MP4BoxType.tfdt)
        {
            _decodeTime = decodeTime;
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="box">the box to copy from</param>
        public tfdtBox(Box box) :
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.tfdt);
        }

        /// <summary>
        /// Get or set the decode time for the chunk.
        /// </summary>
        public UInt64 DecodeTime
        {
            get
            {
                return _decodeTime;
            }
            set
            {
                _decodeTime = value;
                SetDirty();
            }
        }

        public UInt64 _decodeTime;

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
                    _decodeTime = Body.ReadUInt64();
                }
                else if (Version == 0)
                {
                    _decodeTime = Body.ReadUInt32();
                }
                else
                {
                    throw new MP4DeserializeException(
                        TypeDescription,
                        -BodyPreBytes,
                        BodyInitialOffset,
                        String.Format(CultureInfo.InvariantCulture, "The version field for a tfdt box must be either 0 or 1. Instead found {0}",
                        Version));
                }
            }
            catch (EndOfStreamException ex)
            {
                // Reported error offset will point to start of box
                throw new MP4DeserializeException(TypeDescription, -BodyPreBytes, BodyInitialOffset,
                    String.Format(CultureInfo.InvariantCulture, "Could not read tfdt fields, only {0} bytes left in reported size, expected {1}",
                        Body.BaseStream.Length - startPosition, ComputeLocalSize()), ex);
            }
        }

        protected override void WriteToInternal(MP4Writer writer)
        {
            //Version is always 1.
            Version = 1;
            base.WriteToInternal(writer);

            writer.WriteUInt64(_decodeTime);
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
            return 8; // Decode Time
        }

        #region equality methods
        /// <summary>
        /// Compare this box to another object.
        /// </summary>
        /// <param name="other">the object to compare equality against</param>
        /// <returns></returns>
        public override bool Equals(Object? other)
        {
            return this.Equals(other as tfdtBox);
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
            return this.Equals(other as tfdtBox);
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
            return this.Equals(other as tfdtBox);
        }

        /// <summary>
        /// Compare two tfdtBoxes for equality.
        /// </summary>
        /// <param name="other">other tfdtBox to compare against</param>
        /// <returns></returns>
        public bool Equals(tfdtBox? other)
        {
            if (!base.Equals((FullBox?)other))
                return false;

            if (DecodeTime != other.DecodeTime)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Compare two tfdtBox objects for equality.
        /// </summary>
        /// <param name="lhs">left hand side of ==</param>
        /// <param name="rhs">right hand side of ==</param>
        /// <returns></returns>
        public static bool operator ==(tfdtBox? lhs, tfdtBox? rhs)
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
        /// Compare two tfdtBox objects for equality
        /// </summary>
        /// <param name="lhs">left side of !=</param>
        /// <param name="rhs">right side of !=</param>
        /// <returns>true if not equal else false.</returns>
        public static bool operator !=(tfdtBox? lhs, tfdtBox? rhs)
        {
            return !(lhs == rhs);
        }

        #endregion
    }
}
