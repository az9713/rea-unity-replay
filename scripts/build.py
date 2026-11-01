"""Package exact UPM paths and a hash-verified, generated CLI publish allowlist."""
import hashlib
import io
import json
from pathlib import Path
import tarfile
import zipfile

ROOT=Path(__file__).resolve().parents[1]
OUT=ROOT/'artifacts'
UPM=['package.json','Editor/REA.UnityReplay.Editor.asmdef','Editor/ReplayWindow.cs','Editor/Core/ReplayCore.cs']
DEMO=['manifest.json','baseline.json','context.json','result.json','demo-report.json','verified-plan.json','Assets/Fixture.mesh','Assets/Fixture.mesh.meta']


def checksum(path):
    digest=hashlib.sha256(path.read_bytes()).hexdigest()
    path.with_suffix(path.suffix+'.sha256').write_text(digest+'  '+path.name+'\n')
    print(path.name,path.stat().st_size,digest)


def zip_exact(path,entries):
    with zipfile.ZipFile(path,'w',zipfile.ZIP_DEFLATED) as archive:
        for source,name in entries:
            if not source.is_file() or not source.resolve().is_relative_to(ROOT) or '..' in name.split('/') or name.startswith('/'):
                raise RuntimeError('Missing or escaping archive entry: '+name)
            archive.write(source,name)
    with zipfile.ZipFile(path) as archive:
        if archive.testzip() is not None:raise RuntimeError('ZIP integrity failure')
    checksum(path)


def build():
    verification=json.loads((OUT/'verification.json').read_text())
    tests=verification['policy_tests']
    if tests['passed']!=tests['total'] or tests['total']<20:raise RuntimeError('Real core tests must pass')
    if not verification['api_compile']['passed'] or verification['api_compile']['native_unity_execution']:
        raise RuntimeError('Compile-only evidence must retain its explicit native-unverified boundary')
    for name,digest in verification['tested_files'].items():
        if hashlib.sha256((ROOT/name).read_bytes()).hexdigest()!=digest:raise RuntimeError('Stale code evidence: '+name)
    published=json.loads((OUT/'publish-manifest.json').read_text())
    if not published['self_contained'] or not published['published_executable_demo_passed'] or not published['published_file_verified_replay_passed']:
        raise RuntimeError('Published EXE verification incomplete')
    entries=[(ROOT/'package'/name,'com.rea.unity-replay/'+name) for name in UPM]
    entries += [(ROOT/name,'com.rea.unity-replay/'+name) for name in ['README.md','NOTICE.md','LICENSE']]
    zip_exact(OUT/'GuardedReplay-0.1.0-upm.zip',entries)
    tgz=OUT/'com.rea.unity-replay-0.1.0.tgz'
    with tarfile.open(tgz,'w:gz') as archive:
        for source,name in entries:
            data=source.read_bytes();info=tarfile.TarInfo('package/'+name.split('/',1)[1]);info.size=len(data);info.mode=0o644;info.mtime=0;archive.addfile(info,io.BytesIO(data))
    checksum(tgz)
    cli=[]
    for item in published['files']:
        name=item['name'];source=ROOT/'.runtime/publish'/name
        if '/' in name or '\\' in name or not source.is_file() or hashlib.sha256(source.read_bytes()).hexdigest()!=item['sha256']:
            raise RuntimeError('Published file differs from exact allowlist: '+name)
        cli.append((source,name))
    cli += [(ROOT/name,name) for name in ['README.md','NOTICE.md','LICENSE']]
    cli += [(OUT/'published-demo'/name,'example/'+name) for name in DEMO]
    zip_exact(OUT/'GuardedReplay-0.1.0-cli-win-x64.zip',cli)
    (ROOT/'docs/evidence.json').write_text(json.dumps(verification,indent=2)+'\n')
    (ROOT/'docs/demo-result.json').write_text((OUT/'demo/result.json').read_text())
    print('Packages validated. Native Unity execution remains UNVERIFIED.')


if __name__=='__main__':build()
