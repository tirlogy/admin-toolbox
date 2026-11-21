#!/usr/bin/env python3
# SPDX-License-Identifier: MIT
"""Adapts the published site for a sub-path host (GitHub Pages preview).

Sets <base href>, adds 404.html for deep links and updates the index.html hash in the
service worker manifest so offline caching keeps working. Usage: pages-subpath.py <site_dir> </sub/path/>
"""
import base64
import hashlib
import json
import pathlib
import shutil
import sys

site = pathlib.Path(sys.argv[1])
base = sys.argv[2]
assert base.startswith('/') and base.endswith('/'), 'base must look like /name/'

index = site / 'index.html'
html = index.read_text(encoding='utf-8')
assert '<base href="/" />' in html, 'unexpected base tag'
html = html.replace('<base href="/" />', f'<base href="{base}" />')
index.write_text(html, encoding='utf-8')
for suffix in ('.br', '.gz'):
    (site / f'index.html{suffix}').unlink(missing_ok=True)
shutil.copyfile(index, site / '404.html')

manifest_file = site / 'service-worker-assets.js'
text = manifest_file.read_text(encoding='utf-8')
start, end = text.index('{'), text.rindex('}') + 1
manifest = json.loads(text[start:end])
digest = 'sha256-' + base64.b64encode(hashlib.sha256(index.read_bytes()).digest()).decode()
for asset in manifest['assets']:
    if asset['url'] == 'index.html':
        asset['hash'] = digest
manifest_file.write_text(text[:start] + json.dumps(manifest, indent=2) + text[end:], encoding='utf-8')
for suffix in ('.br', '.gz'):
    (site / f'service-worker-assets.js{suffix}').unlink(missing_ok=True)
print(f'adapted {site} for {base}')
