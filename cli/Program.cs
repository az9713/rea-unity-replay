using System;
using System.IO;
using Rea.UnityReplay.Core;
using Rea.UnityReplay.Tooling;

static class Program
{
    static int Main(string[] args)
    {
        try
        {
            if(args.Length==2&&args[0]=="demo")
            {
                var output=Path.GetFullPath(args[1]);Directory.CreateDirectory(output);
                var manifest=Fixture.Create(output);JsonFiles.VerifyAssetFiles(output,manifest.context);
                var backend=new MemoryBackend(manifest.baseline);
                var result=ReplayPolicy.ApplyTransaction(backend,manifest,manifest.context);
                JsonFiles.Write(Path.Combine(output,"result.json"),result);
                JsonFiles.Write(Path.Combine(output,"demo-report.json"),new {host="Real standalone .NET core; NOT Unity Editor",commands=manifest.commands.Count,baselineHash=manifest.baselineHash,resultHash=ReplayPolicy.StateHash(result),committed=backend.committed,scope="Original synthetic IDs/assets; no native Unity importer/Undo/runtime proof"});
                Console.WriteLine("PASS standalone core demo: "+manifest.commands.Count+" guarded commands committed");return 0;
            }
            if(args.Length==6&&args[0]=="verify")
            {
                var manifest=JsonFiles.Read<ReplayManifest>(args[1]);var baseline=JsonFiles.Read<Snapshot>(args[2]);var context=JsonFiles.Read<ReplayContext>(args[3]);
                JsonFiles.VerifyAssetFiles(args[4],context);
                var plan=ReplayPolicy.Plan(manifest,baseline,context);
                JsonFiles.Write(args[5],plan);Console.WriteLine("PASS standalone policy replay; native Unity execution remains unverified");return 0;
            }
            Console.WriteLine("Usage: UnityReplay.Cli demo OUTPUT_DIRECTORY\n       UnityReplay.Cli verify MANIFEST BASELINE CONTEXT ASSET_ROOT REPORT");return 2;
        }
        catch(Exception error) { Console.Error.WriteLine("REFUSED: "+error.Message);return 1; }
    }
}
