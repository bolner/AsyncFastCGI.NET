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

namespace AsyncFastCGI
{
    class Output {
        private Socket connection;
        private NetworkStream stream;
        private Record record;
        private int requestID;
        private bool ended = false;

        public Output(Socket connection, int requestID) {
            this.connection = connection;
            this.stream = new NetworkStream(connection);
            this.requestID = requestID;
        }

        public async Task writeAsync(string data) {
            
        }

        public async Task writeBinaryAsync(byte[] data) {
            
        }

        public async Task end() {

            this.ended = true;
        }

        public bool isEnded() {
            return this.ended;
        }

        public void setHttpStatus(int status) {

        }

        public Task endAsync() {
            return Task.FromResult(0);
        }

        public void setHeader(string name, string value) {
            
        }
    }
}
