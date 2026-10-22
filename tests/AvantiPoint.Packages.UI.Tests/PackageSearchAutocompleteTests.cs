using AvantiPoint.Packages.Core;
using TestContext = Xunit.TestContext;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;

namespace AvantiPoint.Packages.UI.Tests;

public sealed class PackageSearchAutocompleteTests : IDisposable
{
    private readonly BunitContext context = new();
    private readonly SearchService search = new();

    public PackageSearchAutocompleteTests()
    {
        context.Services.AddLogging();
        context.Services.AddSingleton<INuGetSearchService>(search);
        context.JSInterop.Mode = JSRuntimeMode.Loose;
    }

    [Fact]
    public async Task Enter_WithSuggestions_SearchesTypedText()
    {
        var component = context.Render<PackageSearch>();
        await Type(component, "prism");
        await Key(component, "Enter");

        Assert.Equal("prism", search.LastQuery);
        Assert.Equal("prism", Input(component).GetAttribute("value"));
        Assert.Empty(component.FindAll("[role=listbox]"));
    }

    [Theory]
    [InlineData("ArrowDown", "Prism.Core")]
    [InlineData("ArrowUp", "Prism.Wpf")]
    public async Task KeyboardSelection_IsExplicitAndAccessible(string key, string expected)
    {
        var component = context.Render<PackageSearch>();
        await Type(component, "prism");
        Assert.Null(Input(component).GetAttribute("aria-activedescendant"));
        var listbox = component.Find("[role=listbox]");
        Assert.Equal(listbox.Id, Input(component).GetAttribute("aria-controls"));

        await Key(component, key);
        Assert.Equal("prism", Input(component).GetAttribute("value"));
        var activeId = Input(component).GetAttribute("aria-activedescendant");
        Assert.Equal(expected, component.Find($"[id='{activeId}']").TextContent.Trim());
        await Key(component, "Enter");

        Assert.Equal(expected, search.LastQuery);
    }

    [Fact]
    public async Task Escape_ClearsKeyboardSelectionAndPreservesText()
    {
        var component = context.Render<PackageSearch>();
        await Type(component, "prism");
        await Key(component, "ArrowDown");
        await Key(component, "Escape");
        await Key(component, "Enter");

        Assert.Equal("prism", search.LastQuery);
        Assert.Equal("false", Input(component).GetAttribute("aria-expanded"));
    }

    [Fact]
    public async Task PointerClick_SelectsTheClickedSuggestion()
    {
        var component = context.Render<PackageSearch>();
        await Type(component, "prism");
        await component.FindAll("[role=option] button")[1].ClickAsync(new MouseEventArgs());
        Assert.Equal("Prism.Wpf", search.LastQuery);
    }

    [Fact]
    public async Task Blur_DismissesWithoutCommitting()
    {
        var component = context.Render<PackageSearch>();
        await Type(component, "prism");
        await Key(component, "ArrowDown");
        await Input(component).BlurAsync(new FocusEventArgs());

        Assert.Equal("prism", Input(component).GetAttribute("value"));
        Assert.Empty(component.FindAll("[role=listbox]"));
        Assert.Null(Input(component).GetAttribute("aria-activedescendant"));
    }

    [Fact]
    public async Task Editing_ClearsPreviousOptionsBeforeTheNextResponse()
    {
        var component = context.Render<PackageSearch>();
        await Type(component, "prism");
        await Key(component, "ArrowDown");
        var pending = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        search.Autocomplete = (_, _) => { started.TrySetResult(); return pending.Task; };
        var typing = Type(component, "prisma");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await Key(component, "Enter");
        var submittedQuery = search.LastQuery;
        pending.SetResult(["Prisma.Client"]);
        await typing;
        Assert.Equal("prisma", submittedQuery);
        Assert.Empty(component.FindAll("[role=listbox]"));
    }

    [Theory]
    [InlineData("Escape")]
    [InlineData("Blur")]
    public async Task Dismissal_PreventsPendingResponseFromReopening(string dismissal)
    {
        var pending = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        search.Autocomplete = (_, _) => { started.TrySetResult(); return pending.Task; };
        var component = context.Render<PackageSearch>();
        var typing = Type(component, "prism");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        if (dismissal == "Blur") await Input(component).BlurAsync(new FocusEventArgs());
        else await Key(component, dismissal);
        pending.SetResult(["Prism.Core"]);
        await typing;

        Assert.Empty(component.FindAll("[role=listbox]"));
        Assert.Equal("prism", Input(component).GetAttribute("value"));
    }

    [Fact]
    public async Task OlderResponse_CannotReplaceNewerSuggestions()
    {
        var old = new TaskCompletionSource<IReadOnlyList<string>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        search.Autocomplete = (query, _) =>
        {
            if (query != "pri") return Task.FromResult<IReadOnlyList<string>>(["Prism.Wpf"]);
            started.TrySetResult();
            return old.Task;
        };
        var component = context.Render<PackageSearch>();
        var first = Type(component, "pri");
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken);
        await Type(component, "prism");
        old.SetResult(["Prism.Core"]);
        await first;

        Assert.Equal("Prism.Wpf", component.Find("[role=option]").TextContent.Trim());
        await Key(component, "Enter");
        Assert.Equal("prism", search.LastQuery);
    }

    private static AngleSharp.Dom.IElement Input(IRenderedComponent<PackageSearch> component) => component.Find("input[role=combobox]");
    private static Task Type(IRenderedComponent<PackageSearch> component, string value) => Input(component).InputAsync(new ChangeEventArgs { Value = value });
    private static Task Key(IRenderedComponent<PackageSearch> component, string key) => Input(component).KeyDownAsync(new KeyboardEventArgs { Key = key });
    public void Dispose() => context.Dispose();

    private sealed class SearchService : INuGetSearchService
    {
        public string? LastQuery { get; private set; }
        public string? AutocompleteQuery { get; private set; }
        public Func<string?, CancellationToken, Task<IReadOnlyList<string>>> Autocomplete { get; set; } =
            (_, _) => Task.FromResult<IReadOnlyList<string>>(["Prism.Core", "Prism.Wpf"]);

        public Task<SearchResponse> SearchAsync(SearchRequest request, CancellationToken cancellationToken = default)
        {
            LastQuery = request.Query;
            return Task.FromResult(new SearchResponse { Data = [], TotalHits = 0 });
        }

        public Task<SearchResponse> SearchAsync(string? query = null, int skip = 0, int take = 20, bool includePrerelease = false,
            string? packageType = null, string? framework = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SearchResponse { Data = [], TotalHits = 0 });

        public Task<IReadOnlyList<string>> AutocompleteAsync(string? query = null, int skip = 0, int take = 10,
            bool includePrerelease = false, CancellationToken cancellationToken = default)
        {
            AutocompleteQuery = query;
            return Autocomplete(query, cancellationToken);
        }
    }
}
