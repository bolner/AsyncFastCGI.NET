# AsyncFastCGI.NET

Fully async FastCGI client library for `.NET Core`, written in C#. A non-intrusive alternative for developing web applications.

Development hasn't reached the release state yet. The FastCGI specification is not fully implemented.

## Example

```csharp
class Program
{
    static async Task Main(string[] args)
    {
        var client = new AsyncFastCGI.Client();

        client.setPort(9090);
        client.setBindAddress("0.0.0.0");       // Bind to all interfaces
        client.setMaxConcurrentRequests(256);   // When reached then queued
        client.setMaxInputSize(8388608);        // 8 MB
        client.setConnectionTimeout(2000);      // 2 seconds
        client.requestHandler = Program.requestHandler;
        
        await client.startAsync();
    }

    // Here comes your code to handle HTTP requests
    private static async Task requestHandler(AsyncFastCGI.Input input, AsyncFastCGI.Output output) {
        output.setHttpStatus(200);
        output.setHeader("Content-Type", "text/html; charset=utf-8");

        await output.writeAsync("<!DOCTYPE html><html><body><h1>Hello World!");
        await output.writeAsync("</h1></body></html>");
        await output.endAsync();
    }
}
```

## Benchmark results

The benchmarking is done in another project. For more information see [FastCGI-bench](https://github.com/bolner/FastCGI-bench). The following result sample is a high concurrency comparison between a NodeJS client, this library, and a [non-async library](https://github.com/LukasBoersma/FastCGI), written in C#.

### Concurrency: **400** simultaneous connections / 200'000 requests

    ab -c 400 -n 200000 127.0.0.1/PATH

| Library          | Req. /sec | Req. Time | Conc. R.T. | Longest R. | Failed |
|------------------|-----------|-----------|------------|------------|--------|
| AsyncFastCGI.NET | 19893.88  | 20.107 ms | 0.050 ms   | 2044 ms    | 0      |
| NodeJS           | 21411.16  | 18.682 ms | 0.047 ms   | 1062 ms    | 0      |
| LB FastCGI       | fails     | fails     | fails      | fails      | fails  |

*Req. Time: mean | Conc. R.T.: mean, across all concurrent requests*

## Build and run

1. Install `.NET Core` on Ubuntu 18.04

    Execute the following as root:

    ```bash
    wget -q https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb
    dpkg -i packages-microsoft-prod.deb

    add-apt-repository universe
    apt-get install apt-transport-https
    apt-get update
    apt-get install dotnet-sdk-2.2
    ```

2. Build or run for debug and development

    ```bash
    dotnet build
    dotnet run 8080
    ```

3. Release build

    For Ubuntu and Windows:

    ```bash
    dotnet publish -c Release -r ubuntu.18.04-x64

    dotnet publish -c Release -r win10-x64
    ```

### Nginx config

Notes: the perfomance is much worse with `fastcgi_keep_conn on` and `keepalive 100`, I'll have to figure out why. Also, the performance is worse when using the `least_conn` load balancing mode.

        upstream fastcgi_backend_csharp {
                server 127.0.0.1:9090;
                server 127.0.0.1:9091;
                server 127.0.0.1:9092;
                server 127.0.0.1:9093;
        }

        server {
                listen 80 default_server;
                listen [::]:80 default_server;

                root /var/www/html;
                server_name _;

                fastcgi_keep_conn off;
                fastcgi_request_buffering off;

                location / {
                        try_files $uri $uri/ =404;
                }

                location /csharp {
                        include /etc/nginx/fastcgi_params;
                        fastcgi_pass fastcgi_backend_csharp;
                }
        }
