using AvantiPoint.Packages.Elasticsearch;

namespace AvantiPoint.Packages.Aws.Configuration;

public class OpenSearchOptions : ElasticsearchSearchOptions
{
    public string Region { get; set; }

    public bool UseIamAuth { get; set; } = true;
}
