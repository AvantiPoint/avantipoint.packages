#!/usr/bin/env python3
"""Create a fresh validation copy without modifying the preserved checkout."""
import json
from pathlib import Path
import shutil
import subprocess
import sys

revision = 'db3c29924dbaf30da285b57877f4486964ed6c51'
source = Path(sys.argv[1]).resolve()
destination = Path(sys.argv[2]).resolve()
root = Path(__file__).resolve().parents[2]
assert subprocess.check_output(['git', '-C', str(source), 'rev-parse', revision], text=True).strip() == revision
destination.mkdir(parents=True, exist_ok=False)
archive = subprocess.Popen(['git', '-C', str(source), 'archive', revision], stdout=subprocess.PIPE)
try:
    subprocess.run(['tar', '-x', '-C', str(destination)], stdin=archive.stdout, check=True)
finally:
    archive.stdout.close()
assert archive.wait() == 0
component = Path('src/AvantiPoint.Packages.Registry.Oci')
for path in (destination / component).rglob('*.cs'):
    path.unlink()
for path in (root / component).rglob('*.cs'):
    target = destination / path.relative_to(root)
    target.parent.mkdir(parents=True, exist_ok=True)
    shutil.copyfile(path, target)
adapter = Path('src/AvantiPoint.Feed.Platform/Authentication/OciFeedAuthenticationAdapter.cs')
shutil.copyfile(root / adapter, destination / adapter)
shutil.copyfile(root / 'tests/FullSourceRegression/OciReferrersHttpTests.cs',
                destination / 'tests/AvantiPoint.Packages.Registry.Oci.Tests/OciReferrersHttpTests.cs')
commit = subprocess.check_output(['git', '-C', str(root), 'rev-parse', 'HEAD'], text=True).strip()
(destination / 'validation-source.json').write_text(json.dumps({
    'preservedSource': revision, 'recoveryOverlay': commit,
    'purpose': 'Local synthetic source validation; not a production image qualification.'
}, indent=2) + '\n')
print(destination)
