namespace AvantiPoint.Feed.Platform.Storage;

public interface IStorageBackendFactory
{
    IPathBlobStore CreatePathStore(string subPrefix);

    IDigestBlobStore CreateDigestStore(string subPrefix);
}
