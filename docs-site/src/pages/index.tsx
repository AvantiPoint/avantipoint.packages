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
                            Protect and sign your packages with enterprise-grade security, automate what happens when packages are pushed or pulled,
                            and run anywhere with flexible storage options.
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
                Give consumers and publishers the right access with fine-grained permissions that are easy to manage.
            </>
        ),
    },
    {
        title: 'Automated Workflows',
        icon: '🪝',
        description: (
            <>
                Trigger your own workflows on package uploads and downloads—no changes to your apps required.
                Send alerts, log audits, update dashboards, or enforce business rules automatically.
            </>
        ),
    },
    {
        title: 'Cloud-Ready & Multi-Provider Storage',
        icon: '☁️',
        description: (
            <>
                First-class support for Azure Blob Storage, AWS S3, and S3-compatible providers like MinIO, Spaces,
                Wasabi, Backblaze B2, and Alibaba OSS.
                Run on-premises or in any cloud with the same code.
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
        title: 'Repository Signing & HSM Integration',
        icon: '🛡️',
        description: (
            <>
                Keep consumers safe with server-side signing, long-lived trusted signatures, and automatic key rotation.
                Plug into your existing key infrastructure including Azure Key Vault, AWS KMS/Signer, and Google Cloud KMS/HSM.
            </>
        ),
    },
    {
        title: 'Vulnerability & Policy Aware',
        icon: '🐞',
        description: (
            <>
                Automatically flag or block risky packages before they reach your developers.
                Combine authentication, signing, and vulnerability data so your feeds are secure by default.
            </>
        ),
    },
    {
        title: 'UI Templates & Components',
        icon: '🧩',
        description: (
            <>
                Quickly stand up a polished feed experience with reusable Razor components today,
                and soon, ready-made UI templates and Docker images to get from zero to production even faster.
            </>
        ),
    },
    {
        title: 'Modern .NET & Performance',
        icon: '⚡',
        description: (
            <>
                Built on .NET 10.0 for fast startup, low latency, and efficient resource usage,
                so your feeds stay snappy and reliable even as your team and package volume grow.
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
                <div className={clsx('row', styles.featureRow)}>
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
                description="Modern, extensible NuGet package feed server with advanced security, automation, and cloud storage flexibility">
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
