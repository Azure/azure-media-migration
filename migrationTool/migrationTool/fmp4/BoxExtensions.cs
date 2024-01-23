using System.Text;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// This class contains Box extension methods.
    /// </summary>
    public static class BoxExtensions
    {
        /// <summary>
        /// Clone (deep copy) the given box type by serializing and deserializing.
        /// This is the non-generic method which is used when you don't know ahead of time
        /// what the box is going to be.
        /// Note that this method has side effects on the given box.Dirty flag (will go false).
        /// </summary>
        /// <typeparam name="BoxT">The box type.</typeparam>
        /// <param name="box">The box instance to round-trip. Note that as a result of serialization,
        /// the box.Dirty flag will become false.</param>
        /// <returns>The cloned box.</returns>
        public static Box CloneBySerialization(this Box box)
        {
            return CloneBySerialization<Box>(box, parseBox: (reader) => MP4BoxFactory.ParseSingleBox(reader));
        }

        /// <summary>
        /// Clone (deep copy) the given box type by serializing and deserializing.
        /// This is the generic method which is used when you know ahead of time what the
        /// box is going to be. If your prediction is wrong, an MP4DeserializeException will be thrown.
        /// Note that this method has side effects on the given box.Dirty flag (will go false).
        /// </summary>
        /// <typeparam name="BoxT">The box type.</typeparam>
        /// <param name="box">The box instance to round-trip. Note that as a result of serialization,
        /// the box.Dirty flag will become false.</param>
        /// <returns>The cloned box.</returns>
        public static BoxT CloneBySerialization<BoxT>(this BoxT box) where BoxT : Box
        {
            return CloneBySerialization<BoxT>(box, parseBox: (reader) => MP4BoxFactory.ParseSingleBox<BoxT>(reader));
        }

        /// <summary>
        /// Clone (deep copy) the given box type by serializing and deserializing.
        /// This is the generic method which is used when you know ahead of time what the
        /// box is going to be. If your prediction is wrong, an MP4DeserializeException will be thrown.
        /// Note that this method has side effects on the given box.Dirty flag (will go false).
        /// </summary>
        /// <typeparam name="BoxT">The box type.</typeparam>
        /// <param name="box">The box instance to round-trip. Note that as a result of serialization,
        /// the box.Dirty flag will become false.</param>
        /// <param name="parseBox">The function which will parse and return a BoxT using the given reader.</param>
        /// <returns>The cloned box.</returns>
        private static BoxT CloneBySerialization<BoxT>(this BoxT box, Func<MP4Reader, BoxT> parseBox) where BoxT : Box
        {
            using (var stream = new MemoryStream())
            using (var writer = new MP4Writer(stream, Encoding.Default, leaveOpen: true))
            using (var reader = new MP4Reader(stream, Encoding.Default, leaveOpen: true))
            {
                box.WriteTo(writer);
                stream.Position = 0;
                BoxT result = parseBox(reader);

                // We get to choose our policy for trunCopy.Dirty: always true/false, or copy from source.
                // At the moment, we choose always dirty, to reflect that it has never been serialized.
                result.Dirty = true;
                return result;
            }
        }
    }
}
