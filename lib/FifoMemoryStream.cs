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
    class FifoMemoryStream {
        private int length;

        private class Segment {
            public byte[] data;
            public int offset;

            public Segment(byte[] data) {
                this.data = data;
                this.offset = 0;
            }
        }

        List<Segment> buffer;

        public FifoMemoryStream() {
            this.length = 0;
            this.buffer = new List<Segment>();
        }

        /// <summary>
        /// Get the number of bytes in the FIFO stream.
        /// </summary>
        /// <returns></returns>
        public int getLength() {
            return this.length;
        }

        /// <summary>
        /// Transfers the ownership of the data array to the FIFO stream.
        /// No copy made. FifoMemoryStream will not make any changes to them.
        /// </summary>
        /// <param name="data">Byte array to add to the FIFO stream.</param>
        public void write(byte[] data) {
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
        public int read(int count, byte[] outBuffer, int outBufferOffset) {
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
    }
}
