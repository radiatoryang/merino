using UnityEditor;

public static class MerinoMenuItems
{
#if MERINO_DEVELOPER
    [MenuItem("Merino/Utilities/Export Minimal Package", priority = 0)]
    static void ExportMinimalPackage()
    {
        string path = EditorUtility.SaveFilePanel("Export Package", "", "merino_0.0.0_minimal", "unitypackage");
        if (path.Length == 0) return;

        AssetDatabase.ExportPackage("Assets/Merino", path, ExportPackageOptions.Recurse);
    }
    
    [MenuItem("Merino/Utilities/Export Complete Package", priority = 1)]
    static void ExportCompletePackage()
    {
        string path = EditorUtility.SaveFilePanel("Export Package", "", "merino_0.0.0_complete", "unitypackage");
        if (path.Length == 0) return;

        string[] folders = new string[] {"Assets/Merino", "Assets/YarnSpinner"};
        AssetDatabase.ExportPackage(folders, path, ExportPackageOptions.Recurse);
    }
#endif
}
