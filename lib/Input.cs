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
using System.Text;
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
            while(!this.parametersReceived) {
                await this.ProcessRecordsAsync(true);
            }

            this.initialized = true;
        }

        /// <summary>
        /// Read all data from the input, but don't store it,
        /// dicard all instead.
        /// This can be used, when you want to send a response,
        /// without processing the full input. Since most
        /// browsers won't accept any response before their
        /// request got fully read.
        /// </summary>
        public async Task ReadAllAndDiscard() {
            while(!this.inputCompleted) {
                await this.ProcessRecordsAsync(false);
            }
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

        /// <summary>
        /// Returns all server parameter parameters.
        /// See the full list of passed parameters in:
        ///     - /etc/nginx/fastcgi_params
        /// </summary>
        /// <returns></returns>
        public Dictionary<string, string> GetAllParameters() {
            return this.parameters;
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
            return this.parameters[name];
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
        /// Reads the input, until records are reconstructed,
        /// sets object properties based on the record contents.
        /// Returns at three states: once after the parameters are
        /// processed, then each time when some input data read
        /// into the buffer, finally when input completed.
        /// </summary>
        /// <param name="allowStartingNewRequest"></param>
        private async Task ProcessRecordsAsync(bool allowStartingNewRequest) {
            bool result;

            while(true) {
                result = await this.inputRecord.ProcessInputAsync(stream);

                if (result) {
                    // A complete record has been reconstructed.
                    //  (The next call to processInputAsync will reset the state of the record.)

                    if (this.inputRecord.GetRecordType() != Record.TYPE_BEGIN_REQUEST
                        && this.fastCgiRequestID != this.inputRecord.GetRequestID())
                    {
                        throw(new ClientException("Uknown Request ID received in FastCGI connection."));
                    }

                    switch(this.inputRecord.GetRecordType()) {
                        case Record.TYPE_BEGIN_REQUEST: {
                            this.fastCgiRequestID = this.inputRecord.GetRequestID();
                            this.role = this.inputRecord.GetRole();
                            this.keepConnection = this.inputRecord.IsKeepConnection();

                            break;
                        }
                        case Record.TYPE_PARAMS: {
                            if (this.inputRecord.GetContentLength() == 0) {
                                this.parameters = this.inputBuffer.GetNameValuePairs();

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
                            if (this.inputRecord.GetContentLength() == 0) {
                                this.inputCompleted = true;
                                return;
                            }

                            this.inputRecord.CopyContentTo(this.inputBuffer);

                            return;
                        }
                        case Record.TYPE_GET_VALUES: {
                            throw(new ClientException("The server sent a FastCGI 'GET_VALUES' request, which is not yet supported."));
                        }
                        case Record.TYPE_ABORT_REQUEST: {
                            throw(new ClientException("Webserver aborted the request."));
                        }
                        default: {
                            throw(new ClientException($"Unknown record Type: {this.inputRecord.GetRecordType()}. Length: {this.inputRecord.GetContentLength()}"));
                        }
                    }
                }
            }
        }

        /// <summary>
        /// You can use this for debugging, to get an
        /// overview of all parameters.
        /// </summary>
        /// <returns>A text with lines containing key-value pairs,
        /// separated by a colon and a space.</returns>
        public string GetParametersAsText() {
            StringBuilder sb = new StringBuilder();

            foreach(var item in this.GetAllParameters()) {
                sb.Append($"{item.Key}: {item.Value}\n");
            }

            return sb.ToString();
        }
    }
}
