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

namespace AsyncFastCGI {
    class Request {
        private int index;
        private int maxInputSize;
        private Record record;
        MemoryStream contentStream;

        public delegate void RequestEndedDelegate(Request request);
        private RequestEndedDelegate OnRequestEnded;
        private Client.RequestHandlerDelegate requestHandler;

        public Request(int index, int maxInputSize, Client.RequestHandlerDelegate requestHandler,
                RequestEndedDelegate onRequestEnded) {
            
            this.index = index;
            this.maxInputSize = maxInputSize;
            this.record = new Record();
            this.contentStream = new MemoryStream(4096);
            this.OnRequestEnded = onRequestEnded;
            this.requestHandler = requestHandler;
        }

        public int getIndex() {
            return this.index;
        }

        /// <summary>
        /// Handles new incoming connections.
        /// The caller should not wait on it.
        /// </summary>
        /// <param name="connection">The socket for the new incoming connection.</param>
        public async void newConnection(Socket connection) {
            this.record.reset();
            NetworkStream stream = new NetworkStream(connection);
            bool result = false;
            this.contentStream.Position = 0;
            this.contentStream.SetLength(4096);
            UInt16 requestID = 0;

            while(true) {
                result = await record.processInputAsync(stream);

                if (result) {
                    // A complete record has been reconstructed.
                    //  (The next call to processInputAsync will reset the state of the record.)

                    switch(record.getType()) {
                        case Record.TYPE_BEGIN_REQUEST: {
                            // Console.WriteLine($"Record Type: Begin request. Length: {record.getLength()}");
                            requestID = record.getRequestID();
                            break;
                        }
                        case Record.TYPE_PARAMS: {
                            // Console.WriteLine($"Record Type: Params. Length: {record.getLength()}");
                            break;
                        }
                        case Record.TYPE_STDIN: {
                            // Console.WriteLine($"Record Type: STDIN. Length: {record.getLength()}");
                            if (record.getContentLength() < 1) {
                                // Closing record for STDIN
                                // Pass execution to the client callback

                                Input stdin = new Input(this.contentStream.ToArray(), null);
                                Output stdout = new Output(connection, requestID);
                                await this.requestHandler(stdin, stdout);
                                connection.Shutdown(SocketShutdown.Both);
                                connection.Disconnect(false);
                                this.OnRequestEnded(this);

                                return;
                            }

                            if (this.contentStream.Length + record.getContentLength() > this.maxInputSize) {
                                // TODO: Send abort record
                                this.OnRequestEnded(this);
                            }

                            record.addContentToMemoryStream(contentStream);

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
