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

        private bool isLittleEndian = true;

        private byte recordVersion = 0;
        private byte recordType = 0;
        private int recordRequestID = 0;
        private int recordContentLength = 0;
        private int recordLength = 0;
        private int recordPaddingLength = 0;

        public int getType() {
            return this.recordType;
        }

        public int getLength() {
            return this.recordLength;
        }

        public int getRequestID() {
            return this.recordRequestID;
        }

        public int getContentLength() {
            return this.recordContentLength;
        }

        public Record() {
            this.buffer = new byte[MAX_RECORD_SIZE];
            this.isLittleEndian = BitConverter.IsLittleEndian;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns>True if a complete record has been reconstructed, false otherwise.</returns>
        public async Task<bool> processInputAsync(NetworkStream stream) {
            bool skipRead = false;
            if (this.completeRecordReconstructed) {
                skipRead = this.startNextRecord();
            }

            if (!skipRead) {
                int remaining = MAX_RECORD_SIZE - bufferEnd;
                int bytesRead = await stream.ReadAsync(this.buffer, this.bufferEnd, remaining);
                if (bytesRead == 0) {
                    await Task.Delay(10);
                    return false;
                }
                this.bufferEnd += bytesRead;
                Console.WriteLine($"* Bytes read: {bytesRead}");
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
                    this.recordRequestID = (this.buffer[2] << 8) | this.buffer[3];
                    this.recordContentLength = (this.buffer[4] << 8) | this.buffer[5];
                } else {
                    this.recordRequestID = (this.buffer[3] << 8) | this.buffer[2];
                    this.recordContentLength = (this.buffer[5] << 8) | this.buffer[4];
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

        private bool startNextRecord() {
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

        public void reset() {
            this.bufferEnd = 0;
            this.completeRecordReconstructed = false;
            this.headerReconstructed = false;
        }

        public int getRole() {
            if (this.recordType != TYPE_BEGIN_REQUEST) {
                throw(new Exception("Called 'getRole' on a non-BEGIN_REQUEST record."));
            }

            if (this.isLittleEndian) {
                return (this.buffer[HEADER_SIZE + 0] << 8) | this.buffer[HEADER_SIZE + 1];
            }

            return (this.buffer[HEADER_SIZE + 1] << 8) | this.buffer[HEADER_SIZE + 0];
        }

        public bool isKeepConnection() {
            if (this.recordType != TYPE_BEGIN_REQUEST) {
                throw(new Exception("Called 'isKeepConnection' on a non-BEGIN_REQUEST record."));
            }

            return this.buffer[HEADER_SIZE + 2] > 0;
        }

        public void addContentToMemoryStream(MemoryStream stream) {
            stream.Write(this.buffer, HEADER_SIZE, this.recordContentLength);
        }

        public int STDOUT(int requestID, byte[] data, int offset) {
            int length = data.Length - offset;
            int returnOffset = 0;

            if (length > MAX_CONTENT_SIZE) {
                length = MAX_CONTENT_SIZE;
                returnOffset = offset + length;
            }

            this.buffer[0] = (byte)1;     // Version
            this.buffer[1] = (byte)TYPE_STDOUT;

            // TODO
            if (isLittleEndian) {

            } else {

            }

            return returnOffset;
        }
    }
}
