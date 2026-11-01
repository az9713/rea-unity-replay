// SPDX-License-Identifier: MIT
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace Rea.UnityReplay.Core
{
    [Serializable] public sealed class VectorValue
    {
        public float x, y, z;
        public VectorValue() { }
        public VectorValue(float x, float y, float z) { this.x=x; this.y=y; this.z=z; }
        public VectorValue Copy() { return new VectorValue(x,y,z); }
    }
    [Serializable] public sealed class RotationValue
    {
        public float x, y, z, w=1;
        public RotationValue Copy() { return new RotationValue {x=x,y=y,z=z,w=w}; }
    }
    [Serializable] public sealed class ObjectState
    {
        public string id="", parentId="", name="";
        public bool active=true;
        public VectorValue position=new VectorValue(), scale=new VectorValue(1,1,1);
        public RotationValue rotation=new RotationValue();
        public string[] unsupportedComponents=new string[0];
        public ObjectState Copy() { return new ObjectState {id=id,parentId=parentId,name=name,active=active,position=position.Copy(),scale=scale.Copy(),rotation=rotation.Copy(),unsupportedComponents=(string[])unsupportedComponents.Clone()}; }
    }
    [Serializable] public sealed class Snapshot
    {
        public string rootIdentity="", sceneGuid="";
        public List<ObjectState> objects=new List<ObjectState>();
        public Snapshot Copy() { return new Snapshot {rootIdentity=rootIdentity,sceneGuid=sceneGuid,objects=objects.Select(o=>o.Copy()).ToList()}; }
    }
    [Serializable] public sealed class AssetIdentity
    {
        public string path="", guid="", contentSha256="", metaSha256="", importerType="", dependencyHash="";
    }
    [Serializable] public sealed class ReplayContext
    {
        public string unityVersion="", environmentSha256="";
        public List<AssetIdentity> assets=new List<AssetIdentity>();
    }
    [Serializable] public sealed class ReplayCommand
    {
        public string targetId="", kind="", beforeObjectHash="", name="";
        public bool active;
        public VectorValue vector=new VectorValue();
        public RotationValue rotation=new RotationValue();
    }
    [Serializable] public sealed class Invariants
    {
        public float maxDisplacement=10, minScale=.001f, maxScale=100, tolerance=.00001f;
    }
    [Serializable] public sealed class ReplayManifest
    {
        public int schema=1;
        public Snapshot baseline=new Snapshot();
        public string baselineHash="";
        public ReplayContext context=new ReplayContext();
        public Invariants invariants=new Invariants();
        public List<ReplayCommand> commands=new List<ReplayCommand>();
    }
    [Serializable] public sealed class ReplayPlan
    {
        public Snapshot result;
        public string beforeHash="", afterHash="";
        public int commands;
        public string scope="Declared local transforms, name and activeSelf only; not arbitrary Editor/runtime event equivalence.";
    }
    public interface IReplayBackend
    {
        Snapshot Read();
        void Begin();
        void Apply(ReplayCommand command);
        void Commit();
        void Rollback();
    }

    public static class ReplayPolicy
    {
        public const int MaxObjects=256, MaxCommands=512, MaxAssets=512;
        static readonly CultureInfo Culture=CultureInfo.InvariantCulture;
        public static string Sha(string text)
        {
            byte[] bytes;
            try {bytes=new UTF8Encoding(false,true).GetBytes(text);}
            catch(EncoderFallbackException) {throw new InvalidOperationException("Malformed Unicode identity");}
            using(var sha=SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();
        }
        static void Require(bool condition,string reason) { if(!condition) throw new InvalidOperationException(reason); }
        static bool Finite(float value) { return !float.IsNaN(value)&&!float.IsInfinity(value); }
        static bool Hex(string value,int length) { return value!=null&&value.Length==length&&value.All(c=>(c>='0'&&c<='9')||(c>='a'&&c<='f')); }
        static void Text(string value,int max,string label) { Require(value!=null&&value.Length>0&&value.Length<=max&&!value.Any(char.IsControl),"Invalid "+label); }
        static string FloatKey(float value) {return value==0?"00000000":BitConverter.ToInt32(BitConverter.GetBytes(value),0).ToString("X8",Culture);}
        static string V(VectorValue v) { return FloatKey(v.x)+","+FloatKey(v.y)+","+FloatKey(v.z); }
        static void Vec(VectorValue value) { Require(value!=null&&Finite(value.x)&&Finite(value.y)&&Finite(value.z),"Nonfinite vector"); }
        static void Rotation(RotationValue value)
        {
            Require(value!=null&&Finite(value.x)&&Finite(value.y)&&Finite(value.z)&&Finite(value.w),"Nonfinite rotation");
            double norm=(double)value.x*value.x+(double)value.y*value.y+(double)value.z*value.z+(double)value.w*value.w;
            Require(Math.Abs(norm-1)<.0001,"Rotation must be a unit quaternion");
        }
        static void Name(string value) { Text(value,128,"name"); Require(value.IndexOfAny(new[]{'/','\\'})<0,"Path separators in name are unsupported"); }
        public static void Validate(Snapshot snapshot)
        {
            Require(snapshot!=null&&snapshot.objects!=null,"Missing snapshot"); Text(snapshot.rootIdentity,512,"root identity"); Text(snapshot.sceneGuid,128,"scene identity");
            Require(snapshot.objects.Count>0&&snapshot.objects.Count<=MaxObjects,"Object resource cap");
            var map=new Dictionary<string,ObjectState>(StringComparer.Ordinal);
            foreach(var item in snapshot.objects)
            {
                Require(item!=null,"Null object"); Text(item.id,512,"object identity"); Name(item.name);
                Require(!map.ContainsKey(item.id),"Ambiguous duplicate object identity"); map.Add(item.id,item);
                Require(item.parentId!=null&&item.parentId.Length<=512,"Invalid parent identity");
                Vec(item.position); Vec(item.scale); Rotation(item.rotation);
                Require(item.unsupportedComponents!=null&&item.unsupportedComponents.Length==0,"Unreviewed/nondeterministic component present");
            }
            Require(map.ContainsKey(snapshot.rootIdentity),"Root is absent"); Require(map[snapshot.rootIdentity].parentId=="","Root must be top-level");
            foreach(var item in snapshot.objects)
            {
                var seen=new HashSet<string>(StringComparer.Ordinal); var cursor=item;
                while(cursor.id!=snapshot.rootIdentity)
                {
                    Require(seen.Add(cursor.id),"Hierarchy cycle");
                    Require(map.ContainsKey(cursor.parentId),"Object escapes captured root"); cursor=map[cursor.parentId];
                }
            }
        }
        public static string ObjectHash(ObjectState item)
        {
            var text=new StringBuilder(); Action<string> add=s=>text.Append(s.Length.ToString(Culture)).Append(':').Append(s);
            add(item.id); add(item.parentId); add(item.name); add(item.active?"1":"0"); add(V(item.position)); add(V(item.scale));
            add(FloatKey(item.rotation.x)); add(FloatKey(item.rotation.y)); add(FloatKey(item.rotation.z)); add(FloatKey(item.rotation.w));
            foreach(var component in item.unsupportedComponents.OrderBy(c=>c,StringComparer.Ordinal)) add(component);
            return Sha(text.ToString());
        }
        public static string StateHash(Snapshot snapshot)
        {
            Validate(snapshot);
            return Sha(snapshot.rootIdentity.Length.ToString(Culture)+":"+snapshot.rootIdentity+snapshot.sceneGuid.Length.ToString(Culture)+":"+snapshot.sceneGuid+string.Join("",snapshot.objects.OrderBy(o=>o.id,StringComparer.Ordinal).Select(ObjectHash)));
        }
        public static string ContextHash(ReplayContext context)
        {
            Require(context!=null&&context.assets!=null,"Missing context"); Text(context.unityVersion,128,"Unity version"); Require(Hex(context.environmentSha256,64),"Invalid environment SHA256");
            Require(context.assets.Count<=MaxAssets,"Asset resource cap"); var paths=new HashSet<string>(StringComparer.Ordinal);
            Require(context.assets.All(a=>a!=null),"Null asset identity");
            var text=new StringBuilder(context.unityVersion.Length.ToString(Culture)+":"+context.unityVersion+context.environmentSha256);
            foreach(var asset in context.assets.OrderBy(a=>a.path,StringComparer.Ordinal))
            {
                Require(asset!=null,"Null asset"); Text(asset.path,512,"asset path");
                Require((asset.path.StartsWith("Assets/",StringComparison.Ordinal)||asset.path.StartsWith("Packages/",StringComparison.Ordinal))&&!asset.path.Split('/').Contains("..")&&!asset.path.Contains("\\")&&!asset.path.Contains(":"),"Asset path escapes project");
                Require(paths.Add(asset.path),"Duplicate asset identity"); Require(Hex(asset.guid,32)&&Hex(asset.contentSha256,64)&&Hex(asset.metaSha256,64)&&Hex(asset.dependencyHash,32),"Incomplete asset/importer identity"); Text(asset.importerType,256,"importer type");
                foreach(var field in new[]{asset.path,asset.guid,asset.contentSha256,asset.metaSha256,asset.importerType,asset.dependencyHash}) text.Append(field.Length.ToString(Culture)).Append(':').Append(field);
            }
            return Sha(text.ToString());
        }
        public static void ApplyCommand(Snapshot snapshot,ReplayCommand command)
        {
            Require(command!=null,"Null command");
            var item=snapshot.objects.SingleOrDefault(o=>o.id==command.targetId);
            Require(item!=null,"Command object is outside captured root"); Require(ObjectHash(item)==command.beforeObjectHash,"Stale per-object command precondition");
            switch(command.kind)
            {
                case "position": Vec(command.vector); item.position=command.vector.Copy(); break;
                case "scale": Vec(command.vector); item.scale=command.vector.Copy(); break;
                case "rotation": Rotation(command.rotation); item.rotation=command.rotation.Copy(); break;
                case "name": Name(command.name); item.name=command.name; break;
                case "active": item.active=command.active; break;
                default: throw new InvalidOperationException("Unknown command; arbitrary reflection/code is forbidden");
            }
        }
        static void CheckInvariants(Snapshot baseline,Snapshot result,Invariants limits)
        {
            Require(limits!=null&&Finite(limits.maxDisplacement)&&limits.maxDisplacement>=0&&limits.maxDisplacement<=10000&&Finite(limits.minScale)&&limits.minScale>0&&Finite(limits.maxScale)&&limits.maxScale>=limits.minScale&&limits.maxScale<=10000&&Finite(limits.tolerance)&&limits.tolerance>0&&limits.tolerance<.01f,"Invalid invariant bounds");
            var old=baseline.objects.ToDictionary(o=>o.id,StringComparer.Ordinal);
            foreach(var item in result.objects)
            {
                var start=old[item.id];
                double dx=(double)item.position.x-start.position.x,dy=(double)item.position.y-start.position.y,dz=(double)item.position.z-start.position.z;
                Require(Math.Sqrt(dx*dx+dy*dy+dz*dz)<=limits.maxDisplacement+limits.tolerance,"Displacement invariant violated");
                Require(new[]{item.scale.x,item.scale.y,item.scale.z}.All(s=>s>=limits.minScale&&s<=limits.maxScale),"Positive bounded-scale invariant violated");
            }
        }
        public static ReplayPlan Plan(ReplayManifest manifest,Snapshot actual,ReplayContext actualContext)
        {
            Require(manifest!=null&&manifest.schema==1,"Unsupported schema"); Validate(manifest.baseline); Validate(actual);
            Require(manifest.commands!=null&&manifest.commands.Count<=MaxCommands,"Command resource cap");
            Require(StateHash(manifest.baseline)==manifest.baselineHash,"Baseline manifest hash mismatch");
            Require(StateHash(actual)==manifest.baselineHash,"Wrong root/baseline or unrecorded state change");
            Require(ContextHash(manifest.context)==ContextHash(actualContext),"Asset/importer/environment identity conflict");
            var result=actual.Copy(); CheckInvariants(manifest.baseline,result,manifest.invariants);
            foreach(var command in manifest.commands) { ApplyCommand(result,command); CheckInvariants(manifest.baseline,result,manifest.invariants); }
            return new ReplayPlan {result=result,beforeHash=manifest.baselineHash,afterHash=StateHash(result),commands=manifest.commands.Count};
        }
        public static bool Equivalent(Snapshot expected,Snapshot actual,float tolerance)
        {
            Validate(expected); Validate(actual); Require(Finite(tolerance)&&tolerance>0&&tolerance<.01,"Invalid tolerance");
            if(expected.rootIdentity!=actual.rootIdentity||expected.sceneGuid!=actual.sceneGuid||expected.objects.Count!=actual.objects.Count) return false;
            var map=actual.objects.ToDictionary(o=>o.id,StringComparer.Ordinal);
            foreach(var e in expected.objects)
            {
                ObjectState a; if(!map.TryGetValue(e.id,out a)||e.parentId!=a.parentId||e.name!=a.name||e.active!=a.active) return false;
                var left=new[]{e.position.x,e.position.y,e.position.z,e.scale.x,e.scale.y,e.scale.z,e.rotation.x,e.rotation.y,e.rotation.z,e.rotation.w};
                var right=new[]{a.position.x,a.position.y,a.position.z,a.scale.x,a.scale.y,a.scale.z,a.rotation.x,a.rotation.y,a.rotation.z,a.rotation.w};
                if(left.Where((v,i)=>Math.Abs(v-right[i])>tolerance).Any()) return false;
            }
            return true;
        }
        public static Snapshot ApplyTransaction(IReplayBackend backend,ReplayManifest manifest,ReplayContext context)
        {
            var initial=backend.Read(); var plan=Plan(manifest,initial,context); bool begun=false;
            try
            {
                backend.Begin(); begun=true;
                foreach(var command in manifest.commands) backend.Apply(command);
                var actual=backend.Read(); Require(Equivalent(plan.result,actual,manifest.invariants.tolerance),"Backend result diverges from preview");
                backend.Commit(); return actual;
            }
            catch(Exception primary)
            {
                if(begun)
                {
                    try { backend.Rollback(); Require(Equivalent(initial,backend.Read(),manifest.invariants.tolerance),"Rollback did not restore captured fields"); }
                    catch(Exception rollback) { throw new AggregateException("Replay failed AND rollback could not be verified",primary,rollback); }
                }
                throw;
            }
        }
    }
}
