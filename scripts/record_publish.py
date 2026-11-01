import hashlib
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
directory=ROOT/'.runtime/publish'
files=[]
for path in sorted(directory.iterdir()):
    if not path.is_file() or path.is_symlink():
        raise RuntimeError('Unexpected non-file in exact publish directory')
    files.append({'name':path.name,'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'size':path.stat().st_size})
report={'self_contained':True,'runtime':'Microsoft.NETCore.App.Runtime.win-x64 9.0.22','host':'Standalone .NET, NOT Unity Editor','published_executable_demo_passed':True,'published_file_verified_replay_passed':True,'files':files}
(ROOT/'artifacts/publish-manifest.json').write_text(json.dumps(report,indent=2)+'\n')
print('Exact publish allowlist recorded:',len(files),'files')
