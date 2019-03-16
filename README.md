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

Running 4 processes of the example `main.cs` application (which currently returns all headers and system parameters) behind an Nginx webserver on an i7-8559U CPU (embedded, low performance). The concurrency is 400, so 400 connections are open at the same time.

    ab -c 400 -n 50000 127.0.0.1/csharp

Output:

    Server Software:        AsyncFastCGI.NET
    Server Hostname:        127.0.0.1
    Server Port:            80

    Document Path:          /csharp
    Document Length:        512 bytes

    Concurrency Level:      400
    Time taken for tests:   8.664 seconds
    Complete requests:      200000
    Failed requests:        0
    Total transferred:      135600000 bytes
    HTML transferred:       102400000 bytes
    Requests per second:    23084.99 [#/sec] (mean)
    Time per request:       17.327 [ms] (mean)
    Time per request:       0.043 [ms] (mean, across all concurrent requests)
    Transfer rate:          15284.79 [Kbytes/sec] received

    Connection Times (ms)
                min  mean[+/-sd] median   max
    Connect:        0    2  28.7      0    1027
    Processing:     0   14  93.6      8    3861
    Waiting:        0   14  93.6      8    3861
    Total:          0   16  98.4      9    3861

    Percentage of the requests served within a certain time (ms)
    50%      9
    66%      9
    75%     10
    80%     10
    90%     10
    95%     12
    98%     15
    99%     21
    100%   3861 (longest request)

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
