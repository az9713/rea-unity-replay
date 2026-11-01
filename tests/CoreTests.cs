using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Rea.UnityReplay.Core;
using Rea.UnityReplay.Tooling;

static class CoreTests
{
    sealed class Result { public string name=""; public bool passed; public string error=""; }
    static readonly List<Result> Results=new List<Result>();
    static void Assert(bool condition,string text) { if(!condition) throw new Exception(text); }
    static void Refuses(Action action,string contains="")
    {
        try {action();} catch(InvalidOperationException error) {Assert(error.Message.Contains(contains),error.Message);return;}
        throw new Exception("Expected conservative refusal");
    }
    static void Test(string name,Action action)
    {
        try {action();Results.Add(new Result{name=name,passed=true});Console.WriteLine("PASS "+name);}
        catch(Exception e) {Results.Add(new Result{name=name,error=e.ToString()});Console.WriteLine("FAIL "+name+" "+e.Message);}
    }
    static ReplayPlan Plan(ReplayManifest m) {return ReplayPolicy.Plan(m,m.baseline,m.context);}
    static int Main(string[] args)
    {
        Test("bounded replay produces declared result without mutating baseline",()=>{var m=Fixture.Create();var plan=Plan(m);var child=plan.result.objects[1];Assert(child.position.x==2&&child.scale.x==1.5f&&child.name=="VerifiedMarker"&&!child.active,"Wrong result");Assert(ReplayPolicy.StateHash(m.baseline)==m.baselineHash,"Baseline mutated");});
        Test("wrong baseline and scene root are refused",()=>{var m=Fixture.Create();var actual=m.baseline.Copy();actual.objects[1].name="Other";Refuses(()=>ReplayPolicy.Plan(m,actual,m.context),"Wrong root/baseline");actual=m.baseline.Copy();actual.sceneGuid=new string('d',32);Refuses(()=>ReplayPolicy.Plan(m,actual,m.context));});
        Test("duplicate object IDs are ambiguous",()=>{var m=Fixture.Create();m.baseline.objects.Add(m.baseline.objects[1].Copy());Refuses(()=>Plan(m),"Ambiguous");});
        Test("hierarchy escape and cycle are refused",()=>{var m=Fixture.Create();m.baseline.objects[1].parentId="outside";Refuses(()=>Plan(m),"escapes");m=Fixture.Create();m.baseline.objects[1].parentId=m.baseline.objects[1].id;Refuses(()=>Plan(m),"cycle");});
        Test("unknown components and nondeterministic callbacks are refused",()=>{var m=Fixture.Create();m.baseline.objects[1].unsupportedComponents=new[]{"ArbitraryMonoBehaviour"};Refuses(()=>Plan(m),"nondeterministic");});
        Test("stale command precondition is refused",()=>{var m=Fixture.Create();m.commands[1].beforeObjectHash=new string('0',64);Refuses(()=>Plan(m),"Stale");});
        Test("unknown reflection command is refused",()=>{var m=Fixture.Create();m.commands[0].kind="invoke:System.IO.File.Delete";Refuses(()=>Plan(m),"reflection");});
        Test("nonfinite vectors are refused",()=>{foreach(float n in new[]{float.NaN,float.PositiveInfinity,float.NegativeInfinity}){var m=Fixture.Create();m.commands[0].vector.x=n;Refuses(()=>Plan(m),"Nonfinite");}});
        Test("nonpositive scale and nonunit rotation are refused",()=>{var m=Fixture.Create();m.commands[1].vector.x=0;Refuses(()=>Plan(m),"scale");m=Fixture.Create();m.commands[0].kind="rotation";m.commands[0].rotation.w=0;Refuses(()=>Plan(m),"quaternion");});
        Test("intermediate invariant violation cannot hide behind final state",()=>{var m=Fixture.Create();m.commands[0].vector.x=50;Refuses(()=>Plan(m),"Displacement");});
        Test("command and object resource caps are enforced",()=>{var m=Fixture.Create();m.commands=Enumerable.Repeat(m.commands[0],ReplayPolicy.MaxCommands+1).ToList();Refuses(()=>Plan(m),"Command resource");m=Fixture.Create();m.baseline.objects=Enumerable.Repeat(m.baseline.objects[0],ReplayPolicy.MaxObjects+1).ToList();Refuses(()=>Plan(m),"Object resource");});
        Test("asset content importer and environment conflicts are refused",()=>{foreach(int kind in new[]{0,1,2}){var m=Fixture.Create();var context=JsonFiles.Clone(m.context);if(kind==0)context.assets[0].contentSha256=new string('e',64);if(kind==1)context.assets[0].importerType="ChangedImporter";if(kind==2)context.environmentSha256=new string('f',64);Refuses(()=>ReplayPolicy.Plan(m,m.baseline,context),"identity conflict");}});
        Test("asset paths cannot escape fixture project",()=>{var m=Fixture.Create();m.context.assets[0].path="Assets/../outside";Refuses(()=>Plan(m),"escapes");});
        Test("JSON roundtrip and decimal culture preserve identity",()=>{var m=Fixture.Create();var restored=JsonFiles.Clone(m);var culture=CultureInfo.CurrentCulture;try{CultureInfo.CurrentCulture=new CultureInfo("de-DE");Assert(Plan(restored).afterHash==Plan(m).afterHash,"Culture changed replay");}finally{CultureInfo.CurrentCulture=culture;}});
        Test("IEEE float hashing normalizes signed zero",()=>{var m=Fixture.Create();var other=m.baseline.Copy();other.objects[1].position.x=-0.0f;Assert(ReplayPolicy.StateHash(other)==m.baselineHash,"Signed zero produced false drift");});
        Test("malformed Unicode cannot collapse into replacement-byte identity",()=>{var m=Fixture.Create();m.baseline.objects[1].name="Bad\ud800";Refuses(()=>ReplayPolicy.StateHash(m.baseline),"Malformed Unicode");});
        Test("backend failure restores checkpoint without commit",()=>{var m=Fixture.Create();var backend=new MemoryBackend(m.baseline){failOn=2};Refuses(()=>ReplayPolicy.ApplyTransaction(backend,m,m.context),"Injected");Assert(ReplayPolicy.StateHash(backend.state)==m.baselineHash&&!backend.committed,"Failed transaction escaped");});
        Test("post-apply divergence is detected and rolled back",()=>{var m=Fixture.Create();var backend=new MemoryBackend(m.baseline){diverge=true};Refuses(()=>ReplayPolicy.ApplyTransaction(backend,m,m.context),"diverges");Assert(ReplayPolicy.StateHash(backend.state)==m.baselineHash,"Divergence rollback failed");});
        Test("failed rollback is explicit rather than reported as safe",()=>{var m=Fixture.Create();var backend=new MemoryBackend(m.baseline){failOn=2,breakRollback=true};try{ReplayPolicy.ApplyTransaction(backend,m,m.context);}catch(AggregateException e){Assert(e.InnerExceptions.Count==2,"Both failures must be preserved");return;}throw new Exception("Unverified rollback was hidden");});
        Test("actual fixture file hash rejects tampering",()=>{var output=Path.Combine(args[0],".runtime/test-fixture");var m=Fixture.Create(output);JsonFiles.VerifyAssetFiles(output,m.context);File.AppendAllText(Path.Combine(output,"Assets/Fixture.mesh"),"tampered");Refuses(()=>JsonFiles.VerifyAssetFiles(output,m.context),"SHA256 conflict");});
        var report=new {host="Real .NET C# policy/core execution, not Unity Editor",passed=Results.Count(r=>r.passed),total=Results.Count,results=Results};
        JsonFiles.Write(Path.Combine(args[0],"artifacts/core-tests.json"),report);Console.WriteLine("CORE_TESTS "+report.passed+"/"+report.total);return report.passed==report.total?0:1;
    }
}
