//-----------------------------------------------------------------------
// <copyright company="Microsoft">
//     Copyright (C) Microsoft Corporation. All rights reserved.
// </copyright>
//-----------------------------------------------------------------------

using System.Security.Cryptography;

namespace AMSMigrate.Decryption
{
    /// <summary>
    /// An implementation of the ICryptoTransform interface that implements the 
    /// <see href="https://en.wikipedia.org/wiki/Block_cipher_mode_of_operation#CTR">AES Counter Mode</see>
    /// encryption used for the Azure Media Services Storage Encryption.  
    /// Note that the encryption and decryption transforms are the same for AES Counter Mode.
    /// </summary>
    public class AesCtrTransform : ICryptoTransform
    {
        private const int AesBlockSize = 16;
        private readonly ulong _initializationVector;
        private ICryptoTransform? _transform;

        /// <summary>
        /// Initializes a new instance of the <see cref="AesCtrTransform" /> class.  Note that the given initialization
        /// vector and file offset are used to determine the starting counter value used in the AES Counter Mode algorithm.
        /// </summary>
        /// <param name="transform">an ICryptoTransform that implements the AES algorithm in Electronic Codebook mode (ECB)</param>
        /// <param name="iv">initialization vector for the encryption</param>
        /// <param name="fileOffset">file offset</param>
        public AesCtrTransform(ICryptoTransform transform, ulong iv, long fileOffset)
        {
            if (fileOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fileOffset), "fileOffset cannot be less than zero");
            }

            _initializationVector = iv;
            _transform = transform;
            FileOffset = fileOffset;
        }

        private delegate void AsyncAesDelegate(byte[] data, int offset, int length, long fileOffset);

        /// <summary>
        /// Gets or sets the file offset used to determine the counter value used in the AES Counter Mode algorithm.
        /// </summary>
        public long FileOffset { get; set; }

        #region ICryptoTransformMembers

        /// <summary>
        /// Gets the input block size.
        /// </summary>
        public int InputBlockSize
        {
            get { return 1; }
        }

        /// <summary>
        /// Gets the output block size.
        /// </summary>
        public int OutputBlockSize
        {
            get { return 1; }
        }

        /// <summary>
        /// Gets a value indicating whether multiple blocks can be transformed.
        /// </summary>
        public bool CanTransformMultipleBlocks
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a value indicating whether the current transform can be reused.
        /// </summary>
        public bool CanReuseTransform
        {
            get { return false; }
        }

        /// <summary>
        /// Transforms the specified region of the input byte array and copies the resulting transform to the specified region of the output byte array.
        /// </summary>
        /// <param name="inputBuffer">The input for which to compute the transform. </param>
        /// <param name="inputOffset">The offset into the input byte array from which to begin using data. </param>
        /// <param name="inputCount">The number of bytes in the input byte array to use as data. </param>
        /// <param name="outputBuffer">The output to which to write the transform. </param>
        /// <param name="outputOffset">The offset into the output byte array from which to begin writing data. </param>
        /// <returns>The number of bytes written.</returns>
        public int TransformBlock(byte[] inputBuffer, int inputOffset, int inputCount, byte[] outputBuffer, int outputOffset)
        {
            Array.Copy(inputBuffer, inputOffset, outputBuffer, outputOffset, inputCount);
            AesCtr(outputBuffer, outputOffset, inputCount, FileOffset);
            FileOffset += inputCount;

            return inputCount;
        }

        /// <summary>
        /// Transforms the specified region of the specified byte array.
        /// </summary>
        /// <param name="inputBuffer">The input for which to compute the transform. </param>
        /// <param name="inputOffset">The offset into the byte array from which to begin using data. </param>
        /// <param name="inputCount">The number of bytes in the byte array to use as data. </param>
        /// <returns>The computed transform.</returns>
        public byte[] TransformFinalBlock(byte[] inputBuffer, int inputOffset, int inputCount)
        {
            byte[] tempOutputBuffer = new byte[inputCount];

            if (inputCount != 0)
            {
                TransformBlock(inputBuffer, inputOffset, inputCount, tempOutputBuffer, 0);
            }

            return tempOutputBuffer;
        }

        #endregion

        #region IDisposeMembers

        /// <summary>
        /// Helps implement the IDisposable pattern.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);

            // Take this object off the finalization queue and prevent the finalization
            // code from running a second time.
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Helps implement the IDisposable pattern.
        /// </summary>
        /// <param name="disposing">true to release both managed and unmanaged resources; false to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_transform != null)
                {
                    _transform.Dispose();
                    _transform = null;
                }
            }
        }

        #endregion

        private static void ConvertToBigEndianBytes(ulong original, byte[] outputBuffer, int indexToWriteTo)
        {
            byte[] originalAsBytes = BitConverter.GetBytes(original);

            int destIndex = indexToWriteTo;
            for (int sourceIndex = originalAsBytes.Length - 1; sourceIndex >= 0; sourceIndex--)
            {
                outputBuffer[destIndex] = originalAsBytes[sourceIndex];
                destIndex++;
            }
        }

        private void AesCtr(byte[] data, int offset, int length, long fileOffset)
        {
            if (offset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(offset), offset, "Negative values are not allowed for offset.");
            }

            if (length < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(length), length, "Negative values are not allowed for length.");
            }

            if (fileOffset < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(fileOffset), fileOffset, "Negative values are not allowed for fileOffset.");
            }

            //
            //  The fileOffset represents the number of bytes from the start of the file that
            //  data[offset] was copied from.  We need to convert this byte offset into a block
            //  offset (number of 16 byte blocks from the front of the file) and the byte offset 
            //  within the block.
            //
            long offsetFromStartOfFileToFirstByteToProcess = fileOffset;
            ulong currentBlock = Convert.ToUInt64(offsetFromStartOfFileToFirstByteToProcess) / AesBlockSize;
            int startByteInFirstBlock = Convert.ToInt32(offsetFromStartOfFileToFirstByteToProcess % AesBlockSize);

            //
            //  Calculate the byte length of the cryptostream
            //
            int totalLength = startByteInFirstBlock + length;
            int totalBlockCount = (totalLength / AesBlockSize) + ((totalLength % AesBlockSize > 0) ? 1 : 0);
            int cryptoStreamLength = totalBlockCount * AesBlockSize;

            //
            //  Write the data to encrypt to the cryptostream
            //
            byte[] initializationVectorAsBigEndianBytes = new byte[8];
            byte[] cryptoStream = new byte[cryptoStreamLength];
            ConvertToBigEndianBytes(_initializationVector, initializationVectorAsBigEndianBytes, 0);

            for (int i = 0; i < totalBlockCount; i++)
            {
                Array.Copy(initializationVectorAsBigEndianBytes, 0, cryptoStream, i * 16, initializationVectorAsBigEndianBytes.Length);
                ConvertToBigEndianBytes(currentBlock, cryptoStream, (i * 16) + 8);
                currentBlock++;
            }

            _transform?.TransformBlock(cryptoStream, 0, cryptoStream.Length, cryptoStream, 0);

            int cryptoStreamIndex = startByteInFirstBlock;
            int dataIndex = offset;
            while (dataIndex < (offset + length))
            {
                data[dataIndex] ^= cryptoStream[cryptoStreamIndex];

                // increment our indexes
                cryptoStreamIndex++;
                dataIndex++;
            }
        }
    }
}
