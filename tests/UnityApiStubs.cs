// Original MIT test scaffolding. COMPILE-ONLY SHIMS, NOT UNITY IMPLEMENTATIONS.
// This checks C# syntax/type flow against a small handwritten signature surface.
// It cannot establish native Unity API compatibility, Editor behavior or Undo.
using System;
namespace UnityEngine
{
    public class Object {public string name="";}
    public class Component:Object {public GameObject gameObject;}
    public class Transform:Component {public Transform parent;public Vector3 localPosition,localScale;public Quaternion localRotation;}
    public class MeshFilter:Component {}
    public class MeshRenderer:Component {}
    public class GameObject:Object
    {
        public Transform transform;public bool activeSelf;public SceneManagement.Scene scene;
        public T[] GetComponentsInChildren<T>(bool inactive) {return new T[0];}
        public T[] GetComponents<T>() {return new T[0];}
        public void SetActive(bool value) {}
    }
    public struct Vector3 {public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static Vector3 zero {get{return new Vector3();}}}
    public struct Vector2 {}
    public struct Quaternion {public float x,y,z,w;public Quaternion(float x,float y,float z,float w){this.x=x;this.y=y;this.z=z;this.w=w;}public static Quaternion Euler(Vector3 v){return new Quaternion();}}
    public sealed class SerializeField:Attribute {}
    public static class JsonUtility {public static T FromJson<T>(string json){return default(T);}public static string ToJson(object value,bool pretty){return "";}}
    public static class Application {public static string dataPath="",unityVersion="";}
    public static class Debug {public static void LogError(object value){}public static void LogWarning(object value){}}
    public static class GUILayout {public static bool Button(string label){return false;}}
    public class GUIStyle {}
    public struct Hash128 {public override string ToString(){return "";}}
}
namespace UnityEngine.SceneManagement {public struct Scene {public bool isDirty,isLoaded;public string path;public bool IsValid(){return true;}}}
namespace UnityEditor
{
    using UnityEngine;
    public class EditorWindow:Object {public static T GetWindow<T>(string title) where T:new(){return new T();}public void Repaint(){}}
    public sealed class MenuItem:Attribute {public MenuItem(string path){}}
    public enum MessageType {Info}
    public static class EditorStyles {public static GUIStyle boldLabel=new GUIStyle();}
    public static class EditorGUILayout
    {
        public static void LabelField(string label,GUIStyle style){}public static void HelpBox(string text,MessageType type){}
        public static Object ObjectField(string label,Object value,Type type,bool scene){return value;}
        public static float FloatField(string label,float value){return value;}public static Enum EnumPopup(string label,Enum value){return value;}
        public static string TextField(string label,string value){return value;}public static bool Toggle(string label,bool value){return value;}
        public static Vector3 Vector3Field(string label,Vector3 value){return value;}public static void Space(){}
        public static void BeginHorizontal(){}public static void EndHorizontal(){}public static bool Foldout(bool value,string label){return value;}
        public static Vector2 BeginScrollView(Vector2 position){return position;}public static void EndScrollView(){}public static string TextArea(string value){return value;}
    }
    public struct GUID {public override string ToString(){return "";}}
    public struct GlobalObjectId {public ulong targetObjectId;public GUID assetGUID;public static GlobalObjectId GetGlobalObjectIdSlow(Object value){return new GlobalObjectId();}public override string ToString(){return "";}}
    public static class EditorApplication {public static bool isPlayingOrWillChangePlaymode,isCompiling;}
    public static class PrefabUtility {public static bool IsPartOfPrefabInstance(Object value){return false;}}
    public static class AssetDatabase
    {
        public static string AssetPathToGUID(string path){return "";}public static string[] GetDependencies(string path,bool recursive){return new string[0];}
        public static Hash128 GetAssetDependencyHash(string path){return new Hash128();}
    }
    public class AssetImporter:Object {public static AssetImporter GetAtPath(string path){return null;}}
    public static class EditorUserBuildSettings {public static object activeBuildTarget;}
    public static class EditorSettings {public static object serializationMode;}
    public static class EditorUtility
    {
        public static string SaveFilePanel(string title,string directory,string name,string extension){return "";}
        public static string OpenFilePanel(string title,string directory,string extension){return "";}
    }
    public static class Undo
    {
        public static void IncrementCurrentGroup(){}public static int GetCurrentGroup(){return 0;}public static void SetCurrentGroupName(string name){}
        public static void RegisterCompleteObjectUndo(Object[] objects,string name){}public static void CollapseUndoOperations(int group){}public static void RevertAllDownToGroup(int group){}
    }
}
namespace UnityEditor.SceneManagement {public static class EditorSceneManager {public static bool MarkSceneDirty(UnityEngine.SceneManagement.Scene scene){return true;}}}
