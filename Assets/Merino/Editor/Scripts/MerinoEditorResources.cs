// https://github.com/snozbot/fungus/blob/master/Assets/Fungus/Scripts/Editor/FungusEditorResources.cs

using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace Merino
{
    internal partial class MerinoEditorResources : ScriptableObject
    {
        [Serializable]
        internal class EditorTexture
        {
            [SerializeField] private Texture2D free;
            [SerializeField] private Texture2D pro;

            public Texture2D Texture2D
            {
                get { return EditorGUIUtility.isProSkin && pro != null ? pro : free; }
            }

            public EditorTexture(Texture2D free, Texture2D pro)
            {
                this.free = free;
                this.pro = pro;
            }
        }

        public static Texture Error { 
            get { return EditorGUIUtility.Load("icons/d_console.erroricon.sml.png") as Texture; }
        }

        public static Texture TextAsset {
            get { return EditorGUIUtility.IconContent("TextAsset Icon").image; }
        }

        private static MerinoEditorResources instance;
        private static readonly string editorResourcesFolderName = "\"EditorResources\"";
        private static readonly string PartialEditorResourcesPath = System.IO.Path.Combine("Merino", "EditorResources");
        [SerializeField] [HideInInspector] private bool updateOnReloadScripts = false;

        internal static MerinoEditorResources Instance
        {
            get
            {
                if (instance == null)
                {
                    var guids = AssetDatabase.FindAssets("MerinoEditorResources t:MerinoEditorResources");

                    if (guids.Length == 0)
                    {
                        instance = ScriptableObject.CreateInstance(typeof(MerinoEditorResources)) as MerinoEditorResources;
                        AssetDatabase.CreateAsset(instance, GetRootFolder() + "/MerinoEditorResources.asset");
                    }
                    else 
                    {
                        if (guids.Length > 1)
                        {
                            MerinoDebug.Log(LoggingLevel.Error, "Multiple MerinoEditorResources assets found!");
                        }

                        var path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        instance = AssetDatabase.LoadAssetAtPath(path, typeof(MerinoEditorResources)) as MerinoEditorResources;
                    }
                }

                return instance;
            }
        }

        private static string GetRootFolder()
        {
            var res = AssetDatabase.FindAssets(editorResourcesFolderName);

            foreach (var item in res)
            {
                var path = AssetDatabase.GUIDToAssetPath(item);
                var safePath = System.IO.Path.GetFullPath(path);
                if (safePath.IndexOf(PartialEditorResourcesPath) != -1)
                    return path;
            }

            return string.Empty;
        }

        internal static void GenerateResourcesScript()
        {
            // Get all unique filenames
            var textureNames = new HashSet<string>();
            var guids = AssetDatabase.FindAssets("t:Texture2D", new [] { GetRootFolder() });
            var paths = guids.Select(guid => AssetDatabase.GUIDToAssetPath(guid));
            
            foreach (var path in paths)
            {
                textureNames.Add(Path.GetFileNameWithoutExtension(path));
            }

            var scriptGuid = AssetDatabase.FindAssets("MerinoEditorResources t:MonoScript")[0];
            var relativePath = AssetDatabase.GUIDToAssetPath(scriptGuid).Replace("MerinoEditorResources.cs", "MerinoEditorResourcesGenerated.cs");
            var absolutePath = Application.dataPath + relativePath.Substring("Assets".Length);
            
            using (var writer = new StreamWriter(absolutePath))
            {
                writer.WriteLine("using UnityEngine;");
                writer.WriteLine("#pragma warning disable 649");
                writer.WriteLine("");
                writer.WriteLine("namespace Merino");
                writer.WriteLine("{");
                writer.WriteLine("    internal partial class MerinoEditorResources : ScriptableObject");
                writer.WriteLine("    {");
                
                foreach (var name in textureNames)
                {
                    writer.WriteLine("        [SerializeField] private EditorTexture " + name + ";");
                }

                writer.WriteLine("");

                foreach (var name in textureNames)
                {
                    var pascalCase = string.Join("", name.Split(new [] { '_' }, StringSplitOptions.RemoveEmptyEntries).Select(
                        s => s.Substring(0, 1).ToUpper() + s.Substring(1)).ToArray()
                    );
                    writer.WriteLine("        public static Texture2D " + pascalCase + " { get { return Instance." + name + ".Texture2D; } }");
                }

                writer.WriteLine("    }");
                writer.WriteLine("}");
            }

            Instance.updateOnReloadScripts = true;
            AssetDatabase.ImportAsset(relativePath);
        }

        [DidReloadScripts]
        private static void OnDidReloadScripts()
        {
            if (Instance.updateOnReloadScripts)
            {                
                UpdateTextureReferences(Instance);
            }
        }

        internal static void UpdateTextureReferences(MerinoEditorResources instance)
        {
            // Iterate through all fields in instance and set texture references
            var serializedObject = new SerializedObject(instance);
            var prop = serializedObject.GetIterator();
            var rootFolder = new [] { GetRootFolder() };

            prop.NextVisible(true);
            while (prop.NextVisible(false))
            {
                if (prop.propertyType == SerializedPropertyType.Generic)
                {
                    var guids = AssetDatabase.FindAssets(prop.name + " t:Texture2D", rootFolder);
                    var paths = guids.Select(guid => AssetDatabase.GUIDToAssetPath(guid)).Where(
                        path => path.Contains(prop.name + ".")
                    );

                    foreach (var path in paths)
                    {
                        var texture = AssetDatabase.LoadAssetAtPath(path, typeof(Texture2D)) as Texture2D;
                        if (path.ToLower().Contains("/pro/"))
                        {
                            prop.FindPropertyRelative("pro").objectReferenceValue = texture;
                        }
                        else
                        {
                            prop.FindPropertyRelative("free").objectReferenceValue = texture;
                            // if there isn't a pro version, then just use the free version
                            if ( prop.FindPropertyRelative("pro").objectReferenceValue == null ) {
                                prop.FindPropertyRelative("pro").objectReferenceValue = texture;
                            }
                        }
                    }       
                }
            }

            serializedObject.FindProperty("updateOnReloadScripts").boolValue = false;
            serializedObject.ApplyModifiedPropertiesWithoutUndo();
        }
    }
    
    [CustomEditor(typeof(MerinoEditorResources))]
    internal class MerinoEditorResourcesInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            if (serializedObject.FindProperty("updateOnReloadScripts").boolValue)
            {
                GUILayout.Label("Updating...");
            }
            else
            {
                if (GUILayout.Button("Sync with EditorResources folder"))
                {
                    MerinoEditorResources.GenerateResourcesScript();
                }

                DrawDefaultInspector();
            }
        }
    }

    // Handle reimporting all assets
    internal class EditorResourcesPostProcessor : AssetPostprocessor 
    {
        private static void OnPostprocessAllAssets(string[] importedAssets, string[] _, string[] __, string[] ___) 
        {
            foreach (var path in importedAssets)
            {
                if (path.EndsWith("MerinoEditorResources.asset"))
                {
                    var asset = AssetDatabase.LoadAssetAtPath(path, typeof(MerinoEditorResources)) as MerinoEditorResources;
                    if (asset != null)
                    {
                        MerinoEditorResources.UpdateTextureReferences(asset);
                        AssetDatabase.SaveAssets();
                        return;
                    }
                }
            }
        }
    }
}
