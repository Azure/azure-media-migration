
namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// Extensions for handling Fmp4FragmentSample enumerations.
    /// </summary>
    public static class Fmp4FragmentSampleExtensions
    {
        /// <summary>
        /// This method loops the given sequence of samples until the desired number of samples is reached.
        /// </summary>
        /// <param name="samples">A sequence a samples.</param>
        /// <param name="numSamples">The number of samples desired.</param>
        /// <returns>The given sequence of samples, looped if necessary, so that the requested number
        /// of samples is returned.</returns>
        public static IEnumerable<Fmp4FragmentSample> Loop(this IEnumerable<Fmp4FragmentSample> samples, int numSamples)
        {
            int i = 0;
            while (i < numSamples)
            {
                foreach (Fmp4FragmentSample sample in samples)
                {
                    yield return sample;
                    i += 1;

                    if (i >= numSamples)
                    {
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Given a sequence of unsigned integers, returns the count of these unsigned
        /// integers where their sum is strictly greater than to the given threshold.
        /// </summary>
        /// <param name="addends">A sequence of unsigned integers.</param>
        /// <param name="threshold">A threshold for the sequence to strictly exceed.</param>
        /// <returns>The number of unsigned integers whose sum strictly exceeds the given threshold,
        /// or -1 if the unsigned integers do not exceed the threshold. Note that the return value
        /// will never be zero.</returns>
        public static int CountWhenSumGreaterThan(this IEnumerable<UInt32> addends, UInt64 threshold)
        {
            int count = 0;
            UInt64 currentSum = 0;
            foreach (UInt32 addend in addends)
            {
                count += 1;
                currentSum += addend;
                if (currentSum > threshold)
                {
                    return count;
                }
            }

            // If we reach this point, we have not exceeded the threshold
            return -1;
        }

        /// <summary>
        /// Sum of a sequence of UInt32's.
        /// </summary>
        /// <param name="numbers">An enumeration of UInt32's.</param>
        /// <returns>The sum of the sequence of UInt32's.</returns>
        public static UInt64 Sum(this IEnumerable<UInt32> numbers)
        {
            UInt64 sum = 0;
            foreach (UInt32 number in numbers)
            {
                sum += number;
            }
            return sum;
        }

        /// <summary>
        /// A method supplied by the caller to modify a sample.
        /// </summary>
        /// <param name="lastSampleCopy">A copy-constructed Fmp4Fragment (deep TrunEntry/SdtpEntry copy, shallow Data copy).</param>
        /// <param name="sumOfSampleDurationsExLast">The sum of sample durations up to but not including lastSampleCopy.</param>
        /// <param name="numSamplesExLast">The number of samples up to but not including lastSampleCopy.</param>
        public delegate void ModifySample(Fmp4FragmentSample lastSampleCopy, long sumOfSampleDurationsExLast, int numSamplesExLast);

        /// <summary>
        /// This method modifies a copy of the last sample, according to the supplied modifyLastSample method.
        /// In other words, the incoming sequence is passed verbatim, but for the last sample, which is
        /// deep-copied and modified before being returned. In this way, there are no side effects to the incoming
        /// enumeration, unless the caller himself performs it, and even then, the last sample shall not be affected.
        /// </summary>
        /// <param name="samples">An enumeration of samples.</param>
        /// <param name="modifyLastSample">A method which will be called to modify a deep copy of the last sample.</param>
        /// <returns>An enumeration of samples with the last sample deep-copied and modified.</returns>
        public static IEnumerable<Fmp4FragmentSample> ModifyLastSample(this IEnumerable<Fmp4FragmentSample> samples,
                                                                       ModifySample modifyLastSample)
        {
            if (null == samples)
            {
                throw new ArgumentNullException(nameof(samples));
            }
            
            if (null == modifyLastSample)
            {
                throw new ArgumentNullException(nameof(modifyLastSample));
            }

            int numSamplesExLast = 0;
            long sumOfSampleDurationsExLast = 0;
            Fmp4FragmentSample? prevSample = null;
            foreach (Fmp4FragmentSample sample in samples)
            {
                if (null != prevSample)
                {
                    sumOfSampleDurationsExLast += prevSample.Duration;
                    numSamplesExLast += 1;
                    yield return prevSample;
                }

                prevSample = sample;
            }

            // Here we are, on the last sample
            if (null != prevSample)
            {
                Fmp4FragmentSample lastSampleCopy = new Fmp4FragmentSample(prevSample);
                modifyLastSample(lastSampleCopy, sumOfSampleDurationsExLast, numSamplesExLast);
                yield return lastSampleCopy;
            }
        }

        /// <summary>
        /// This method normalizes the last sample duration. It does so by averaging the durations
        /// of all samples except for the last one, then overwriting the last sample's duration with
        /// that average. If the input seqeuence only has a single sample, then no normalization
        /// takes place. The last sample is still deep-copied, but the duration remains unchanged.
        /// </summary>
        /// <param name="samples">An enumeration of samples. This enumeration shall not be modified
        /// by the actions of this method (no side effects from calling this method).</param>
        /// <returns>An enumeration of samples, with the last sample's duration replaced with the
        /// normalized (average) duration. This last sample is a deep copy and so the act of
        /// normalizing the last duration has no side effects on the original sequence.
        /// If the input seqeuence only has a single sample, then no normalization
        /// takes place. The last sample is still deep-copied, but the duration remains unchanged.</returns>
        public static IEnumerable<Fmp4FragmentSample> NormalizeLastSample(this IEnumerable<Fmp4FragmentSample> samples)
        {
            return samples.ModifyLastSample(modifyLastSample:
                (lastSampleCopy, sumOfSampleDurationsExLast, numSamplesExLast) =>
                {
                    // If no other samples, we can't normalize, so don't
                    if (numSamplesExLast > 0)
                    {
                        long normalizedDuration = (sumOfSampleDurationsExLast + numSamplesExLast / 2) / numSamplesExLast;
                        lastSampleCopy.TrunEntry.SampleDuration = Convert.ToUInt32(normalizedDuration);
                    }
                });
        }

        /// <summary>
        /// This method pads the last sample duration so that the sum of all sample durations
        /// is equal to the desired total duration.
        /// </summary>
        /// <param name="samples">An enumeration of samples. This enumeration shall not be modified
        /// by the actions of this method (no side effects from calling this method).</param>
        /// <param name="totalDuration">The desired total duration of all the durations in samples.
        /// If the durations do not already sum to totalDuration, then the last sample will be modified
        /// to achieve the desired sum of totalDuration.</param>
        /// <returns>An enumeration of samples, with the last sample's duration replaced with the
        /// padded duration to make the sum of totalDuration. This last sample is a deep copy and so the act of
        /// normalizing the last duration has no side effects on the original sequence.</returns>
        public static IEnumerable<Fmp4FragmentSample> PadLastSample(this IEnumerable<Fmp4FragmentSample> samples,
                                                                    long totalDuration)
        {
            if (totalDuration < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(totalDuration), "Cannot be less than zero.");
            }

            return samples.ModifyLastSample(modifyLastSample:
                (lastSampleCopy, sumOfSampleDurationsExLast, numSamplesExLast) =>
                {
                    long padDuration = totalDuration - sumOfSampleDurationsExLast;
                    if (padDuration <= 0)
                    {
                        throw new InvalidOperationException(
                            String.Format("Unable to pad to totalDuration ({0}), sum of {1} sample durations has already met or exceeded it ({2})!",
                                totalDuration, numSamplesExLast, sumOfSampleDurationsExLast));
                    }

                    lastSampleCopy.TrunEntry.SampleDuration = Convert.ToUInt32(padDuration);
                });
        }
    }
}
