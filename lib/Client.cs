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

        /// <summary>
        /// Set here the async callback, that should
        /// be called to handle incoming requests.
        /// </summary>
        public RequestHandlerDelegate RequestHandler;

        private static Dictionary<int, string> httpStatuses;

        private int port;

        /// <summary>
        /// Set the port on which the library is listening for
        /// connections from the webserver.
        /// </summary>
        /// <param name="port">Listening port. Usually: 1025 - 65535</param>
        public void SetPort(int port)
        {
            this.port = port;
        }

        public int GetPort()
        {
            return this.port;
        }

        private int maxConcurrentRequests;

        /// <summary>
        /// Set the maximum number of requests that
        /// can run in parallel.
        /// </summary>
        /// <param name="maxConcurrentRequests">Typical range: 50 - 500</param>
        public void SetMaxConcurrentRequests(int maxConcurrentRequests)
        {
            this.maxConcurrentRequests = maxConcurrentRequests;
        }

        public int GetMaxConcurrentRequests()
        {
            return this.maxConcurrentRequests;
        }

        private IPAddress bindAddress;

        /// <summary>
        /// This is a security feature. You can limit where your
        /// FastCGI client is accessible from. By setting it
        /// to "0.0.0.0", its port is accessible from anywhere
        /// on your local network (or internet), but if you
        /// set it to "127.0.0.1", then it's only accessible
        /// from the localhost.
        /// </summary>
        /// <param name="bindAddress"></param>
        public void SetBindAddress(string bindAddress)
        {
            try {
                this.bindAddress = IPAddress.Parse(bindAddress);
            } catch (Exception e) {
                throw(new Exception($"Invalid bind address '{bindAddress}'.", e));
            }
        }

        public string GetBindAddress()
        {
            return this.bindAddress.ToString();
        }

        private int connectionTimeout;

        public void SetConnectionTimeout(int ms) {
            this.connectionTimeout = ms;
        }

        public int GetConnectionTimeout() {
            return this.connectionTimeout;
        }

        private int maxHeaderSize;

        public int GetMaxHeaderSize() {
            return this.maxHeaderSize;
        }

        /// <summary>
        /// Set the maximum allowed size for HTTP headers.
        /// </summary>
        /// <param name="value">The maximum allowed size for HTTP headers.</param>
        public void SetMaxHeaderSize(int value) {
            this.maxHeaderSize = value;
        }

        /*
            Managing requests
        */
        private Request[] requests;
        private Task<int>[] tasks;

        /// <summary>
        /// Constructor. Setting defaults.
        /// </summary>
        public Client() {
            /*
                Defaults
            */
            this.port = 8080;
            this.maxConcurrentRequests = 256;
            this.bindAddress = IPAddress.Parse("0.0.0.0");  // Listen on all interfaces
            this.connectionTimeout = 5000;  // 5 sec
            this.maxHeaderSize = 16384; // 16 KB
        }

        /// <summary>
        /// Main entry point of the client library.
        /// </summary>
        public async Task StartAsync()
        {
            Socket connection = null;

            int callbackCount = this.RequestHandler.GetInvocationList().Length;
            if (callbackCount < 1) {
                throw(new Exception("Please set a callback for new requests. (Client.OnNewRequest)"));
            }

            if (callbackCount > 1) {
                throw(new Exception("It isn't allowed to set more than one callback for new requests. (Client.OnNewRequest)"));
            }

            if (this.port < 1 || this.port > 65535) {
                throw(new Exception($"The specified port is invalid: {this.port}"));
            }

            Client.InitHttpStatuses();

            /*
                Initialize the array of Request objects
            */
            this.requests = new Request[this.GetMaxConcurrentRequests()];
            this.tasks = new Task<int>[this.GetMaxConcurrentRequests()];

            /*
                Listen on socket, wait for the webserver to connect.
            */
            Socket listeningSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            IPEndPoint bindEP = new IPEndPoint(this.bindAddress, this.port);
            listeningSocket.Bind(bindEP);
            listeningSocket.ReceiveTimeout = 5000;
            listeningSocket.SendTimeout = 5000;

            listeningSocket.Listen(this.GetMaxConcurrentRequests() * 2);

            /*
                First fill the arrays with Requests and Tasks as
                the new connections arrive.
            */
            for (int i = 0; i < this.GetMaxConcurrentRequests(); i++) {
                connection = await this.AcceptConnection(listeningSocket);

                this.requests[i] = new Request(i, this.RequestHandler, this.GetMaxHeaderSize());
                this.tasks[i] = this.requests[i].NewConnection(connection);
            }

            /*
                When they got full, then go into a loop of waiting
                on finished tasks before accepting new connections.
                Re-use the Request objects.
            */
            while(true) {
                int index = (await Task.WhenAny(this.tasks)).Result;
                connection = await this.AcceptConnection(listeningSocket);
                this.tasks[index] = this.requests[index].NewConnection(connection);
            }
        }

        /// <summary>
        /// Waits for an incoming connection from the webserver,
        /// then configures its socket.
        /// </summary>
        /// <param name="listeningSocket"></param>
        /// <returns></returns>
        private async Task<Socket> AcceptConnection(Socket listeningSocket) {
            Socket connection;

            try {
                connection = await listeningSocket.AcceptAsync();
            } catch (Exception e) {
                throw(new Exception("Listening socket lost. (Socket.AcceptAsync)", e));
            }

            // Configure the socket of the connection
            connection.ReceiveTimeout = this.GetConnectionTimeout();
            connection.SendTimeout = this.GetConnectionTimeout();

            return connection;
        }

        /// <summary>
        /// Returns the text for an HTTP status code.
        /// Example: returns "Not Found" for code 404.
        /// </summary>
        /// <param name="httpStatusCode"></param>
        /// <returns>Text representation of the code.</returns>
        public static string GetHttpStatusText(int httpStatusCode) {
            if (!Client.httpStatuses.ContainsKey(httpStatusCode)) {
                return "";
            }

            return Client.httpStatuses[httpStatusCode];
        }

        /// <summary>
        /// Initialize the dictionary of HTTP status codes/texts.
        /// </summary>
        private static void InitHttpStatuses() {
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
