using System.Diagnostics;
using System.Security.Cryptography;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// An object for the 'moof' box from ISO 14496-12.
    /// </summary>

    public class mdatBox : Box, IEquatable<mdatBox>
    {
        /// <summary>
        /// Default constructor for the box.
        /// </summary>
        public mdatBox() :
            base(MP4BoxType.mdat)
        {
            // We used to call ComputeSize(), but that is overridable and can cause us to call an uninitialized derived object, so unroll
            // THIS class's ComputeSize() here. A derived class will like call its ComputeSize() and overwrite our value.
            Size.Value = base.ComputeSize() + ComputeLocalSize();
        }

        /// <summary>
        /// Deserializing constructor.
        /// </summary>
        /// <param name="box">the box to construct from</param>
        public mdatBox(Box box) :
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.mdat);
        }

        /// <summary>
        /// Deep copy constructor.
        /// </summary>
        /// <param name="other">The mdat box to deep-copy.</param>
        public mdatBox(mdatBox other) :
            base(MP4BoxType.mdat)
        {
            if (null != other.SampleData)
            {
                SampleData = (byte[])other.SampleData.Clone(); // Clone is shallow copy wrt object refs but with byte it's deep
            }

            // Normally would set _prevSampleDataChecksum here, but there is no "correct" value
            // since our policy is for a deep copy's Dirty to be true (unconditionally), and today
            // this is achieved via base.Dirty being true. So, save some CPU cycles and don't set it.
            // If our copy.Dirty policy changes in future, then:
            //   copy.Dirty = other.Dirty suggests _prevSampleDataChecksum = other._prevSampleDataChecksum;
            //   copy.Dirty = false suggests _prevSampleDataChecksum = ComputeSampleDataChecksum(SampleData);
        }

        /// <summary>
        /// Byte array containing the sample data.
        /// </summary>
        public byte[]? SampleData { get; set; }

        /// <summary>
        /// Checksum of the contents of SampleData the last time Dirty was reset to false.
        /// </summary>
        private byte[]? _prevSampleDataChecksum;

        /// <summary>
        /// Parses the body of the box.
        /// </summary>
        protected override void ReadBody()
        {
            base.ReadBody();

            // We know that Box.cs saves the bytes to a MemoryStream. I tried calling
            // MemoryStream.GetBuffer, but got UnauthorizedAccessException. So we can't
            // avoid the memory copy.
            long startPosition = Body!.BaseStream.Position;
            int bytesToRead = (int)(Body.BaseStream.Length - startPosition);
            if (bytesToRead > 0)
            {
                SampleData = Body.ReadBytes(bytesToRead);
            }
            else
            {
                SampleData = null;
            }
            _prevSampleDataChecksum = ComputeSampleDataChecksum(SampleData);
        }

        /// <summary>
        /// Serialize the box contents to the writer.
        /// </summary>
        /// <param name="writer">the MP4Writer to write to</param>
        protected override void WriteToInternal(MP4Writer writer)
        {
            base.WriteToInternal(writer);
            if (SampleData != null)
            {
                writer.Write(SampleData);
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
            if (null == SampleData)
            {
                return 0;
            }
            else
            {
                return (UInt64)SampleData.Length;
            }
        }

        /// <summary>
        /// Checks if the given sample data is null or empty.
        /// </summary>
        /// <param name="sampleData">The sample data array to check.</param>
        /// <returns>True if null or empty (count of 0).</returns>
        internal static bool SampleDataIsNullOrEmpty(byte[]? sampleData)
        {
            if (null == sampleData)
            {
                return true;
            }

            if (0 == sampleData.Length)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// Checksum the contents of the given sample data, or if null, return null.
        /// </summary>
        /// <param name="sampleData">The sample data array to checksum.</param>
        /// <returns>A byte array containing the checksum of the contents of sampleData,
        /// or null if sampleData was null.</returns>
        internal static byte[]? ComputeSampleDataChecksum(byte[]? sampleData)
        {
            if (SampleDataIsNullOrEmpty(sampleData))
            {
                return null;
            }

            using (var hash = SHA256.Create())
            {
                return hash.ComputeHash(sampleData!);
            }
        }

        /// <summary>
        /// Compares a given sample data buffer against a another sample data checksum.
        /// </summary>
        /// <param name="thisSampleData">The sample data buffer to compare.</param>
        /// <param name="thatSampleDataChecksum">The checksum of another sample data
        /// buffer to compare to.</param>
        /// <returns>True if they are equal, otherwise false.</returns>
        internal static bool SampleDataAreEqual(byte[]? thisSampleData, byte[]? thatSampleDataChecksum)
        {
            var currentChecksum = ComputeSampleDataChecksum(thisSampleData);
            var thatChecksum = ComputeSampleDataChecksum(thatSampleDataChecksum);

            if (currentChecksum != thatChecksum)
            {
                return false;
            }

            if (null != currentChecksum && false == currentChecksum.SequenceEqual(thatSampleDataChecksum!))
            {
                return false;
            }

            // If we reach this point, they are equal
            return true;
        }

        /// <summary>
        /// This property indicates whether any of the members of this class are dirty, but not
        /// this class's base class. The reason this is separated from base class is explained in
        /// Box.cs and has to do with decoupling the dependency chain which arises when base classes
        /// call derived classes for information during their construction.
        /// 
        /// It is true if the members of this box match their on-disk representation, either because
        /// we deserialized and no further changes were made, or because we just saved to disk
        /// and no further changes were made.
        /// </summary>
        public override bool Dirty
        {
            get
            {
                if (base.Dirty)
                {
                    return true;
                }

                if (false == SampleDataAreEqual(SampleData, _prevSampleDataChecksum))
                {
                    return true;
                }

                // If we reached this stage, we are not dirty
                return false;
            }

            set
            {
                base.Dirty = value;
                if (false == value)
                {
                    _prevSampleDataChecksum = ComputeSampleDataChecksum(SampleData);
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
            return this.Equals(obj as mdatBox);
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
            return this.Equals(other as mdatBox);
        }

        /// <summary>
        /// Implements IEquatable(mdatBox). This function is virtual and it is expected that
        /// derived classes will override it, so that programs which attempt to test equality
        /// using pointers to base classes will can enjoy results from the fully derived
        /// equality implementation.
        /// </summary>
        /// <param name="obj">The box to test equality against.</param>
        /// <returns>True if the this and the given box are equal.</returns>
        public virtual bool Equals(mdatBox? other)
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
            bool thisSampleDataNullOrEmpty = SampleDataIsNullOrEmpty(SampleData);
            if (SampleDataIsNullOrEmpty(other.SampleData) != thisSampleDataNullOrEmpty)
            {
                return false;
            }

            if (false == thisSampleDataNullOrEmpty && false == Enumerable.SequenceEqual(SampleData!, other.SampleData!))
            {
                return false;
            }

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
            var checksum = ComputeSampleDataChecksum(SampleData);
            if (null == checksum)
                return 0;

            // Return the lower 32 bits of the checksum XOR hash of any children.
            // If checksum length < 4, we expect an exception to be thrown.
            int hashCode = BitConverter.ToInt32(checksum, 0);
            int childHash = 0;
            foreach (Box child in Children)
            {
                childHash ^= child.GetHashCode();
            }
            return (hashCode ^ childHash);
        }

        /// <summary>
        ///  Override == operation (as recommended by MSDN).
        /// </summary>
        /// <param name="lhs">The box on the left-hand side of the ==.</param>
        /// <param name="rhs">The box on the right-hand side of the ==.</param>
        /// <returns>True if the two boxes are equal.</returns>
        public static bool operator ==(mdatBox? lhs, mdatBox? rhs)
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
        public static bool operator !=(mdatBox? lhs, mdatBox? rhs)
        {
            return !(lhs == rhs);
        }

        #endregion
    }
}
