
using System.Diagnostics;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// Represents a Media Box ('mdia'). ISO 14496-12 Sec:8.4.1
    /// </summary>
    public class mdiaBox : Box, IEquatable<mdiaBox>
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public mdiaBox() :
            base(MP4BoxType.mdia)
        {
            Size.Value = ComputeSize(); // Derived class is responsible for updating Size.Value after construction complete
        }

        /// <summary>
        /// Copy constructor
        /// </summary>
        /// <param name="box">the box to copy from</param>
        public mdiaBox(Box box) :
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.mdia);
        }

        /// <summary>
        /// parse the body of box.
        /// </summary>
        protected override void ReadBody()
        {
            //the body of an mdia box is just its children.
            ReadChildren();
        }

        /// <summary>
        /// Returns the Handler Reference Box ('hdlr') of this 'mdia' (mandatory).
        /// </summary>
        public hdlrBox? Handler
        {
            get
            {
                var hdlr = GetExactlyOneChildBox<hdlrBox>();
                return hdlr;
            }
        }

        /// <summary>
        /// Returns the Media Header Box ('mdhd') of this 'mdia' (mandatory).
        /// </summary>
        public mdhdBox? MediaHeader
        {
            get
            {
                var mdhd = GetExactlyOneChildBox<mdhdBox>();
                return mdhd;
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
        // Equals(base) is not overriden, then the base implementation of
        // Equals(base) is used, which will not compare derived fields.
        //=====================================================================

        /// <summary>
        /// Object.Equals override.
        /// </summary>
        /// <param name="obj">The object to test equality against.</param>
        /// <returns>True if the this and the given object are equal.</returns>
        public override bool Equals(Object? obj)
        {
            return this.Equals(obj as mdiaBox);
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
            return this.Equals(other as mdiaBox);
        }

        /// <summary>
        /// Implements IEquatable(mdiaBox). This function is virtual and it is expected that
        /// derived classes will override it, so that programs which attempt to test equality
        /// using pointers to base classes will can enjoy results from the fully derived
        /// equality implementation.
        /// </summary>
        /// <param name="obj">The box to test equality against.</param>
        /// <returns>True if the this and the given box are equal.</returns>
        public virtual bool Equals(mdiaBox? other)
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
            // Base class already compared all the Children boxes

            // If we reach this point, the fields all match
            return true;
        }

        /// <summary>
        /// Object.GetHashCode override. This must be done as a consequence of overridding
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
        public static bool operator ==(mdiaBox? lhs, mdiaBox? rhs)
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
        public static bool operator !=(mdiaBox? lhs, mdiaBox? rhs)
        {
            return !(lhs == rhs);
        }

        #endregion

    }
}
