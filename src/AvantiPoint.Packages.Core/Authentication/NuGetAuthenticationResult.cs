using System.Security.Claims;

namespace AvantiPoint.Packages.Core
{
    public record NuGetAuthenticationResult
    {
        public string Realm { get; private init; }

        public bool Succeeded { get; private init; }

        private string _message;
        public string Message
        {
            get => !Succeeded && string.IsNullOrEmpty(_message) ? "Invalid User Credentials" : _message;
            private init => _message = value;
        }

        private string _server;
        public string Server
        {
            get => string.IsNullOrEmpty(_server) ? "AvantiPoint Packages" : _server;
            private init => _server = value;
        }

        public ClaimsPrincipal User { get; init; }

        public static NuGetAuthenticationResult Success() =>
            new()
            {
                Succeeded = true
            };

        public static NuGetAuthenticationResult Success(ClaimsPrincipal user) =>
            new NuGetAuthenticationResult()
            {
                Succeeded = true,
                User = user
            };

        public static NuGetAuthenticationResult Fail(string message, string server) =>
            new()
            {
                Message = message,
                Server = server,
            };

        public static NuGetAuthenticationResult Fail(string message, string server, string realm) =>
            new()
            {
                Message = message,
                Realm = realm,
                Server = server,
            };
    }
}
