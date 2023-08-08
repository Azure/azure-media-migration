
using System.Globalization;
using System.Runtime.Serialization;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// This class is used to report errors which have occurred during MP4 deserialization.
    /// </summary>
    [Serializable]
    public class MP4DeserializeException : Exception
    {
        /// <summary>
        /// Empty implementation to satisfy CA1032 (Implement standard exception constructors).
        /// </summary>
        public MP4DeserializeException()
            : base()
        {
        }

        /// <summary>
        /// Empty implementation to satisfy CA1032 (Implement standard exception constructors).
        /// </summary>
        public MP4DeserializeException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Empty implementation to satisfy CA1032 (Implement standard exception constructors).
        /// </summary>
        public MP4DeserializeException(string message, Exception ex)
            : base(message, ex)
        {
        }
        
        /// <summary>
        /// Creates an MP4DeserializeException.
        /// 
        /// Example error message:
        ///
        /// "Error deserializing 'trun' at offset 512: Could not read Version and Flags for FullBox,
        /// only 3 bytes left in reported size, expected 4"
        /// 
        /// </summary>
        /// <param name="boxTypeDescription">The description of the box type which could not be
        /// deserialized, eg. 'trun'.</param>
        /// <param name="relativeOffset">The offset to add to initialOffset to produce the final
        /// offset. Can be negative. In general, the expected value here is the value which will
        /// produce the offset to the start of the current box. In other words, if an error occurred
        /// while deserializing the version/flags for FullBox, the offset we report should point
        /// to the start of the current Box, ie. should point to the MSB of the Box.Size field.</param>
        /// <param name="initialOffset">Typically the initialOffset of the stream currently being
        /// read from. This is simply added to relativeOffset to make a single number.</param>
        /// <param name="message">The error message.</param>
        public MP4DeserializeException(string boxTypeDescription,
                                       long relativeOffset,
                                       long initialOffset,
                                       String message)
            : base(String.Format(CultureInfo.InvariantCulture, "Error deserializing {0} at offset {1}: {2}",
                 boxTypeDescription, initialOffset + relativeOffset, message))
        {
        }

        /// <summary>
        /// Creates an MP4DeserializeException.
        /// 
        /// Example error message:
        ///
        /// "Error deserializing 'trun' at offset 512: Could not read Version and Flags for FullBox,
        /// only 3 bytes left in reported size, expected 4"
        /// 
        /// </summary>
        /// <param name="boxTypeDescription">The description of the box type which could not be
        /// deserialized, eg. 'trun'.</param>
        /// <param name="relativeOffset">The offset to add to initialOffset to produce the final
        /// offset. Can be negative. In general, the expected value here is the value which will
        /// produce the offset to the start of the current box. In other words, if an error occurred
        /// while deserializing the version/flags for FullBox, the offset we report should point
        /// to the start of the current Box, ie. should point to the MSB of the Box.Size field.</param>
        /// <param name="initialOffset">Typically the initialOffset of the stream currently being
        /// read from. This is simply added to relativeOffset to make a single number.</param>
        /// <param name="message">The error message.</param>
        /// <param name="innerException">The exception which caused this MP4DeserializeException.</param>
        public MP4DeserializeException(string boxTypeDescription,
                                       long relativeOffset,
                                       long initialOffset,
                                       String message,
                                       Exception innerException)
            : base(String.Format(CultureInfo.InvariantCulture, "Error deserializing {0} at offset {1}: {2}",
                 boxTypeDescription, initialOffset + relativeOffset, message), innerException)
        {
        }

        /// <summary>
        /// Empty implementation to satisfy CA1032 (Implement standard exception constructors).
        /// </summary>
        protected MP4DeserializeException(SerializationInfo info, StreamingContext context)
            : base(info, context)
        {
        }
    }
}
