using System.Diagnostics;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// An object model for the 'trak' box from ISO 14496-12.
    /// </summary>
    public class trakBox : Box, IEquatable<trakBox>
    {

        /// <summary>
        /// Default constructor.
        /// </summary>
        public trakBox() :
            base(MP4BoxType.trak)
        {
        }

        /// <summary>
        /// Copy/Deserializing constructor.
        /// </summary>
        /// <param name="box">the box to copy from</param>
        public trakBox(Box box) :
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.trak);
        }


        /// <summary>
        /// Returns the Header box for this track.
        /// </summary>
        public tkhdBox Header
        {
            get
            {
                return Children.Where((box) => box is tkhdBox).Cast<tkhdBox>().Single();
            }
        }

        /// <summary>
        /// Returns the Media box for this track.
        /// </summary>
        public mdiaBox Media
        {
            get
            {
                return Children.Where((box) => box is mdiaBox).Cast<mdiaBox>().Single();
            }
        }

        /// <summary>
        /// Parses the contents of the box.
        /// </summary>
        protected override void ReadBody()
        {
            //The body of a trak box is just its children.
            ReadChildren();
        }


        #region equality methods.
        /// <summary>
        /// Compare the trakBox against another object for equality.
        /// </summary>
        /// <param name="other">the other object to compare with</param>
        /// <returns></returns>
        public override bool Equals(Object? other)
        {
            return this.Equals(other as trakBox);
        }

        /// <summary>
        /// returns the hash code of the object.
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
        public bool Equals(trakBox? other)
        {
            return base.Equals(other as Box);
        }

        /// <summary>
        /// Compare two trakBox objects for equality.
        /// </summary>
        /// <param name="lhs">left side of ==</param>
        /// <param name="rhs">right side of ==</param>
        /// <returns>true if the two boxes are equal else false.</returns>
        public static bool operator ==(trakBox? lhs, trakBox? rhs)
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
        public static bool operator !=(trakBox? lhs, trakBox? rhs)
        {
            return !(lhs == rhs);
        }

        #endregion
    }
}
