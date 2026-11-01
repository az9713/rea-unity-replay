"""Fetch pinned public reference files into an UNTRACKED folder; never redistribute."""
import hashlib
import json
from pathlib import Path
import urllib.request
ROOT=Path(__file__).resolve().parents[1]
pin=json.loads((ROOT/'investigation/public-source/pin.json').read_text())
target=ROOT/'.runtime/reference-source'
target.mkdir(parents=True,exist_ok=True)
for item in pin['files']:
    data=urllib.request.urlopen(item['url'],timeout=60).read()
    if hashlib.sha256(data).hexdigest()!=item['sha256']:
        raise RuntimeError('Pinned reference source SHA256 mismatch')
    (target/Path(item['path']).name).write_bytes(data)
print('Pinned reference reviewed locally; upstream implementation is excluded from packages.')
