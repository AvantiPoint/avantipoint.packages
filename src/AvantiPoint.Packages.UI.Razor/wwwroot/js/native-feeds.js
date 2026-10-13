function selectTab(button) {
    const container = button.closest('[data-native-tabs]');
    if (!container) return;
    for (const tab of container.querySelectorAll('[role="tab"]')) {
        if (tab.closest('[data-native-tabs]') !== container) continue;
        const selected = tab === button;
        tab.setAttribute('aria-selected', String(selected));
        tab.tabIndex = selected ? 0 : -1;
        const panel = document.getElementById(tab.dataset.nativeTab);
        if (panel) panel.hidden = !selected;
    }
}

document.addEventListener('keydown', event => {
    const tab = event.target.closest('[data-native-tab]');
    if (!tab || !['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
    const list = [...tab.parentElement.querySelectorAll('[role="tab"]')];
    const index = list.indexOf(tab);
    const next = event.key === 'Home' ? 0 : event.key === 'End' ? list.length - 1
        : (index + (event.key === 'ArrowRight' ? 1 : -1) + list.length) % list.length;
    event.preventDefault();
    selectTab(list[next]);
    list[next].focus();
});

document.addEventListener('click', async event => {
    const tab = event.target.closest('[data-native-tab]');
    if (tab) { selectTab(tab); return; }
    const button = event.target.closest('[data-native-copy]');
    if (!button) return;
    const target = document.getElementById(button.dataset.nativeCopy);
    if (!target) return;
    const status = button.closest('[data-copy-container]')?.querySelector('[data-copy-status]');
    try {
        await navigator.clipboard.writeText(target.textContent);
        button.textContent = 'Copied';
        if (status) status.textContent = 'Copied to clipboard.';
        setTimeout(() => { button.textContent = 'Copy'; }, 2500);
    } catch {
        if (status) status.textContent = 'Copy unavailable. Select the code to copy it.';
    }
});
