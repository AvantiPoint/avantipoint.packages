import type { SidebarsConfig } from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
    docsSidebar: [
        'getting-started',
        'comparison',
        {
            type: 'category',
            label: 'Configuration',
            collapsible: true,
            items: [
                'registration',
                'configuration',
                'mirrors',
            ],
        },
        {
            type: 'category',
            label: 'Database',
            collapsible: true,
            link: {
                type: 'doc',
                id: 'database/database',
            },
            items: ['database/sqlite', 'database/sqlserver', 'database/mysql', 'database/postgresql'],
        },
        {
            type: 'category',
            label: 'Search',
            collapsible: true,
            link: {
                type: 'doc',
                id: 'search/index',
            },
            items: [
                'search/elasticsearch',
                'search/opensearch',
                'search/azure-search',
                'search/migration',
            ],
        },
        {
            type: 'category',
            label: 'Storage',
            collapsible: true,
            link: {
                type: 'doc',
                id: 'storage/storage',
            },
            items: [
                'storage/filestorage',
                'storage/azureblob',
                'storage/awss3',
                'storage/gcs',
                'storage/s3-compatible',
                'storage/sftp',
                'storage/ftp',
                'storage/minio',
                'storage/localstack-s3',
                'storage/digitalocean-spaces',
                'storage/backblaze-b2',
                'storage/wasabi',
                'storage/alibaba-oss',
            ],
        },
        {
            type: 'category',
            label: 'Feeds',
            collapsible: true,
            items: ['feeds/npm-registry', 'feeds/multi-feed-ui'],
        },
        {
            type: 'category',
            label: 'Features',
            collapsible: true,
            items: ['authentication', 'callbacks', 'download-tracking', 'shields', 'vulnerability-support', 'ui-components'],
        },
        {
            type: 'category',
            label: 'Deployment',
            collapsible: true,
            items: ['hosting', 'host/email'],
        },
        {
            type: 'category',
            label: 'Performance',
            collapsible: true,
            items: ['performance-optimization'],
        },
        'templates',
    ],
};

export default sidebars;
