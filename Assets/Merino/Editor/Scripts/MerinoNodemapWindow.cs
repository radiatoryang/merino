using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Merino
{
    // RY, 2 March 2019 - just throwing this in here to prototype a speedier nodemap data cache thing, without messing with ScriptableObject stuff
    public class MerinoNodemapData {
        [SerializeField] internal IList<MerinoTreeElement> TreeElements = new List<MerinoTreeElement>();
    }


    public class MerinoNodemapWindow : MerinoEditorWindow
    {
        public const string windowTitle = "Merino (Nodemap)";
        private const string popupControl = "currentFilePopup";

        [SerializeField] private TextAsset currentFile; // the current file we are displaying a nodemap for. we only display one of the loaded files at a time.
        [NonSerialized] private bool shouldRepaint; //use for "delayed" repaints, call Repaint directly for instant refresh
        [NonSerialized] private bool forceUpdateCurrentFile;
        [NonSerialized] private string previousFileName;

        [SerializeField] Vector2 scrollPos;
        [SerializeField] float zoom = maxZoom;
        const float minZoom = 0.2f;
        const float maxZoom = 1f;

        [NonSerialized] MerinoTreeElement dragNode;
        List<MerinoTreeElement> selectedNodes = new List<MerinoTreeElement>();
        MerinoTreeElement SelectedNode
        {
            get
            {
                return selectedNodes.FirstOrDefault();
            }
			
            set
            {
                selectedNodes.Clear();
                selectedNodes.Add(value);
            }
        }
        
        [NonSerialized] private MerinoNodemapData data = new MerinoNodemapData();

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
            DrawNodemap();
            DrawToolbar(Event.current);

            HandleEvents(Event.current);
            
            if (shouldRepaint)
            {
                Repaint();
                shouldRepaint = false;
            }
        }

        private void DrawNodemap()
        {
            Rect zoomRect = new Rect(0, 0, position.width / zoom, position.height / zoom);
            EditorZoomArea.Begin(zoom, zoomRect);
			
            DrawGrid();
            DrawConnections();
            DrawNodes();

            EditorZoomArea.End();
        }
        
        private void DrawGrid()
        {
            const float gridSize = 100f;
            const float smallGridSize = 25f;
            
            float width = position.width / zoom;
            float height = position.height / zoom;
            float x = scrollPos.x % smallGridSize;
            float y = scrollPos.y % smallGridSize;
			
            //draw small grid
            Handles.color = new Color(0, 0, 0, 0.5f);
            if (zoom > maxZoom / 2)
            {
                while (x < width)
                {
                    Handles.DrawLine(new Vector2(x, 0), new Vector2(x, height));
                    x += smallGridSize;
                }
            
                while (y < height)
                {
                    if (y >= 0)
                    {
                        Handles.DrawLine(new Vector2(0, y), new Vector2(width, y));
                    }
                    y += smallGridSize;
                }
            }
			
            //draw large grid
            x = scrollPos.x % gridSize;
            y = scrollPos.y % gridSize;
            Handles.color = Color.black;
            while (x < width)
            {
                Handles.DrawLine(new Vector2(x, 0), new Vector2(x, height));
                x += gridSize;
            }
            
            while (y < height)
            {
                if (y >= 0)
                {
                    Handles.DrawLine(new Vector2(0, y), new Vector2(width, y));
                }
                y += gridSize;
            }

            Handles.color = Color.white;
        }

        void DrawConnections()
        {
            for (int i = 0; i < data.TreeElements.Count; i++)
            {
                var node = data.TreeElements[i];
                if (node.depth == -1) continue; // skip root node and non-yarn node nodes

                Handles.color = Color.white;
                var connectedNodes = GetConnectedNodes(node);
                foreach (var connectedNode in connectedNodes)
                {
                    //get position to draw connections between
                    Rect start = new Rect(node.nodeRect);
                    start.x += scrollPos.x;
                    start.y += scrollPos.y;

                    Rect end = new Rect(connectedNode.nodeRect);
                    end.x += scrollPos.x;
                    end.y += scrollPos.y;

                    Handles.DrawLine(start.center, end.center);
                }
                Handles.color = Color.white;
            }
        }
        
        private void DrawNodes()
        {
            for (int i = 0; i < data.TreeElements.Count; ++i)
            {
                var node = data.TreeElements[i];
                if (node.depth == -1 || node.leafType != MerinoTreeElement.LeafType.Node) continue; // skip root node and non-yarn node nodes

                Rect windowRect = new Rect(node.nodeRect); //todo: adjust size
                windowRect.position += scrollPos;

                bool isSelected = false;
                foreach (var selectedNode in selectedNodes)
                {
                    if (selectedNode.name == node.name)
                    {
                        isSelected = true;
                        break;
                    }
                }

                GUIStyle style = GUI.skin.GetStyle(isSelected ? "flow node 1 on" : "flow node 1");
                style.alignment = TextAnchor.MiddleCenter;
                
                GUI.Box(windowRect, node.name, style);
            }
        }

        void HandleZoom(float delta, Vector2 center)
        {
            float prevZoom = zoom;
            zoom += delta;
            zoom = Mathf.Clamp(zoom, minZoom, maxZoom);
            var deltaSize = position.size / prevZoom - position.size / zoom;
            var offset = -Vector2.Scale(deltaSize, center);
            scrollPos += offset;
            shouldRepaint = true;
        }

        private void DrawToolbar(Event e)
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            {
                GUILayout.Space(2); //small space to mimic unity editor

                if (MerinoData.CurrentFiles.Count > 0)
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
                        var newFile = MerinoData.CurrentFiles.Find(x => x.name == fileOptions[newCurrentFile]);
                        
                        currentFile = newFile;
                        data.TreeElements = MerinoCore.GetDataFromFile( currentFile, 1, useFastMode:true );
                        shouldRepaint = true;
                        forceUpdateCurrentFile = false;
                    }
                }
                else
                {
                    //todo: implement into logic above, or just keep as a dummy popup i guess.
                    var fileOptions = new string[] {"No Files Loaded"};
                    GUI.SetNextControlName(popupControl);
                    EditorGUILayout.Popup(0, fileOptions, EditorStyles.toolbarDropDown);
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
        
        MerinoTreeElement GetNodeAt(Vector2 point)
        {
            for (int i = data.TreeElements.Count - 1; i >= 0; i--) // reverse for loop so we get the nodes on top first
            {
                var node = data.TreeElements[i];
                if (node.depth == -1 || node.leafType != MerinoTreeElement.LeafType.Node) continue; // skip root node and non-yarn node nodes

                var rect = node.nodeRect;
                rect.position += scrollPos;
                if (rect.Contains(point / zoom)) 
                    return node;
            }

            return null;
        }
        
        private bool GetAppendKeyDown()
        {
            return Event.current.shift || EditorGUI.actionKey;
        }

        #region Prototyping Methods

        public List<string> GetCurrentFileNames()
        {
            var list = new List<string>();

            foreach (var file in MerinoData.CurrentFiles)
                list.Add(file.name);

            return list;
        }
                
        List<MerinoTreeElement> GetConnectedNodes(MerinoTreeElement baseNode)
        {
            List<MerinoTreeElement> connected = new List<MerinoTreeElement>();

            // TODO: refactor to use regex
            // parse body for node names
            string[] splitBody = baseNode.nodeBody.Split('[', ']');
            for (int i = 2; i < splitBody.Length; i = i + 4)
            {
                // remove delimiter and text prior to it if applicable
                int delimiter = splitBody[i].IndexOf('|');
                if (delimiter != -1) splitBody[i] = splitBody[i].Remove(0, delimiter + 1);

                // skip options which link to the same node
                if (splitBody[i] == baseNode.name) continue;
				
                // TODO: add a way to prevent drawing multiple of the same connection
                // get and add connected node to list of connections
                var node = GetNode(splitBody[i]);
                if (node != null) connected.Add(node);
            }

            return connected;
        }
        
        private MerinoTreeElement GetNode(string name)
        {
            foreach (var node in data.TreeElements)
            {
                if (node.depth == -1) continue;
				
                if (node.name == name)
                    return node;
            }

            return null;
        }

        #endregion

        #region EventHandler Methods

        //todo: better repainting of window when a folder loaded
        private void FileLoadedHandler()
        {
            Repaint();
        }

        private void FileUnloadedHandler()
        {
            if (MerinoData.CurrentFiles.Count == 0)
            {
                //clear nodemap data being displayed if files loaded is now 0
                data.TreeElements = new List<MerinoTreeElement>();
                Repaint();
            }
            
            if (currentFile != null) // the file was just unloaded, but not deleted
                previousFileName = currentFile.name; //we will attempt to recover the file
            else
                previousFileName = null;
            
            currentFile = null;
            forceUpdateCurrentFile = true;
        }

        #endregion

        #region EventWindow Methods
        
        protected override void OnMouseDown(Event e)
        {
            var node = GetNodeAt(e.mousePosition);

            switch (e.button)
            {
                case MouseButton.Left:
                {
                    if (node != null)
                    {
                        // handle hit node
                        
                        if (GetAppendKeyDown())
                        {
                            // add or remove node from selection
                            if (selectedNodes.Contains(node))
                                selectedNodes.Remove(node);
                            else
                                selectedNodes.Add(node);
                        }
                        else
                        {
                            if (!selectedNodes.Contains(node))
                                SelectedNode = node;
							
                            dragNode = node;
                        }
						
                        e.Use();
                    }
                    else if (!(Tools.current == Tool.View && Tools.viewTool == ViewTool.Zoom))
                    {
                        // no node hit, and not not view/zoom tool
						
                        if (!GetAppendKeyDown())
                            selectedNodes.Clear(); // deselect all nodes
						
                        e.Use();
                    }
                    break;
                }
            }
        }

        protected override void OnMouseDrag(Event e)
		{
			bool drag = false;
			
			switch (e.button)
			{
			    case MouseButton.Left:
			    {
			        if (dragNode != null)
			        {
			            // node dragging
			            foreach (var node in selectedNodes)
			            {
			                node.nodePosition += Vector2Int.RoundToInt(e.delta / zoom);
			            }

			            e.Use();
			        }
			        if (e.alt)
			        {
			            // pan nodemap via alt/left-click
			            drag = true;
			        }	
			        break;
			    }
			    case MouseButton.Right:
				{
					if (!e.alt)
					{
						// pan nodemap via right-click 
						drag = true;
					}
					else
					{
						// TODO: add support for zooming left to right like the editor supports
						const float sensitivity = 0.001f;
						HandleZoom(-e.delta.y * sensitivity, Vector2.one * 0.5f);
						e.Use();
					}
					break;
				}
				case MouseButton.Middle:
				{
					// pan nodemap via middle-click
					drag = true;
					break;
				}
			}
			
			if (drag)
			{
				scrollPos += e.delta / zoom;
				e.Use();
			}
		}
        
        protected override void OnRawMouseUp(Event e)
        {
            var node = GetNodeAt(e.mousePosition);
			
            switch (e.button)
            {
                case MouseButton.Left:
                {
                    // release dragged node
                    if (dragNode != null)
                    {
                        dragNode = null;
                        e.Use();
                    }
					
                    //TODO: autosave changes made to nodemap
                    break;
                }
            }
        }
        
        protected override void OnScrollWheel(Event e)
        {
            // get point to zoom in on based on mouse position
            Vector2 zoomCenter;
            zoomCenter.x = e.mousePosition.x / zoom / position.width;
            zoomCenter.y = e.mousePosition.y / zoom / position.height;
            zoomCenter *= zoom;

            HandleZoom(-e.delta.y * 0.01f, zoomCenter);
            e.Use();
        }

        #endregion
    }
}
