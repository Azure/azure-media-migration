
using System.Diagnostics;

namespace AMSMigrate.Fmp4
{
    /// <summary>
    /// An object model representation for the move extended header object.
    /// </summary>
    public class mvexBox : Box
    {
        /// <summary>
        /// Default constructor for the box.
        /// </summary>
        public mvexBox() :
            base(MP4BoxType.mvex)
        {
        }

        /// <summary>
        /// Deserializing/copy constructor.
        /// </summary>
        /// <param name="box">the box to construct from</param>
        public mvexBox(Box box):
            base(box)
        {
            Debug.Assert(box.Type == MP4BoxType.mvex);
        }

        /// <summary>
        /// Pases the body of box. A mvex is nothing but a collection of child boxes.
        /// </summary>
        protected override void ReadBody()
        {
            ReadChildren();
        }

        /// <summary>
        /// Returns a collection of child boxes that are 'trex' boxes
        /// </summary>
        public IEnumerable<trexBox> Tracks
        {
            get
            {
                return Children.Where((box) => box is trexBox).Cast<trexBox>();
            }
        }

    }
}
