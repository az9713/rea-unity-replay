// SPDX-License-Identifier: MIT
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Rea.UnityReplay.Core;
using Object=UnityEngine.Object;

namespace Rea.UnityReplay
{
    public sealed class ReplayWindow : EditorWindow
    {
        enum CommandKind { Position,Scale,Rotation,Name,Active }
        [SerializeField] GameObject root,target;
        [SerializeField] string manifestJson="",status="Capture a saved, clean, top-level scene root.";
        [SerializeField] CommandKind kind;
        [SerializeField] Vector3 vector=Vector3.zero;
        [SerializeField] string nextName="VerifiedObject";
        [SerializeField] bool nextActive=true,showEvidence;
        [SerializeField] float maxDisplacement=10;
        Vector2 scroll;

        [MenuItem("Tools/REA/Guarded Replay Ledger")]
        static void Open() { GetWindow<ReplayWindow>("Guarded Replay"); }
        ReplayManifest Draft()
        {
            if(string.IsNullOrEmpty(manifestJson)) throw new InvalidOperationException("Capture or import a manifest first");
            return JsonUtility.FromJson<ReplayManifest>(manifestJson);
        }
        void Store(ReplayManifest manifest) { manifestJson=JsonUtility.ToJson(manifest,true); }
        void Safe(Action action)
        {
            try {action();}
            catch(AggregateException error) {status="FAILED; ROLLBACK UNVERIFIED: "+error;Debug.LogError(status);}
            catch(Exception error) {status="Refused/failed: "+error.Message;Debug.LogWarning(status);}
            Repaint();
        }
        void OnGUI()
        {
            EditorGUILayout.LabelField("Guarded Replay Ledger",EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Explicit commands only. No arbitrary reflection, object creation or global event recorder. Native Unity execution has not been verified by this package's build pipeline.",MessageType.Info);
            root=(GameObject)EditorGUILayout.ObjectField("Scene root",root,typeof(GameObject),true);
            maxDisplacement=EditorGUILayout.FloatField("Max local displacement",maxDisplacement);
            if(GUILayout.Button("Capture saved baseline + identities")) Safe(Capture);
            EditorGUILayout.Space();
            target=(GameObject)EditorGUILayout.ObjectField("Command target",target,typeof(GameObject),true);
            kind=(CommandKind)EditorGUILayout.EnumPopup("Allowed command",kind);
            if(kind==CommandKind.Name) nextName=EditorGUILayout.TextField("New name",nextName);
            else if(kind==CommandKind.Active) nextActive=EditorGUILayout.Toggle("Active self",nextActive);
            else vector=EditorGUILayout.Vector3Field(kind==CommandKind.Rotation?"Local Euler degrees":"Local value",vector);
            if(GUILayout.Button("Record and apply one guarded command")) Safe(Record);
            if(GUILayout.Button("Preview full replay and verify recorded checkpoint")) Safe(Preview);
            EditorGUILayout.Space();
            if(GUILayout.Button("Restore captured checkpoint (guarded, native Undo)")) Safe(Restore);
            if(GUILayout.Button("Apply full replay from baseline (native Undo)")) Safe(Apply);
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("Export manifest")) Safe(Export);
            if(GUILayout.Button("Import manifest")) Safe(Import);
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.HelpBox(status,MessageType.Info);
            showEvidence=EditorGUILayout.Foldout(showEvidence,"Complete manifest evidence");
            if(showEvidence) {scroll=EditorGUILayout.BeginScrollView(scroll);EditorGUILayout.TextArea(manifestJson);EditorGUILayout.EndScrollView();}
        }
        void Capture()
        {
            UnitySnapshot.RequireEditable(root);
            if(root.scene.isDirty) throw new InvalidOperationException("Save the scene before baseline capture; this tool never saves it automatically");
            var snapshot=UnitySnapshot.Read(root);ReplayPolicy.Validate(snapshot);
            var manifest=new ReplayManifest {baseline=snapshot,baselineHash=ReplayPolicy.StateHash(snapshot),context=UnitySnapshot.Context(root),invariants=new Invariants {maxDisplacement=maxDisplacement}};
            ReplayPolicy.Plan(manifest,snapshot,manifest.context);Store(manifest);
            status="Captured "+snapshot.objects.Count+" stable objects. Record through this window; unrecorded edits conflict. Do not save changed scene bytes before baseline replay.";
        }
        ReplayCommand Command(ObjectState before)
        {
            var command=new ReplayCommand {targetId=before.id,beforeObjectHash=ReplayPolicy.ObjectHash(before),kind=kind.ToString().ToLowerInvariant()};
            command.vector=UnitySnapshot.Vector(vector);command.name=nextName;command.active=nextActive;
            var rotation=Quaternion.Euler(vector);command.rotation=UnitySnapshot.Rotation(rotation);return command;
        }
        void Record()
        {
            var manifest=Draft();var context=UnitySnapshot.Context(root);var expected=ReplayPolicy.Plan(manifest,manifest.baseline,context);
            var actual=UnitySnapshot.Read(root);
            if(ReplayPolicy.StateHash(actual)!=expected.afterHash) throw new InvalidOperationException("Current scene differs from the last recorded checkpoint");
            if(target==null) throw new InvalidOperationException("Choose a target inside the captured root");
            var id=UnitySnapshot.Id(target);var before=actual.objects.SingleOrDefault(o=>o.id==id);
            if(before==null) throw new InvalidOperationException("Target is outside captured root");
            var command=Command(before);manifest.commands.Add(command);
            ReplayPolicy.Plan(manifest,manifest.baseline,context);
            var single=new ReplayManifest {baseline=actual,baselineHash=ReplayPolicy.StateHash(actual),context=manifest.context,invariants=manifest.invariants,commands=new List<ReplayCommand>{command}};
            ReplayPolicy.ApplyTransaction(new UnityBackend(root,manifest.context),single,context);Store(manifest);
            status="Recorded and applied command "+manifest.commands.Count+". Native Undo is available; preview detects a diverged checkpoint.";
        }
        void Preview()
        {
            var manifest=Draft();var plan=ReplayPolicy.Plan(manifest,manifest.baseline,UnitySnapshot.Context(root));
            var actual=UnitySnapshot.Read(root);var hash=ReplayPolicy.StateHash(actual);
            if(hash!=manifest.baselineHash&&hash!=plan.afterHash) throw new InvalidOperationException("Current hierarchy is neither baseline nor recorded result");
            status="Preview passes: "+plan.commands+" allowlisted commands; "+plan.result.objects.Count+" objects preserved. Current checkpoint: "+(hash==manifest.baselineHash?"baseline":"recorded result")+". Local-field scope only.";
        }
        void Apply()
        {
            var manifest=Draft();ReplayPolicy.ApplyTransaction(new UnityBackend(root,manifest.context),manifest,UnitySnapshot.Context(root));
            status="Replay committed as one native Undo group. Declared local fields match the preview; external effects are outside this guarantee.";
        }
        void Restore()
        {
            var original=Draft();var context=UnitySnapshot.Context(root);var expected=ReplayPolicy.Plan(original,original.baseline,context).result;
            var current=UnitySnapshot.Read(root);
            if(ReplayPolicy.StateHash(current)!=ReplayPolicy.StateHash(expected)) throw new InvalidOperationException("Restore refuses to overwrite an unrecorded edit");
            var reverse=new ReplayManifest {baseline=current,baselineHash=ReplayPolicy.StateHash(current),context=original.context,invariants=original.invariants};
            var working=current.Copy();
            foreach(var desired in original.baseline.objects)
            {
                var now=working.objects.Single(o=>o.id==desired.id);
                if(now.position.x!=desired.position.x||now.position.y!=desired.position.y||now.position.z!=desired.position.z) Add(reverse,working,new ReplayCommand {targetId=now.id,kind="position",vector=desired.position.Copy()});
                if(now.scale.x!=desired.scale.x||now.scale.y!=desired.scale.y||now.scale.z!=desired.scale.z) Add(reverse,working,new ReplayCommand {targetId=now.id,kind="scale",vector=desired.scale.Copy()});
                if(now.rotation.x!=desired.rotation.x||now.rotation.y!=desired.rotation.y||now.rotation.z!=desired.rotation.z||now.rotation.w!=desired.rotation.w) Add(reverse,working,new ReplayCommand {targetId=now.id,kind="rotation",rotation=desired.rotation.Copy()});
                if(now.name!=desired.name) Add(reverse,working,new ReplayCommand {targetId=now.id,kind="name",name=desired.name});
                if(now.active!=desired.active) Add(reverse,working,new ReplayCommand {targetId=now.id,kind="active",active=desired.active});
            }
            ReplayPolicy.ApplyTransaction(new UnityBackend(root,original.context),reverse,context);
            status="Captured fields restored through a guarded native Undo transaction. Full replay can now start from baseline.";
        }
        static void Add(ReplayManifest manifest,Snapshot working,ReplayCommand command)
        {
            command.beforeObjectHash=ReplayPolicy.ObjectHash(working.objects.Single(o=>o.id==command.targetId));
            manifest.commands.Add(command);ReplayPolicy.ApplyCommand(working,command);
        }
        void Export()
        {
            var manifest=Draft();ReplayPolicy.Plan(manifest,manifest.baseline,UnitySnapshot.Context(root));
            var path=EditorUtility.SaveFilePanel("Export bounded replay manifest",Application.dataPath,"guarded-replay","json");
            if(!string.IsNullOrEmpty(path)) {File.WriteAllText(path,JsonUtility.ToJson(manifest,true));status="Manifest exported. No scene or asset was saved.";}
        }
        void Import()
        {
            var path=EditorUtility.OpenFilePanel("Import bounded replay manifest",Application.dataPath,"json");
            if(string.IsNullOrEmpty(path)) return;
            if(new FileInfo(path).Length>4*1024*1024) throw new InvalidOperationException("Manifest exceeds 4MiB cap");
            var manifest=JsonUtility.FromJson<ReplayManifest>(File.ReadAllText(path));
            ReplayPolicy.Plan(manifest,manifest.baseline,UnitySnapshot.Context(root));Store(manifest);status="Manifest parsed and identities checked. Preview/current-baseline guard still required before mutation.";
        }
    }

    public static class UnitySnapshot
    {
        public static void RequireEditable(GameObject root)
        {
            if(root==null||!root.scene.IsValid()||!root.scene.isLoaded||string.IsNullOrEmpty(root.scene.path)) throw new InvalidOperationException("A saved, loaded scene root is required");
            if(root.transform.parent!=null) throw new InvalidOperationException("Select a top-level root; external parent effects are unsupported");
            if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling) throw new InvalidOperationException("Play mode/compilation is unsupported");
        }
        public static string Id(GameObject value)
        {
            var id=GlobalObjectId.GetGlobalObjectIdSlow(value);
            if(id.targetObjectId==0||id.assetGUID.ToString()==new string('0',32)) throw new InvalidOperationException("Object lacks a stable saved-scene GlobalObjectId");
            return id.ToString();
        }
        public static VectorValue Vector(Vector3 v) {return new VectorValue(v.x,v.y,v.z);}
        public static RotationValue Rotation(Quaternion q) {return new RotationValue {x=q.x,y=q.y,z=q.z,w=q.w};}
        public static Snapshot Read(GameObject root)
        {
            RequireEditable(root);var result=new Snapshot {rootIdentity=Id(root),sceneGuid=AssetDatabase.AssetPathToGUID(root.scene.path)};
            var transforms=root.GetComponentsInChildren<Transform>(true);
            if(transforms.Length>ReplayPolicy.MaxObjects) throw new InvalidOperationException("Object resource cap");
            foreach(var transform in transforms)
            {
                var gameObject=transform.gameObject;var unknown=new List<string>();
                foreach(var component in gameObject.GetComponents<Component>())
                    if(component==null) unknown.Add("MissingScript");
                    else if(component.GetType()!=typeof(Transform)&&component.GetType()!=typeof(MeshFilter)&&component.GetType()!=typeof(MeshRenderer)) unknown.Add(component.GetType().FullName);
                if(PrefabUtility.IsPartOfPrefabInstance(gameObject)) unknown.Add("PrefabInstanceOverridesUnsupported");
                result.objects.Add(new ObjectState {id=Id(gameObject),parentId=gameObject==root?"":Id(transform.parent.gameObject),name=gameObject.name,active=gameObject.activeSelf,position=Vector(transform.localPosition),scale=Vector(transform.localScale),rotation=Rotation(transform.localRotation),unsupportedComponents=unknown.ToArray()});
            }
            result.objects=result.objects.OrderBy(o=>o.id,StringComparer.Ordinal).ToList();ReplayPolicy.Validate(result);return result;
        }
        static string ProjectFile(string relative)
        {
            if(relative.Contains("..")||relative.Contains(":")||relative.Contains("\\")) throw new InvalidOperationException("Noncanonical project path");
            var root=Path.GetFullPath(Path.Combine(Application.dataPath,".."));var target=Path.GetFullPath(Path.Combine(root,relative));
            if(!target.StartsWith(root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Project path escape");
            var cursor=root;
            foreach(var segment in relative.Split('/')) {cursor=Path.Combine(cursor,segment);if(File.Exists(cursor)||Directory.Exists(cursor)) if((File.GetAttributes(cursor)&FileAttributes.ReparsePoint)!=0) throw new InvalidOperationException("Symlink/reparse dependencies are unsupported");}
            return target;
        }
        static string FileHash(string relative,ref long budget)
        {
            var target=ProjectFile(relative);var size=new FileInfo(target).Length;budget+=size;
            if(size>32*1024*1024||budget>128*1024*1024) throw new InvalidOperationException("Asset hashing resource cap");
            using(var sha=SHA256.Create()) using(var file=File.OpenRead(target)) return BitConverter.ToString(sha.ComputeHash(file)).Replace("-","").ToLowerInvariant();
        }
        public static ReplayContext Context(GameObject root)
        {
            RequireEditable(root);long budget=0;
            var environment=Application.unityVersion+"|"+EditorUserBuildSettings.activeBuildTarget+"|"+EditorSettings.serializationMode;
            foreach(var path in new[]{"Packages/manifest.json","Packages/packages-lock.json","ProjectSettings/ProjectVersion.txt"}) environment+="|"+path+":"+(File.Exists(ProjectFile(path))?FileHash(path,ref budget):"<missing>");
            var context=new ReplayContext {unityVersion=Application.unityVersion,environmentSha256=ReplayPolicy.Sha(environment)};
            var paths=AssetDatabase.GetDependencies(root.scene.path,true).Concat(new[]{root.scene.path}).Where(p=>p.StartsWith("Assets/",StringComparison.Ordinal)).Distinct().OrderBy(p=>p,StringComparer.Ordinal).ToArray();
            if(paths.Length>ReplayPolicy.MaxAssets) throw new InvalidOperationException("Asset resource cap");
            foreach(var path in paths)
            {
                var importer=AssetImporter.GetAtPath(path);
                context.assets.Add(new AssetIdentity {path=path,guid=AssetDatabase.AssetPathToGUID(path),contentSha256=FileHash(path,ref budget),metaSha256=FileHash(path+".meta",ref budget),importerType=importer==null?"MissingImporter":importer.GetType().FullName,dependencyHash=AssetDatabase.GetAssetDependencyHash(path).ToString()});
            }
            ReplayPolicy.ContextHash(context);return context;
        }
    }
    sealed class UnityBackend : IReplayBackend
    {
        readonly GameObject root;readonly string contextHash;Dictionary<string,GameObject> objects;int group;
        public UnityBackend(GameObject root,ReplayContext context) {this.root=root;contextHash=ReplayPolicy.ContextHash(context);}
        public Snapshot Read() {return UnitySnapshot.Read(root);}
        public void Begin()
        {
            if(ReplayPolicy.ContextHash(UnitySnapshot.Context(root))!=contextHash) throw new InvalidOperationException("Context changed between preview and transaction");
            objects=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>UnitySnapshot.Id(t.gameObject),t=>t.gameObject,StringComparer.Ordinal);
            Undo.IncrementCurrentGroup();group=Undo.GetCurrentGroup();Undo.SetCurrentGroupName("Guarded Replay Ledger");
            Undo.RegisterCompleteObjectUndo(objects.Values.SelectMany(g=>new Object[]{g,g.transform}).ToArray(),"Guarded Replay Ledger");
        }
        public void Apply(ReplayCommand command)
        {
            var observed=Read().objects.SingleOrDefault(o=>o.id==command.targetId);
            if(observed==null||ReplayPolicy.ObjectHash(observed)!=command.beforeObjectHash) throw new InvalidOperationException("Live object precondition changed during replay");
            var obj=objects[command.targetId];
            switch(command.kind)
            {
                case "position":obj.transform.localPosition=new Vector3(command.vector.x,command.vector.y,command.vector.z);break;
                case "scale":obj.transform.localScale=new Vector3(command.vector.x,command.vector.y,command.vector.z);break;
                case "rotation":obj.transform.localRotation=new Quaternion(command.rotation.x,command.rotation.y,command.rotation.z,command.rotation.w);break;
                case "name":obj.name=command.name;break;
                case "active":obj.SetActive(command.active);break;
                default:throw new InvalidOperationException("Unknown command");
            }
        }
        public void Commit()
        {
            if(ReplayPolicy.ContextHash(UnitySnapshot.Context(root))!=contextHash) throw new InvalidOperationException("Asset/importer/environment changed during replay");
            EditorSceneManager.MarkSceneDirty(root.scene);Undo.CollapseUndoOperations(group);
        }
        public void Rollback() {Undo.RevertAllDownToGroup(group);}
    }
}
#endif
