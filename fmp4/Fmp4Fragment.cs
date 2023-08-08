
using System.Diagnostics;

namespace AMSMigrate.Fmp4
{
    public class Fmp4Fragment : IEquatable<Fmp4Fragment>
    {
        /// <summary>
        /// Construct a fragment object with moof and mdat.
        /// </summary>
        /// <param name="moof">the fragment header</param>
        /// <param name="mdat">the fragment data</param>
        public Fmp4Fragment(moofBox moof, mdatBox mdat)
        {
            Header = moof;
            Data = mdat;
        }

        /// <summary>
        /// Deep copy constructor.
        /// </summary>
        /// <param name="other">The Fmp4Fragment to deep-copy.</param>
        public Fmp4Fragment(Fmp4Fragment other)
        {
            Header = other.Header.CloneBySerialization();
            Data = new mdatBox(other.Data); // mdatBox has its own deep copy constructor
        }

        /// <summary>
        /// The moof header of the fragment.
        /// </summary>
        public moofBox Header { get; private set; }

        /// <summary>
        /// The mdat for the fragment.
        /// </summary>
        public mdatBox Data { get; private set; }

        /// <summary>
        /// Reads an MP4 fragment (moof + mdat) from the stream
        /// </summary>
        /// <param name="stream">The stream to read from</param>
        /// <returns>an Fmp4Fragment object with moof and mdat</returns>
        public static Fmp4Fragment Create(Stream stream)
        {
            MP4Reader reader = new MP4Reader(stream);

            moofBox moof = MP4BoxFactory.ParseSingleBox<moofBox>(reader);
            mdatBox mdat = MP4BoxFactory.ParseSingleBox<mdatBox>(reader);
            return new Fmp4Fragment(moof, mdat);
        }

        /// <summary>
        /// Saves this MP4 to a stream and returns it (rewound to 0).
        /// </summary>
        /// <param name="setSampleDefaults">True to use sample defaults (duration, size, flags), if possible.
        /// If false, the header is written as-is.</param>
        /// <returns>This fragment, serialized to a stream, rewound to position 0. It is
        /// the responsibility of the caller to Dispose this stream.</returns>
        public Stream ToStream(bool setSampleDefaults = true)
        {
            var outStream = new MemoryStream();
            var writer = new MP4Writer(outStream); // Don't dispose, that will close the stream.

            WriteTo(writer, setSampleDefaults);
            writer.Flush();

            outStream.Position = 0; // Rewind stream
            return outStream;
        }

        /// <summary>
        /// Get the data offset from byte 0 of moof to byte 0 of mdat/sample_data.
        /// </summary>
        /// <returns></returns>
        private long ComputeMdatByte0Offset()
        {
            return (long)(Header.ComputeSize() + Data.ComputeBaseSizeBox());
        }

        /// <summary>
        /// Returns the current data offset as specified in the tfhd/base_offset + trun/data_offset.
        /// </summary>
        /// <returns>The current data offset as specified in tfhd/base_offset + trun/data_offset.</returns>
        public long GetDataOffset()
        {
            long totalOffset = 0; // In the absence of any data offsets, default offset is byte 0 of moof, which is byte 0 of stream

            if (Header.Track.Header.BaseOffset.HasValue)
            {
                totalOffset += (long)Header.Track.Header.BaseOffset.Value;
            }

            if (Header.Track.TrackRun.DataOffset.HasValue)
            {
                totalOffset += Header.Track.TrackRun.DataOffset.Value;
            }

            return totalOffset;
        }

        /// <summary>
        /// Maximum number of tries to recompute data offset. This is marked as internal to
        /// allow for deep unit testing.
        /// </summary>
        internal int _maxTries = 5;

        /// <summary>
        /// Reset the data offset (tfhd/base_offset + trun/data_offset),
        /// overwriting whatever was there before, to point to byte 0 of mdat/sample_data.
        /// </summary>
        private void ResetDataOffset()
        {
            // It may take more than one try to set data offset, because overwriting the
            // previous values may cause a change in box sizes (see UT's - they exercise this).
            int i = 0;
            for ( ; i < _maxTries; i++)
            {
                // Server/ChannelSink and Dash operate counter to Smooth Spec v6.0
                // which states that tfhd/base_data_offset is relative to mdat and
                // which fails to specify a method of specifying data offset in trun.
                // Instead, they all use trun/data_offset, relative to moof byte 0.
                // We shall do the same here, and in addition, we shall remove any
                // existing tfhd/base_data_offset and overwrite to point to
                // mdatBox.SampleData[0], as is done in ChannelSink and Server code.

                // Note that Smooth spec REQUIRES tfhd and trun to be present. Should
                // this fail to be true, we will crash below.
                Header.Track.Header.BaseOffset = null;
                Header.Track.TrackRun.DataOffset = Convert.ToInt32(ComputeMdatByte0Offset());

                if (ComputeMdatByte0Offset() == Header.Track.TrackRun.DataOffset.Value)
                {
                    break;
                }
            }

            if (i == _maxTries)
            {
                throw new NotSupportedException(string.Format(
                    "Unable to recompute data offset after {0} tries: trun/data_offset = {1} but should be {2}!",
                        _maxTries, Header.Track.TrackRun.DataOffset!.Value, ComputeMdatByte0Offset()));
            }
        }

        /// <summary>
        /// Serialize this fragment to disk.
        /// </summary>
        /// <param name="writer">The MP4Writer to write to.</param>
        /// <param name="setSampleDefaults">True to use sample defaults (duration, size, flags), if possible.
        /// If false, the header is written as-is.</param>
        public void WriteTo(MP4Writer writer, bool setSampleDefaults = true)
        {
            if (setSampleDefaults)
            {
                SetSampleDefaults();
            }
            
            ResetDataOffset();
            Header.WriteTo(writer);
            Data.WriteTo(writer);
        }

        /// <summary>
        /// When preparing a fragment for VOD, or modifying it, it is best to remove
        /// any existing tfxd or tfrf boxes.
        /// </summary>
        public void RemoveTfxdTfrf()
        {
            Header.Track.RemoveChildren((trafChild) =>
                trafChild.ExtendedType == MP4BoxType.tfxd || trafChild.ExtendedType == MP4BoxType.tfrf);

            // If we change the size of the moof we should recompute data offset
            ResetDataOffset();
        }

        /// <summary>
        /// Computes the size of this fragment if it were to be serialized to a stream.
        /// </summary>
        /// <returns>The size of this fragment if it were to be serialized to a stream.</returns>
        public UInt64 ComputeSize()
        {
            return Header.ComputeSize() + Data.ComputeSize();
        }

        /// <summary>
        /// Returns the sdtp box of this fragment, or null if none is found.
        /// </summary>
        private sdtpBox? _sdtp
        {
            get
            {
                return Header.Track.Children.Where(b => b is sdtpBox).SingleOrDefault() as sdtpBox; // This box is optional
            }
        }

        /// <summary>
        /// This method removes the sdtp box from this fragment.
        /// When not using sdtp, we remove it, to avoid accidentally writing out an empty sdtp box.
        /// </summary>
        private void RemoveSdtp()
        {
            sdtpBox? sdtp = _sdtp;
            if (sdtp != null)
            {
                sdtp.Entries.Clear(); // Not strictly necessary, but some of the UTs are looking for this
                Header.Track.Children.Remove(sdtp);
            }
        }

        /// <summary>
        /// Clears all samples from this fragment, including any previous sample defaults.
        /// </summary>
        public void ClearSamples()
        {
            trunBox trun = Header.Track.TrackRun;
            trun.Entries.Clear();
            RemoveSdtp();
            Data.SampleData = null;

            // Delete all sample defaults
            tfhdBox tfhd = Header.Track.Header;
            tfhd.DefaultSampleDuration = null;
            tfhd.DefaultSampleSize = null;
            tfhd.DefaultSampleFlags = null;
            trun.FirstSampleFlags = null;
        }

        /// <summary>
        /// Searches the given samples to see if there are any constant values
        /// which could be represented by a single default value. Note that each
        /// attribute will only report a constant value once a given minimum is
        /// exceeded. The minimum is determined by bit savings: when constant versus
        /// explicit (trunEntry) cost the same number of bits, then we choose the
        /// more flexible explicit trunEntry method (non-default).
        /// </summary>
        /// <param name="samples">The samples to search for constant values.</param>
        /// <param name="defaultDuration">If two or more samples use a constant duration,
        /// it is returned here, otherwise returns null.</param>
        /// <param name="defaultSize">If two or more sizes use a constant size,
        /// it is returned here, otherwise returns null.</param>
        /// <param name="defaultFlags">If three or more samples use constant flags,
        /// they are returned here, otherwise returns null.</param>
        /// <param name="firstSampleFlags">If defaultFlags returns a non-null value and
        /// the first sample flags differ from the rest, then this is a non-null value
        /// representing value of the first sample's flags. Otherwise, it is null.</param>
        private static void GetSampleDefaults(List<Fmp4FragmentSample> samples,
                                              out UInt32? defaultDuration,
                                              out UInt32? defaultSize,
                                              out UInt32? defaultFlags,
                                              out UInt32? firstSampleFlags)
        {
            // Initialize outputs
            defaultDuration = null;
            defaultSize = null;
            defaultFlags = null;
            firstSampleFlags = null;

            // We only choose to use defaults if there is two or more samples. This is because
            // there are no bit savings when using defaults with a single sample, so we would
            // prefer the more simpler, explicit practice of using trunEntry.
            if (samples.Count < 2)
                return;

            Fmp4FragmentSample firstSample = samples[0];
            bool allDurationsAreSame = true;
            bool allSizesAreSame = true;
            bool allFlagsAreSameExFirst = true;
            for (int i = 1; i < samples.Count; i++)
            {
                Fmp4FragmentSample sample = samples[i];

                if (sample.Duration != firstSample.Duration)
                {
                    allDurationsAreSame = false;
                }

                if (sample.Size != firstSample.Size)
                {
                    allSizesAreSame = false;
                }

                if (i > 1)
                {
                    // We compare third and subsequent sample flags with second sample
                    if (sample.Flags != samples[1].Flags)
                    {
                        allFlagsAreSameExFirst = false;
                    }
                }
            }

            if (allDurationsAreSame)
            {
                defaultDuration = firstSample.Duration;
            }

            if (allSizesAreSame)
            {
                defaultSize = firstSample.Size;
            }

            // For the same reason that we refuse to use defaults for a single sample,
            // we also refuse to use default flags for two or fewer samples.
            if (allFlagsAreSameExFirst && samples.Count >= 3)
            {
                defaultFlags = samples[1].Flags;
                if (firstSample.Flags != defaultFlags)
                {
                    firstSampleFlags = firstSample.Flags;
                }
            }
        }

        /// <summary>
        /// A fragment may contain samples which use a mix of default and explicit values for
        /// duration, size and flags. For each of these three attributes, this method checks
        /// if we can use a default value. If so, it removes all explicit values and replaces them
        /// with a default value (saves space). If not, it removes usage of default values
        /// and forces all samples to use explicit values.
        /// <returns>True if any defaults were found, false if all samples are using explicit values.</returns>
        /// </summary>
        public bool SetSampleDefaults()
        {
            List<Fmp4FragmentSample> samples = Samples.ToList();

            UInt32? defaultDuration;
            UInt32? defaultSize;
            UInt32? defaultFlags;
            UInt32? firstSampleFlags;
            GetSampleDefaults(samples, out defaultDuration, out defaultSize, out defaultFlags, out firstSampleFlags);

            // We first make explicit or make default, each sample. We do this first to make use
            // of existing defaults, before overwriting them.
            for (int i = 0; i < samples.Count; i++)
            {
                Fmp4FragmentSample sample = samples[i];

                if (defaultDuration.HasValue)
                {
                    sample.TrunEntry.SampleDuration = null; // Use default
                }
                else
                {
                    sample.TrunEntry.SampleDuration = sample.Duration; // Make explicit
                }

                if (defaultSize.HasValue)
                {
                    sample.TrunEntry.SampleSize = null; // Use default
                }
                else
                {
                    sample.TrunEntry.SampleSize = sample.Size; // Make explicit
                }

                if (defaultFlags.HasValue)
                {
                    sample.TrunEntry.SampleFlags = null; // Use default
                }
                else
                {
                    sample.TrunEntry.SampleFlags = sample.Flags; // Make explicit
                }
            }

            // Now that all samples are prepared, we no longer need to keep the old defaults in place. Overwrite them.
            tfhdBox tfhd = Header.Track.Header;
            trunBox trun = Header.Track.TrackRun;
            tfhd.DefaultSampleDuration = defaultDuration;
            tfhd.DefaultSampleSize = defaultSize;
            tfhd.DefaultSampleFlags = defaultFlags;
            trun.FirstSampleFlags = firstSampleFlags;

            return (defaultDuration.HasValue || defaultSize.HasValue || defaultFlags.HasValue);
        }

        /// <summary>
        /// Before rewriting the samples in the fragment, check the proposed samples for correctness.
        /// </summary>
        /// <param name="samples">The list of samples to check for correctness.</param>
        private static void ValidateIncomingSamples(List<Fmp4FragmentSample> samples)
        {
            for (int i = 0; i < samples.Count; i++)
            {
                Fmp4FragmentSample sample = samples[i];
                Fmp4FragmentSample firstSample = samples[0];

                if (sample.Data != null && (uint)sample.Data.Count != sample.Size)
                {
                    throw new InvalidDataException(
                        string.Format("Cannot add sample #{0}: Data.Length ({1}) does not match Size ({2})!",
                            i, sample.Data.Count, sample.Size));
                }

                // Validate all samples are consistent on null/non-null SDTP
                if ((null == sample.SdtpEntry) != (null == firstSample.SdtpEntry))
                {
                    throw new InvalidDataException(
                        string.Format("SdtpEntry inconsistency: firstSample.SdtpEntry is {0}, but sample {1} has it {2}!",
                            (null == firstSample.SdtpEntry) ? "null" : "non-null", i,
                            (null == sample.SdtpEntry) ? "null" : "non-null"));
                }
            }
        }

        /// <summary>
        /// Deletes the existing samples of this Fmp4Fragment and sets the new samples
        /// to be the given enumeration of samples.
        /// </summary>
        /// <param name="samplesEnum">The new samples for this Fmp4Fragment.</param>
        public void SetSamples(IEnumerable<Fmp4FragmentSample> samplesEnum)
        {
            // Create a deep copy of the source samples (which could be from this very Fmp4Fragment) BEFORE clearing samples.
            List<Fmp4FragmentSample> samples = samplesEnum.Select(s => new Fmp4FragmentSample(s)).ToList();

            // Analyze incoming samples
            ValidateIncomingSamples(samples);

            // Now that we have checked incoming for consistency, we can start making side effects
            ClearSamples();

            int cbNewData = samples.Sum(s => s.Data!.Count);
            var newData = new byte[cbNewData];
            IList<trunEntry> trunEntries = Header.Track.TrackRun.Entries;

            bool needSdtp = (samples.Count > 0 && null != samples[0].SdtpEntry);
            IList<sdtpEntry>? sdtpEntries;
            if (needSdtp)
            {
                Header.Track.Children.Add(new sdtpBox()); // Any previous sdtpBox is gone (ClearSamples)
                sdtpEntries = _sdtp!.Entries;
            }
            else
            {
                RemoveSdtp();
                sdtpEntries = null;
            }

            // Copy the new samples over
            int i = 0;
            int currOffset = 0;
            foreach (Fmp4FragmentSample sample in samples)
            {
                // Explicitly set duration, size and flags. sample.TrunEntry is a deep copy,
                // free to modify and add to my table at will.
                sample.TrunEntry.SampleDuration = sample.Duration;
                sample.TrunEntry.SampleSize = sample.Size;
                sample.TrunEntry.SampleFlags = sample.Flags;
                trunEntries.Add(sample.TrunEntry);

                if (null != sdtpEntries)
                {
                    Debug.Assert(null != sample.SdtpEntry); // Programmer error - previous checks should have prevented this
                    sdtpEntries.Add(sample.SdtpEntry); // sample.SdtpEntry is already deep copy, free to add to table directly.
                }

                sample.Data!.CopyTo(newData, currOffset);
                currOffset += sample.Data.Count;

                // Advance variables
                i += 1;
            }

            Data.SampleData = newData;
        }

        /// <summary>
        /// The number of samples currently in this fragment.
        /// </summary>
        public int SampleCount => Header.Track.TrackRun.Entries.Count;

        /// <summary>
        /// An enumeration of the samples currently in this fragment. The properties of these samples
        /// (TrunEntry, SdtpEntry, Data) point directly within the fragment, meaning that modifications
        /// to the sample are reflected immediately in the fragment. If a larger change is desired,
        /// such as addition or removal of samples or sample data bytes, use the SetSamples() method.
        /// </summary>
        public IEnumerable<Fmp4FragmentSample> Samples
        {
            get
            {
                int currOffset = 0; // We just assume that the data starts at mdatBox.SampleData[0]

                trunBox trun = Header.Track.TrackRun;
                IList<trunEntry> trunEntries = trun.Entries;
                int numSamples = trunEntries.Count;

                // We will provide sdtp if and only if it exists and matches trun entry count
                IList<sdtpEntry>? sdtpEntries = null;
                if (null != _sdtp && _sdtp.Entries.Count == numSamples)
                {
                    sdtpEntries = _sdtp.Entries;
                }

                for (int i = 0; i < numSamples; i++)
                {
                    sdtpEntry? thisSdtpEntry = (null == sdtpEntries) ? null : sdtpEntries[i];
                    UInt32? firstSampleFlags = (0 == i) ? trun.FirstSampleFlags : null;
                    var sample = new Fmp4FragmentSample(Header.Track.Header, trunEntries[i],
                        Data.SampleData, currOffset, thisSdtpEntry, firstSampleFlags);

                    yield return sample;

                    // Update variables
                    currOffset += Convert.ToInt32(sample.Size);
                }
            }
        }
        
        #region equality methods

        /// <summary>
        /// Compare this box to another object.
        /// </summary>
        /// <param name="other">the object to compare equality against</param>
        /// <returns></returns>
        public override bool Equals(Object? other)
        {
            return this.Equals(other as Fmp4Fragment);
        }

        /// <summary>
        /// Returns the hashcode of the object.
        /// </summary>
        /// <returns></returns>
        public override int GetHashCode()
        {
            return Header.GetHashCode() ^ Data.GetHashCode();
        }

        /// <summary>
        /// Compare two Fmp4Fragment's for equality.
        /// </summary>
        /// <param name="other">other Fmp4Fragment to compare against</param>
        /// <returns></returns>
        public bool Equals(Fmp4Fragment? other)
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

            // Note that the base class is not invoked because it is 
            // System.Object, which defines Equals as reference equality. 

            // === Now, compare the fields which are specific to this class ===
            if (Header != other.Header)
                return false;

            if (Data != other.Data)
                return false;

            // If we reach this point, the fields all match
            return true;
        }

        /// <summary>
        /// Compare two Fmp4Fragment objects for equality.
        /// </summary>
        /// <param name="lhs">left hand side of ==</param>
        /// <param name="rhs">right hand side of ==</param>
        /// <returns></returns>
        public static bool operator ==(Fmp4Fragment? lhs, Fmp4Fragment? rhs)
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

        public static bool operator !=(Fmp4Fragment? lhs, Fmp4Fragment? rhs)
        {
            return !(lhs == rhs);
        }

        #endregion

    }
}
