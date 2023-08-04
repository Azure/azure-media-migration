
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// An object model for a track run box (as per ISO 14496-12)
    /// </summary>
    public class trunBox : FullBox, IEquatable<trunBox>
    {

        /// <summary>
        /// Default constructor.
        /// </summary>
        public trunBox() :
            base(version:0, flags:0, boxtype:MP4BoxType.trun)
        {
            Size.Value = ComputeSize();
            _entries.CollectionChanged += Entries_CollectionChanged;
        }

        /// <summary>
        /// De-serializing/copy constructor.
        /// </summary>
        /// <param name="box">the box to construct from</param>
        public trunBox(Box box):
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.trun);

            _entries.CollectionChanged +=Entries_CollectionChanged;
        }

        /// <summary>
        /// destructor. unregister the delegate for collection changes.
        /// </summary>
        ~trunBox()
        {
            _entries.CollectionChanged -= Entries_CollectionChanged;
        }

        /// <summary>
        /// A delegate that is called when the entries collection is changes (entry added/removed/changed)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Entries_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            SetDirty();
        }

        /// <summary>
        /// An enumeration to defined the various allowed flags for a tfhd box.
        /// </summary>
        [Flags]
        public enum TrunFlags : uint
        {
            None = 0,
            DataOffsetPresent = 0x1,
            FirstSampleFlagsPresent = 0x4,
            SampleDurationPresent = 0x100,
            SampleSizePresent = 0x200,
            SampleFlagsPresent = 0x400,
            SampleCompositionOffsetPresent = 0x0800
        }

        /// <summary>
        /// The offset of the data 
        /// </summary>
        public Int32? DataOffset
        {
            get
            {
                return _dataOffset;
            }
            set
            {
                _dataOffset = value;
                SetDirty();
            }
        }

        private Int32? _dataOffset;

        /// <summary>
        /// The flags for the first sample in the track.
        /// </summary>
        public UInt32? FirstSampleFlags
        {
            get
            {
                return _firstSampleFlags;
            }
            set
            {
                _firstSampleFlags = value;
                SetDirty();
            }
        }

        /// <summary>
        /// The first sample flags.. this overrides the values
        /// </summary>
        private UInt32? _firstSampleFlags;

        /// <summary>
        /// Calculates the size of each trun entry based on the flags.
        /// </summary>
        /// <param name="flags">The flags to </param>
        /// <returns></returns>
        public static UInt32 GetEntrySize(TrunFlags flags)
        {
            UInt32 entrySize = 0;

            if ((flags & TrunFlags.SampleDurationPresent) == TrunFlags.SampleDurationPresent)
            {
                entrySize += 4;
            }

            if ((flags & TrunFlags.SampleSizePresent) == TrunFlags.SampleSizePresent)
            {
                entrySize += 4;
            }
            
            if ((flags & TrunFlags.SampleFlagsPresent) == TrunFlags.SampleFlagsPresent)
            {
                entrySize += 4;
            }
            
            if ((flags & TrunFlags.SampleCompositionOffsetPresent) == TrunFlags.SampleCompositionOffsetPresent)
            {
                entrySize += 4;
            }

            return entrySize;
        }

        /// <summary>
        /// Pases the body of box. As per 14496-12 section 8.36
        /// </summary>
        protected override void ReadBody()
        {
            base.ReadBody();

            TrunFlags flags = (TrunFlags)base.Flags;
            Int64 startPosition = Body!.BaseStream.Position;
            try
            {
                //Only fixed field is sample count. rest all are optional.
                UInt32 sampleCount = Body.ReadUInt32();

                if ((flags & TrunFlags.DataOffsetPresent) == TrunFlags.DataOffsetPresent)
                {
                    _dataOffset = Body.ReadInt32();
                }

                if ((flags & TrunFlags.FirstSampleFlagsPresent) == TrunFlags.FirstSampleFlagsPresent)
                {
                    _firstSampleFlags = Body.ReadUInt32();
                }

                UInt32 entrySize = GetEntrySize(flags);

                if (sampleCount * entrySize > Body.BaseStream.Length - Body.BaseStream.Position)
                {
                    // Reported error offset will point to start of box
                    throw new MP4DeserializeException(TypeDescription, -BodyPreBytes, BodyInitialOffset,
                        String.Format(CultureInfo.InvariantCulture, "{0} trun entries at {1} bytes per entry exceeds the remaining box size of {2}!",
                            sampleCount, entrySize, Body.BaseStream.Length - Body.BaseStream.Position));
                }

                for (UInt32 i = 0; i < sampleCount; ++i)
                {
                    trunEntry entry = new trunEntry();

                    if ((flags & TrunFlags.SampleDurationPresent) == TrunFlags.SampleDurationPresent)
                    {
                        entry.SampleDuration = Body.ReadUInt32();
                    }

                    if ((flags & TrunFlags.SampleSizePresent) == TrunFlags.SampleSizePresent)
                    {
                        entry.SampleSize = Body.ReadUInt32();
                    }
                
                    if ((flags & TrunFlags.SampleFlagsPresent) == TrunFlags.SampleFlagsPresent)
                    {
                        entry.SampleFlags = Body.ReadUInt32();
                    }

                    if ((flags & TrunFlags.SampleCompositionOffsetPresent) == TrunFlags.SampleCompositionOffsetPresent)
                    {
                        entry.SampleCompositionOffset = Body.ReadInt32();
                    }

                    //An entry just de-serialized is not dirty.
                    entry.Dirty = false;

                    _entries.Add(entry);
                }
            }
            catch (EndOfStreamException ex)
            {
                // Reported error offset will point to start of box
                throw new MP4DeserializeException(TypeDescription, -BodyPreBytes, BodyInitialOffset,
                    String.Format(CultureInfo.InvariantCulture, "Could not read trun fields, only {0} bytes left in reported size, expected {1}",
                        Body.BaseStream.Length - startPosition, ComputeLocalSize()), ex);
            }
        }

        /// <summary>
        /// Writes the contents of the box to the writer.
        /// </summary>
        /// <param name="writer">MP4Writer to write to</param>
        protected override void WriteToInternal(MP4Writer writer)
        {
            if (Dirty)
            {
                ComputeFlags();
            }

            base.WriteToInternal(writer);

            writer.WriteInt32(_entries.Count);
            
            if (_dataOffset.HasValue)
            {
                writer.WriteInt32(_dataOffset.Value);
            }

            if (_firstSampleFlags.HasValue)
            {
                writer.WriteUInt32(_firstSampleFlags.Value);
            }

            foreach (trunEntry entry in _entries)
            {
                entry.WriteTo(writer);    
            }
        }


        /// <summary>
        /// Helper method to trow an InvalidDataException if the trun entries are not consistent.
        /// </summary>
        /// <param name="field">The name of field in trun entry that is inconsitent</param>
        /// <param name="first">first trun entry</param>
        /// <param name="current">current trun entry</param>
        /// <param name="index">index of the current entry</param>
        private static void ThrowValidationException(String field, bool first, bool current, int index)
        {
            throw new InvalidDataException(
                String.Format(CultureInfo.InvariantCulture, "Field:{0} is inconsistent for trun entries. Entry[0]: {1}, Entry[{3}]: {2}",
                field,
                first,
                current,
                index));
        }

        /// <summary>
        /// Compute the flags to be set. 
        /// Also validate the entries to make sure all entries have exact same fields.
        /// </summary>
        private void ComputeFlags()
        {
            TrunFlags flags = TrunFlags.None;

            if (_dataOffset.HasValue)
            {
                flags |= TrunFlags.DataOffsetPresent;
            }

            if (_firstSampleFlags.HasValue)
            {
                flags |= TrunFlags.FirstSampleFlagsPresent;
            }

            if (_entries.Count > 0)
            {
                trunEntry firstEntry = _entries[0];

                if (firstEntry.SampleDuration.HasValue)
                {
                    flags |= TrunFlags.SampleDurationPresent;
                }

                if (firstEntry.SampleSize.HasValue)
                {
                    flags |= TrunFlags.SampleSizePresent;
                }
                if (firstEntry.SampleFlags.HasValue)
                {
                    flags |= TrunFlags.SampleFlagsPresent;
                }
                if (firstEntry.SampleCompositionOffset.HasValue)
                {
                    flags |= TrunFlags.SampleCompositionOffsetPresent;
                }

                for (int i = 1; i < _entries.Count; ++i)
                {
                    trunEntry entry = _entries[i];

                    if (entry.SampleDuration.HasValue != firstEntry.SampleDuration.HasValue)
                    {
                        ThrowValidationException("SampleDuration", firstEntry.SampleDuration.HasValue, entry.SampleDuration.HasValue, i);
                    }

                    if (entry.SampleSize.HasValue != firstEntry.SampleSize.HasValue)
                    {
                        ThrowValidationException("SampleSize", firstEntry.SampleSize.HasValue, entry.SampleSize.HasValue, i);
                    }

                    if (entry.SampleFlags.HasValue != firstEntry.SampleFlags.HasValue)
                    {
                        ThrowValidationException("SampleFlags", firstEntry.SampleFlags.HasValue, entry.SampleFlags.HasValue, i);
                    }

                    if (entry.SampleCompositionOffset.HasValue != firstEntry.SampleCompositionOffset.HasValue)
                    {
                        ThrowValidationException("SampleCompositionOffset", firstEntry.SampleCompositionOffset.HasValue, entry.SampleCompositionOffset.HasValue, i);
                    }
                }
            }

            Flags = (UInt32)flags;
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
            UInt64 thisSize = 4; //for the sample count. rest all are optional.
            UInt64 entrySize = 0;

            if(_dataOffset.HasValue)
            {
                thisSize += 4;
            }

            if (_firstSampleFlags.HasValue)
            {
                thisSize += 4;
            }

            if (_entries.Count > 0)
            {
                trunEntry firstEntry = _entries[0];

                if (firstEntry.SampleDuration.HasValue)
                {
                    entrySize += 4;
                }

                if (firstEntry.SampleSize.HasValue)
                {
                    entrySize += 4;
                }

                if (firstEntry.SampleFlags.HasValue)
                {
                    entrySize += 4;
                }

                if (firstEntry.SampleCompositionOffset.HasValue)
                {
                    entrySize += 4;
                }

            }

            thisSize += (entrySize * (UInt64)_entries.Count);
            return thisSize;
        }

        /// <summary>
        /// Indicate if the box is dirty or not.
        /// </summary>
        public override bool Dirty
        {
            get
            {
                if (base.Dirty)
                {
                    return true;
                }

                foreach (trunEntry entry in _entries)
                {
                    if (entry.Dirty)
                    {
                        return true;
                    }
                }

                return false;
            }
            set
            {
                base.Dirty = value;
                foreach (trunEntry entry in _entries)
                {
                    entry.Dirty = value;
                }
            }
        }

        /// <summary>
        /// A collection of track run entries.
        /// </summary>
        public IList<trunEntry> Entries
        {
            get
            {
                return _entries;
            }
        }

        private ObservableCollection<trunEntry> _entries = new ObservableCollection<trunEntry>();

        #region equality methods.

        /// <summary>
        /// Compare this box to another object.
        /// </summary>
        /// <param name="obj">the object to compare against.</param>
        /// <returns>true if both are equal else false.</returns>
        public override bool Equals(object? obj)
        {
            return this.Equals(obj as trunBox);
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
        /// Box.Equals override. This is done so that programs which attempt to test equality
        /// using pointers to Box will can enjoy results from the fully derived
        /// equality implementation.
        /// </summary>
        /// <param name="other">The box to test equality against.</param>
        /// <returns>True if the this and the given box are equal.</returns>
        public override bool Equals(Box? other)
        {
            return this.Equals(other as trunBox);
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
            return this.Equals(other as trunBox);
        }
        
        /// <summary>
        /// compare this object against another trunBox object.
        /// </summary>
        /// <param name="other">other trunBox to compare against.</param>
        /// <returns>true if equal else false.</returns>
        public bool Equals(trunBox? other)
        {
            if (!base.Equals((FullBox?)other))
            {
                return false;
            }
            if (_dataOffset != other._dataOffset)
            {
                return false;
            }
            if (_firstSampleFlags != other._firstSampleFlags)
            {
                return false;
            }

            return _entries.SequenceEqual(other._entries);
        }

        /// <summary>
        /// Compare two trunBox objects for equality using Equals() method.
        /// </summary>
        /// <param name="lhs">left side of ==</param>
        /// <param name="rhs">right side of ==</param>
        /// <returns>true if the two boxes are equal else false.</returns>
        public static bool operator ==(trunBox? lhs, trunBox? rhs)
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
        public static bool operator !=(trunBox? lhs, trunBox? rhs)
        {
            return !(lhs ==rhs);
        }

        #endregion
    }
}
