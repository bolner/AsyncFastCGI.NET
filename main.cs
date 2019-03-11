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

                try {
                    port = Int32.Parse(args[0]);
                } catch (Exception e) {
                    throw(new Exception("Invalid port value.", e));
                }

                /*
                    Create and start the async FastCGI client
                */
                var client = new AsyncFastCGI.Client();

                client.setPort(port);                   // The port was passed as first argument
                client.setBindAddress("0.0.0.0");       // Bind to all interfaces
                client.setMaxConcurrentRequests(256);   // Requests that are running in parallel
                client.setMaxInputSize(8388608);        // 8 MB
                client.setConnectionTimeout(10000);     // 10 seconds

                client.ClientEventHandler = async (AsyncFastCGI.Input input, AsyncFastCGI.Output output) => {
                    output.setHttpStatus(200);
                    output.setHeader("Content-Type", "text/html; charset=utf-8");
                    await output.writeAsync("<!DOCTYPE html><html><body><h1>Hello World!</h1></body></html>");
                    await output.endAsync();
                };
                
                await client.startAsync();
            } catch (Exception e) {
                Console.Error.WriteLine(e.Message);
                Environment.Exit(1);
            }
        }
    }
}
