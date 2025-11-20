import Link from '@docusaurus/Link';
import useDocusaurusContext from '@docusaurus/useDocusaurusContext';
import Layout from '@theme/Layout';
import clsx from 'clsx';
import React from 'react';
import styles from './index.module.css';

function HomepageHeader() {
    const { siteConfig } = useDocusaurusContext();
    return (
        <header className={clsx('hero hero--primary', styles.heroBanner)}>
            <div className="container">
                <div className={styles.heroContent}>
                    <div className={styles.heroText}>
                        <h1 className="hero__title">{siteConfig.title}</h1>
                        <p className="hero__subtitle">{siteConfig.tagline}</p>
                        <p className={styles.heroDescription}>
                            A modern, extensible NuGet package feed server built on .NET 10.0.
                            Secure your packages with advanced authentication, track events with lifecycle hooks,
                            and deploy to any cloud platform.
                        </p>
                        <div className={styles.buttons}>
                            <Link
                                className="button button--secondary button--lg"
                                to="/docs/getting-started">
                                Get Started →
                            </Link>
                            <Link
                                className="button button--outline button--lg"
                                to="https://github.com/AvantiPoint/avantipoint.packages"
                                style={{ marginLeft: '1rem' }}>
                                View on GitHub
                            </Link>
                        </div>
                    </div>
                    <div className={styles.heroImage}>
                        <img src="/img/banner.png" alt="AvantiPoint Packages" />
                    </div>
                </div>
            </div>
        </header>
    );
}

interface FeatureItem {
    title: string;
    icon: string;
    description: JSX.Element;
}

const FeatureList: FeatureItem[] = [
    {
        title: 'Advanced Authentication',
        icon: '🔐',
        description: (
            <>
                Secure your package feed with flexible authentication options.
                Separate permissions for package consumers and publishers with role-based access control.
            </>
        ),
    },
    {
        title: 'Lifecycle Event Hooks',
        icon: '🪝',
        description: (
            <>
                React to package upload and download events with custom business logic.
                Send notifications, track metrics, and implement compliance rules.
            </>
        ),
    },
    {
        title: 'Cloud-Ready Hosting',
        icon: '☁️',
        description: (
            <>
                Built-in support for AWS S3, Azure Blob Storage, and on-premises deployments.
                Deploy anywhere with flexible storage backends.
            </>
        ),
    },
    {
        title: 'Multiple Upstream Sources',
        icon: '🔄',
        description: (
            <>
                Mirror NuGet.org, consolidate commercial feeds (Telerik, Syncfusion, etc.),
                and provide a single endpoint for your team.
            </>
        ),
    },
    {
        title: 'Modern .NET',
        icon: '⚡',
        description: (
            <>
                Built on .NET 10.0 for best performance and latest features.
                Actively maintained with regular security and feature updates.
            </>
        ),
    },
    {
        title: 'Performance Optimized',
        icon: '🚀',
        description: (
            <>
                Database views for aggregated queries, optimized indexing, and query batching
                reduce N+1 patterns and improve feed responsiveness.
            </>
        ),
    },
];

function Feature({ title, icon, description }: FeatureItem) {
    return (
        <div className={clsx('col col--4', styles.feature)}>
            <div className={styles.featureCard}>
                <div className={styles.featureIcon}>{icon}</div>
                <h3>{title}</h3>
                <p>{description}</p>
            </div>
        </div>
    );
}

function HomepageFeatures() {
    return (
        <section className={styles.features}>
            <div className="container">
                <div className="row">
                    {FeatureList.map((props, idx) => (
                        <Feature key={idx} {...props} />
                    ))}
                </div>
            </div>
        </section>
    );
}

function UseCases() {
    return (
        <section className={styles.useCases}>
            <div className="container">
                <h2 className="text--center margin-bottom--lg">Built for Real-World Scenarios</h2>
                <div className="row">
                    <div className={clsx('col col--4', styles.useCase)}>
                        <div className={styles.useCaseCard}>
                            <h3>🏢 Enterprise Teams</h3>
                            <p>
                                Secure your intellectual property with authenticated feeds, track usage,
                                and integrate with your existing identity provider.
                            </p>
                        </div>
                    </div>
                    <div className={clsx('col col--4', styles.useCase)}>
                        <div className={styles.useCaseCard}>
                            <h3>📦 Component Vendors</h3>
                            <p>
                                Provide licensed packages to customers, control access based on subscriptions,
                                and monitor usage patterns.
                            </p>
                        </div>
                    </div>
                    <div className={clsx('col col--4', styles.useCase)}>
                        <div className={styles.useCaseCard}>
                            <h3>🔧 SaaS Platforms</h3>
                            <p>
                                Power subscription-based package distribution with user management
                                and automated licensing.
                            </p>
                        </div>
                    </div>
                </div>
            </div>
        </section>
    );
}

function ProductionFeeds() {
    return (
        <section className={styles.productionFeeds}>
            <div className="container">
                <h2 className="text--center margin-bottom--lg">Proven in Production</h2>
                <p className="text--center margin-bottom--lg">
                    These deployments demonstrate real-world usage and ongoing hardening:
                </p>
                <div className={styles.feedsList}>
                    <ul>
                        <li>AvantiPoint's Internal NuGet Server</li>
                        <li>AvantiPoint's Enterprise Support NuGet Server</li>
                        <li><a href="https://sponsorconnect.dev/" target="_blank" rel="noopener noreferrer">Sponsor Connect NuGet Server</a></li>
                        <li>Prism Library Commercial Plus NuGet Server</li>
                    </ul>
                </div>
            </div>
        </section>
    );
}

function CallToAction() {
    return (
        <section className={styles.cta}>
            <div className="container">
                <div className={styles.ctaContent}>
                    <h2>Ready to Get Started?</h2>
                    <p>
                        Set up your own secure, extensible NuGet feed in minutes.
                        Choose from our sample projects and customize to your needs.
                    </p>
                    <div className={styles.ctaButtons}>
                        <Link
                            className="button button--primary button--lg"
                            to="/docs/getting-started">
                            Quick Start Guide
                        </Link>
                        <Link
                            className="button button--outline button--lg"
                            to="/docs/authentication"
                            style={{ marginLeft: '1rem' }}>
                            Learn About Authentication
                        </Link>
                    </div>
                </div>
            </div>
        </section>
    );
}

export default function Home(): JSX.Element {
    const { siteConfig } = useDocusaurusContext();
    return (
        <Layout
            title={`${siteConfig.title}`}
            description="Modern, extensible NuGet package feed server with advanced authentication and lifecycle hooks">
            <HomepageHeader />
            <main>
                <HomepageFeatures />
                <UseCases />
                <ProductionFeeds />
                <CallToAction />
            </main>
        </Layout>
    );
}
