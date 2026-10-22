export function attach(root, callback) {
    const navigate = event => {
        const link = event.target.closest('a[href]');
        if (!link || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || link.target) return;
        const url = new URL(link.href, location.href);
        const current = new URL(location.href);
        // Intercept only links to a version of this package. Other packages keep normal navigation.
        const packagePath = root.dataset.packagePath;
        if (url.origin !== current.origin || !url.pathname.startsWith(packagePath + '/') || url.pathname.slice(packagePath.length + 1).includes('/')) return;
        event.preventDefault();
        callback.invokeMethodAsync('NavigateDetail', url.href, true);
    };
    const keys = event => {
        const tab = event.target.closest('[role="tab"]');
        if (!tab || !['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
        const tabs = [...root.querySelectorAll('[role="tab"]')];
        const index = tabs.indexOf(tab);
        const next = event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1
            : (index + (event.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length;
        event.preventDefault(); tabs[next].focus(); tabs[next].click();
    };
    const back = () => callback.invokeMethodAsync('NavigateDetail', location.href, false);
    root.addEventListener('click', navigate);
    root.addEventListener('keydown', keys);
    window.addEventListener('popstate', back);
    return DotNet.createJSObjectReference({ dispose() { root.removeEventListener('click', navigate); root.removeEventListener('keydown', keys); window.removeEventListener('popstate', back); } });
}
export function updateUrl(url, replace) {
    history[replace ? 'replaceState' : 'pushState'](null, '', url);
}
export function showLicense(dialog) { if (!dialog.open) dialog.showModal(); }
export function closeLicense(dialog) { if (dialog.open) dialog.close(); }
