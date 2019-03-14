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
using System.Threading.Tasks;

namespace FastCgiExampleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            try {
                /*
                    Check the input parameter (port)
                */
                if (args.Length < 1) {
                    throw(new Exception("Input parameter 'port' missing."));
                }

                int port = 0;
                if (!Int32.TryParse(args[0], out port)) {
                    throw(new Exception("Invalid port value."));
                }

                /*
                    Create and start the async FastCGI client
                */
                var client = new AsyncFastCGI.Client();

                client.SetPort(port);                   // The port was passed as command line argument
                client.SetBindAddress("0.0.0.0");       // Bind to all interfaces
                client.SetMaxConcurrentRequests(256);   // Requests that are running in parallel
                client.SetConnectionTimeout(10000);     // 10 seconds
                client.SetMaxHeaderSize(16384);         // 16 KB. Max HTTP header length
                client.RequestHandler = Program.RequestHandler;
                
                await client.StartAsync();
            } catch (Exception e) {
                Console.Error.WriteLine(e.ToString());
                Environment.Exit(1);
            }
        }

        private static async Task RequestHandler(AsyncFastCGI.Input input, AsyncFastCGI.Output output) {
            output.SetHttpStatus(200);
            output.SetHeader("Content-Type", "text/html; charset=utf-8");
            
            await output.WriteAsync("<!DOCTYPE html><html><body><h1>Hello World!");
            await output.WriteAsync("</h1></body></html>");
            await output.EndAsync();
        }
    }
}
