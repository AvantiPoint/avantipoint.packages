using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AvantiPoint.Packages.Core;

namespace AvantiPoint.Packages.Azure
{
    /// <summary>
    /// AvantiPoint Packages's configurations to use Azure Blob Storage to store packages.
    /// See: https://avantipoint.github.io/avantipoint.packages/docs/storage/azureblob
    /// </summary>
    public class AzureBlobStorageOptions : IValidatableObject, IConnectionStringOptions
    {
        /// <summary>
        /// The Azure Blob Storage connection string.
        /// If provided, ignores <see cref="AccountName"/> and <see cref="AccessKey"/>.
        /// </summary>
        public string ConnectionString { get; set; }

        /// <summary>
        /// The name of the configuration entry that contains the Azure Blob Storage connection string.
        /// </summary>
        public string ConnectionStringName { get; set; }

        /// <summary>
        /// The Azure Blob Storage account name. Ignored if <see cref="ConnectionString"/> is provided.
        /// </summary>
        public string AccountName { get; set; }

        /// <summary>
        /// The Azure Blob Storage access key. Ignored if <see cref="ConnectionString"/> is provided.
        /// </summary>
        public string AccessKey { get; set; }

        /// <summary>
        /// The Azure Blob Storage container name.
        /// </summary>
        public string Container { get; set; } = "packages";

        public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
        {
            const string helpUrl = "https://avantipoint.github.io/avantipoint.packages/docs/storage/azureblob";

            if (string.IsNullOrEmpty(ConnectionString) && string.IsNullOrEmpty(ConnectionStringName))
            {
                if (string.IsNullOrEmpty(AccountName))
                {
                    yield return new ValidationResult(
                        $"The {nameof(AccountName)} configuration is required. See {helpUrl}",
                        [nameof(AccountName)]);
                }

                if (string.IsNullOrEmpty(AccessKey))
                {
                    yield return new ValidationResult(
                        $"The {nameof(AccessKey)} configuration is required. See {helpUrl}",
                        [nameof(AccessKey)]);
                }
            }

            if (string.IsNullOrEmpty(Container))
            {
                yield return new ValidationResult(
                    $"The {nameof(Container)} configuration is required. See {helpUrl}",
                    [nameof(Container)]);
            }
        }
    }
}
