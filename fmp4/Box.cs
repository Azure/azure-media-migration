

using System.Globalization;
using System.Text;
using System.Diagnostics;
using System.Net;
using System.Collections.ObjectModel;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// Implements the Box object defined in ISO 14496-12.
    /// 
    /// Everything within an MP4 object is a Box. This implementation of Box includes size and type,
    /// as per the spec, but also includes what we call the "Body" or content of the box, which is to say
    /// the bytes referred to by the size field.
    /// 
    /// Thus, just by itself, this Box implementation can either represent an unknown Box (including its contents),
    /// and be serialized as such (its contents will be written as well). Or, if the box is recognized, then
    /// a class which derives from Box is expected to fully consume the Body during its deserialization.
    /// 
    /// The design of this Box supports round-trip deserialization-serialization whenever possible. There
    /// are certain known exceptions to this, such as when Box.Size is expressed as "0" on disk. Thus, if the
    /// Box.Size is expressed using extended (64-bit) sizing, but the value of Size is small and therefore would
    /// fit into 32-bits, this class does not automatically downshift to compact (32-bit) size representation on
    /// serialization. The serialization would continue to use 64-bit size fields. If, however, the user modifies
    /// the box (Box.Dirty becomes true), then size would be written in compact (32-bit) form. Even this can be
    /// overridden to always force 64-bit sizes, but by default, this class does support round-trip.
    /// 
    /// The general pattern for deserialization of a known box type is therefore:
    /// 1) Deserialize Box from a Stream. At this stage it is treated as an "unknown" box. The act of deserializing
    ///    makes the box type available, and saves the Body (contents) for future use.
    /// 2) Use the boxtype to instantiate an object which understands it. This is done via a class derived from Box which
    ///    contains a constructor taking a Box as its argument. The Box (including its contents) are copied to the base
    ///    instance via copy constructor.
    /// 3) The derived class then fully consumes the Body.Body stream as it deserializes.
    /// 4) The chain of derived classes are expected to mark themselves as clean (ie. Dirty = false), but NOT
    ///    to touch the dirty state of another class, including base classes. In this way, Box.Size can be dirty
    ///    because it was set to 0 on-disk, but the derived classes can themselves be clean. The overall value
    ///    of Dirty in this case would be false. This was necessary to decouple the dependency chain, ie. not have a
    ///    base class call the derived class's overridden Dirty function during construction, before the
    ///    derived class is itself constructed.
    /// 
    /// The general pattern for serialization is:
    /// 1) Call Box.WriteTo.
    /// 2) This calls to the virtual function, Box.WriteToInternal, which all derived classes should override.
    /// 3) Serialization therefore always proceeds from derived class down through base classes.
    /// </summary>
    public class Box : IEquatable<Box>
    {
        ~Box()
        {
            _children.CollectionChanged -= Children_CollectionChanged;
        }

        /// <summary>
        /// Constructor for compact type. 
        /// Only used by derived classes.
        /// </summary>
        /// <param name="boxtype">The compact type.</param>
        protected Box(UInt32 boxtype) :
            this(boxtype, null)
        {
        }

        /// <summary>
        /// Constructor for extended type ('uuid' box). 
        /// Only used by derived classes.
        /// </summary>
        /// <param name="extendedType">The extended type.</param>
        protected Box(Guid extendedType)
            :this(MP4BoxType.uuid, extendedType)
        {
        }

        /// <summary>
        /// Constructor to initialize with a type and an optional sub type.
        /// Private constructor to keep all initialization at one place.
        /// </summary>
        /// <param name="boxtype">the type of the box</param>
        /// <param name="extendedType">the extended type if any</param>
        private Box(UInt32 boxtype, Guid? extendedType)
        {
            UInt32 size = 4 + 4; // Size (32) + Type (32)
            Type = boxtype;
            _dirty = true;

            if (extendedType != null)
            {
                ExtendedType = extendedType;
                size += 16; //ExtendedType (128)
            }

            int[] validBitDepths = { 32, 64 };
            Size = new VariableLengthField(validBitDepths, size);

            _children.CollectionChanged += Children_CollectionChanged;
        }

        /// <summary>
        /// Listen to changes to the children.
        /// </summary>
        /// <param name="sender">the sender of the event</param>
        /// <param name="e">event parameter</param>
        private void Children_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            //mark the size as dirty.
            Size.Dirty = true;
        }

        /// <summary>
        /// Copy constructor. Only used by derived classes while deserializing.
        /// </summary>
        /// <param name="box"></param>
        protected Box(Box other)
        {
            _dirty = false; //By default not dirty for deserialization.

            Type = other.Type;
            ExtendedType = other.ExtendedType;
            Size = other.Size;
            
            Body = other.Body;
            BodyInitialOffset = other.BodyInitialOffset;
            BodyPreBytes = other.BodyPreBytes;

            if (Body != null)
            {
                //mark begin of deserialization.
                _deserializing = true;
                
                ReadBody();
                ConsumeBody();

                if (Size.Value == 0)
                {
                    SetDirty();
                }

                _deserializing = false;
            }
            else
            {
                //Copy constructor.  we current do not have member copy so mark as dirty since we don't round trip.
                SetDirty();
            }

            _children.CollectionChanged += Children_CollectionChanged; //register after the children are parsed.
        }

        /// <summary>
        /// This is overridden by the derived classes to read the actual body of the box.
        /// </summary>
        /// <returns>true if the whole body is consumed else false.</returns>
        protected virtual void ReadBody()
        {
        }

        /// <summary>
        /// Deserializing constructor. Currently, deserialization requires a stream which
        /// can provide Length and Seek (eg. CanSeek == true). If it becomes important to
        /// read from non-seeking streams (ie. streaming parser), this class can be modified,
        /// derived, etc. The vast majority of boxes are small enough that writing a streaming
        /// parser would be a waste of time. The main beneficiary would be big boxes like 'mdat'.
        /// </summary>
        /// <param name="reader">Binary reader for use during deserialization.</param>
        /// <param name="initialOffset">The file offset which corresponds with
        /// reader.BaseStream.Position == 0. This is used to get the correct file offset in error messages.</param>
        public Box(MP4Reader reader, long initialOffset = 0)
        {
            long startPosition = reader.BaseStream.Position;
            bool dirty = false; // We just deserialized, so by default we're not dirty - unless size == 0
            long bytesRead;

            try
            {
                int[] validBitDepths = { 32, 64 };
                Size = new VariableLengthField(validBitDepths, reader.ReadUInt32());

                Type = reader.ReadUInt32();

                // Size field may be compact (32-bit) or extended (64-bit), with special case for 0.
                if (1 == Size.Value)
                {
                    // This signals that actual size is in the largesize field
                    Size.Value = reader.ReadUInt64();
                    Size.BitDepth = 64; // Must set this AFTER setting Value, to ensure round-trip
                }
                else if (0 == Size.Value)
                {
                    // This signals that this is last box in file, Size extends to EOF
                    Size.Value = (UInt64)(reader.BaseStream.Length - startPosition);
                    Size.BitDepth = 32; // Must set this AFTER setting Value

                    // This makes us dirty because value on disk is 0, but we set to actual size on disk
                    // This means, by the way, we will never be able to serialize out a size of 0, either.
                    dirty = true;
                }
                else
                {
                    Size.BitDepth = 32; // Must set this AFTER setting Value, to ensure round-trip
                }

                // Check if size is valid - it must clear the number of bytes we have already read
                // We will already have read past the desired size, but I don't care - for instance,
                // if I set Size == 2, just reading the 4 bytes then puts us over. So this cannot be avoided.
                bytesRead = reader.BaseStream.Position - startPosition;
                if ((UInt64)bytesRead > Size.Value)
                {
                    // Reported error offset will point to start of box
                    throw new MP4DeserializeException(TypeDescription, startPosition, initialOffset,
                        String.Format(CultureInfo.InvariantCulture, "Stated size of {0} is too small, we have already read {1} bytes!",
                            Size.Value, bytesRead));
                }

                if (MP4BoxType.uuid == Type)
                {
                    const long sizeOfGuid = 16;

                    // Before reading the GUID, check if we have enough size
                    if ((UInt64)(bytesRead + sizeOfGuid) > Size.Value)
                    {
                        // Reported error offset will point to start of box
                        throw new MP4DeserializeException(TypeDescription, startPosition, initialOffset,
                            String.Format(CultureInfo.InvariantCulture, "Stated size of {0} is too small, must be at least {1} to clear the extended user type",
                                Size.Value, bytesRead + sizeOfGuid));
                    }

                    ExtendedType = reader.ReadGuid();
                }
                else
                {
                    Debug.Assert(null == ExtendedType);
                }
            }
            catch (EndOfStreamException ex)
            {
                String errMsg;

                errMsg = String.Format(CultureInfo.InvariantCulture, "Unexpected end-of-stream, box.Size was {0}, stream length was {1}",
                             Size!.Value, reader.BaseStream.Length - startPosition);

                // Reported error offset will point to start of box
                throw new MP4DeserializeException(TypeDescription, startPosition, initialOffset, errMsg, ex);
            }

            // We are not dirty, we just deserialized - unless Box.Size was 0 on disk.
            Size.Dirty = dirty;

            SaveBody(reader, initialOffset, startPosition);

        }

        /// <summary>
        /// Save the content of the body of the box as a stream.
        /// </summary>
        /// <param name="reader">The reader from which the box is being parsed</param>
        /// <param name="initialOffset">The file offset that corresponds to reader.BaseStreamPosition == 0</param>
        /// <param name="startPosition">The position with in the stream where the box starts.</param>
        private void SaveBody(MP4Reader reader, long initialOffset, long startPosition)
        {
            // Save the remainder of the stream for further processing by derived classes
            long bytesRead = reader.BaseStream.Position - startPosition;

            if (Size.Value > Int32.MaxValue)
            {
                // This size is so large that when casting to integer below, it will go negative.
                // Any attempt to allocate this amount of memory (actually, even less than this),
                // will fail, so bail out now.

                // Reported error offset will point to start of Box.Body
                throw new MP4DeserializeException(TypeDescription, startPosition + bytesRead, initialOffset,
                    String.Format(CultureInfo.InvariantCulture, "Error saving Box.Body. Size of {0} is too large for this implementation of Box to handle.",
                    Size.Value));
            }

            // Before actually allocating the array and reading from the stream, let's see if the
            // stream length is sufficient to satisfy our request.
            int bytesToCopy = (int)(Size.Value - (UInt64)bytesRead);
            if (bytesToCopy > reader.BaseStream.Length - reader.BaseStream.Position)
            {
                long shortfall = bytesToCopy - (reader.BaseStream.Length - reader.BaseStream.Position);

                // Reported error offset will point to start of Box.Body
                throw new MP4DeserializeException(TypeDescription, startPosition + bytesRead, initialOffset,
                    String.Format(CultureInfo.InvariantCulture, "Error saving Box.Body. About to read {0} bytes, stream length is {1} bytes short",
                        bytesToCopy, shortfall));
            }

            Byte[] bytes = new Byte[bytesToCopy];
            int bytesCopied = reader.BaseStream.Read(bytes, 0, bytes.Length);

            // Even though the stream length was checked to be sufficient, the stream could still end up under delivering
            // Check for this here.
            if (bytesCopied < bytesToCopy)
            {
                // Reported error offset will point to start of Box.Body
                throw new MP4DeserializeException(TypeDescription, startPosition + bytesRead, initialOffset,
                    String.Format(CultureInfo.InvariantCulture, "Error saving Box.Body. Trying to read {0} bytes, hit end-of-stream {1} bytes short",
                        bytesToCopy, bytesToCopy - bytesCopied));
            }

            MemoryStream stream = new MemoryStream(bytes);
            Body = new MP4Reader(stream);
            BodyInitialOffset = initialOffset + startPosition + bytesRead;
            BodyPreBytes = bytesRead;
        }

        /// <summary>
        /// This helper function allows non-Box entities to peek at the size and compact type of the next box
        /// in the stream. This is used to enhance security, because once we commit to deserializing the full
        /// box, we are compelled to allocate memory to store the body.
        /// </summary>
        /// <param name="size">Returns the compact size of this box.</param>
        /// <param name="type">Returns the compact type of this box.</param>
        /// <param name="reader">The MP4Reader to read from.</param>
        /// <param name="initialOffset">The file offset which corresponds with
        /// reader.BaseStream.Position == 0. This is used to get the correct file offset in error messages.</param>
        public static void PeekCompactType(out UInt32 size,
                                           out UInt32 type,
                                           BinaryReader reader,
                                           long initialOffset = 0)
        {
            long startPosition = reader.BaseStream.Position;
            bool haveSize = false;

            // Initialize outputs
            size = 0;
            type = 0;

            try
            {
                size = reader.ReadUInt32();
                haveSize = true;
                type = reader.ReadUInt32();

                reader.BaseStream.Position = startPosition; // Rewind stream
            }
            catch (EndOfStreamException ex)
            {
                String errMsg;
                if (false == haveSize)
                {
                    errMsg = String.Format(CultureInfo.InvariantCulture, "Unexpected end-of-stream, box.Size could not be deserialized");
                }
                else
                {
                    errMsg = String.Format(CultureInfo.InvariantCulture, "Unexpected end-of-stream, box.Size was {0}, stream length was {1}",
                        size, reader.BaseStream.Length - startPosition);
                }

                // Reported error offset will point to start of box
                throw new MP4DeserializeException("(unknown box)", startPosition, initialOffset, errMsg, ex);
            }
        }

        /// <summary>
        /// True if this box matches the on-disk representation, either because we deserialized and
        /// no further changes were made, or because we just saved to disk and no further changes were made.
        /// The derived class is expected to override this property to incorporate its own Dirty state
        /// in the result.
        /// </summary>
        public virtual bool Dirty
        {
            get
            {
                if (_dirty)
                {
                    return true;
                }

                if (Size.Dirty)
                    return true;

                //If any of the child boxes are dirty then we are dirty too.
                foreach (Box child in _children)
                {
                    if (child.Dirty)
                        return true;
                }

                // If we reached this stage, we are not dirty
                return false;
            }
            set
            {
                if (_deserializing)
                {
                    throw new InvalidOperationException("Cannot set Dirty while deserializing. Call SetDirty instead");
                }

                _dirty = value;

                Size.Dirty = value;
                
                foreach (Box child in _children)
                {
                    child.Dirty = value;
                }
            }
        }

        /// <summary>
        /// Marks the box contents to be dirty so that size needs to be recomputed.
        /// </summary>
        protected void SetDirty()
        {
            _dirty = true;
        }

        /// <summary>
        /// This variable tracks miscellaneous sources of dirtiness from direct members of this class,
        /// </summary>
        private  bool _dirty;

        /// <summary>
        /// This variable indicate we are currently deserializing the box. 
        /// Derived classes should only call SetDirty() to mark the box dirty instead of .Dirty=true.
        /// </summary>
        private bool _deserializing;


        /// <summary>
        /// Returns the child boxes of this box.
        /// </summary>
        public IList<Box> Children => _children;


        /// <summary>
        /// An observable collection of child boxes for this box.
        /// </summary>
        private ObservableCollection<Box> _children = new ObservableCollection<Box>();

        /// <summary>
        /// The size of the box, as currently expressed on disk. The latter is especially important to note.
        /// This field DOES NOT return the current size of the box, especially after modifications have been made.
        /// 
        /// If Box.Dirty is false, then the caller can be assured that Box.Size represents the declared box size
        /// in the bitstream as encountered during deserialization, or as written during serialization.
        /// 
        /// If Box.Dirty is true, then changes have been made (for instance, entries added to 'trun', etc). The
        /// Box.Size does not automatically update as a result of this. If it is necessary to know what the size
        /// currently is, then one should call Box.ComputeSize().
        /// 
        /// The other thing to mention is the special case there this is the last box in the file, and the box size
        /// was expressed as "0" on disk (meaning that the box extends to end of file). This box object does not
        /// have a representation for this state, so it simply substitutes the actual number of bytes from here to
        /// end of file. In such a case, Box.Dirty will be set to true to signal the fact that Size, although correct,
        /// does not represent the on-disk value.
        /// 
        /// By default, the bit-depth either matches the on-disk bit depth, or else is automatically adjusted when
        /// Box.Dirty is true. The BitDepth can also be set and locked, if desired. See VariableLengthField for more details.
        /// </summary>
        public VariableLengthField Size { get; private set; }

        /// <summary>
        /// The "type" field as per ISO 14496-12.
        /// </summary>
        public UInt32 Type { get; private set; }

        /// <summary>
        /// The extended_type field as per ISO 14496-12. This is set to null if this box uses a compact type.
        /// </summary>
        public Guid? ExtendedType { get; private set; }

        /// <summary>
        /// Returns a human-readable string describing the "type" field. If this is a compact
        /// type, then the ASCII representation is returned within single-quotes, eg. 'moof'.
        /// If user extended type, then this may return something like,
        /// uuid:d899e70d-cc78-475a-aaa6-0a890d62895c.
        /// </summary>
        public string TypeDescription
        {
            get
            {
                string result;

                if (MP4BoxType.uuid == Type)
                {
                    // It's OK if ExtendedType is null, no exceptions appear to be thrown
                    result = String.Format(CultureInfo.InvariantCulture, "uuid:{0}", ExtendedType);
                }
                else
                {
                    Debug.Assert(null == ExtendedType);
                    result = CompactTypeToString(Type);
                }

                return result;
            }
        }

        /// <summary>
        /// Helper function to convert a compact type to a string, eg. 'trun'.
        /// </summary>
        /// <param name="type">The compact type value to convert to string.</param>
        /// <returns>A string form of the compact type, eg. 'trun'.</returns>
        public static string CompactTypeToString(UInt32 type)
        {
            Encoding wireEncoding = Encoding.UTF8;
            Byte[] typeBytes = BitConverter.GetBytes(IPAddress.NetworkToHostOrder((Int32)type));
            return String.Format(CultureInfo.InvariantCulture, "'{0}'", wireEncoding.GetString(typeBytes));
        }

        /// <summary>
        /// The contents of this Box. 
        /// This is used by derived classes to deserialize themselves.
        /// </summary>
        protected MP4Reader? Body { get; private set; }

        /// <summary>
        /// The file offset which corresponds with Body.BaseStream.Position == 0. This is used
        /// by derived classes to get the correct file offset in error messages.
        /// </summary>
        protected long BodyInitialOffset { get; private set; }

        /// <summary>
        /// The number of bytes from start of this box to start of Body (ie. the bytes used to
        /// convey size/type/extended size/extended type). This is used to by derived classes
        /// to report an offset to the start of this box when an error is encountered during
        /// deserialization.
        /// </summary>
        protected long BodyPreBytes { get; private set; }

        /// <summary>
        /// This is called at the end of deserialization, when box has fully consumed the Body.
        /// </summary>
        private void ConsumeBody()
        {
            if (Body?.BaseStream.Position < Body?.BaseStream.Length)
            {
                // Reported error offset will point just past end of currently processed Box
                throw new MP4DeserializeException(TypeDescription, Body.BaseStream.Position, BodyInitialOffset,
                    String.Format(CultureInfo.InvariantCulture, "Failure to fully read (or skip) Box.Body during deserialization, {0} bytes unhandled",
                        Body.BaseStream.Length - Body.BaseStream.Position));
            }

            Body?.Dispose();
            Body = null;
            BodyInitialOffset = 0;
            BodyPreBytes = 0;
        }

        /// <summary>
        /// This computes the size of this box, particularly after modifications have been made.
        /// If Box.Dirty is false, one would expect ComputeSize() to exactly equal the on-disk
        /// representation (Box.Size). If Box.Dirty is true, then ComputeSize() will return the
        /// current size of the box, while Box.Size will be stale (ie. incorrect).
        /// 
        /// It is expected that derived classes will override this method and aggregate their
        /// own sizes plus their base class sizes.
        /// </summary>
        /// <returns>The current size of this box, if it were to be written to disk now.</returns>
        public virtual UInt64 ComputeSize()
        {
            UInt64 size = ComputeLocalSize();
            
            foreach (Box child in _children)
            {
                size += child.ComputeSize();
            }

            return size;
        }

        /// <summary>
        /// Calculates the current size of just this class (base classes excluded). This is called by
        /// ComputeSize().
        /// 
        /// It is expected that derived classes will "new" this method (ie. hide the inherited method).
        /// </summary>
        /// <returns>The current size of just the fields from this box.</returns>
        protected UInt64 ComputeLocalSize()
        {
            UInt64 thisSize = 0;

            thisSize += 4; // 32-bit size field
            thisSize += 4; // 32-bit type field

            if (64 == Size.BitDepth)
            {
                thisSize += 8; // 64-bit size field
            }

            if (MP4BoxType.uuid == Type)
            {
                thisSize += 16; // Extended type GUID
            }

            if (null != Body)
            {
                thisSize += (UInt64)Body.BaseStream.Length;
            }

            return thisSize;
        }

        /// <summary>
        /// Computes the size of the box portion (size, type) of a derived box type,
        /// for the purpose of computing offsets within the derived box type. For instance,
        /// by calling this method, we can calculate the offset within the mdatBox where
        /// mdatBox.SampleData starts.
        /// </summary>
        /// <returns>The size of the box portion of the current derived box type.</returns>
        public UInt64 ComputeBaseSizeBox()
        {
            return ComputeLocalSize();
        }

        /// <summary>
        /// This function serializes the box to disk. In doing so, Box.Size must be updated. At the end of
        /// serialization, Box.Dirty will be false. This is a base class function which engages the derived
        /// class by calling the virtual function, WriteToInternal, which is not exposed to the public.
        /// By doing it this way, things which are always done during serialization (updating Size if Dirty,
        /// setting Dirty to false at the end) can be done once here, and not require cut-and-paste into
        /// all derived classes.
        /// </summary>
        /// <param name="writer">The MP4Writer to write to.</param>
        public void WriteTo(MP4Writer writer)
        {
            long startPosition = writer.BaseStream.Position;

            // The top WriteTo call is responsible for setting Box.Size, but only if Dirty
            if (Dirty)
            {
                UInt64 newSize = ComputeSize();
                Size.Value = newSize;
            }

            // This call actually performs the serialization
            WriteToInternal(writer);

            WriteChildren(writer);

            // Now handle post-serialization tasks
            Dirty = false; // We just wrote to disk, our contents match exactly

            // Confirm that we wrote what we said we would write
            Debug.Assert(writer.BaseStream.Position - startPosition == (long)Size.Value ||
                         Stream.Null == writer.BaseStream);
        }

        /// <summary>
        /// Serialize the child boxes to the writer.
        /// </summary>
        /// <param name="writer">The writer to write the boxes.</param>
        protected virtual void WriteChildren(MP4Writer writer)
        {
            //Write the child boxes.
            foreach (Box child in _children)
            {
                child.WriteTo(writer);
            }

        }

        /// <summary>
        /// This function does the actual work of serializing this class to disk. It is expected that derived
        /// classes will override this function. By the time this function is called, Box.Size has already been
        /// updated to reflect the current size of the box as reported by ComputeSize(). Also, this function
        /// does not need to set dirty state. It should simply serialize.
        /// </summary>
        /// <param name="writer">The MP4Writer to write to.</param>
        protected virtual void WriteToInternal(MP4Writer writer)
        {
            UInt64 thisSize = ComputeLocalSize();
            long startPosition = writer.BaseStream.Position;

            bool largesize;
            if (64 == Size.BitDepth)
            {
                largesize = true;
            }
            else
            {
                largesize = false;
            }

            // Write the 32-bit size field
            if (largesize)
            {
                writer.WriteUInt32(1);
            }
            else
            {
                writer.WriteUInt32((UInt32)Size.Value);
            }

            // Write the type field
            writer.WriteUInt32(Type);

            // Write the 64-bit size field
            if (largesize)
            {
                writer.WriteUInt64(Size.Value);
            }

            // Write the extended type field
            if (MP4BoxType.uuid == Type && ExtendedType != null)
            {
                writer.Write(ExtendedType.Value);
            }

            // If the body was not consumed, this means this box was unknown. Write out the Body, unchanged
            if (null != Body)
            {
                Body.BaseStream.Position = 0; // Rewind the stream
                Body.BaseStream.CopyTo(writer.BaseStream);
            }

            // Confirm that we wrote what we said we would write
            Debug.Assert(writer.BaseStream.Position - startPosition == (long)thisSize ||
                         Stream.Null == writer.BaseStream);
        }


        /// <summary>
        /// Parse the body to read the child boxes and add it to the _children collection.
        /// </summary>
        protected void ReadChildren()
        {
            long fileOffset = BodyInitialOffset;

            if (Body != null)
            {
                // This is a container box, so use the MP4BoxFactory to deserialize the contained boxes.
                while (Body.BaseStream.Position < Body.BaseStream.Length)
                {
                    long streamOffset = Body.BaseStream.Position;

                    Box subbox = MP4BoxFactory.ParseSingleBox(Body, fileOffset);

                    //Make sure we advanced after reading a box. Avoid infinite loop and break if not.
                    if (Body.BaseStream.Position <= streamOffset)
                    {
                        break;
                    }

                    //check we did not cross the stream while parsing a box.
                    Debug.Assert(Body.BaseStream.Position <= Body.BaseStream.Length);

                    _children.Add(subbox);

                    fileOffset += Body.BaseStream.Position;
                }
            }
        }

        /// <summary>
        /// Helper function to get exactly one box of the requested type,
        /// and to throw if exactly one cannot be found.
        /// </summary>
        /// <typeparam name="TChildBoxType">The type of the requested child box.</typeparam>
        /// <returns>A box of the requested type.</returns>
        public TChildBoxType? GetExactlyOneChildBox<TChildBoxType>() where TChildBoxType : Box
        {
            IEnumerable<Box> boxes = _children.Where(b => b is TChildBoxType);
            int numBoxes = boxes.Count();
            if (1 != numBoxes)
            {
                // Perform a reverse dictionary lookup to find the name of this child box
                string childBoxName;
                IEnumerable<KeyValuePair<UInt32,Type>> compactBoxTypes = MP4BoxType.CompactType.Where(entry => entry.Value == typeof(TChildBoxType));
                if (1 != compactBoxTypes.Count())
                {
                    // We don't want to throw an exception, as we are already in the middle of throwing one, so just use class name
                    childBoxName = typeof(TChildBoxType).Name;
                }
                else
                {
                    childBoxName = Box.CompactTypeToString(compactBoxTypes.Single().Key);
                }

                throw new InvalidOperationException(
                    String.Format(CultureInfo.InvariantCulture, "{0} expected exactly 1 {1}, instead found {2}!",
                        GetType().Name, childBoxName, numBoxes));
            }

            return boxes.Single() as TChildBoxType;
        }

        /// <summary>
        /// Remove those children selected by the given child selector from Children property (list).
        /// </summary>
        /// <param name="shouldRemove">A function which will select the children to remove.</param>
        public void RemoveChildren(Func<Box, bool> shouldRemove)
        {
            if (null == shouldRemove)
            {
                throw new ArgumentNullException(nameof(shouldRemove));
            }

            int numChildren = _children.Count;
            for (int i = numChildren - 1; i >= 0; i--)
            {
                if (shouldRemove(_children[i]))
                {
                    _children.RemoveAt(i);
                }
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
        // Equals(base) is not overridden, then the base implementation of
        // Equals(base) is used, which will not compare derived fields.
        //=====================================================================

        /// <summary>
        /// Object.Equals override.
        /// </summary>
        /// <param name="other">The object to test equality against.</param>
        /// <returns>True if the this and the given object are equal.</returns>
        public override bool Equals(Object? other)
        {
            return this.Equals(other as Box);
        }

        /// <summary>
        /// Implements IEquatable(Box). This function is virtual and it is expected that
        /// derived classes will override it, so that programs which attempt to test equality
        /// using pointers to base classes will can enjoy results from the fully derived
        /// equality implementation.
        /// </summary>
        /// <param name="obj">The box to test equality against.</param>
        /// <returns>True if the this and the given box are equal.</returns>
        public virtual bool Equals(Box? other)
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

            if (Type != other.Type)
                return false;

            if (ExtendedType != other.ExtendedType)
                return false;

            if (ComputeSize() != other.ComputeSize())
                return false;

            if (!Children.SequenceEqual(other.Children))
                return false;

            if (Body != other.Body)
            {
                if (null == Body || null == other.Body)
                    return false;

                // If we reached this point, both Bodies are non-null and non-equal
                if (Body.BaseStream.Length != other.Body.BaseStream.Length)
                    return false;

                // Same length. Compare the contents of the streams. Ignore different starting points, that is simply state.
                long thisPosition = Body.BaseStream.Position;
                long thatPosition = other.Body.BaseStream.Position;
                
                // Rewind streams
                Body.BaseStream.Position = 0;
                other.Body.BaseStream.Position = 0;

                long bytesTested = 0;
                while (bytesTested < Body.BaseStream.Length)
                {
                    int thisByte = Body.BaseStream.ReadByte();
                    int thatByte = other.Body.BaseStream.ReadByte();

                    if (-1 == thisByte || -1 == thatByte)
                    {
                        // Stream stopped returning bytes before Length was reached, bail out
                        return (thisByte == thatByte);
                    }

                    if (thisByte != thatByte)
                    {
                        // Found a difference between the streams
                        return false;
                    }

                    bytesTested += 1;
                }

                // If we reached this stage, the streams are equal. Restore their positions.
                Body.BaseStream.Position = thisPosition;
                other.Body.BaseStream.Position = thatPosition;
            }

            // Ignore Dirty - that's merely a state and does not convey Box content

            // If we reach this point, the fields all match
            return true;
        }

        /// <summary>
        /// Object.GetHashCode override. This must be done as a consequence of overriding
        /// Object.Equals.
        /// </summary>
        /// <returns>Hash code which will be match the hash code of an object which is equal.</returns>
        public override int GetHashCode()
        {
            return Type.GetHashCode(); // Only the Type of a Box is immutable
        }

        /// <summary>
        ///  Override == operation (as recommended by MSDN).
        /// </summary>
        /// <param name="lhs">The box on the left-hand side of the ==.</param>
        /// <param name="rhs">The box on the right-hand side of the ==.</param>
        /// <returns>True if the two boxes are equal.</returns>
        public static bool operator ==(Box? lhs, Box? rhs)
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
        public static bool operator !=(Box? lhs, Box? rhs)
        {
            return !(lhs == rhs);
        }

        #endregion
    }
}
