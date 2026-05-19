using System.ComponentModel.DataAnnotations;

namespace AvantiPoint.Packages.Elasticsearch;

public class ElasticsearchSearchOptions
{
    [Required]
    public string Endpoint { get; set; }

    public string IndexName { get; set; } = "packages";

    public string Username { get; set; }

    public string Password { get; set; }

    public bool DisableCertificateValidation { get; set; }
}
