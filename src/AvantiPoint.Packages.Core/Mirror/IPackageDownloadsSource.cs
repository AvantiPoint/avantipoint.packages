using System.Collections.Generic;
using System.Threading.Tasks;

namespace AvantiPoint.Packages.Core
{
    public interface IPackageDownloadsSource
    {
        Task<Dictionary<string, Dictionary<string, long>>> GetPackageDownloadsAsync();
    }
}
