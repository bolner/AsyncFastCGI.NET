/*
 * Copyright 2019 Tamas Bolner
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *   http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;

namespace AsyncFastCGI {
    /// <summary>
    /// This class helps the memory-efficient handling of chunks of binary
    /// data. Its helps to avoid memory allocations and copying.
    /// Mostly used in the binary representation of strings, produced by
    /// the client application for output, and in the input data
    /// processing.
    /// </summary>
    class FifoStream {
        /// <summary>
        /// The amount of data in the stream,
        /// which hasn't been read.
        /// </summary>
        private int length;
        private bool isLittleEndian;

        /// <summary>
        /// The data is encapsulated into these objects.
        /// It helps in reading an arbitrary amount, 
        /// regardless of the chunk sizes.
        /// The "offset" shows which part of the data is
        /// read already, if points to the first byte which
        /// hasn't been read.
        /// When a segment is fully read, it is removed
        /// from the FifoStream.
        /// </summary>
        private class Segment {
            public byte[] data;
            public int offset;

            public Segment(byte[] data) {
                this.data = data;
                this.offset = 0;
            }

            public int GetLength() {
                return this.data.Length - offset;
            }
        }

        private List<Segment> buffer;
        private byte[] workBuffer;

        /// <summary>
        /// Constructor
        /// </summary>
        public FifoStream(int workBufferSize) {
            this.length = 0;
            this.buffer = new List<Segment>();
            this.workBuffer = new byte[workBufferSize];
            this.isLittleEndian = BitConverter.IsLittleEndian;
        }

        /// <summary>
        /// Remove all data and reset state.
        /// </summary>
        public void Reset() {
            this.length = 0;
            this.buffer.Clear();
        }

        /// <summary>
        /// Get the number of bytes in the FIFO stream.
        /// </summary>
        /// <returns></returns>
        public int GetLength() {
            return this.length;
        }

        /// <summary>
        /// Transfers the ownership of the data array to the FIFO stream.
        /// No copy made. FifoMemoryStream will not make any changes to them.
        /// </summary>
        /// <param name="data">Byte array to add to the FIFO stream.</param>
        public void Write(byte[] data) {
            this.buffer.Add(new Segment(data));
            this.length += data.Length;
        }

        /// <summary>
        /// Reads bytes from the FIFO stream and writes them into the destination buffer.
        /// Removes the copied bytes from the FIFO stream.
        /// </summary>
        /// <param name="count">The number of bytes to transfer.</param>
        /// <param name="outBuffer">The target buffer, where we copy the bytes to.</param>
        /// <param name="outBufferOffset">The copying to the target buffer starts with this offset.</param>
        /// <returns>The number of bytes written to the output.</returns>
        public int Read(int count, byte[] outBuffer, int outBufferOffset) {
            if (this.buffer.Count < 1) {
                return 0;
            }

            count = Math.Min(count, outBuffer.Length - outBufferOffset);
            if (count < 1) {
                return 0;
            }

            Segment current;
            int currentLength;
            int transferred = 0;
            int remaining = count;

            while(remaining > 0) {
                current = this.buffer[0];
                currentLength = current.data.Length - current.offset;

                if (currentLength > remaining) {
                    Array.Copy(current.data, current.offset, outBuffer, outBufferOffset, remaining);
                    this.length -= remaining;
                    transferred += remaining;
                    current.offset -= remaining;

                    return transferred; // remaining = 0
                } else if (currentLength == remaining) {
                    Array.Copy(current.data, current.offset, outBuffer, outBufferOffset, remaining);
                    this.length -= remaining;
                    transferred += remaining;
                    this.buffer.RemoveAt(0);

                    return transferred; // remaining = 0
                } else {
                    // currentLength < remaining
                    Array.Copy(current.data, current.offset, outBuffer, outBufferOffset, currentLength);
                    this.length -= currentLength;
                    transferred += currentLength;
                    remaining -= currentLength;
                    outBufferOffset += currentLength;
                    this.buffer.RemoveAt(0);

                    if (this.buffer.Count < 1) {
                        return transferred;
                    }
                }
            }

            return transferred;
        }

        /// <summary>
        /// Expects the FastCGI name-value pairs in the data.
        /// Parses them, and returns them as a dictinary.
        /// </summary>
        /// <returns>A dictionary of name-value pairs.</returns>
        public Dictionary<string, string> GetNameValuePairs() {
            int length = Math.Min(this.length, this.workBuffer.Length);
            Dictionary<string, string> dict = new Dictionary<string, string>();

            this.Read(length, this.workBuffer, 0);
            int cursor = 0;
            int nameLength;
            int valueLength;
            string name;
            string value;

            while(cursor < length) {
                nameLength = ParseNameValueLength(ref cursor, length);
                if (nameLength == -1) {
                    break;
                }

                valueLength = ParseNameValueLength(ref cursor, length);
                if (valueLength == -1) {
                    break;
                }

                if (cursor + nameLength + valueLength >= length - 1) {
                    break;
                }

                name = System.Text.Encoding.UTF8.GetString(this.workBuffer, cursor, nameLength);
                cursor += nameLength;
                value = System.Text.Encoding.UTF8.GetString(this.workBuffer, cursor, valueLength);
                cursor += valueLength;

                dict[name] = value;
            }

            return dict;
        }

        private int ParseNameValueLength(ref int cursor, int bufferSize) {
            if (cursor >= bufferSize - 1) {
                return -1;
            }

            if (this.workBuffer[cursor] <= 127) {
                return this.workBuffer[cursor++];
            }

            if (cursor + 3 >= bufferSize - 1) {
                return -1;
            }

            int lenght;

            if (this.isLittleEndian) {
                lenght = ((this.workBuffer[cursor] & 0x7f) << 24) + (this.workBuffer[cursor + 1] << 16)
                    + (this.workBuffer[cursor + 2] << 8) + this.workBuffer[cursor + 3];
            } else {
                lenght = ((this.workBuffer[cursor] & 0x7f)) + (this.workBuffer[cursor + 1] >> 8)
                    + (this.workBuffer[cursor + 2] >> 16) + (this.workBuffer[cursor + 3] >> 24);
            }

            cursor += 4;
            return lenght;
        }

        /// <summary>
        /// Makes a copy of the data inside the FIFO stream.
        /// </summary>
        /// <returns>A byte array containing all data in the stream.</returns>
        public byte[] Copy() {
            byte[] data = new byte[this.length];
            int offset = 0, length;

            foreach(var segment in this.buffer) {
                length = segment.GetLength();
                Array.Copy(segment.data, segment.offset, data, offset, length);
                offset += length;
            }

            return data;
        }
    }
}
