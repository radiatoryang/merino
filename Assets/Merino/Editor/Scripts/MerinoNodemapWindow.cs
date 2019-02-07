using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Merino
{
    public class MerinoNodemapWindow : MerinoEditorWindow
    {
        public const string windowTitle = "Merino (Nodemap)";
        private const string popupControl = "currentFilePopup";

        [SerializeField] private TextAsset currentFile; // the current file we are displaying a nodemap for. we only display one of the loaded files at a time.
        [NonSerialized] private bool shouldRepaint;
        [NonSerialized] private bool forceUpdateCurrentFile;
        [NonSerialized] private string previousFileName;

        [MenuItem("Window/Merino/Nodemap", priority = 1)]
        static void MenuItem_GetWindow()
        {
            GetWindow<MerinoNodemapWindow>(windowTitle, true);
        }

        private void OnEnable()
        {
            MerinoYarnEditorWindow.OnFileLoaded += FileLoadedHandler;
            MerinoYarnEditorWindow.OnFileUnloaded += FileUnloadedHandler;
        }

        private void OnDisable()
        {
            MerinoYarnEditorWindow.OnFileLoaded -= FileLoadedHandler;
            MerinoYarnEditorWindow.OnFileUnloaded -= FileUnloadedHandler;
        }

        void OnGUI()
        {
            DrawToolbar(Event.current);
            DebugCurrentFileNodes();

            if (shouldRepaint)
            {
                Repaint();
                shouldRepaint = false;
            }
        }

        private void DebugCurrentFileNodes()
        {
            if (currentFile == null) return;

            int nodeID;
            if (MerinoTreeData.Instance.fileToNodeID.TryGetValue(currentFile, out nodeID))
            {
                
            }
        }

        private void DrawToolbar(Event e)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Space(2); //small space to mimic unity editor

                if (MerinoTreeData.Instance.currentFiles.Count > 0)
                {
                    var fileOptions = GetCurrentFileNames();
                    int currentCurrentFile = 0;
                    if (currentFile != null)
                    {
                        currentCurrentFile = fileOptions.IndexOf(currentFile.name);
                    }
                    else
                    {
                        if (!string.IsNullOrEmpty(previousFileName)) // was the previous file not deleted
                        {
                            if (fileOptions.Contains(previousFileName)) //is the file still loaded
                            {
                                currentCurrentFile = fileOptions.IndexOf(previousFileName);
                            }
                            else
                            {
                                currentCurrentFile = fileOptions.IndexOf(fileOptions[0]); // reset to 0 index
                            }
                        }
                        else
                        {
                            currentCurrentFile = fileOptions.IndexOf(fileOptions[0]); //reset to 0 index
                        }
                    }
                
                    GUI.SetNextControlName(popupControl);
                    int newCurrentFile = EditorGUILayout.Popup( 
                        currentCurrentFile, 
                        fileOptions.ToArray(), EditorStyles.toolbarDropDown);
                
                    if (currentCurrentFile != newCurrentFile || forceUpdateCurrentFile)
                    {
                        // change current file to new file
                        var newFile = MerinoTreeData.Instance.currentFiles.Find(x => x.name == fileOptions[newCurrentFile]);
                        
                        currentFile = newFile;
                        shouldRepaint = true;
                        forceUpdateCurrentFile = false;
                    }
                }
                else
                {
                    //todo: implement into logic above, or just keep as a dummy popup i guess.
                    var fileOptions = new string[] {"No Files Loaded"};
                    GUI.SetNextControlName(popupControl);
                    EditorGUILayout.Popup(0, fileOptions);
                }
                
                GUILayout.FlexibleSpace();
            }
            EditorGUILayout.EndHorizontal();
            
            if (e.type == EventType.MouseDown)
            {
                // todo: eat clicks on toolbar

                // clicked out of current file popup, eat click
                if (GUI.GetNameOfFocusedControl() == popupControl)
                {
                    e.Use();
                    GUI.FocusControl(null);
                }
            }
        }

        public override void Refresh()
        {
            Repaint();
        }

        #region Prototyping Methods

        public List<string> GetCurrentFileNames()
        {
            var list = new List<string>();

            foreach (var file in MerinoTreeData.Instance.currentFiles)
                list.Add(file.name);

            return list;
        }

        #endregion

        #region EventHandler Methods

        private void FileLoadedHandler()
        {
            shouldRepaint = true;
        }

        private void FileUnloadedHandler()
        {
            if (currentFile != null) // the file was just unloaded, but not deleted
                previousFileName = currentFile.name; //we will attempt to recover the file
            else
                previousFileName = null;
            
            currentFile = null;
            forceUpdateCurrentFile = true;
        }

        #endregion
    }
}
