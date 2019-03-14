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
        private Record inputRecord;
        private Record outputRecord;
        private int maxHeaderSize;

        private Client.RequestHandlerDelegate requestHandler;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="index">The index of the request, by which the Client object identifies it.</param>
        /// <param name="requestHandler">The client callback, which handles the incoming HTTP requests.</param>
        /// <param name="maxHeaderSize">The maximum allowed HTTP header size.</param>
        public Request(int index, Client.RequestHandlerDelegate requestHandler, int maxHeaderSize) {
            this.index = index;
            this.inputRecord = new Record();
            this.outputRecord = new Record();
            this.requestHandler = requestHandler;
            this.maxHeaderSize = maxHeaderSize;
        }

        /// <summary>
        /// Get the index of the request, by which the Client object identifies it.
        /// </summary>
        /// <returns>The integer index of the request.</returns>
        public int GetIndex() {
            return this.index;
        }

        /// <summary>
        /// Handles new incoming connections.
        /// The caller should not wait on it.
        /// </summary>
        /// <param name="request">The socket for the new incoming connection.</param>
        /// <returns>The index of the Request.</returns>
        public async Task<int> NewConnection(Socket request) {
            NetworkStream stream = new NetworkStream(request);
            Input stdin = new Input(request, stream, this.inputRecord, this.maxHeaderSize);
            await stdin.Initialize();
            Output stdout = new Output(request, stream, stdin.GetFastCgiRequestID());

            await this.requestHandler(stdin, stdout);

            request.Shutdown(SocketShutdown.Both);
            request.Disconnect(false);

            return this.index;
        }
    }
}
