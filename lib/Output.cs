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
using System.Text;

namespace AsyncFastCGI
{
    class Output {
        private Socket connection;
        private NetworkStream stream;
        private Record record;
        private UInt16 requestID;
        private bool ended = false;
        private bool headerSent = false;
        private Dictionary<string, string> header;
        private int httpStatus = 200;
        private FifoMemoryStream fifo;

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="connection">Socket of the client connection.</param>
        /// <param name="requestID">FastCGI request ID</param>
        public Output(Socket connection, UInt16 requestID) {
            this.connection = connection;
            this.stream = new NetworkStream(connection);
            this.requestID = requestID;
            this.header = new Dictionary<string, string>();
            this.fifo = new FifoMemoryStream();
            this.record = new Record();

            // Default headers (can be overwritten)
            this.header["Content-Type"] = "text/html; charset=utf-8";
            this.header["Cache-Control"] = "no-cache";
            this.header["Date"] = DateTime.Today.ToUniversalTime().ToString("r");
            this.header["Server"] = "AsyncFastCGI.NET";
        }

        /// <summary>
        /// Send a string response back through the connection.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task writeAsync(string data) {
            if (this.ended) {
                return;
            }

            if (!this.headerSent) {
                /*
                    Send HTTP header if it wasn't sent yet.
                */
                this.writeHeader();
            }

            this.fifo.write(
                Encoding.UTF8.GetBytes(data)
            );
            await this.sendBuffer(false);
        }

        /// <summary>
        /// Send a binary response back through the connection.
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task writeBinaryAsync(byte[] data) {
            if (this.ended) {
                return;
            }

            if (!this.headerSent) {
                /*
                    Send HTTP header if it wasn't sent yet.
                */
                this.writeHeader();
            }

            this.fifo.write(data);
            await this.sendBuffer(false);
        }

        /// <summary>
        /// Flush the remaining output, prevent further writes,
        /// and close the FastCGI STDOUT with an empty record.
        /// </summary>
        /// <returns></returns>
        public async Task endAsync() {
            if (this.ended) {
                return;
            }

            await this.sendBuffer(true);

            this.record.STDOUT(this.requestID, null);
            try {
                await this.record.sendAsync(this.stream);
            } catch (Exception e) {
                Console.WriteLine(e.ToString());
            }
            
            this.ended = true;
        }

        /// <summary>
        /// Returns true if the output has been closed already, false otherwise.
        /// </summary>
        /// <returns>bool</returns>
        public bool isEnded() {
            return this.ended;
        }

        /// <summary>
        /// Set the HTTP response status.
        /// </summary>
        /// <param name="status">HTTP response status.await Example: 200</param>
        public void setHttpStatus(int status) {
            this.httpStatus = status;
        }

        /// <summary>
        /// Set HTTP header.
        /// </summary>
        /// <param name="name">Name of the header entry. Example: "Content-Type"</param>
        /// <param name="value">Value of the header entry. Example: "text/html; charset=utf-8"</param>
        public void setHeader(string name, string value) {
            this.header[name] = value;
        }

        /// <summary>
        /// Writes the HTTP header into the output buffer.
        /// Call it after the first call to a "Write" method.
        /// </summary>
        private void writeHeader() {
            string codeText = Client.getHttpStatusText(this.httpStatus);
            if (codeText == "") {
                this.fifo.write(
                    Encoding.UTF8.GetBytes($"HTTP/1.1 {this.httpStatus}\r\n")
                );
            } else {
                this.fifo.write(
                    Encoding.UTF8.GetBytes($"HTTP/1.1 {this.httpStatus} {codeText}\r\n")
                );
            }

            foreach(KeyValuePair<string, string> entry in this.header)
            {
                this.fifo.write(
                    Encoding.UTF8.GetBytes($"{entry.Key}: {entry.Value}\r\n")
                );
            }

            // Last CR/LF
            this.fifo.write(new byte[] { 0x0D, 0x0A });

            this.headerSent = true;
        }

        /// <summary>
        /// Sends the output buffer in 64 KBytes long records, and
        /// empties it out.
        /// </summary>
        /// <param name="sendLeftover">False: Don't send the last segment
        /// if it's not exactly 65535 bytes. True: send all.</param>
        /// <returns></returns>
        private async Task sendBuffer(bool sendLeftover = false) {
            while(this.fifo.getLength() > 0) {
                if (!sendLeftover && this.fifo.getLength() < Record.MAX_CONTENT_SIZE) {
                    return;
                }

                this.record.STDOUT(this.requestID, this.fifo);
                try {
                    await this.record.sendAsync(this.stream);
                } catch (Exception e) {
                    Console.WriteLine(e.ToString());
                    this.ended = true;
                }
            }
        }
    }
}
