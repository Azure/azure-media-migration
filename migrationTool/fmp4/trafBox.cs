using System.Diagnostics;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// An object model for the 'traf' box from ISO 14496-12.
    /// </summary>
    public class trafBox : Box, IEquatable<trafBox>
    {

        /// <summary>
        /// Default constructor.
        /// </summary>
        public trafBox() :
            base(MP4BoxType.traf)
        {
        }

        /// <summary>
        /// Copy/Deserializing constructor.
        /// </summary>
        /// <param name="box">the box to copy from</param>
        public trafBox(Box box) :
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.traf);
        }


        /// <summary>
        /// Returns the Header box for this track.
        /// </summary>
        public tfhdBox Header
        {
            get
            {
                return Children.Where((box) => box is tfhdBox).Cast<tfhdBox>().Single();
            }
        }

        /// <summary>
        /// Return the trun child box for this track.
        /// </summary>
        public trunBox TrackRun
        {
            get
            {
                return Children.Where((box) => box is trunBox).Cast<trunBox>().Single();
            }
        }

        /// <summary>
        /// Returns the sample dependency information for this track.
        /// </summary>
        public sdtpBox? DependencyInfo
        {
            get
            {
                return Children.Where((box) => box is sdtpBox).Cast<sdtpBox>().SingleOrDefault();
            }
        }

        /// <summary>
        /// Parses the contents of the box.
        /// </summary>
        protected override void ReadBody()
        {
            //The body of a traf box is just its children.
            ReadChildren();
        }


        #region equality methods.
        /// <summary>
        /// Compare the trafBox against another object for equality.
        /// </summary>
        /// <param name="other">the other object to compare with</param>
        /// <returns></returns>
        public override bool Equals(Object? other)
        {
            return this.Equals(other as trafBox);
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
        public bool Equals(trafBox? other)
        {
            return base.Equals(other as Box);
        }

        /// <summary>
        /// Compare two trafBox objects for equality.
        /// </summary>
        /// <param name="lhs">left side of ==</param>
        /// <param name="rhs">right side of ==</param>
        /// <returns>true if the two boxes are equal else false.</returns>
        public static bool operator ==(trafBox? lhs, trafBox? rhs)
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
        public static bool operator !=(trafBox? lhs, trafBox? rhs)
        {
            return !(lhs == rhs);
        }



        #endregion
    }
}
