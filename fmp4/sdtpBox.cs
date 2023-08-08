
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;

namespace AMSMigrate.Fmp4
{

    /// <summary>
    /// An object that represent the sdtp box in an ISO file as per 14496-12
    /// </summary>
    public class sdtpBox : FullBox, IEquatable<sdtpBox>
    {
        /// <summary>
        /// Default constructor.
        /// </summary>
        public sdtpBox() :
            base(version:0, flags:0, boxtype:MP4BoxType.sdtp)
        {
            Size.Value = ComputeSize();
            _entries.CollectionChanged += _dependencies_CollectionChanged;
        }

        /// <summary>
        /// Deserializing/copy constructor.
        /// </summary>
        /// <param name="box">the box to construct from</param>
        public sdtpBox(Box box):
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.sdtp);

            _entries.CollectionChanged += _dependencies_CollectionChanged;
        }

        /// <summary>
        /// If the collection of sample dependencies is modified marks the box dirty.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        void _dependencies_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            SetDirty();
        }

        /// <summary>
        /// Passes the body of box. A moof is nothing but a collection of child boxes.
        /// </summary>
        protected override void ReadBody()
        {
            base.ReadBody();

            // Check on version - we expect 0 , reject anything else
            if (Version != 0)
            {
                // Reported error offset will point to start of box
                throw new MP4DeserializeException(TypeDescription, -BodyPreBytes, BodyInitialOffset,
                    String.Format(CultureInfo.InvariantCulture, "Unexpected version: {0}. Expected 0!", Version));
            }

            // Check on version - we expect 0 , reject anything else
            if (Flags != 0)
            {
                // Reported error offset will point to start of box
                throw new MP4DeserializeException(TypeDescription, -BodyPreBytes, BodyInitialOffset,
                    String.Format(CultureInfo.InvariantCulture, "Unexpected Flags: {0}. Expected 0!", Flags));
            }

            //Read till the end of the body.
            while (Body!.BaseStream.Position < Body.BaseStream.Length)
            {
                _entries.Add(new sdtpEntry(Body.ReadByte()));
            }
        }

        /// <summary>
        /// Serialize the box contents to an MP4Writer
        /// </summary>
        /// <param name="writer">the MP4Writer to serialize the box.</param>
        protected override void WriteToInternal(MP4Writer writer)
        {
            base.WriteToInternal(writer);
            foreach (sdtpEntry dependency in _entries)
            {
                writer.Write(dependency.Value);
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
        protected new UInt64 ComputeLocalSize()
        {
            return (UInt64)_entries.Count;
        }


        /// <summary>
        /// A collection of sample dependency entries one per sample..
        /// </summary>
        public IList<sdtpEntry> Entries
        {
            get
            {
                return _entries;
            }
        }

        /// <summary>
        /// An internal collection of SampleDependency entries.
        /// </summary>
        private ObservableCollection<sdtpEntry> _entries = new ObservableCollection<sdtpEntry>();

        #region equality methods.

        /// <summary>
        /// Object.Equals override.
        /// </summary>
        /// <param name="obj">The object to test equality against.</param>
        /// <returns>True if the this and the given object are equal.</returns>
        public override bool Equals(object? obj)
        {
            return this.Equals(obj as sdtpBox);
        }

        /// <summary>
        /// Object.GetHashCode override. This must be done as a consequence of overriding
        /// Object.Equals.
        /// </summary>
        /// <returns>Hash code which will be match the hash code of an object which is equal.</returns>
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
            return this.Equals(other as sdtpBox);
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
            return this.Equals(other as sdtpBox);
        }
        
        /// <summary>
        /// compare the box with another sdtpBox
        /// </summary>
        /// <param name="other">other sdtpBox to compare with</param>
        /// <returns></returns>
        public bool Equals(sdtpBox? other)
        {
            if (!base.Equals(other as FullBox))
            {
                return false;
            }

            //Each element in the sequence should be equal.
            if (!_entries.SequenceEqual(other._entries))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Compare two sdtpBox objects for equality using Equals() method.
        /// </summary>
        /// <param name="lhs">left side of ==</param>
        /// <param name="rhs">right side of ==</param>
        /// <returns>true if the two boxes are equal else false.</returns>
        public static bool operator ==(sdtpBox? lhs, sdtpBox? rhs)
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
        public static bool operator !=(sdtpBox? lhs, sdtpBox? rhs)
        {
            return !(lhs == rhs);
        }


        #endregion

    }
}
