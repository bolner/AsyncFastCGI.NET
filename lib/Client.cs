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
        public delegate Task RequestHandlerDelegate(AsyncFastCGI.Input input, AsyncFastCGI.Output output);
        public RequestHandlerDelegate requestHandler;

        private static Dictionary<int, string> httpStatuses;

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

            int callbackCount = this.requestHandler.GetInvocationList().Length;
            if (callbackCount < 1) {
                throw(new Exception("Please set a callback for new requests. (Client.OnNewRequest)"));
            }

            if (callbackCount > 1) {
                throw(new Exception("It isn't allowed to set more than one callback for new requests. (Client.OnNewRequest)"));
            }

        	if (this.port < 1 || this.port > 65535) {
                throw(new Exception($"The specified port is invalid: {this.port}"));
            }

            Client.initHttpStatuses();

            /*
                Initialize the array of Request objects
            */
            this.requests = new Request[this.getMaxConcurrentRequests()];
            this.freeRequests = new Stack<Request>();
            this.runningRequests = new HashSet<Request>();

            for (int i = 0; i < this.getMaxConcurrentRequests(); i++) {
                request = new Request(this.getMaxInputSize(), this.requestHandler, this.OnRequestEnded);
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
                
                connection.ReceiveTimeout = this.getConnectionTimeout();
                connection.SendTimeout = this.getConnectionTimeout();

                request = await this.fetchFreeRequestOrIdle();

                // This call should not suspend the loop
                request.newConnection(connection);
            }
        }

        /// <summary>
        /// Fetch a free request if any. Otherwise await 10 ms
        /// if the concurrent request capacity is exhausted.
        /// </summary>
        /// <returns>A free request to be used.</returns>
        private async Task<Request> fetchFreeRequestOrIdle() {
            while (this.freeRequests.Count < 1) {
                await Task.Delay(10);
            }

            Request request = this.freeRequests.Pop();
            this.runningRequests.Add(request);

            return request;
        }

        /// <summary>
        /// Handles the "request ended" event. When this method is
        /// called, the connection socket has been closed already.
        /// </summary>
        /// <param name="request">The request that ended.</param>
        public void OnRequestEnded(Request request) {
            this.runningRequests.Remove(request);
            this.freeRequests.Push(request);
        }

        /// <summary>
        /// Example: returns "Not Found" for code 404.
        /// </summary>
        /// <param name="httpStatusCode"></param>
        /// <returns>Text representation of the code.</returns>
        public static string getHttpStatusText(int httpStatusCode) {
            if (!Client.httpStatuses.ContainsKey(httpStatusCode)) {
                return "";
            }

            return Client.httpStatuses[httpStatusCode];
        }

        /// <summary>
        /// Initialize the dictionary of HTTP status codes/texts.
        /// </summary>
        private static void initHttpStatuses() {
            Client.httpStatuses = new Dictionary<int, string>();

            Client.httpStatuses[100] = "Continue";
            Client.httpStatuses[101] = "Switching Protocols";
            Client.httpStatuses[102] = "Processing";
            Client.httpStatuses[103] = "Early Hints";
            Client.httpStatuses[200] = "OK";
            Client.httpStatuses[201] = "Created";
            Client.httpStatuses[202] = "Accepted";
            Client.httpStatuses[203] = "Non-Authoritative Information";
            Client.httpStatuses[204] = "No Content";
            Client.httpStatuses[205] = "Reset Content";
            Client.httpStatuses[206] = "Partial Content";
            Client.httpStatuses[207] = "Multi-Status";
            Client.httpStatuses[208] = "Already Reported";
            Client.httpStatuses[226] = "IM Used";
            Client.httpStatuses[300] = "Multiple Choices";
            Client.httpStatuses[301] = "Moved Permanently";
            Client.httpStatuses[302] = "Found";
            Client.httpStatuses[303] = "See Other";
            Client.httpStatuses[304] = "Not Modified";
            Client.httpStatuses[305] = "Use Proxy";
            Client.httpStatuses[307] = "Temporary Redirect";
            Client.httpStatuses[308] = "Permanent Redirect";
            Client.httpStatuses[400] = "Bad Request";
            Client.httpStatuses[401] = "Unauthorized";
            Client.httpStatuses[402] = "Payment Required";
            Client.httpStatuses[403] = "Forbidden";
            Client.httpStatuses[404] = "Not Found";
            Client.httpStatuses[405] = "Method Not Allowed";
            Client.httpStatuses[406] = "Not Acceptable";
            Client.httpStatuses[407] = "Proxy Authentication Required";
            Client.httpStatuses[408] = "Request Timeout";
            Client.httpStatuses[409] = "Conflict";
            Client.httpStatuses[410] = "Gone";
            Client.httpStatuses[411] = "Length Required";
            Client.httpStatuses[412] = "Precondition Failed";
            Client.httpStatuses[413] = "Payload Too Large";
            Client.httpStatuses[414] = "URI Too Long";
            Client.httpStatuses[415] = "Unsupported Media Type";
            Client.httpStatuses[416] = "Range Not Satisfiable";
            Client.httpStatuses[417] = "Expectation Failed";
            Client.httpStatuses[418] = "I'm a teapot";
            Client.httpStatuses[422] = "Unprocessable Entity";
            Client.httpStatuses[423] = "Locked";
            Client.httpStatuses[424] = "Failed Dependency";
            Client.httpStatuses[426] = "Upgrade Required";
            Client.httpStatuses[428] = "Precondition Required";
            Client.httpStatuses[429] = "Too Many Requests";
            Client.httpStatuses[431] = "Request Header Fields Too Large";
            Client.httpStatuses[451] = "Unavailable For Legal Reasons";
            Client.httpStatuses[500] = "Internal Server Error";
            Client.httpStatuses[501] = "Not Implemented";
            Client.httpStatuses[502] = "Bad Gateway";
            Client.httpStatuses[503] = "Service Unavailable";
            Client.httpStatuses[504] = "Gateway Time-out";
            Client.httpStatuses[505] = "HTTP Version Not Supported";
            Client.httpStatuses[506] = "Variant Also Negotiates";
            Client.httpStatuses[507] = "Insufficient Storage";
            Client.httpStatuses[508] = "Loop Detected";
            Client.httpStatuses[510] = "Not Extended";
            Client.httpStatuses[511] = "Network Authentication Required";
        }
    }
}
