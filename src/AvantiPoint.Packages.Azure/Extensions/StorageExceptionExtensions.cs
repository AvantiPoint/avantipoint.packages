using System.Net;
using Azure;

namespace AvantiPoint.Packages.Azure
{
    internal static class StorageExceptionExtensions
    {
        public static bool IsAlreadyExistsException(this RequestFailedException e)
        {
            return e?.Status == (int?)HttpStatusCode.Conflict;
        }
    }
}
