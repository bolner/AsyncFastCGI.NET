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
using System.IO;
using System.Threading.Tasks;
using System.Net.Sockets;

namespace AsyncFastCGI {
    class Record {
        /*
            Size constraints
        */
        public const int HEADER_SIZE = 8;
        public const int MAX_CONTENT_SIZE = 65535;
        public const int MAX_PADDING_SIZE = 255;
        public const int MAX_RECORD_SIZE = HEADER_SIZE + MAX_CONTENT_SIZE + MAX_PADDING_SIZE;

        /*
            Record types
        */
        public const int TYPE_BEGIN_REQUEST = 1;
        public const int TYPE_ABORT_REQUEST = 2;
        public const int TYPE_END_REQUEST = 3;
        public const int TYPE_PARAMS = 4;
        public const int TYPE_STDIN = 5;
        public const int TYPE_STDOUT = 6;
        public const int TYPE_STDERR = 7;
        public const int TYPE_DATA = 8;
        public const int TYPE_GET_VALUES = 9;
        public const int TYPE_GET_VALUES_RESULT = 10;
        public const int TYPE_UNKNOWN_TYPE = 11;

        /*
            Request Roles
        */
        public const int ROLE_RESPONDER = 1;
        public const int ROLE_AUTHORIZER = 2;
        public const int ROLE_FILTER = 3;

        /*
            Protocol status values for an end request response.
        */
        public const int PROTOCOL_STATUS_REQUEST_COMPLETE = 0;
        public const int PROTOCOL_STATUS_CANT_MPX_CONN = 1;
        public const int PROTOCOL_STATUS_OVERLOADED = 2;
        public const int PROTOCOL_STATUS_UNKNOWN_ROLE = 3;

        private byte[] buffer;
        private int bufferEnd = 0;

        private bool headerReconstructed = false;
        private bool completeRecordReconstructed = false;

        private bool isLittleEndian;

        private byte recordVersion = 0;
        private byte recordType = 0;
        private UInt16 recordRequestID = 0;
        private UInt16 recordContentLength = 0;
        private int recordLength = 0;
        private UInt16 recordPaddingLength = 0;

        public int GetRecordType() {
            return this.recordType;
        }

        public int GetLength() {
            return this.recordLength;
        }

        public UInt16 GetRequestID() {
            return this.recordRequestID;
        }

        public UInt16 GetContentLength() {
            return this.recordContentLength;
        }

        public Record() {
            this.buffer = new byte[MAX_RECORD_SIZE];
            this.isLittleEndian = BitConverter.IsLittleEndian;
        }

        /// <summary>
        /// Use it in an iteration. Keeps reading from the network
        /// stream until at least one complete record is reconstructed.
        /// </summary>
        /// <returns>True if a complete record has been reconstructed, false otherwise.</returns>
        public async Task<bool> ProcessInputAsync(NetworkStream stream) {
            bool skipRead = false;
            if (this.completeRecordReconstructed) {
                skipRead = this.StartNextRecord();
            }

            if (!skipRead) {
                int remaining = MAX_RECORD_SIZE - bufferEnd;
                int bytesRead = await stream.ReadAsync(this.buffer, this.bufferEnd, remaining);
                if (bytesRead == 0) {
                    await Task.Delay(10);
                    return false;
                }
                this.bufferEnd += bytesRead;
            }
        
            /*
                Reconstruct the header
            */
            if (!headerReconstructed) {
                if (this.bufferEnd + 1 < HEADER_SIZE) {
                    return false;
                }

                this.recordVersion = this.buffer[0];
                this.recordType = this.buffer[1];
                if (this.isLittleEndian) {
                    this.recordRequestID = (UInt16)((this.buffer[2] << 8) | this.buffer[3]);
                    this.recordContentLength = (UInt16)((this.buffer[4] << 8) | this.buffer[5]);
                } else {
                    this.recordRequestID = (UInt16)((this.buffer[3] << 8) | this.buffer[2]);
                    this.recordContentLength = (UInt16)((this.buffer[5] << 8) | this.buffer[4]);
                }
                this.recordPaddingLength = this.buffer[6];

                this.recordLength = HEADER_SIZE + this.recordContentLength + this.recordPaddingLength;
                this.headerReconstructed = true;
            }

            if (this.bufferEnd >= this.recordLength) {
                this.completeRecordReconstructed = true;
                return true;
            }

            return false;
        }

        private bool StartNextRecord() {
            int leftover = this.bufferEnd - this.recordLength;
            if (leftover > 0) {
                Array.Copy(this.buffer, this.recordLength, this.buffer, 0, leftover);
            }

            this.bufferEnd = leftover;
            this.completeRecordReconstructed = false;
            this.headerReconstructed = false;

            if (leftover > 0) {
                return true;
            }

            return false;
        }

        public void Reset() {
            this.bufferEnd = 0;
            this.completeRecordReconstructed = false;
            this.headerReconstructed = false;
        }

        /// <summary>
        /// Returns the role, which can be: responder, authorizer, filter.
        /// </summary>
        /// <returns>Role identifier value</returns>
        public UInt16 GetRole() {
            if (this.isLittleEndian) {
                return (UInt16)((this.buffer[HEADER_SIZE + 0] << 8) | this.buffer[HEADER_SIZE + 1]);
            }

            return (UInt16)((this.buffer[HEADER_SIZE + 1] << 8) | this.buffer[HEADER_SIZE + 0]);
        }

        /// <summary>
        /// For BEGIN_REQUEST records. If zero, the application closes the connection
        /// after responding to this request. If not zero, the application does not
        /// close the connection after responding to this request; the Web server
        /// retains responsibility for the connection.
        /// </summary>
        /// <returns>0 for closing, 1 for keeping the connection open after this request.</returns>
        public bool IsKeepConnection() {
            return this.buffer[HEADER_SIZE + 2] > 0;
        }

        /// <summary>
        /// Makes a copy of the content data, and pushes it into
        /// the passed FIFO stream.
        /// </summary>
        /// <param name="stream">The stream which receives the data</param>
        public void CopyContentTo(FifoStream stream) {
            byte[] data = new byte[this.recordContentLength];
            Array.Copy(this.buffer, HEADER_SIZE, data, 0, this.recordContentLength);
            stream.Write(data);
        }

        /// <summary>
        /// Converts the record buffer into an STDOUT record, and fills it with data.
        /// </summary>
        /// <param name="requestID">FastCGI request ID</param>
        /// <param name="fifo">Data source. Pass null to create an empty closing record.</param>
        /// <returns>Number of bytes transferred from the FIFO stream.</returns>
        public int STDOUT(UInt16 requestID, FifoStream fifo) {
            /*
                Set content
            */
            UInt16 length;

            if (fifo == null) {
                length = 0;
            } else {
                length = (UInt16)fifo.Read(Record.MAX_CONTENT_SIZE, this.buffer, 8);
            }
            
            /*
                Set header
            */
            this.buffer[0] = (byte)1;               // Version
            this.buffer[1] = (byte)TYPE_STDOUT;     // Type

            if (isLittleEndian) {
                this.buffer[2] = (byte)(requestID >> 8);      // Request ID 1
                this.buffer[3] = (byte)(requestID & 0x00FF);  // Request ID 0

                this.buffer[4] = (byte)(length >> 8);         // Content Length 1
                this.buffer[5] = (byte)(length & 0x00FF);     // Content Length 0
            } else {
                this.buffer[2] = (byte)(requestID << 8);      // Request ID 1
                this.buffer[3] = (byte)(requestID & 0xFF00);  // Request ID 0

                this.buffer[4] = (byte)(length << 8);         // Content Length 1
                this.buffer[5] = (byte)(length & 0xFF00);     // Content Length 0
            }

            this.buffer[6] = 0;     // Padding
            this.buffer[7] = 0;     // Reserved

            this.bufferEnd = 8 + length;

            return length;
        }

        /// <summary>
        /// Send the record through the connection.
        /// </summary>
        /// <param name="stream">Stream of the connection socket.</param>
        public async Task sendAsync(NetworkStream stream) {
            await stream.WriteAsync(this.buffer, 0, this.bufferEnd);
        }
    }
}
