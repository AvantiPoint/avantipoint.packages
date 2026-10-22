/* Progressive enhancement only. Authorization and submitted values are checked by the server. */
(() => {
    function show(container, visible) {
        if (!container) return;
        container.hidden = !visible;
        container.querySelectorAll('input, select').forEach(input => { input.disabled = !visible; });
    }
    document.querySelectorAll('[data-registry-form]').forEach(form => {
        const ecosystem = form.querySelector('[data-ecosystem]');
        const auth = form.querySelector('[data-auth-mode]');
        function sync() {
            const protocol = ecosystem.value.toLowerCase();
            const oci = protocol.startsWith('oci');
            const npm = protocol === 'npm';
            const target = form.dataset.registryForm === 'target';
            form.querySelector('[data-endpoint]').placeholder = oci
                ? (target ? 'https://ghcr.io/your-organization' : protocol === 'oci:docker' ? 'https://registry-1.docker.io' : 'https://ghcr.io')
                : npm ? 'https://registry.npmjs.org' : 'https://api.nuget.org/v3/index.json';
            form.querySelector('[data-endpoint-help]').textContent = oci
                ? 'Use the registry' + (target ? ' or namespace' : ' base') + ' URL without the /v2 API path.'
                : npm ? 'Use the npm registry base URL, not a package page.' : 'Use the NuGet v3 service index URL, not the gallery website.';
            if (target) show(form.querySelector('[data-target-username]'), oci);
            if (auth) {
                const tokenOption = form.querySelector('[data-token-option]');
                tokenOption.disabled = oci;
                tokenOption.hidden = oci;
                tokenOption.textContent = npm ? 'Bearer access token' : 'NuGet API key';
                form.querySelector('[data-token-label]').textContent = tokenOption.textContent;
                if (oci && auth.value === 'Token') auth.value = 'Basic';
                form.querySelectorAll('[data-auth-basic]').forEach(field => show(field, auth.value === 'Basic'));
                show(form.querySelector('[data-auth-token]'), auth.value === 'Token');
            }
        }
        ecosystem.addEventListener('change', sync);
        auth?.addEventListener('change', sync);
        form.addEventListener('submit', () => {
            const button = form.querySelector('[data-testing-submit]');
            if (!button) return;
            button.disabled = true;
            button.textContent = 'Testing connection…';
            form.querySelector('[data-testing-status]').textContent = 'Checking the registry before saving. This may take up to 15 seconds.';
        });
        sync();
    });
    document.querySelectorAll('[data-package-picker]').forEach((form, index) => {
        const input = form.elements.packageId;
        const ecosystem = form.elements.ecosystem;
        const status = form.querySelector('[data-picker-status]');
        const list = document.createElement('datalist');
        list.id = `hosted-packages-${index}`;
        form.append(list);
        input.setAttribute('list', list.id);
        let timer, controller, choices = [];
        async function search() {
            controller?.abort();
            controller = new AbortController();
            choices = [];
            list.replaceChildren();
            if (!input.value.trim()) { status.textContent = 'Start typing to find hosted packages.'; return; }
            status.textContent = 'Searching hosted packages…';
            const url = new URL(form.dataset.searchUrl, location.href);
            url.searchParams.set('query', input.value);
            url.searchParams.set('ecosystem', ecosystem.value);
            try {
                const response = await fetch(url, { signal: controller.signal, headers: { Accept: 'application/json' } });
                if (!response.ok) throw new Error('Search failed');
                choices = await response.json();
                list.replaceChildren(...choices.map(name => { const option = document.createElement('option'); option.value = name; return option; }));
                status.textContent = choices.length ? `${choices.length} matching packages. Select a suggestion or enter an exact hosted package name.` : 'No hosted packages match. Try a different name or ecosystem.';
            } catch (error) {
                if (error.name !== 'AbortError') status.textContent = 'Search is unavailable. You can retry or submit an exact hosted package name for validation.';
            }
        }
        input.addEventListener('input', () => { clearTimeout(timer); controller?.abort(); timer = setTimeout(search, 200); });
        ecosystem.addEventListener('change', () => { input.value = ''; search(); });
    });
    const allFeeds = document.getElementById('NewAllFeeds');
    if (allFeeds) {
        const sync = () => document.querySelectorAll('input[name="NewFeedIds"]').forEach(input => { input.disabled = allFeeds.checked; });
        allFeeds.addEventListener('change', sync);
        sync();
    }
})();
