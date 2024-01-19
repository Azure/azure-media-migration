using System.Diagnostics;
using System.Globalization;
using System.Reflection;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// This class creates MP4 boxes from the given stream.
    /// </summary>
    public static class MP4BoxFactory
    {
        /// <summary>
        /// This function parses and returns a single MP4 box from the given stream. It should be
        /// noted that some MP4 boxes, such as 'moof', are container boxes and actually contain many
        /// other boxes. Thus in those cases, "single" MP4 box refers to the single 'moof' box returned,
        /// but in fact many boxes were parsed and returned in the course of returning the single 'moof'.
        /// </summary>
        /// <param name="reader">The MP4Reader to read from.</param>
        /// <param name="initialOffset">The file offset which corresponds with
        /// reader.BaseStream.Position == 0. This is used to get the correct file offset in error messages.</param>
        /// <returns>The next box in the stream.</returns>
        public static Box ParseSingleBox(MP4Reader reader, long initialOffset = 0)
        {
            // First, parse the Box to get type and size
            Box box = new Box(reader, initialOffset);

            // Next, figure out what type of box this is
            Type? boxType;
            bool found;

            if (MP4BoxType.uuid == box.Type)
            {
                found = MP4BoxType.ExtendedType.TryGetValue(box.ExtendedType!.Value, out boxType);
            }
            else
            {
                found = MP4BoxType.CompactType.TryGetValue(box.Type, out boxType);
            }

            if (found && boxType != null)
            {
                // We know what kind of object represents this box. Call that object's deserializing constructor.
                object[] ctorParams = new object[] { box };
                Type[] ctorParamTypes = new Type[] { typeof(Box) };
                ConstructorInfo? ctor = boxType.GetConstructor(ctorParamTypes);
                Debug.Assert(null != ctor); // Notify programmer that the box was registered, but could not find the constructor
                if (null != ctor)
                {
                    var regenedBox = ctor.Invoke(ctorParams) as Box;
                    box = regenedBox!;
                }
            }

            return box;
        }

        /// <summary>
        /// This function parses and returns a specific MP4 box from the given stream. It should be
        /// noted that some MP4 boxes, such as 'moof', are container boxes and actually contain many
        /// other boxes. Thus in those cases, "single" MP4 box refers to the single 'moof' box returned,
        /// but in fact many boxes were parsed and returned in the course of returning the single 'moof'.
        /// 
        /// This function should be called when it is known exactly what the next box WILL be. If the
        /// specified box is not found to be the very next box, then an exception is thrown. This is all
        /// done in the name of security. The act of identifying the box exposes very little for an attacker
        /// to work with, while the act of fully deserializing exposes much more. By calling this function,
        /// we choose to fully deserialize only if the next box matches the requested type.
        /// </summary>
        /// <param name="reader">The MP4Reader to read from.</param>
        /// <param name="initialOffset">The file offset which corresponds with
        /// reader.BaseStream.Position == 0. This is used to get the correct file offset in error messages.</param>
        /// <returns>The next box in the stream, IF it is the specific type requested.</returns>
        public static T ParseSingleBox<T>(MP4Reader reader, long initialOffset = 0) where T : Box
        {
            long startPosition = reader.BaseStream.Position;

            UInt32 size;
            UInt32 type;
            Box.PeekCompactType(out size, out type, reader, initialOffset);

            // Next, figure out what type of box this is
            Type? boxType;
            bool found;
            if (MP4BoxType.uuid == type)
            {
                throw new NotImplementedException("Cannot PeekExtendedType yet.");
            }
            else
            {
                found = MP4BoxType.CompactType.TryGetValue(type, out boxType);
            }

            if (found)
            {
                if (boxType == typeof(T))
                {
                    var singleBox = ParseSingleBox(reader, initialOffset);
                    var targetBox = singleBox as T;

                    return targetBox!;
                }
            }

            // If we reached this stage, this box is NOT the box the caller was expecting.
            // Find out the name of caller's requested type, for the error message
            var requestedBoxTypeDescription = typeof(T).FullName!;
            foreach (KeyValuePair<UInt32, Type> entry in MP4BoxType.CompactType)
            {
                if (entry.Value == typeof(T))
                {
                    requestedBoxTypeDescription = Box.CompactTypeToString(entry.Key);
                }
            }

            // Offset should point to start of box
            throw new MP4DeserializeException(requestedBoxTypeDescription, startPosition, initialOffset,
                String.Format(CultureInfo.InvariantCulture, "Instead of expected box, found a box of type {0}!",
                    Box.CompactTypeToString(type)));
        }

    }
}
