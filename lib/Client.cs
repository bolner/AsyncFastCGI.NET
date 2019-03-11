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
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace AsyncFastCGI
{
    class Client
    {
        public delegate void ClientHandlerDelegate(AsyncFastCGI.Input input, AsyncFastCGI.Output output);
        public ClientHandlerDelegate ClientEventHandler;

        private int port = 8080;

        public void setPort(int port)
        {
            this.port = port;
        }

        public int getPort()
        {
            return this.port;
        }

        private int maxConcurrentRequests = 256;

        public void setMaxConcurrentRequests(int maxConcurrentRequests)
        {
            this.maxConcurrentRequests = maxConcurrentRequests;
        }

        public int getMaxConcurrentRequests()
        {
            return this.maxConcurrentRequests;
        }

        private IPAddress bindAddress = IPAddress.Parse("0.0.0.0");

        public void setBindAddress(string bindAddress)
        {
            try {
                this.bindAddress = IPAddress.Parse(bindAddress);
            } catch (Exception e) {
                throw(new Exception($"Invalid bind address '{bindAddress}'.", e));
            }
        }

        public string getBindAddress()
        {
            return this.bindAddress.ToString();
        }

        private int maxInputSize = 2097152; // 2 MB

        public void setMaxInputSize(int maxInputSize) {
            this.maxInputSize = maxInputSize;
        }

        public int getMaxInputSize() {
            return this.maxInputSize;
        }

        private int connectionTimeout = 5000;

        public void setConnectionTimeout(int ms) {
            this.connectionTimeout = ms;
        }

        public int getConnectionTimeout() {
            return this.connectionTimeout;
        }

        /*
            Managing requests
        */
        private Request[] requests;
        private Stack<Request> freeRequests;
        private HashSet<Request> runningRequests;

        public Client() {
            
        }

        public async Task startAsync()
        {
            Socket connection = null;
            Request request = null;

            int callbackCount = this.ClientEventHandler.GetInvocationList().Length;
            if (callbackCount < 1) {
                throw(new Exception("Please set a callback for new requests. (Client.OnNewRequest)"));
            }

            if (callbackCount > 1) {
                throw(new Exception("It isn't allowed to set more than one callback for new requests. (Client.OnNewRequest)"));
            }

        	if (this.port < 1 || this.port > 65535) {
                throw(new Exception($"The specified port is invalid: {this.port}"));
            }

            /*
                Initialize the array of Request objects
            */
            this.requests = new Request[this.getMaxConcurrentRequests()];
            this.freeRequests = new Stack<Request>();
            this.runningRequests = new HashSet<Request>();

            for (int i = 0; i < this.getMaxConcurrentRequests(); i++) {
                request = new Request(this.getMaxInputSize(), this.ClientEventHandler, this.OnRequestEnded);
                this.requests[i] = request;
                this.freeRequests.Push(request);
            }

            /*
                Listen on socket, wait for the webserver to connect.
            */
            Socket listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint bindEP = new IPEndPoint(this.bindAddress, this.port);
            listeningSocket.Bind(bindEP);
            listeningSocket.ReceiveTimeout = 5000;
            listeningSocket.SendTimeout = 5000;

            listeningSocket.Listen(this.getMaxConcurrentRequests() * 2);

            while(true) {
                try {
                    connection = await listeningSocket.AcceptAsync();
                } catch (Exception e) {
                    throw(new Exception("Listening socket lost. (Socket.AcceptAsync)", e));
                }
                
                Console.WriteLine("--- New connection");
                connection.ReceiveTimeout = this.getConnectionTimeout();
                connection.SendTimeout = this.getConnectionTimeout();

                // Await 10 ms if the concurrent request capacity is exhausted
                request = await this.fetchFreeRequestOrIdle();

                // This call should not suspend the loop
                request.newConnection(connection);
            }
        }

        private async Task<Request> fetchFreeRequestOrIdle() {
            while (this.freeRequests.Count < 1) {
                await Task.Delay(10);
            }

            Request request = this.freeRequests.Pop();
            this.runningRequests.Add(request);

            return request;
        }

        public void OnRequestEnded(Request request) {
            // TODO

            this.runningRequests.Remove(request);
            this.freeRequests.Push(request);
        }
    }
}
