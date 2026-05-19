import type { Config } from '@docusaurus/types';
import { themes as prismThemes } from 'prism-react-renderer';

const organizationName = 'AvantiPoint';
const projectName = 'avantipoint.packages';

// GitHub Pages project site: https://avantipoint.github.io/avantipoint.packages/
const siteUrl = process.env.DOCUSAURUS_URL ?? 'https://avantipoint.github.io';
const siteBaseUrl = process.env.DOCUSAURUS_BASE_URL ?? `/${projectName}/`;

const copyrightStartYear = 2021;
const currentYear = new Date().getFullYear();
const copyrightYears =
    currentYear > copyrightStartYear
        ? `${copyrightStartYear}-${currentYear}`
        : `${copyrightStartYear}`;

const config: Config = {
    title: 'AvantiPoint Packages',
    tagline: 'Project docs for the AvantiPoint Packages',
    url: siteUrl,
    baseUrl: siteBaseUrl,
    favicon: 'assets/favicon.ico',
    organizationName,
    projectName,
    trailingSlash: false,
    staticDirectories: ['static'],
    headTags: [
        {
            tagName: 'meta',
            attributes: {
                property: 'og:image',
                content: `${siteUrl}${siteBaseUrl.replace(/\/$/, '')}/img/og.png`,
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
            copyright: `Copyright © ${copyrightYears} AvantiPoint`,
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
                    editUrl: `https://github.com/${organizationName}/${projectName}/edit/master/docs/`,
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
