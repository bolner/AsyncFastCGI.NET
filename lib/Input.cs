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
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace AsyncFastCGI
{
    class Input {
        private Socket socket;
        private Record inputRecord;
        private NetworkStream stream;
        private int maxHeaderSize;
        private FifoStream inputBuffer;
        private UInt16 fastCgiRequestID;
        private UInt16 role;
        private bool keepConnection;
        private Dictionary<string, string> parameters;

        /*
            Status of processing
        */
        private bool parametersReceived;
        private bool headersReceived;
        private bool initialized;
        private bool inputCompleted;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="socket">The socket of the client request</param>
        /// <param name="stream">A stream that is associated with the socket in the first parameter</param>
        /// <param name="inputRecord">An instance of the Record class to be used</param>
        /// <param name="maxHeaderSize">Maximum size of the HTTP header</param>
        public Input(Socket socket, NetworkStream stream, Record inputRecord, FifoStream inputBuffer, int maxHeaderSize)
        {
            this.socket = socket;
            this.inputRecord = inputRecord;
            this.inputRecord.Reset();
            this.stream = stream;
            this.inputBuffer = inputBuffer;
            this.inputBuffer.Reset();
            this.maxHeaderSize = maxHeaderSize;
            this.fastCgiRequestID = 0;
            this.role = 0;
            this.keepConnection = false;

            this.parametersReceived = false;
            this.headersReceived = false;
            this.initialized = false;
            this.inputCompleted = false;
        }

        /// <summary>
        /// For internal use only. This method does nothing if called
        /// in the request handler.
        /// </summary>
        public async Task Initialize() {
            if (this.initialized) {
                return;
            }

            /*
                Read the parameters and the header.
            */
            while(!this.parametersReceived || !this.headersReceived) {
                await this.ProcessNextRecordAsync();
            }

            this.initialized = true;
        }

        /// <summary>
        /// Read all data from the input, but don't store it,
        /// dicard all instead.
        /// </summary>
        public async Task ReadAllAndDiscard() {
            await Task.Delay(10);
        }

        public string GetHeader(string name) {
            return "";
        }

        public string GetContent() {
            return "";
        }

        public byte[] GetBinaryContent() {
            return new byte[1];
        }

        public int GetHttpStatus() {
            return 200;
        }

        public UInt16 GetFastCgiRequestID() {
            return this.fastCgiRequestID;
        }

        /// <summary>
        /// Returns the value of a server parameter.
        /// See the full list of passed parameters in:
        ///     - /etc/nginx/fastcgi_params
        /// </summary>
        /// <param name="name">The name of the parameter</param>
        /// <returns>The parameter value</returns>
        public string GetParameter(string name) {
            return "";
        }

        /// <summary>
        /// Returns true if all data is read from the input channel, false otherwise.
        /// </summary>
        /// <returns>True if all data is read from the input channel, false otherwise</returns>
        public bool IsInputCompleted() {
            return this.inputCompleted;
        }

        /// <summary>
        /// If zero, the application closes the connection after responding to this
        /// request. If not zero, the application does not close the connection after
        /// responding to this request; the Web server retains responsibility for the
        /// connection.
        /// </summary>
        /// <returns>0 for closing, 1 for keeping the connection open after this request.</returns>
        public bool IsKeepConnection() {
            return this.keepConnection;
        }

        /// <summary>
        /// Reads the input, until the next record is reconstructed,
        /// sets object properties based on the record content, then
        /// returns.
        /// </summary>
        private async Task ProcessNextRecordAsync() {
            bool result;

            while(true) {
                result = await this.inputRecord.ProcessInputAsync(stream);

                if (this.inputRecord.GetRecordType() != Record.TYPE_BEGIN_REQUEST
                        && this.fastCgiRequestID != this.inputRecord.GetRequestID())
                {
                    // Possible desynchronized state
                    // Best is to close the connection
                }

                if (result) {
                    // A complete record has been reconstructed.
                    //  (The next call to processInputAsync will reset the state of the record.)

                    switch(this.inputRecord.GetRecordType()) {
                        case Record.TYPE_BEGIN_REQUEST: {
                            this.fastCgiRequestID = this.inputRecord.GetRequestID();
                            this.role = this.inputRecord.GetRole();
                            this.keepConnection = this.inputRecord.IsKeepConnection();
                            Console.WriteLine($"Record Type: Begin request. Keep conn: {this.keepConnection}");

                            break;
                        }
                        case Record.TYPE_PARAMS: {
                            Console.WriteLine($"Record Type: Params. Length: {this.inputRecord.GetContentLength()}");
                            if (this.inputRecord.GetContentLength() == 0) {
                                Console.WriteLine($"Input buffer size: {this.inputBuffer.GetLength()}");
                                this.parameters = this.inputBuffer.GetNameValuePairs();


                                foreach(var item in this.parameters) {
                                    Console.WriteLine($"- {item.Key} = {item.Value}");
                                }

                                this.parametersReceived = true;
                                this.inputBuffer.Reset();
                                return;
                            }

                            this.inputRecord.CopyContentTo(this.inputBuffer);

                            if (this.inputBuffer.GetLength() > this.maxHeaderSize) {
                                throw(new ClientException($"Parameter data exceeds the maximal size of {this.maxHeaderSize} bytes."));
                            }

                            break;
                        }
                        case Record.TYPE_STDIN: {
                            // Console.WriteLine($"Record Type: STDIN. Length: {record.getLength()}");
                            if (this.inputRecord.GetContentLength() == 0) {
                                this.inputCompleted = true;
                                return;
                            }

                            this.inputRecord.CopyContentTo(this.inputBuffer);

                            // Search for header closing sequence in the FIFO. ( \r\n\r\n )
                            // If not found and the data is larger than the maxHeaderSize, then close connection.

                            break;
                        }
                        case Record.TYPE_GET_VALUES: {
                            // Console.WriteLine($"Record Type: Get values. Length: {record.getLength()}");
                            break;
                        }
                        case Record.TYPE_ABORT_REQUEST: {
                            // Console.WriteLine($"Record Type: Abort request. Length: {record.getLength()}");
                            break;
                        }
                        default: {
                            // Console.WriteLine($"Record Type: {record.getType()}. Length: {record.getLength()}");
                            break;
                        }
                    }
                }
            }
        }
    }
}
