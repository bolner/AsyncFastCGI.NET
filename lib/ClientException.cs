using System;

namespace AsyncFastCGI
{
    /// <summary>
    /// All exceptions thrown by the AsyncFastCGI.NET library
    /// are of this type.
    /// </summary>
    class ClientException : Exception {
        public ClientException(string message): base(message) {

        }

        public ClientException(string message, Exception innerException): base(message, innerException) {
            
        }
    }
}
