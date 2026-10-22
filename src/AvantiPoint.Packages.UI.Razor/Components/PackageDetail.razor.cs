using AvantiPoint.Packages.Core;
using AvantiPoint.Packages.Protocol.Models;
using AvantiPoint.Packages.UI.Services;
using Markdig;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using NuGet.Versioning;

namespace AvantiPoint.Packages.UI.Components;

public partial class PackageDetail
{
    private readonly string versionPickerId = $"package-version-{Guid.NewGuid():N}";
    private readonly string licenseTitleId = $"package-license-{Guid.NewGuid():N}";
    private ElementReference detailRoot, licenseDialog;
    private IJSObjectReference? module, browserListeners;
    private DotNetObjectReference<PackageDetail>? browserCallback;
    private AuthenticationStateProvider? authentication;
    private PackageDetailTextCache textCache = new();
    private CancellationTokenSource work = new();
    private int navigation;
    private PackageInfoCollection? lastPackage;
    private List<PackageInfo> packageVersions = [];
    private string DetailPackageId => SelectedPackageVersion?.PackageId ?? packageVersions.FirstOrDefault()?.PackageId ?? string.Empty;
    private string? lastFeed, lastInputVersion;
    private int generation;
    private bool interactive, disposed, isReadmeLoading, isLicenseLoading;
    private string? licenseText;
    private Task? readmeWork;

    protected override void OnInitialized()
    {
        authentication = Services.GetService<AuthenticationStateProvider>();
        if (authentication is not null) authentication.AuthenticationStateChanged += AuthenticationChanged;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (Package is null) return;
        packageVersions = Package.Versions;
        var changed = !ReferenceEquals(lastPackage, Package) || lastFeed != FeedBaseUrl;
        var inputVersion = RequestedVersion ?? Package.Version?.OriginalVersion ?? packageVersions.FirstOrDefault()?.Version?.OriginalVersion ?? "";
        if (changed || lastInputVersion != inputVersion)
        {
            ResetContent();
            lastPackage = Package;
            lastFeed = FeedBaseUrl;
        }
        if (changed || lastInputVersion != inputVersion)
        {
            SelectedVersionString = packageVersions.FirstOrDefault(v =>
                NuGetVersion.TryParse(inputVersion, out var parsed) && v.Version == parsed)?.Version.OriginalVersion ?? inputVersion;
            lastInputVersion = inputVersion;
        }
        ActiveContentTab = NormalizeTab(Tab);
        ActiveInstallTab = string.IsNullOrWhiteSpace(Install) ? "cli" : Install.Trim().ToLowerInvariant();
        if (interactive)
            StartReadme();
        await Task.CompletedTask;
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        interactive = true;
        try
        {
            module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", new Uri(new Uri(NavigationManager.BaseUri), "_content/AvantiPoint.Packages.UI.Razor/js/ap-package-detail.js").ToString());
            if (module is not null)
            {
                browserCallback = DotNetObjectReference.Create(this);
                browserListeners = await module.InvokeAsync<IJSObjectReference>("attach", detailRoot, browserCallback);
            }
        }
        catch (InvalidOperationException) { } // Static renderers keep ordinary links usable.
        catch (JSException) { } // Ordinary links remain a fallback if the optional module cannot load.
        StartReadme();
    }

    private static string NormalizeTab(string? tab) => tab?.Trim().ToLowerInvariant() switch
    {
        "dependencies" => "dependencies", "badges" => "badges", "versions" => "versions", _ => "readme"
    };

    private static string VersionText(NuGetVersion? version) => version?.OriginalVersion ?? version?.ToNormalizedString() ?? string.Empty;

    private string VersionNavigationUrl(string version) => $"{PackageVersionUrl(version)}?tab={ActiveContentTab}&install={ActiveInstallTab}";

    private string PackageVersionUrl(string version) => $"{PackageBaseUrl}/{Uri.EscapeDataString(version)}";

    private Task SetContentTab(string tab) => NavigateDetail($"{PackageVersionUrl(SelectedVersionString)}?tab={tab}&install={ActiveInstallTab}", true);

    private Task SetInstallTab(string install) => NavigateDetail($"{PackageVersionUrl(SelectedVersionString)}?tab={ActiveContentTab}&install={install}", true);

    private Task VersionChangedAsync(string version) => NavigateDetail(
        $"{PackageVersionUrl(version)}?tab={ActiveContentTab}&install={ActiveInstallTab}", true);

    /// <summary>Applies same-package history navigation without reloading metadata or the Blazor circuit.</summary>
    [JSInvokable]
    public Task NavigateDetail(string destination, bool pushHistory) =>
        InvokeAsync(() => NavigateDetailCoreAsync(destination, pushHistory));

    private async Task NavigateDetailCoreAsync(string destination, bool pushHistory)
    {
        if (disposed || Package is null) return;
        var navigationId = ++navigation;
        var uri = new Uri(new Uri(NavigationManager.BaseUri), destination);
        var packagePath = new Uri(new Uri(NavigationManager.BaseUri), PackageBaseUrl).AbsolutePath + "/";
        var isBasePath = string.Equals(uri.AbsolutePath.TrimEnd('/'), packagePath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase);
        if (!isBasePath && !uri.AbsolutePath.StartsWith(packagePath, StringComparison.OrdinalIgnoreCase))
        {
            // Back to another package/page: let the server reconstruct its own scoped state.
            NavigationManager.NavigateTo(destination, forceLoad: true);
            return;
        }
        var version = isBasePath ? lastInputVersion : Uri.UnescapeDataString(uri.AbsolutePath[packagePath.Length..]);
        var selected = packageVersions.FirstOrDefault(v => NuGetVersion.TryParse(version, out var parsed) && v.Version == parsed);
        if (selected is null) { NavigationManager.NavigateTo(destination, forceLoad: true); return; }
        var query = QueryHelpers.ParseQuery(uri.Query);
        var nextTab = NormalizeTab(query.GetValueOrDefault("tab").ToString());
        var versionChanged = SelectedVersionString != selected.Version.OriginalVersion;
        if (versionChanged)
        {
            generation++;
            work.Cancel(); work.Dispose(); work = new();
            SelectedVersionString = selected.Version.OriginalVersion ?? selected.Version.ToNormalizedString();
            ReadmeHtml = string.Empty;
            isReadmeLoading = false; isLicenseLoading = false;
            licenseText = null;
            readmeWork = null;
        }
        ActiveContentTab = nextTab;
        ActiveInstallTab = string.IsNullOrWhiteSpace(query.GetValueOrDefault("install").ToString()) ? "cli" : query.GetValueOrDefault("install").ToString() ?? "cli";
        StateHasChanged();
        StartReadme();
        if (module is not null)
        {
            if (versionChanged) await module.InvokeVoidAsync("closeLicense", licenseDialog);
            if (navigationId != navigation) return;
            if (pushHistory) await module.InvokeVoidAsync("updateUrl", uri.PathAndQuery, false);
        }
        if (navigationId == navigation && versionChanged && pushHistory && OnVersionSelected.HasDelegate)
            await OnVersionSelected.InvokeAsync(selected.Version);
    }

    private void StartReadme()
    {
        if (disposed || !interactive || readmeWork is not null || !string.IsNullOrEmpty(ReadmeHtml)) return;
        readmeWork = LoadReadmeAndWarmAsync(generation, work.Token);
    }

    private async Task LoadReadmeAndWarmAsync(int expected, CancellationToken cancellationToken)
    {
        var selected = SelectedPackageVersion;
        if (selected?.HasReadme != true) return;
        isReadmeLoading = true;
        StateHasChanged();
        try
        {
            var markdown = await GetTextAsync(selected, license: false).WaitAsync(cancellationToken);
            if (expected != generation || disposed) return;
            ReadmeHtml = string.IsNullOrWhiteSpace(markdown) ? string.Empty : Markdown.ToHtml(markdown, Pipeline);
        }
        catch (OperationCanceledException) { return; }
        catch { /* A failed optional README must not block metadata navigation. */ }
        finally
        {
            if (expected == generation && !disposed) { isReadmeLoading = false; await InvokeAsync(StateHasChanged); }
        }
        // Warm at most ONE adjacent version, after a quiet interval. Never download archives/diffs.
        try
        {
            await Task.Delay(300, cancellationToken);
            if (expected != generation || disposed) return;
            var versions = packageVersions;
            var index = versions.FindIndex(v => v.Version == selected.Version);
            var adjacent = versions.ElementAtOrDefault(index + 1) ?? versions.ElementAtOrDefault(index - 1);
            if (adjacent?.HasReadme == true)
                await GetTextAsync(adjacent, license: false, speculative: true).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private async Task<bool> HasSignedInContextAsync(CancellationToken cancellationToken)
    {
        if (authentication is null) return false;
        try { return (await authentication.GetAuthenticationStateAsync().WaitAsync(cancellationToken)).User.Identity?.IsAuthenticated == true; }
        catch (InvalidOperationException) { return false; } // No initialized circuit principal: use the protocol boundary.
    }

    private Task<string?> GetTextAsync(PackageInfo selected, bool license, bool speculative = false)
    {
        var id = selected.PackageId;
        var version = selected.Version;
        var baseUrl = ContentBaseUrl;
        return textCache.GetAsync($"{baseUrl}|{id.ToLowerInvariant()}|{version.ToNormalizedString()}|{license}", async token =>
        {
            var signedIn = await HasSignedInContextAsync(token);
            // Signed-out consumers continue through the existing protocol authorization boundary.
            var content = signedIn ? Services.GetService<IPackageContentService>() : null;
            Stream? stream;
            if (content is not null)
                stream = license
                    ? await content.GetPackageLicenseStreamOrNullAsync(id, version, token)
                    : await content.GetPackageReadmeStreamOrNullAsync(id, version, token);
            else
            {
                var client = HttpClientFactory.CreateClient();
                var url = $"{baseUrl}/v3/package/{Uri.EscapeDataString(id.ToLowerInvariant())}/{version.ToNormalizedString().ToLowerInvariant()}/{(license ? "license" : "readme")}";
                stream = await client.GetStreamAsync(url, token);
            }
            if (stream is null) return null;
            await using (stream)
            using (var reader = new StreamReader(stream))
            {
                // Bound memory even if an optional text asset is unexpectedly large.
                var buffer = new char[512 * 1024 + 1];
                var count = 0;
                while (count < buffer.Length)
                {
                    var read = await reader.ReadAsync(buffer.AsMemory(count), token);
                    if (read == 0) break;
                    count += read;
                }
                if (count == buffer.Length) return "This text is too large to preview.";
                return new string(buffer, 0, count);
            }
        }, speculative);
    }

    private async Task ShowLicenseAsync()
    {
        var expected = generation;
        isLicenseLoading = true;
        licenseText = null;
        if (module is not null) await module.InvokeVoidAsync("showLicense", licenseDialog);
        try
        {
            var text = SelectedPackageVersion?.HasEmbeddedLicense == true
                ? await GetTextAsync(SelectedPackageVersion, license: true).WaitAsync(work.Token)
                : null;
            if (expected == generation && !disposed) licenseText = text;
        }
        catch (OperationCanceledException) { }
        catch { if (expected == generation) licenseText = "Could not load license contents. Please try again."; }
        finally { if (expected == generation && !disposed) isLicenseLoading = false; }
    }

    private void ResetContent()
    {
        generation++;
        work.Cancel(); work.Dispose(); work = new();
        textCache.Dispose(); textCache = new();
        ReadmeHtml = string.Empty; licenseText = null; isReadmeLoading = false; isLicenseLoading = false;
        readmeWork = null;
    }

    private void AuthenticationChanged(Task<AuthenticationState> state) => _ = InvokeAsync(async () =>
    {
        if (disposed) return;
        ResetContent();
        StateHasChanged();
        // Re-enter the host's existing authentication boundary with a fresh request and scope.
        if (module is not null) await module.InvokeVoidAsync("reload");
        else NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
    });

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        if (authentication is not null) authentication.AuthenticationStateChanged -= AuthenticationChanged;
        work.Cancel(); work.Dispose(); textCache.Dispose();
        try
        {
            if (browserListeners is not null) { await browserListeners.InvokeVoidAsync("dispose"); await browserListeners.DisposeAsync(); }
            if (module is not null) await module.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
        browserCallback?.Dispose();
    }
}
