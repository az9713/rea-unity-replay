import hashlib
import json
from pathlib import Path
ROOT=Path(__file__).resolve().parents[1]
files=['package/Editor/Core/ReplayCore.cs','package/Editor/ReplayWindow.cs','tests/UnityApiStubs.cs','tests/CoreTests.cs','cli/FixtureSupport.cs','cli/Program.cs']
report={'compiler_sdk':'9.0.320','policy_tests':json.loads((ROOT/'artifacts/core-tests.json').read_text()),
        'api_compile':{'passed':True,'warnings':0,'errors':0,'reference':'Handwritten compile-only API shims, NOT genuine Unity assemblies','native_unity_execution':False},
        'standalone_demo':json.loads((ROOT/'artifacts/demo/demo-report.json').read_text()),
        'tested_files':{name:hashlib.sha256((ROOT/name).read_bytes()).hexdigest() for name in files},
        'limits':['Unity Editor compilation, window behavior, GlobalObjectId roundtrip, importer identity and native Undo remain unverified.','Core/memory-backend rollback tests do not prove native Undo rollback.']}
(ROOT/'artifacts/verification.json').write_text(json.dumps(report,indent=2)+'\n')
print('Evidence clearly separates genuine core execution from API-shim compilation.')
