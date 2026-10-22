# Synthetic package-details screenshot evidence

These screenshots come from the committed loopback browser fixture. Package metadata, versions and license text are generated test inputs, not live feed measurements or private package contents.

## AppPortal.Cli header

![Synthetic AppPortal.Cli header with consistent Website, Source and Custom license actions](appportal-header.png)

## Prism.Core header

![Synthetic Prism.Core header with consistent Website, Source and MIT actions](prism-header.png)

## Custom-license modal

![Synthetic license dialog rendering script-like text literally, with a labelled Close action](custom-license.png)

## Narrow dark layout

![Synthetic Prism.Core page at a narrow viewport in the dark theme](prism-mobile-dark.png)

Reproduce from the repository root while the fixture is running:

```sh
python scripts/profile-package-detail.py --url http://127.0.0.1:5187 --screenshots-dir docs/recovery/package-details-ui-fixtures
```
