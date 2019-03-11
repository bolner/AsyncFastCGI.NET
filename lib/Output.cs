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
    class Output {
        private Socket connection;
        private NetworkStream stream;
        private Record record;
        private int requestID;
        private bool ended = false;
        private bool headerSent = false;
        private Dictionary<string, string> header;
        private int httpStatus = 200;
        private MemoryStream outputBuffer;
        private int outputBufferSize;

        public Output(Socket connection, int requestID, int outputBufferSize) {
            this.connection = connection;
            this.stream = new NetworkStream(connection);
            this.requestID = requestID;
            this.header = new Dictionary<string, string>();
            this.outputBuffer = new MemoryStream(1024); // Minimum size of OB
            this.outputBufferSize = outputBufferSize;   // Maximum of OB

            // Default headers (can be overwritten)
            this.header["Content-Type"] = "text/html; charset=utf-8";
            this.header["Cache-Control"] = "no-cache";
            this.header["Date"] = DateTime.Today.ToUniversalTime().ToString("r");
            this.header["Server"] = "AsyncFastCGI.NET";
        }

        public async Task writeAsync(string data) {
            if (!this.headerSent) {
                /*
                    Send HTTP header if it wasn't sent yet.
                */
                this.writeHeader();
            }

            this.outputBuffer.Write(
                System.Text.Encoding.UTF8.GetBytes(data)
            );

            if (this.outputBuffer.Length > this.outputBufferSize) {
                await this.sendBuffer(false);
            }
        }

        public async Task writeBinaryAsync(byte[] data) {
            if (!this.headerSent) {
                /*
                    Send HTTP header if it wasn't sent yet.
                */
                this.writeHeader();
            }

            this.outputBuffer.Write(data);

            if (this.outputBuffer.Length > this.outputBufferSize) {
                await this.sendBuffer(false);
            }
        }

        public async Task endAsync() {
            await this.sendBuffer(true);
            this.ended = true;
        }

        public bool isEnded() {
            return this.ended;
        }

        public void setHttpStatus(int status) {
            this.httpStatus = status;
        }

        public void setHeader(string name, string value) {
            
        }

        /// <summary>
        /// Writes the HTTP header into the output buffer.
        /// Call it after the first call to a "Write" method.
        /// </summary>
        private void writeHeader() {
            string codeText = Client.getHttpStatusText(this.httpStatus);
            if (codeText == "") {
                this.outputBuffer.Write(
                    System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 {this.httpStatus}\r\n")
                );
            } else {
                this.outputBuffer.Write(
                    System.Text.Encoding.UTF8.GetBytes($"HTTP/1.1 {this.httpStatus} {codeText}\r\n")
                );
            }

            foreach(KeyValuePair<string, string> entry in this.header)
            {
                this.outputBuffer.Write(
                    System.Text.Encoding.UTF8.GetBytes($"{entry.Key}: {entry.Value}\r\n")
                );
            }

            // Last CR/LF
            this.outputBuffer.WriteByte(13);
            this.outputBuffer.WriteByte(10);
        }

        /// <summary>
        /// Sends the output buffer in 64 KBytes long records, and
        /// empties it out.
        /// </summary>
        /// <param name="sendLeftover">False: Don't send the last segment
        /// if it's not exactly 65535 bytes. True: send all.</param>
        /// <returns></returns>
        private async Task sendBuffer(bool sendLeftover = false) {
            await Task.Delay(10);
        }
    }
}
