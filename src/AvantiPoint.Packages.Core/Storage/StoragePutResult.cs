namespace AvantiPoint.Packages.Core
{
    /// <summary>
    /// The result of a <see cref="IStorageService.PutAsync(string, Stream, string, CancellationToken)"/> operation.
    /// </summary>
    public enum StoragePutResult
    {
        /// <summary>
        /// The given path is already used to store different content.
        /// </summary>
        Conflict,

        /// <summary>
        /// This content is already stored at the given path.
        /// </summary>
        AlreadyExists,

        /// <summary>
        /// The content was sucessfully stored.
        /// </summary>
        Success,
    }
}
