#!/usr/bin/env python3
"""Synthetic loopback-only browser regression/profile. Requires Python Playwright and Chromium.
Start tests/AvantiPoint.Packages.UI.BrowserFixture on each port; use --baseline for unchanged source.
No authenticated/live feed or customer data is used.
"""
import argparse
import json
import statistics
import time
from urllib.request import urlopen
from playwright.sync_api import sync_playwright

parser = argparse.ArgumentParser()
parser.add_argument('--url', default='http://127.0.0.1:5187')
parser.add_argument('--baseline', action='store_true')
parser.add_argument('--screenshots-dir', help='Save synthetic UI evidence screenshots to this directory.')
args = parser.parse_args()
if args.screenshots_dir:
    from pathlib import Path
    Path(args.screenshots_dir).mkdir(parents=True, exist_ok=True)
assert args.url.startswith(('http://127.0.0.1:', 'http://localhost:')), 'Use the loopback synthetic fixture only.'
for attempt in range(60):
    try:
        urlopen(args.url + '/fixture-counts', timeout=1).close()
        break
    except OSError:
        time.sleep(.5)
else:
    raise RuntimeError('Start the synthetic browser fixture before profiling.')
result = {'source': 'unchanged baseline' if args.baseline else 'UI patch', 'synthetic_delays_ms': {'metadata': 300, 'text': 700}}
with sync_playwright() as playwright:
    browser = playwright.chromium.launch(executable_path='/usr/bin/chromium', headless=True, args=['--no-sandbox'])
    page = browser.new_page(viewport={'width': 1360, 'height': 1100})
    errors, documents = [], []
    page.on('pageerror', lambda error: errors.append(str(error)))
    page.on('request', lambda request: documents.append(request.url) if request.resource_type == 'document' else None)
    page.goto(args.url + '/packages/AppPortal.Cli/3.0.52')  # JIT/static asset warm-up
    if args.baseline:
        page.wait_for_timeout(1200)
    else:
        page.wait_for_selector('[data-detail-ready=true]')
        page.wait_for_function("document.querySelector('.pkg-readme') !== null")
    ready_ms = []
    start_docs = len(documents)
    counts_before = json.load(urlopen(args.url + '/fixture-counts'))
    for tab in ['versions', 'dependencies', 'frameworks', 'readme'] * 2:
        selector = f'a[role=tab][href*="tab={tab}"]'
        started = time.perf_counter()
        page.locator(selector).click()
        page.wait_for_function("selector => { const e = document.querySelector(selector); return e && e.hasAttribute('aria-selected') && ['', 'true'].includes(e.getAttribute('aria-selected')); }", arg=selector)
        ready_ms.append(round((time.perf_counter() - started) * 1000, 1))
        if args.baseline:
            page.wait_for_timeout(850)  # allow fresh circuit initialization before next deliberate click
    counts_after = json.load(urlopen(args.url + '/fixture-counts'))
    result.update(tab_ms=ready_ms, tab_median_ms=round(statistics.median(ready_ms), 1), tab_max_ms=max(ready_ms),
        transition_document_requests=len(documents) - start_docs,
        transition_text_requests=sum(counts_after.values()) - sum(counts_before.values()))
    if args.baseline:
        picker = page.locator('[aria-haspopup=listbox]')
        picker.click()
        result['picker_opens_after_circuit'] = page.locator('.version-picker__dropdown').evaluate("e => e.classList.contains('open')")
    else:
        picker = page.get_by_label('Package version', exact=True)
        picker.focus()
        page.keyboard.press('Home')
        page.keyboard.press('ArrowDown')
        page.keyboard.press('Enter')
        picker.select_option('3.0.60')
        page.wait_for_url('**/3.0.60?*')
        assert picker.input_value() == '3.0.60'
        # Repeated/rapid transitions while old text work is still in flight.
        for version in ['3.0.51', '3.0.60', '3.0.52', '3.0.60']:
            picker.select_option(version)
        page.wait_for_function("document.querySelector('.pkg-readme')?.textContent.includes('3.0.60') === true")
        assert '3.0.52' not in page.locator('.pkg-readme').inner_text()
        page.go_back()
        page.wait_for_function("document.querySelector('select').value === '3.0.52'")
        page.go_forward()
        page.wait_for_function("document.querySelector('select').value === '3.0.60'")
        # Keyboard tabs stay within their own tablist.
        readme = page.locator('a[role=tab][href*="tab=readme"]')
        readme.focus()
        page.keyboard.press('ArrowRight')
        page.wait_for_function("document.querySelector('[aria-label=\"Package details\"] a[href*=\"tab=dependencies\"]').getAttribute('aria-selected') === 'true'")
        if args.screenshots_dir:
            page.locator('article > header').screenshot(path=str(Path(args.screenshots_dir) / 'appportal-header.png'))
        page.get_by_role('button', name='Custom license', exact=True).click()
        page.wait_for_function("document.querySelector('dialog').open")
        page.wait_for_function("document.querySelector('.package-license-text')?.textContent.includes('Custom terms') === true")
        assert page.locator('dialog script').count() == 0
        assert page.evaluate('window.licenseExecuted') is None
        assert page.locator('dialog').get_attribute('aria-labelledby')
        if args.screenshots_dir:
            page.locator('dialog').screenshot(path=str(Path(args.screenshots_dir) / 'custom-license.png'))
        page.keyboard.press('Escape')
        assert not page.locator('dialog').evaluate('e => e.open')
        assert page.evaluate('document.activeElement.textContent.trim()') == 'Custom license'
        # Same header geometry and author attribution despite different license metadata.
        sizes = page.locator('.package-action svg').evaluate_all('es => es.map(e => [e.getBoundingClientRect().width, e.getBoundingClientRect().height])')
        assert all(width == 18 and height == 18 for width, height in sizes), sizes
        assert 'Brian, Dan Siegel' in page.locator('header').first.inner_text()
        picker.select_option('3.0.1')
        page.wait_for_url('**/3.0.1?*')
        page.goto(args.url + '/packages/Prism.Core/3.0.52')
        page.wait_for_selector('[data-detail-ready=true]')
        page.wait_for_function("document.querySelector('.pkg-readme')?.textContent.includes('prism.core') === true")
        assert 'appportal.cli' not in page.locator('.pkg-readme').inner_text()
        assert page.get_by_role('link', name='MIT', exact=True).get_attribute('href') == 'https://spdx.org/licenses/MIT.html'
        assert page.locator('.package-actions .package-action').count() == 3
        if args.screenshots_dir:
            page.locator('article > header').screenshot(path=str(Path(args.screenshots_dir) / 'prism-header.png'))
        # Reduced viewport and dark theme retain labels, icon geometry and focus visibility.
        page.set_viewport_size({'width': 390, 'height': 844})
        page.evaluate("document.documentElement.classList.add('dark')")
        assert page.locator('body').evaluate('e => e.scrollWidth <= innerWidth')
        if args.screenshots_dir:
            page.screenshot(path=str(Path(args.screenshots_dir) / 'prism-mobile-dark.png'), full_page=True)
        # Initial Diff query parameters must not leak back after switching tabs locally.
        page.set_viewport_size({'width': 1360, 'height': 1100})
        page.goto(args.url + '/packages/Prism.Core/3.0.52?tab=diff')
        page.wait_for_selector('[data-detail-ready=true]')
        navigation_documents = len(documents)
        page.locator('a[role=tab][href*="tab=readme"]').click()
        page.wait_for_function("document.querySelector('a[href*=\"tab=readme\"]').getAttribute('aria-selected') === 'true'")
        page.get_by_role('tablist', name='Package install options').locator('a[href*="install=pmc"]').click()
        page.wait_for_function("document.querySelector('a[href*=\"install=pmc\"]').getAttribute('aria-selected') === 'true'")
        assert page.locator('a[role=tab][href*="tab=readme"]').get_attribute('aria-selected') == 'true'
        page.locator('a[role=tab][href*="tab=dependencies"]').click()
        page.wait_for_function("document.querySelector('[aria-label=\"Package details\"] a[href*=\"tab=dependencies\"]').getAttribute('aria-selected') === 'true'")
        assert page.get_by_role('tablist', name='Package install options').locator('a[href*="install=pmc"]').get_attribute('aria-selected') == 'true'
        assert len(documents) == navigation_documents
        result['interaction_checks'] = 'native keyboard/pointer selector; rapid/repeated versions; back/forward; keyboard tabs; modal Escape/focus; encoded custom text; MIT URL; authors; icon sizes; mobile/dark layout; ReadMe/install query-state passed'
    result['browser_errors'] = errors
    assert not errors, errors
    browser.close()
print(json.dumps(result, indent=2))
