# AsyncFastCGI.NET

Fully async FastCGI client library, written in C#.

## Build and run

1. Install .NET Core on Ubuntu 18.04

    Execute the following as root:

        wget -q https://packages.microsoft.com/config/ubuntu/18.04/packages-microsoft-prod.deb
        dpkg -i packages-microsoft-prod.deb

        add-apt-repository universe
        apt-get install apt-transport-https
        apt-get update
        apt-get install dotnet-sdk-2.2

2. Build or run for debug and development

        dotnet build
        dotnet run

3. Release build

    For Ubuntu and Windows:

        dotnet publish -c Release -r ubuntu.18.04-x64

        dotnet publish -c Release -r win10-x64
