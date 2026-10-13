# Native package feeds

Opt-in Maven release hosting, authenticated Swift binary-target artifacts, and the hosted
pub v2 protocol for Dart/Flutter. The existing NuGet/npm/OCI routes remain unchanged.

## Enable in the managed host

```json
{
  "Feed": {
    "PublicBaseUrl": "https://packages.example.com",
    "Authentication": { "AllowAnonymousPull": false },
    "Maven": { "Enabled": true },
    "Swift": { "Enabled": true },
    "Pub": { "Enabled": true }
  }
}
```

Apply the `AddNativeArtifacts` database migration through the deployment's existing
migration process before enabling a surface. Merely installing this library does not
change running deployments. SQL Server, PostgreSQL, MySQL, and SQLite have migrations.

The managed host uses its existing approved users and hashed tokens. Read requires the
token's Read scope and the user's consume permission; publication requires Write and
publish permission. Invalid/expired credentials return 401. A valid credential without
the required permission returns 403, so `dart pub` does not discard a valid read token.
Read-only mode blocks publication. Package access callbacks apply to metadata and bytes.
Aggregate Maven, Swift and pub metadata includes only descriptors/artifacts permitted
by those callbacks. Native browse pages count visible packages, scanning identities in
bounded batches so hidden packages do not leave empty catalog slots.

OpenFeed registers native token authentication using its existing configured
`Feed:Authentication:ApiKey` (or legacy `ApiKey`). This sample shared key grants both
publication and private consumption; `AllowAnonymousPull` controls unauthenticated
reads. With no configured key, native publication is denied. Use the managed Host's
scoped tokens or an explicit custom token service when separate read/write grants are
required. No credentials should be committed to sample configuration.

Managed Host administrators manage native enablement, the public URL, anonymous reads,
read-only mode and each protocol's artifact limits and Swift/pub archive limits in **Settings**. Saves are
validated and written atomically to a nonsecret `feed-settings.json` override, loaded
before surface registration on the next host start. The UI clearly marks pending
changes; restart the host to apply them. New-user approval retains its immediate
database-backed behavior. Set `Host:FeedConfigurationPath` to a durable/shared file
location when needed; the default uses the filesystem storage directory (or `App_Data`
for other storage providers). Saved values override the corresponding startup settings.
Share this file and restart every instance in a multi-instance deployment. Tokens,
OAuth credentials and storage credentials are never saved by this page.

Private browser artifact links use the Host's `/native/{protocol}/download` route.
It checks the same consumer-session/read-token guard and per-artifact policy as the
native browse page, then streams an attachment with private/no-store caching. SDK
protocol routes still require their normal feed-token authentication. No token is put
in a URL, cookie permissions are not extended to SDK routes, and browser publishing
is not provided.

Custom hosts call `feed.UseNativeRegistry(FeedProtocol.Maven)` (or Swift/Pub) and
`app.MapNativeFeeds(feed)`. They must register `IFeedTokenAuthenticationService` to
validate credentials by operation. Missing authentication configuration fails closed.
Their storage provider must implement `IStreamingStorageService`; native feeds never
fall back to the legacy buffering storage API.
Do not infer a read grant from legacy publisher authentication; define the host's
read/write token policy explicitly, as the managed Host and OpenFeed do.

`Feed:PublicBaseUrl` is mandatory and must be HTTPS, without userinfo, query, or fragment.
HTTP is accepted only for loopback development. Include any reverse-proxy path prefix.
Native API links use this configured base, not arbitrary request/forwarding headers.
Keep the configured base and the client's repository base aligned.

## Android: Maven and Gradle

Repository base: `https://packages.example.com/maven`.
The standard Maven layout serves AAR/JAR, POM, Gradle `.module` metadata, signatures, and
SHA-256/SHA-512/SHA-1/MD5 sidecars. Both GET and HEAD work. Gradle's `maven-publish`
plugin can publish releases with standard HTTP PUT requests.

Use the same repository in `dependencyResolutionManagement.repositories` and
`publishing.repositories`, with separate consumer and publisher credentials:

```kotlin
maven {
    name = "avantiPoint"
    url = uri("https://packages.example.com/maven")
    credentials {
        username = providers.environmentVariable("PACKAGES_USER").get()
        password = providers.environmentVariable("PACKAGES_TOKEN").get()
    }
    authentication { create<BasicAuthentication>("basic") }
}
```

Store credentials in the developer's user-level Gradle configuration or CI secret
provider. Never check them into build scripts. HTTP-header Bearer authentication is also
accepted for Gradle clients configured with `HttpHeaderCredentials`.

The initial implementation deliberately supports immutable releases. `-SNAPSHOT`
coordinates are rejected. `maven-metadata.xml` is generated from committed POM versions;
publisher metadata and metadata checksum PUTs are accepted as compatibility hints.
Artifact checksum uploads are verified against the committed immutable artifact. SHA-256
sidecars use the digest verified before publication. Other sidecars use a bounded,
one-hour process-local digest cache; a cold cache streams the blob once per algorithm.
The initial storage integrity check always rereads persisted bytes. Downloads always return
server-computed checksums. POM and Gradle metadata coordinates must match their URL.
Missing POM `groupId` and `version` may inherit literal values from the declared
`parent`, matching the [Maven POM reference](https://maven.apache.org/pom.html#Inheritance).
Explicit child coordinates must still match the upload path; `artifactId` is never
inherited. External parent resolution and property interpolation, plugin-group
discovery, snapshot deployment, upstream mirroring and retention are not implemented
in this first increment.

## Apple: SwiftPM binary targets

Upload one ZIP per module:

```
PUT /swift/{package}/{version}/{Module}.xcframework.zip
Authorization: Basic <user-and-publish-token>
Content-Type: application/zip
```

The archive must have `{Module}.xcframework` at its root, an XML root `Info.plist`,
framework binaries and regular public `.swiftinterface` files for every declared slice.
Bundle resources may contain safe spaces, `@`, and Unicode filenames. Paths,
archive entry counts, actual expanded sizes and symlinks are checked without extraction.
ZIP64/multipart ZIPs and central directories larger than 16 MiB are rejected.
Implementation source files and common signing-key files are rejected. Build libraries
for distribution, preserve public interfaces/headers, license and privacy resources, and
validate every advertised platform/architecture in the producer pipeline. The host's
structural checks do not replace Xcode linker/consumer tests or a source-leak review.

The response returns the immutable URL and its SHA-256 checksum. An authenticated
`GET /swift/{package}/{version}/index.json` lists published module URLs/checksums for
release tooling. This index is an AvantiPoint manifest helper, not a Swift registry or
Apple artifact-bundle-index document.

A small Git-hosted `Package.swift` manifest is still the native SwiftPM discovery entry:

```swift
.binaryTarget(
    name: "Example",
    url: "https://packages.example.com/swift/example/1.0.0/Example.xcframework.zip",
    checksum: "<exact SHA-256 returned by upload>"
)
```

Compute/compare the value with `swift package compute-checksum`. Keep product composition
and binary dependencies in the companion manifest. Consumers need only that manifest,
public API interfaces and compiled artifacts; the producer source repository stays
private. No credential belongs in `Package.swift`, a binary URL, or an archive.

For shipped SwiftPM 6.3 direct binary ZIP downloads, Basic authentication through the
host's `.netrc`/Keychain credentials is the compatibility baseline. Configure the user's
email as login and a read-scoped package token as password, protect the credential file,
and provision/remove it securely in CI. Registry login authentication and newer SwiftPM
main-branch environment-token support are different paths and must not be assumed to
work for every Xcode version. Test a cold authenticated resolve/build with the supported
Xcode versions before rollout. Downloads stream directly from the authenticated feed
without redirecting credentials to another host.

Full Swift source-registry APIs, CocoaPods spec hosting and Carthage indexes are outside
this initial surface. These binary ZIPs can underpin those integrations later.

## Flutter and Dart: hosted pub

Repository base: `https://packages.example.com/pub`.

```shell
dart pub token add https://packages.example.com/pub --env-var PACKAGES_TOKEN
```

```yaml
name: example_sdk
version: 1.0.0
publish_to: https://packages.example.com/pub

dependencies:
  another_private_package:
    hosted: https://packages.example.com/pub
    version: ^1.0.0
```

Publish using `dart pub publish`; consume using `dart pub get` or `flutter pub get`.
The native flow initiates publication, uploads a multipart `file`, follows Location to
finalize, and exposes version metadata with `archive_url`, `archive_sha256`, and the
parsed pubspec. Archives stay under the repository URL prefix so pub sends the token
to archive and upload requests. No storage-signed URL or credential-bearing URL is used.
The server validates the tar archive without extraction, rejects traversal and special
files, and applies compressed/expanded size and entry-count limits. A bounded validation copy normalizes Dart's legacy USTAR version header after validating its original header checksum. The original archive
bytes are stored and hashed; authenticated consumers receive precisely those bytes.
YAML aliases and duplicate mapping keys are rejected. Retraction and security-advisory
APIs are not advertised in this initial implementation.

A private pub repository controls access but sends Dart package source to authorized
consumers. It cannot hide Dart implementations from their build pipeline. A Flutter
wrapper can call proprietary AAR/XCFramework/native libraries, but that wrapper's Dart
code remains visible. Do not equate private hosting with binary-only Dart distribution.

## Integrity, limits and operation

Uploads are streamed to bounded temporary files, hashed, and stored by content digest.
File, Azure, S3, GCS, FTP, and SFTP providers expose native bounded-memory upload/copy
operations. Downloads stream from the provider to the HTTP response; persisted-byte
verification and checksum requests stream through a hashing sink. Transfer byte counts
must match the catalog. Legacy protocol transfer APIs are unchanged.
A unique transactional database identity binds feed + ecosystem + exact logical path to
one digest. Same-byte retries are idempotent; replacing a released artifact returns 409.
The database gate avoids relying on mutable object-store PUT behavior for immutability.
Unreferenced content from failed/racing publications can remain; dedicated native blob
retention/garbage collection is future work, not an existing cleanup promise.

Each ecosystem has `MaxArtifactBytes` (default 256 MiB). Swift and pub also enforce `MaxExpandedArchiveBytes`
(default 1 GiB, maximum 16 GiB), and `MaxArchiveEntries` (default 10,000). Configure web-server and
reverse-proxy upload limits consistently. Do not publish unchecked archives, symbols,
credentials, or producer source. Database/storage failures are not reported as successful
publishes. Responses to authenticated routes are private/non-cacheable.

## Protocol references

- [Maven repository layout](https://maven.apache.org/repositories/layout.html)
- [Gradle publishing](https://docs.gradle.org/current/userguide/publishing_maven.html)
- [Gradle repository authentication](https://docs.gradle.org/current/userguide/supported_repository_protocols.html)
- [Gradle module metadata](https://docs.gradle.org/current/userguide/publishing_gradle_module_metadata.html)
- [Apple binary Swift packages](https://developer.apple.com/documentation/xcode/distributing-binary-frameworks-as-swift-packages)
- [SwiftPM 6.3 binary downloads](https://github.com/swiftlang/swift-package-manager/blob/swift-6.3-RELEASE/Sources/Workspace/Workspace%2BBinaryArtifacts.swift)
- [Dart custom repositories](https://dart.dev/tools/pub/custom-package-repositories)
- [Hosted pub v2 specification](https://github.com/dart-lang/pub/blob/master/doc/repository-spec-v2.md)

## Running compatibility tests

`dotnet test --project tests/AvantiPoint.Packages.Registry.Native.Tests` runs the
HTTP/storage/validation suite. For live Gradle and Dart publisher + cold consumer
checks, set `AVP_GRADLE_EXECUTABLE` and `AVP_DART_EXECUTABLE` to installed native
executables and point `JAVA_HOME` to a full JDK, then run the same tests. The two
CLI tests explicitly skip when their executable is not configured. All fixtures,
feed credentials, databases, and caches are isolated local test data. No external
package feed is published to by these tests.

The opt-in `SwiftToolchainTests` builds four synthetic universal macOS XCFrameworks
from public test source on the disposable Xcode runner and uses the real feed on
HTTPS loopback. It checks anonymous/invalid-token rejection,
read-token publication denial, native checksum enforcement, and independently cold
SwiftPM build/run for each product using Basic credentials in a job-only `.netrc`.
It never downloads AppPortal artifacts, checks out an SDK implementation repository,
or uses live feed credentials. The synthetic modules qualify the feed protocol;
they do not qualify actual AppPortal APIs or platform builds. Actual AppPortal
distribution must remain in authorized private repositories and private feeds.
Private AppPortal qualification requires authorized private build outputs and is
separate from this public-source fixture test.
Set `AVP_SWIFT_EXECUTABLE`, `AVP_SWIFT_CERTIFICATE_PATH`, and
`AVP_SWIFT_FIXTURE_DIRECTORY` on the approved macOS test runner; the existing workflow
builds that fixture directory locally. Otherwise this test explicitly skips.

The `Native SwiftPM consumer` workflow qualifies the initial integration in PR #729;
later manual runs require explicit temporary-certificate trust opt-in. SwiftPM only
accepts HTTPS binary URLs. Its fixture helper is restricted
to disposable GitHub-hosted macOS runners, creates a unique certificate in a temporary
keychain and limits trust to SSL at `127.0.0.1`. An always-run cleanup explicitly
revokes that unique certificate, verifies the OS rejects it and all pre-existing
trust records are unchanged, removes private material and restores/verifies the
original keychain search list. macOS trust-removal APIs can hang on hosted runners;
the synthetic certificate's deny record remains only until the runner is destroyed.
It never
disables TLS verification or uploads the certificate, credentials, keychain or caches.
.NET loads the TLS private key into its own temporary macOS keychain and disposes it
with the host; it does not request persistent key storage. The test publisher's .NET
client uses the exact fixture certificate as its custom root, with normal certificate
name, validity and server-authentication checks. SwiftPM uses the OS SSL trust.
Known `.netrc` files are
deleted directly. Swift build workspaces are left to disposable-runner teardown rather
than recursively traversing potentially mounted, read-only Xcode SDK content.
Do not use this helper on a developer workstation or persistent/self-hosted runner.


## Browsing native feeds

The managed Host and OpenFeed provide shared browse, package detail, version
selection and connection instructions at `/native/maven`, `/native/swift` and
`/native/pub`. The protocol must be enabled, and `Feed:PublicBaseUrl` must include
any public path prefix. UI URLs are separate from the Maven, Swift binary and pub
protocol paths. Private metadata reads validate a signed-in Host session or native
read token and apply artifact callbacks; no protocol-filter bypass is used.

Authorization and upload/download callbacks use logical paths rather than storage
digests: Maven coordinate paths, `package/version/module.xcframework.zip`, and
`packages/name/versions/version.tar.gz`. Swift index authorization uses
`package/version/index.json`; pub package-list authorization uses
`api/packages/name`, while individual-version metadata and finalize authorization
use the canonical archive path. Callback denials in pub include a Bearer challenge
with their 403 response. Pubspec values must be JSON-representable; non-finite YAML
numbers are rejected before publication. Every Swift slice must contain a public
interface under the requested module's `Modules/Module.swiftmodule` directory
(including valid versioned framework links).

Native publication callbacks run after the artifact commits, including identical
publication retries. A transient callback failure can therefore be repaired by
retrying the same bytes. Delivery is at least once: callback implementations must
make their side effects idempotent using the feed ID, protocol, and logical artifact
path as the immutable event identity. Conflicting bytes never invoke the callback.
This retry behavior does not provide a background outbox or exactly-once delivery.

Native catalog pages load at most 50 package identities at a time and preserve the
search term in Previous/Next links. Browse queries omit pubspec payloads; detail
queries restrict artifact reads and authorization callbacks to the requested package.
Private managed Host sessions require the PackageConsumer role. Native Razor pages
run their own guard so valid pull tokens reach the same checks without a UI cookie.

Catalog identity comparisons and name deduplication use provider-specific binary
collations before paging, preserving case-distinct Maven coordinates on SQL Server
and MySQL as well as exact feed/protocol isolation. The bounded artifact lookup uses
individual comparisons rather than provider-dependent primitive collection parameters.

### Native interface conventions

Native feeds share the site's color palette, theme and keyboard accessibility, but
use ecosystem-specific presentations. Maven browse uses group/artifact coordinates
and published file kinds, with Maven and Gradle dependency snippets on release pages.
Swift browse emphasizes binary modules and release artifacts; details expose the
binaryTarget URL and exact SHA-256 without claiming platform qualification. Dart/pub
browse and details use published descriptions and declared Dart/Flutter SDK constraints
from pubspec metadata, alongside hosted dependency instructions. Missing metadata is
omitted rather than inferred. Existing NuGet screens retain their established layout.
Browser publishing is outside the native UI scope; connection guides document CLI use.

Maven CI-friendly builds must deploy a POM with a resolved release version matching
its repository path. Use the Flatten Maven Plugin before deployment, as required by
[Maven's CI-friendly version deployment guide](https://maven.apache.org/guides/mini/guide-maven-ci-friendly.html#install-deploy).
Raw version placeholders are rejected; the repository preserves uploaded bytes and
checksums rather than rewriting producer POMs.

NuGet is the default feed in both managed Host and OpenFeed. npm, OCI (including named
Docker/Helm profiles), Maven/Android, Swift and Dart/pub are disabled in fresh default
configuration. Enable only the required surfaces with their `Feed:<ecosystem>:Enabled`
settings (`Feed:Oci:Default:Enabled` for the default OCI catalog). Managed Host Settings
also provides these switches; saved choices take effect after restart. Custom OCI
profiles retain their explicit host configuration. Existing saved enablement overrides
are honored rather than reset to defaults.

Native upload notification completion is persisted separately from the immutable
artifact. Identical retries skip a completed notification; pending or failed notifications
remain retryable, and a bounded database lease prevents concurrent delivery by healthy
processes. Handlers must still be idempotent for a process failure between a side effect
and its receipt, or a callback exceeding the lease. Rows created before receipt tracking
are treated as pending at their first retry. The receipt migration preserves catalog rows.
