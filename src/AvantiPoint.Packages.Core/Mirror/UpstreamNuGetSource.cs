using AvantiPoint.Packages.Protocol;

namespace AvantiPoint.Packages.Core
{
    public class UpstreamNuGetSource : IUpstreamNuGetSource
    {
        public UpstreamNuGetSource(string name, NuGetClient client)
        {
            Name = name;
            Client = client;
        }

        public string Name { get; }
        public NuGetClient Client { get; }
    }
}
