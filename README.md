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

        client.setPort(8080);
        client.setBindAddress("0.0.0.0");       // Bind to all interfaces
        client.setMaxConcurrentRequests(256);   // When reached then queued
        client.setMaxInputSize(8388608);        // 8 MB
        client.setConnectionTimeout(10000);     // 10 seconds
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

Running 4 processes of the example `main.cs` application behind an Nginx webserver on an i7-8559U CPU (embedded, low performance). The concurrency is 400, so 400 connections are open at the same time.

    ab -c 400 -n 50000 127.0.0.1/csharp

Output:

    Server Software:        AsyncFastCGI.NET
    Server Hostname:        127.0.0.1
    Server Port:            80

    Document Path:          /csharp
    Document Length:        62 bytes

    Concurrency Level:      400
    Time taken for tests:   3.763 seconds
    Complete requests:      50000
    Failed requests:        0
    Keep-Alive requests:    0
    Total transferred:      11400000 bytes
    HTML transferred:       3100000 bytes
    Requests per second:    13286.41 [#/sec] (mean)
    Time per request:       30.106 [ms] (mean)
    Time per request:       0.075 [ms] (mean, across all concurrent requests)
    Transfer rate:          2958.30 [Kbytes/sec] received

    Connection Times (ms)
                min  mean[+/-sd] median   max
    Connect:        0    5  63.0      1    1031
    Processing:     0   20  96.4      7    1228
    Waiting:        0   19  96.4      6    1228
    Total:          0   25 115.3      8    2233

    Percentage of the requests served within a certain time (ms)
    50%      8
    66%     12
    75%     15
    80%     17
    90%     27
    95%     40
    98%     94
    99%   1016
    100%   2233 (longest request)

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

1. Build or run for debug and development

    ```bash
    dotnet build
    dotnet run 8080
    ```

2. Release build

    For Ubuntu and Windows:

    ```bash
    dotnet publish -c Release -r ubuntu.18.04-x64

    dotnet publish -c Release -r win10-x64
    ```

### Nginx config

        upstream fastcgi_backend_csharp {
                server 127.0.0.1:9090;
                server 127.0.0.1:9091;
                server 127.0.0.1:9092;
                server 127.0.0.1:9093;

                keepalive 8;
        }

        server {
                listen 80 default_server;
                listen [::]:80 default_server;

                root /var/www/html;
                server_name _;

                location / {
                        try_files $uri $uri/ =404;
                }

                location /csharp {
                        include /etc/nginx/fastcgi_params;
                        fastcgi_pass fastcgi_backend_csharp;
                }
        }
