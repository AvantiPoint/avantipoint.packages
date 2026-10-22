"""Exercise only the master-only NuGet UI using generated loopback data."""
import json, time
from pathlib import Path
from urllib.request import urlopen
from playwright.sync_api import sync_playwright
origin = 'http://127.0.0.1:5188'
for _ in range(60):
    try:
        urlopen(origin + '/fixture-counts').close()
        break
    except OSError:
        time.sleep(.5)
with sync_playwright() as p:
    browser = p.chromium.launch(executable_path='/usr/bin/chromium', headless=True, args=['--no-sandbox'])
    page = browser.new_page(viewport={'width': 1280, 'height': 1000})
    errors, documents = [], []
    page.on('pageerror', lambda e: errors.append(str(e)))
    page.on('request', lambda r: documents.append(r.url) if r.resource_type == 'document' else None)
    page.goto(origin + '/packages/AppPortal.Cli/3.0.52')
    page.wait_for_selector('[data-detail-ready=true]')
    picker = page.get_by_label('Package version', exact=True)
    assert picker.input_value() == '3.0.52'
    picker.click()
    page.keyboard.press('Escape')
    page.wait_for_function("document.querySelector('.package-readme') !== null")
    initial_documents = len(documents)
    for tab in ['Versions', 'References and Dependencies', 'ReadMe'] * 2:
        page.get_by_role('tab', name=tab, exact=True).click()
        page.wait_for_function("label => [...document.querySelectorAll('[role=tab]')].some(e => e.textContent.trim() === label && e.getAttribute('aria-selected') === 'true')", arg=tab)
    tab_document_requests = len(documents) - initial_documents
    assert tab_document_requests == 0
    page.get_by_role('tab', name='Central Package Management', exact=True).click()
    page.get_by_role('tab', name='Versions', exact=True).click()
    picker.select_option('3.0.60')
    page.wait_for_url('**/3.0.60?tab=versions&install=cpm')
    page.wait_for_function("Number(document.querySelector('[data-version-callbacks]').dataset.versionCallbacks) > 0 && document.querySelector('.package-tabs [aria-selected=true]').textContent.trim() === 'Versions' && document.querySelector('.install-tabs [aria-selected=true]').textContent.trim() === 'Central Package Management'")
    page.get_by_role('tab', name='ReadMe', exact=True).click()
    for v in ['3.0.51', '3.0.60', '3.0.52', '3.0.60']:
        picker.select_option(v)
    page.wait_for_function("document.querySelector('.package-readme')?.textContent.includes('3.0.60') === true")
    page.go_back()
    page.wait_for_function("document.querySelector('select').value === '3.0.52'")
    page.go_forward()
    page.wait_for_function("document.querySelector('select').value === '3.0.60'")
    page.get_by_role('button', name='Custom license', exact=True).click()
    page.wait_for_function("document.querySelector('dialog').open")
    page.wait_for_function("document.querySelector('.package-license-text')?.textContent.includes('Custom terms') === true")
    assert page.locator('dialog script').count() == 0
    assert page.evaluate('window.licenseExecuted') is None
    page.keyboard.press('Escape')
    assert page.evaluate('document.activeElement.textContent.trim()') == 'Custom license'
    picker.select_option('3.0.1')
    page.goto(origin + '/packages/Prism.Core/3.0.52')
    page.wait_for_selector('[data-detail-ready=true]')
    assert page.get_by_role('link', name='MIT', exact=True).get_attribute('href') == 'https://spdx.org/licenses/MIT.html'
    page.wait_for_function("document.querySelector('.package-readme')?.textContent.includes('prism.core') === true")
    assert 'appportal.cli' not in page.locator('.package-readme').inner_text()
    page.goto(origin + '/feed/packages/Prism.Core/3.0.52')
    page.wait_for_selector('[data-detail-ready=true]')
    page.get_by_label('Package version', exact=True).select_option('3.0.60')
    page.wait_for_url('**/feed/packages/Prism.Core/3.0.60?*')
    page.wait_for_function("document.querySelector('.package-readme')?.textContent.includes('3.0.60') === true")
    page.set_viewport_size({'width': 390, 'height': 844})
    assert page.locator('body').evaluate('e => e.scrollWidth <= innerWidth')
    assert not errors, errors
    browser.close()
print(json.dumps({'synthetic': True, 'tab_document_requests': tab_document_requests,
                  'browser_errors': errors, 'checks': 'native pointer/keyboard picker, repeated tabs, callback parent rerender tab/URL preservation, rapid versions, history, interrupted package navigation, safe custom license modal, MIT, mounted feed path and mobile layout passed'}))
