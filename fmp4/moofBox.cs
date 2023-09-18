using System.Diagnostics;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// An object for the 'moof' box from ISO 14496-12.
    /// </summary>
    public class moofBox : Box, IEquatable<moofBox>
    {
        /// <summary>
        /// Default constructor for the box.
        /// </summary>
        public moofBox() :
            base(MP4BoxType.moof)
        {
            //at the min have the header box.
            Children.Add(new mfhdBox(1));

            Size.Value = ComputeSize();
        }

        /// <summary>
        /// Deserializing/copy constructor.
        /// </summary>
        /// <param name="box">the box to construct from</param>
        public moofBox(Box box) :
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.moof);
        }

        /// <summary>
        /// Parses the body of box. A moof is nothing but a collection of child boxes.
        /// </summary>
        protected override void ReadBody()
        {
            ReadChildren();
        }


        /// <summary>
        /// Returns a single child header box for this fragment.
        /// </summary>
        public mfhdBox Header
        {
            get
            {
                return Children.Where((box) => box is mfhdBox).Cast<mfhdBox>().Single();
            }
        }

        /// <summary>
        /// Returns the single Child track fragment box of this fragment.
        /// </summary>
        public trafBox Track
        {
            get
            {
                return Children.Where((box) => box is trafBox).Cast<trafBox>().Single();
            }
        }

        #region equality methods.

        /// <summary>
        /// Compare the moofbox against another object for equality.
        /// </summary>
        /// <param name="other">the other object to compare with</param>
        /// <returns></returns>
        public override bool Equals(Object? other)
        {
            return this.Equals(other as moofBox);
        }

        /// <summary>
        /// returns the hashcode of the object.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return base.GetHashCode();
        }

        /// <summary>
        /// Compare two boxes for equality. Use base.Equals() to do the comparison
        /// </summary>
        /// <param name="other">other box to compare against.</param>
        /// <returns>true if both boxes are equal else false.</returns>
        public bool Equals(moofBox? other)
        {
            return base.Equals(other as Box);
        }

        /// <summary>
        /// Compare two moofBox objects for equality.
        /// </summary>
        /// <param name="lhs">left side of ==</param>
        /// <param name="rhs">right side of ==</param>
        /// <returns>true if the two boxes are equal else false.</returns>
        public static bool operator ==(moofBox? lhs, moofBox? rhs)
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
        /// compares two entries using the Equals() method for equality.
        /// </summary>
        /// <param name="lhs">left side of !=</param>
        /// <param name="rhs">right side of !=</param>
        /// <returns>return true if two boxes are not equal else false.</returns>
        public static bool operator !=(moofBox? lhs, moofBox? rhs)
        {
            return !(lhs == rhs);
        }


        #endregion

    }
}
