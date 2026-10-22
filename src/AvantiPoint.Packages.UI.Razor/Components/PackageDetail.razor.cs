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
    private CancellationTokenSource? diffCancellation;
    private int navigation;
    private PackageInfoCollection? lastPackage;
    private string? lastFeed, lastInputVersion;
    private int generation;
    private bool interactive, disposed, isReadmeLoading, isLicenseLoading;
    private string? licenseText;
    private Task? readmeWork, diffWork;
    private string? diffKey;

    protected override void OnInitialized()
    {
        authentication = Services.GetService<AuthenticationStateProvider>();
        if (authentication is not null) authentication.AuthenticationStateChanged += AuthenticationChanged;
    }

    protected override async Task OnParametersSetAsync()
    {
        if (Package is null) return;
        var changed = !ReferenceEquals(lastPackage, Package) || lastFeed != FeedBaseUrl;
        var inputVersion = Package.Version?.OriginalVersion ?? Package.Versions.FirstOrDefault()?.Version?.OriginalVersion ?? "";
        if (changed)
        {
            ResetContent();
            lastPackage = Package;
            lastFeed = FeedBaseUrl;
        }
        if (changed || lastInputVersion != inputVersion)
        {
            SelectedVersionString = inputVersion;
            lastInputVersion = inputVersion;
        }
        ActiveContentTab = NormalizeTab(Tab, Compare);
        ActiveInstallTab = string.IsNullOrWhiteSpace(Install) ? "cli" : Install.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(ReadmeMarkdown))
            ReadmeHtml = Markdown.ToHtml(ReadmeMarkdown, Pipeline);
        if (interactive)
            StartReadme();
        // Diff is explicitly requested, never speculative. Keep SSR diff links working.
        if (ActiveContentTab == "diff") await LoadDiffAsync();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!firstRender) return;
        interactive = true;
        try
        {
            module = await JSRuntime.InvokeAsync<IJSObjectReference>("import", "./_content/AvantiPoint.Packages.UI.Razor/js/ap-package-detail.js");
            if (module is not null)
            {
                browserCallback = DotNetObjectReference.Create(this);
                browserListeners = await module.InvokeAsync<IJSObjectReference>("attach", detailRoot, browserCallback);
            }
        }
        catch (InvalidOperationException) { } // Static renderers keep ordinary links usable.
        StartReadme();
    }

    private static string NormalizeTab(string? tab, string? compare) => tab?.Trim().ToLowerInvariant() switch
    {
        "dependencies" => "dependencies", "release-notes" => "release-notes", "badges" => "badges",
        "diff" => "diff", "versions" => "versions", "frameworks" => "frameworks",
        _ => string.IsNullOrWhiteSpace(compare) ? "readme" : "diff"
    };

    private Task VersionChangedAsync(ChangeEventArgs args) => NavigateDetail(
        $"{PackageVersionUrl(args.Value?.ToString() ?? SelectedVersionString)}?tab={ActiveContentTab}&install={ActiveInstallTab}", true);

    /// <summary>Applies same-package history navigation without reloading metadata or the Blazor circuit.</summary>
    [JSInvokable]
    public async Task NavigateDetail(string destination, bool pushHistory)
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
        var selected = Package.Versions.FirstOrDefault(v => NuGetVersion.TryParse(version, out var parsed) && v.Version == parsed);
        if (selected is null) { NavigationManager.NavigateTo(destination, forceLoad: true); return; }
        var query = QueryHelpers.ParseQuery(uri.Query);
        var nextTab = NormalizeTab(query["tab"], query["compare"]);
        var versionChanged = SelectedVersionString != selected.Version.OriginalVersion;
        if (versionChanged)
        {
            generation++;
            work.Cancel(); work.Dispose(); work = new();
            SelectedVersionString = selected.Version.OriginalVersion;
            ReadmeHtml = string.Empty;
            isReadmeLoading = false; isLicenseLoading = false;
            licenseText = null;
            readmeWork = null;
            DiffResult = null; diffWork = null; diffKey = null;
        }
        ActiveContentTab = nextTab;
        ActiveInstallTab = string.IsNullOrWhiteSpace(query["install"]) ? "cli" : query["install"].ToString();
        Compare = query["compare"]; SelectedFile = query["file"]; DiffView = query["view"]; Tfm = query["tfm"];
        if (ActiveContentTab != "diff")
        {
            diffCancellation?.Cancel(); diffKey = null; diffWork = null;
        }
        StateHasChanged();
        StartReadme();
        if (module is not null)
        {
            if (versionChanged) await module.InvokeVoidAsync("closeLicense", licenseDialog);
            if (navigationId != navigation) return;
            if (pushHistory) await module.InvokeVoidAsync("updateUrl", uri.PathAndQuery, false);
        }
        if (navigationId == navigation && ActiveContentTab == "diff") await LoadDiffAsync();
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
            var versions = Package.Versions;
            var index = versions.FindIndex(v => v.Version == selected.Version);
            var adjacent = versions.ElementAtOrDefault(index + 1) ?? versions.ElementAtOrDefault(index - 1);
            if (adjacent?.HasReadme == true && !IsLiveUpstream(adjacent))
                await GetTextAsync(adjacent, license: false).WaitAsync(cancellationToken);
        }
        catch (OperationCanceledException) { }
        catch { }
    }

    private Task<string?> GetTextAsync(PackageInfo selected, bool license)
    {
        var id = selected.PackageId;
        var version = selected.Version;
        var baseUrl = ContentBaseUrl;
        return textCache.GetAsync($"{baseUrl}|{id.ToLowerInvariant()}|{version.ToNormalizedString()}|{license}", async token =>
        {
            var content = Services.GetService<IPackageContentService>();
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
        });
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

    private Task LoadDiffAsync()
    {
        var compare = string.IsNullOrWhiteSpace(Compare) ? DefaultCompareVersion : Compare;
        var key = $"{SelectedVersionString}|{compare}|{SelectedFile}|{DiffView}|{Tfm}";
        if (key == diffKey && diffWork is not null) return diffWork;
        diffCancellation?.Cancel(); diffCancellation?.Dispose();
        diffCancellation = CancellationTokenSource.CreateLinkedTokenSource(work.Token);
        diffKey = key;
        diffWork = FetchDiffAsync(key, compare, generation, diffCancellation.Token);
        return diffWork;
    }

    private async Task FetchDiffAsync(string key, string? compare, int expected, CancellationToken cancellationToken)
    {
        var id = Package.PackageId;
        var version = SelectedVersionString;
        DiffResult = null;
        try
        {
            var result = await DiffService.GetDiffAsync(id, version, compare, OtherVersionStrings.Count == 0,
                SelectedFile, DiffView, Tfm, cancellationToken);
            if (!disposed && expected == generation && diffKey == key) DiffResult = result;
        }
        catch (OperationCanceledException) { }
        catch
        {
            if (!disposed && expected == generation && diffKey == key)
                DiffResult = new PackageDiffResult { PackageId = id, CurrentVersion = version, CompareVersion = compare, Error = "Could not load package diff." };
        }
        if (!disposed) await InvokeAsync(StateHasChanged);
    }

    private void ResetContent()
    {
        generation++;
        work.Cancel(); work.Dispose(); work = new();
        textCache.Dispose(); textCache = new();
        ReadmeHtml = string.Empty; licenseText = null; isReadmeLoading = false; isLicenseLoading = false;
        readmeWork = null; DiffResult = null; diffWork = null; diffKey = null;
    }

    private void AuthenticationChanged(Task<AuthenticationState> state) => _ = InvokeAsync(() =>
    {
        if (disposed) return;
        ResetContent();
        // Re-enter the host's existing authentication boundary with a fresh request and scope.
        NavigationManager.NavigateTo(NavigationManager.Uri, forceLoad: true);
    });

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        if (authentication is not null) authentication.AuthenticationStateChanged -= AuthenticationChanged;
        work.Cancel(); work.Dispose(); textCache.Dispose();
        diffCancellation?.Cancel(); diffCancellation?.Dispose();
        try
        {
            if (browserListeners is not null) { await browserListeners.InvokeVoidAsync("dispose"); await browserListeners.DisposeAsync(); }
            if (module is not null) await module.DisposeAsync();
        }
        catch (JSDisconnectedException) { }
        browserCallback?.Dispose();
    }
}
