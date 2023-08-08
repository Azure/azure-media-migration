
using System.Diagnostics;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// An object model for the moov Box from ISO 14496-12 Sec:8.2.1 
    /// </summary>
    public class moovBox : Box
    {
        /// <summary>
        /// Default constructor for the box.
        /// </summary>
        public moovBox() :
            base(MP4BoxType.moov)
        {
        }

        /// <summary>
        /// Deserializing/copy constructor.
        /// </summary>
        /// <param name="box">the box to construct from</param>
        public moovBox(Box box):
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.moov);
        }

        /// <summary>
        /// Pases the body of box. A moov is nothing but a collection of child boxes.
        /// </summary>
        protected override void ReadBody()
        {
            ReadChildren();
        }


        /// <summary>
        /// Returns a single child header box for this fragment.
        /// </summary>
        public mvhdBox Header
        {
            get
            {
                return Children.Where((box) => box is mvhdBox).Cast<mvhdBox>().Single();
            }
        }

        /// <summary>
        /// Returns a collection of child boxes that are trakBox objects
        /// </summary>
        public IEnumerable<trakBox> Tracks
        {
            get
            {
                return Children.Where((box) => box is trakBox).Cast<trakBox>();
            }
        }

        /// <summary>
        /// Returns the optional extended movie header for this movie.
        /// </summary>
        public mvexBox? ExtendedHeader
        {
            get
            {
                return Children.Where((box) => box is mvexBox).SingleOrDefault() as mvexBox;
            }
        }

    }
}
