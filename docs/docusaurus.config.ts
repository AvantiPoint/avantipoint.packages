import type { Config } from '@docusaurus/types';
import { themes as prismThemes } from 'prism-react-renderer';

const config: Config = {
    title: 'AvantiPoint Packages',
    tagline: 'Project docs for the AvantiPoint Packages',
    url: 'https://avantipoint.com',
    baseUrl: '/',
    favicon: 'assets/favicon.ico',
    organizationName: 'AvantiPoint',
    projectName: 'avantipoint.packages',
    trailingSlash: false,
    staticDirectories: ['static'],
    headTags: [
        {
            tagName: 'meta',
            attributes: {
                property: 'og:image',
                content: 'https://avantipoint.com/img/banner.png',
            },
        },
        {
            tagName: 'meta',
            attributes: {
                name: 'twitter:card',
                content: 'summary_large_image',
            },
        },
    ],
    themeConfig: {
        colorMode: {
            defaultMode: 'light',
            respectPrefersColorScheme: true,
            disableSwitch: false,
        },
        navbar: {
            title: 'AvantiPoint Packages',
            logo: {
                alt: 'AvantiPoint Logo',
                src: 'assets/logo-small.png',
            },
            items: [
                { type: 'docSidebar', sidebarId: 'docsSidebar', position: 'left', label: 'Documentation' },
                {
                    href: 'https://github.com/AvantiPoint/avantipoint.packages',
                    label: 'GitHub',
                    position: 'right',
                },
            ],
        },
        footer: {
            style: 'dark',
            links: [
                {
                    title: 'AvantiPoint',
                    items: [
                        { label: 'Website', href: 'http://avantipoint.com' },
                        { label: 'LinkedIn', href: 'https://www.linkedin.com/company/avantipoint/' },
                    ],
                },
                {
                    title: 'Community',
                    items: [
                        { label: 'GitHub', href: 'https://github.com/avantipoint' },
                        { label: 'Twitter', href: 'https://twitter.com/AvantiPoint' },
                    ],
                },
            ],
            copyright: `Copyright © ${new Date().getFullYear()} AvantiPoint`,
        },
        prism: {
            theme: prismThemes.github,
            darkTheme: prismThemes.dracula,
        },
    },
    presets: [
        [
            'classic',
            {
                docs: {
                    path: 'docs',
                    routeBasePath: 'docs',
                    sidebarPath: './sidebars.ts',
                    editUrl: 'https://github.com/AvantiPoint/avantipoint.packages/edit/master/docs-site/docs/',
                    showLastUpdateAuthor: true,
                    showLastUpdateTime: true,
                },
                blog: false,
                pages: {
                    path: 'src/pages',
                },
                theme: {
                    customCss: './src/css/custom.css',
                },
            },
        ],
    ],
};

export default config;
