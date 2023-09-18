using System.Diagnostics;
using System.Globalization;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// Represents a movie fragment header box . ISO 14496-12 Sec:8.33.1
    /// </summary>
    public class mfhdBox : FullBox, IEquatable<mfhdBox>
    {
        /// <summary>
        /// Default constructor with a sequence number
        /// </summary>
        /// <param name="sequenceNumber">The sequence number of the fragment.</param>
        public mfhdBox(UInt32 sequenceNumber) :
            base(version: 0, flags: 0, boxtype: MP4BoxType.mfhd)
        {
            SequenceNumber = sequenceNumber;
            Size.Value = ComputeSize();
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="box">the box to copy from</param>
        public mfhdBox(Box box) :
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.mfhd);
        }

        /// <summary>
        /// Get or set the sequence number for this fragment.
        /// </summary>
        public UInt32 SequenceNumber
        {
            get
            {
                return _sequenceNumber;
            }
            set
            {
                _sequenceNumber = value;
                SetDirty();
            }
        }

        /// <summary>
        /// The sequence number for this fragment.
        /// </summary>
        private UInt32 _sequenceNumber;

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
                _sequenceNumber = Body.ReadUInt32();
            }
            catch (EndOfStreamException ex)
            {
                // Reported error offset will point to start of box
                throw new MP4DeserializeException(TypeDescription, -BodyPreBytes, BodyInitialOffset,
                    String.Format(CultureInfo.InvariantCulture, "Could not read sequence number, only {0} bytes left in reported size, expected {1}",
                        Body.BaseStream.Length - startPosition, ComputeLocalSize()), ex);
            }

        }

        /// <summary>
        /// Serialize the box contents
        /// </summary>
        /// <param name="writer">MP4Writer to write to</param>
        protected override void WriteToInternal(MP4Writer writer)
        {
            base.WriteToInternal(writer);
            writer.WriteUInt32(_sequenceNumber);
        }

        /// <summary>
        /// Compute the size of the box.
        /// </summary>
        /// <returns>size of the box itself</returns>
        public override UInt64 ComputeSize()
        {
            return base.ComputeSize() + ComputeLocalSize();
        }

        /// <summary>
        /// Compute the size of just the box specific contents.
        /// </summary>
        /// <returns></returns>
        protected static new UInt64 ComputeLocalSize()
        {
            return 4; //just the sequence number;
        }


        #region equality methods

        /// <summary>
        /// Compare this box to another object.
        /// </summary>
        /// <param name="other">the object to compare equality against</param>
        /// <returns></returns>
        public override bool Equals(Object? other)
        {
            return this.Equals(other as mfhdBox);
        }

        /// <summary>
        /// Returns the hashcode of the object.
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
            return this.Equals(other as mfhdBox);
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
            return this.Equals(other as mfhdBox);
        }

        /// <summary>
        /// Compare two mfhdBoxes for equality.
        /// </summary>
        /// <param name="other">other mfhdBox to compare against</param>
        /// <returns></returns>
        public bool Equals(mfhdBox? other)
        {
            if (!base.Equals((FullBox?)other))
                return false;

            return SequenceNumber == other.SequenceNumber;
        }

        /// <summary>
        /// Compare two mfhdBox objects for equality.
        /// </summary>
        /// <param name="lhs">left hand side of ==</param>
        /// <param name="rhs">right hand side of ==</param>
        /// <returns></returns>
        public static bool operator ==(mfhdBox? lhs, mfhdBox? rhs)
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

        public static bool operator !=(mfhdBox? lhs, mfhdBox? rhs)
        {
            return !(lhs == rhs);
        }

        #endregion
    }
}
