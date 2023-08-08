
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// This class allows us to deal with variable-length fields as specified in some
    /// ISO 14496-12 boxes, such as Box.Size, 'trun' entries such as sample_duration, sample_size, etc.
    /// </summary>
    [DebuggerDisplay("Value = {_value}, BitDepth = {_bitDepth}")]
    public class VariableLengthField
    {
        /// <summary>
        /// Public constructor.
        /// </summary>
        /// <param name="validBitDepths">A sorted integer array of valid bit depths for this field.</param>
        /// <param name="initialValue">The initial value to use for this field.</param>
        public VariableLengthField(int[] validBitDepths, UInt64 initialValue)
        {
            // Confirm that validBitDepths is sorted
            for (int i = 1; i < validBitDepths.Length; i++)
            {
                if (validBitDepths[i - 1] > validBitDepths[i])
                {
                    throw new ArgumentException("validBitDepths array must be sorted!", nameof(validBitDepths));
                }
            }

            _validBitDepths = new List<int>(validBitDepths);
            BitDepth = validBitDepths[0];
            IsOverridable = true;
            Value = initialValue;
            Dirty = true; // We start dirty by default, set to false if this isn't true
        }

        /// <summary>
        /// Copy constructor.
        /// </summary>
        /// <param name="other">The VariableLengthField to copy from.</param>
        public VariableLengthField(VariableLengthField other)
        {
            _validBitDepths = new List<int>(other.ValidBitDepths);
            _bitDepth = other._bitDepth;
            IsOverridable = other.IsOverridable;
            _value = other._value;
            Dirty = other.Dirty;
        }

        /// <summary>
        /// Private backing store for public ValidBitDepths property.
        /// </summary>
        private List<int> _validBitDepths;

        /// <summary>
        /// This property reports the valid bit depths for this field.
        /// </summary>
        public ReadOnlyCollection<int> ValidBitDepths
        {
            get
            {
                return _validBitDepths.AsReadOnly();
            }
        }

        /// <summary>
        /// Backing store for BitDepth.
        /// </summary>
        private int _bitDepth;

        /// <summary>
        /// This property gets or sets the BitDepth for this field. It is the responsibility
        /// of the caller to ensure that BitDepth is not set too low to house the current Value property.
        /// Any attempt to do this will throw an exception.
        /// </summary>
        public int BitDepth
        {
            get
            {
                return _bitDepth;
            }
            set
            {
                if (false == _validBitDepths.Contains(value))
                {
                    throw new ArgumentException(
                        String.Format(CultureInfo.InvariantCulture, "Cannot set BitDepth to {0}, this is not a valid value",
                            value), nameof(value));
                }

                int log2 = Log2(Value);
                if (log2 + 1 > value)
                {
                    throw new ArgumentException(
                        String.Format(CultureInfo.InvariantCulture, "Cannot set BitDepth to {0}, current Value of {1} would be lost!",
                            value, Value), nameof(value));
                }

                _bitDepth = value;
            }
        }

        /// <summary>
        /// This property gets or sets whether BitDepth is overridable, or free to change automatically.
        /// If true, then BitDepth may be adjusted when Value is set. If false, then BitDepth can only
        /// be changed explicitly by having the caller set it.
        /// </summary>
        public bool IsOverridable { get; set; }

        /// <summary>
        /// Backing store for Value.
        /// </summary>
        private UInt64 _value;

        /// <summary>
        /// Gets and sets the value of this field. If IsOverridable is false (default), the setting of
        /// Value will cause BitDepth to automatically be set to the minimum value which can house Value.
        /// If IsOverridable is true, then BitDepth will not change. It is the responsiblity of the caller
        /// to ensure that Value will not be set beyond what can be represented by the maximum, or the current
        /// non-overridable BitDepth. Any attempt to do so will throw an exception.
        /// </summary>
        public UInt64 Value
        {
            get
            {
                return _value;
            }

            set
            {
                // Resize the bit depth to just fit the requested value
                int log2 = Log2(value);

                // Will this fit in the biggest valid bit depth?
                if (log2 + 1 > _validBitDepths.Last())
                {
                    throw new ArgumentOutOfRangeException(nameof(value),
                        String.Format(CultureInfo.InvariantCulture, "Cannot set Value to {0}, exceeds maximum bit depth of {1}!",
                            value, _validBitDepths.Last()));
                }

                int newBitDepth = _validBitDepths.First(bitDepth => bitDepth >= log2 + 1);

                // Are we allowed to override?
                if (IsOverridable)
                {
                    // BitDepth (public property) now checks if caller is trying to set to something which
                    // will not hold the current Value. We haven't even set the new Value yet. So bypass
                    // and set the private variable directly.
                    _bitDepth = newBitDepth;
                }
                else if (log2 + 1 > BitDepth)
                {
                    throw new ArgumentOutOfRangeException(nameof(value),
                        String.Format(CultureInfo.InvariantCulture, "Cannot set Value to {0}, exceeds non-overridable bit depth of {1}!",
                            value, BitDepth));
                }

                // Confirm that no bits lie outside the bit depth
                Debug.Assert(64 == BitDepth || 0 == (~(((UInt64)1 << BitDepth) - 1) & value));

                _value = value;
                Dirty = true;
            }
        }

        /// <summary>
        /// Sets this field to its minimal BitDepth. The typical scenario is that BitDepth was set to match
        /// the on-disk bit depth, during deserialization, but it is found that the on-disk bit depth was
        /// unnecessarily large (this sometimes cannot be avoided, such as in live scenarios).
        /// 
        /// If IsOverridable is false, then this method does nothing.
        /// </summary>
        public void Compact()
        {
            if (false == IsOverridable)
                return; // Just return silently

            Value = _value;
        }

        /// <summary>
        /// True if this field matches the on-disk representation, either because we deserialized and
        /// no further changes were made, or because we just saved to disk and no further changes were made.
        /// </summary>
        public bool Dirty { get; set; }

        /// <summary>
        /// Helper function to compute the log2 value of an integer.
        /// </summary>
        /// <param name="value">The value to compute log2 of.</param>
        /// <returns>Log2 of value.</returns>
        private static int Log2(UInt64 value)
        {
            if (0 == value)
            {
                return int.MinValue;
            }

            int log2 = 0;
            while (value != 0)
            {
                value >>= 1;
                log2 += 1;
            }

            return log2 - 1;
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
            return this.Equals(obj as VariableLengthField);
        }

        /// <summary>
        /// Implements IEquatable(VariableLengthField). This function is virtual and it is expected that
        /// derived classes will override it, so that programs which attempt to test equality
        /// using pointers to base classes will can enjoy results from the fully derived
        /// equality implementation.
        /// </summary>
        /// <param name="obj">The box to test equality against.</param>
        /// <returns>True if the this and the given box are equal.</returns>
        public virtual bool Equals(VariableLengthField? other)
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

            // Compare fields. We only care if the values match. We don't care about BitDepth or IsOverridable.
            if (Value != other.Value)
                return false;

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
            return Value.GetHashCode();
        }

        /// <summary>
        ///  Override == operation (as recommended by MSDN).
        /// </summary>
        /// <param name="lhs">The box on the left-hand side of the ==.</param>
        /// <param name="rhs">The box on the right-hand side of the ==.</param>
        /// <returns>True if the two boxes are equal.</returns>
        public static bool operator ==(VariableLengthField? lhs, VariableLengthField? rhs)
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
        public static bool operator !=(VariableLengthField? lhs, VariableLengthField? rhs)
        {
            return !(lhs == rhs);
        }

        #endregion

    }
}
