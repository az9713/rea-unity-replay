using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using Rea.UnityReplay.Core;

namespace Rea.UnityReplay.Tooling
{
    public static class JsonFiles
    {
        public static readonly JsonSerializerOptions Options=new JsonSerializerOptions {IncludeFields=true,WriteIndented=true,MaxDepth=64};
        public static T Read<T>(string path)
        {
            if(new FileInfo(path).Length>4*1024*1024) throw new InvalidOperationException("JSON exceeds 4MiB cap");
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path),Options);
        }
        public static void Write<T>(string path,T value) { File.WriteAllText(path,JsonSerializer.Serialize(value,Options)+"\n"); }
        public static T Clone<T>(T value) { return JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value,Options),Options); }
        public static string FileHash(string path)
        {
            using(var sha=SHA256.Create()) using(var source=File.OpenRead(path)) return BitConverter.ToString(sha.ComputeHash(source)).Replace("-","").ToLowerInvariant();
        }
        public static void VerifyAssetFiles(string root,ReplayContext context)
        {
            ReplayPolicy.ContextHash(context); root=Path.GetFullPath(root); long total=0;
            foreach(var asset in context.assets)
            {
                for(int fileIndex=0;fileIndex<2;fileIndex++)
                {
                    var relative=asset.path+(fileIndex==1?".meta":"");
                    var target=Path.GetFullPath(Path.Combine(root,relative));
                    if(!target.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Asset escapes fixture root");
                    var cursor=root;
                    foreach(var segment in relative.Split('/'))
                    {
                        cursor=Path.Combine(cursor,segment);
                        if((File.GetAttributes(cursor)&FileAttributes.ReparsePoint)!=0) throw new InvalidOperationException("Symlink/reparse assets are unsupported");
                    }
                    long size=new FileInfo(target).Length; total+=size;
                    if(size>32*1024*1024||total>128*1024*1024) throw new InvalidOperationException("Asset hashing resource cap");
                    var expected=fileIndex==1?asset.metaSha256:asset.contentSha256;
                    if(FileHash(target)!=expected) throw new InvalidOperationException("Asset file SHA256 conflict: "+relative);
                }
            }
        }
    }
    public sealed class MemoryBackend : IReplayBackend
    {
        public Snapshot state;
        Snapshot checkpoint;
        public int applied,failOn=-1;
        public bool diverge,breakRollback,committed;
        public MemoryBackend(Snapshot value) { state=value.Copy(); }
        public Snapshot Read()
        {
            var result=state.Copy();
            if(diverge&&applied>0) result.objects[1].name="UnexpectedBackendMutation";
            return result;
        }
        public void Begin() { checkpoint=state.Copy(); applied=0; }
        public void Apply(ReplayCommand command)
        {
            ReplayPolicy.ApplyCommand(state,command); applied++;
            if(applied==failOn) throw new InvalidOperationException("Injected backend failure");
        }
        public void Commit() { committed=true; }
        public void Rollback() { if(!breakRollback) {state=checkpoint.Copy();applied=0;} }
    }
    public static class Fixture
    {
        public static ReplayManifest Create(string output=null)
        {
            var baseline=new Snapshot {rootIdentity="synthetic-root-id",sceneGuid=new string('a',32),objects=new List<ObjectState> {
                new ObjectState {id="synthetic-root-id",name="ScientificRoot"},
                new ObjectState {id="synthetic-marker-id",parentId="synthetic-root-id",name="Marker"}}};
            var asset=new AssetIdentity {path="Assets/Fixture.mesh",guid=new string('b',32),contentSha256=ReplayPolicy.Sha("synthetic mesh bytes\n"),metaSha256=ReplayPolicy.Sha("synthetic importer metadata\n"),importerType="SyntheticImporter_NotUnityEvidence",dependencyHash=new string('c',32)};
            if(output!=null)
            {
                Directory.CreateDirectory(Path.Combine(output,"Assets"));
                File.WriteAllText(Path.Combine(output,asset.path),"synthetic mesh bytes\n");
                File.WriteAllText(Path.Combine(output,asset.path+".meta"),"synthetic importer metadata\n");
                asset.contentSha256=JsonFiles.FileHash(Path.Combine(output,asset.path));
                asset.metaSha256=JsonFiles.FileHash(Path.Combine(output,asset.path+".meta"));
            }
            var manifest=new ReplayManifest {baseline=baseline,baselineHash=ReplayPolicy.StateHash(baseline),context=new ReplayContext {unityVersion="SYNTHETIC-DOTNET-NOT-UNITY",environmentSha256=ReplayPolicy.Sha("original deterministic core fixture"),assets=new List<AssetIdentity>{asset}},invariants=new Invariants {maxDisplacement=5,minScale=.1f,maxScale=10}};
            var working=baseline.Copy();
            foreach(var command in new[]{new ReplayCommand {kind="position",vector=new VectorValue(2,0,0)},new ReplayCommand {kind="scale",vector=new VectorValue(1.5f,1.5f,1.5f)},new ReplayCommand {kind="name",name="VerifiedMarker"},new ReplayCommand {kind="active",active=false}})
            {
                command.targetId="synthetic-marker-id";command.beforeObjectHash=ReplayPolicy.ObjectHash(working.objects[1]);
                manifest.commands.Add(command);ReplayPolicy.ApplyCommand(working,command);
            }
            if(output!=null)
            {
                JsonFiles.Write(Path.Combine(output,"manifest.json"),manifest);
                JsonFiles.Write(Path.Combine(output,"baseline.json"),baseline);
                JsonFiles.Write(Path.Combine(output,"context.json"),manifest.context);
            }
            return manifest;
        }
    }
}
