using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Merino
{
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

        // selection box
        Rect selectionBox;
        Vector2 startSelectionBoxPos = -Vector2.one;
        List<MerinoTreeElement> boxSelectionNodes;

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
            
            if (Event.current.type == EventType.Repaint)
            {
                // draw selection box
                if (startSelectionBoxPos.x >= 0 && startSelectionBoxPos.y >= 0)
                    GUI.Box(selectionBox, "", GUI.skin.FindStyle("SelectionRect"));
            }

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
            for (int i = 0; i < MerinoData.TreeElements.Count; i++)
            {
                var node = MerinoData.TreeElements[i];
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
            for (int i = 0; i < MerinoData.TreeElements.Count; ++i)
            {
                var node = MerinoData.TreeElements[i];
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

//                if (MerinoData.CurrentFiles.Count > 0)
//                {
//                    var fileOptions = GetCurrentFileNames();
//                    int currentCurrentFile = 0;
//                    if (currentFile != null)
//                    {
//                        currentCurrentFile = fileOptions.IndexOf(currentFile.name);
//                    }
//                    else
//                    {
//                        if (!string.IsNullOrEmpty(previousFileName)) // was the previous file not deleted
//                        {
//                            if (fileOptions.Contains(previousFileName)) //is the file still loaded
//                            {
//                                currentCurrentFile = fileOptions.IndexOf(previousFileName);
//                            }
//                            else
//                            {
//                                currentCurrentFile = fileOptions.IndexOf(fileOptions[0]); // reset to 0 index
//                            }
//                        }
//                        else
//                        {
//                            currentCurrentFile = fileOptions.IndexOf(fileOptions[0]); //reset to 0 index
//                        }
//                    }
//                
//                    GUI.SetNextControlName(popupControl);
//                    int newCurrentFile = EditorGUILayout.Popup( 
//                        currentCurrentFile, 
//                        fileOptions.ToArray(), EditorStyles.toolbarDropDown);
//                
//                    if (currentCurrentFile != newCurrentFile || forceUpdateCurrentFile)
//                    {
//                        // change current file to new file
//                        var newFile = MerinoData.CurrentFiles.Find(x => x.name == fileOptions[newCurrentFile]);
//                        
//                        currentFile = newFile;
//                        //data.TreeElements = MerinoCore.GetDataFromFile( currentFile, 1, useFastMode:true );
//                        shouldRepaint = true;
//                        forceUpdateCurrentFile = false;
//                    }
//                }
//                else
//                {
//                    //todo: implement into logic above, or just keep as a dummy popup i guess.
//                    var fileOptions = new string[] {"No Files Loaded"};
//                    GUI.SetNextControlName(popupControl);
//                    EditorGUILayout.Popup(0, fileOptions, EditorStyles.toolbarDropDown);
//                }
                
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
            for (int i = MerinoData.TreeElements.Count - 1; i >= 0; i--) // reverse for loop so we get the nodes on top first
            {
                var node = MerinoData.TreeElements[i];
                if (node.depth == -1 || node.leafType != MerinoTreeElement.LeafType.Node) continue; // skip root node and non-yarn node nodes

                var rect = node.nodeRect;
                rect.position += scrollPos;
                if (rect.Contains(point / zoom)) 
                    return node;
            }

            return null;
        }
        
        public void FocusNode(int id)
        {
            var node = MerinoData.GetNode(id);
            
            // dont focus on non-node leafs
            if (node.leafType != MerinoTreeElement.LeafType.Node)
                return;
            
            if (zoom < 1) 
                HandleZoom(1 - zoom, Vector2.one * 0.5f); // reset zoom to 1
            scrollPos = -node.nodeRect.center + position.size * 0.5f / zoom;
        }

        public void FocusNode(List<int> ids)
        {
            if (ids.Count == 1)
            {
                // use single node focusing behaviour
                FocusNode(ids[0]);
                return;
            }

            // find respective nodes for the ids
            var nodes = new List<MerinoTreeElement>();
            foreach (var id in ids)
            {
                var found = MerinoData.GetNode(id);                
                if (found != null && found.leafType == MerinoTreeElement.LeafType.Node) 
                    nodes.Add(found);
            }

            // find max and min points of all nodes
            Vector2 min = nodes[0].nodeRect.min;
            Vector2 max = nodes[0].nodeRect.max;
            for (int i = 0; i < nodes.Count; ++i)
            {
                var block = nodes[i];
                min.x = Mathf.Min(min.x, block.nodeRect.center.x);
                min.y = Mathf.Min(min.y, block.nodeRect.center.y);
                max.x = Mathf.Max(max.x, block.nodeRect.center.x);
                max.y = Mathf.Max(max.y, block.nodeRect.center.y);
            }
			
            // find center of all nodes and focus there
            var center = -(min + max) * 0.5f;
            center.x += position.width * 0.5f / zoom;
            center.y += position.height * 0.5f / zoom;
            scrollPos = center;
        }

        public void SetSelectedNode(int id)
        {
            var node = MerinoData.GetNode(id);
            
            if (node.leafType == MerinoTreeElement.LeafType.Node)
                SelectedNode = node;
        }
        
        public void SetSelectedNode(List<int> ids)
        {
            selectedNodes.Clear();
            foreach (var id in ids)
            {
                var node = MerinoData.GetNode(id);
                
                if (node.leafType == MerinoTreeElement.LeafType.Node)
                    selectedNodes.Add(node);
            }
        }
        
        private bool GetAppendKeyDown()
        {
            return Event.current.shift || EditorGUI.actionKey;
        }

        private void AddSelectedNode(MerinoTreeElement node)
        {
            if (!selectedNodes.Contains(node))
            {
                selectedNodes.Add(node);
            }
        }
        
        #region Prototyping Methods

//        public List<string> GetCurrentFileNames()
//        {
//            var list = new List<string>();
//
//            foreach (var file in MerinoData.CurrentFiles)
//                list.Add(file.name);
//
//            return list;
//        }
                
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
                var node = MerinoData.GetNode(splitBody[i]);
                if (node != null) connected.Add(node);
            }

            return connected;
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
                MerinoData.TreeElements = new List<MerinoTreeElement>();
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
                                AddSelectedNode(node);
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
						
                        // start box selection
                        startSelectionBoxPos = e.mousePosition;
                        boxSelectionNodes = new List<MerinoTreeElement>(selectedNodes);
                        
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
			        else if (startSelectionBoxPos.x >= 0 && startSelectionBoxPos.y >= 0)
			        {
			            // handle contents of selection box

			            // figure out rect of selection box
			            var topLeft = Vector2.Min(startSelectionBoxPos, e.mousePosition);
			            var bottomRight = Vector2.Max(startSelectionBoxPos, e.mousePosition);
			            selectionBox = Rect.MinMaxRect(topLeft.x, topLeft.y, bottomRight.x, bottomRight.y);

			            // apply zoom to rect
			            Rect zoomSelectionBox = selectionBox;
			            zoomSelectionBox.position -= scrollPos * zoom;
			            zoomSelectionBox.position /= zoom;
			            zoomSelectionBox.size /= zoom;

			            foreach (var node in MerinoData.TreeElements)
			            {
			                if (zoomSelectionBox.Overlaps(node.nodeRect))
			                {
			                    // the selection box overlaps node
								
			                    if (boxSelectionNodes.Contains(node))
			                        selectedNodes.Remove(node);
			                    else
			                        AddSelectedNode(node);
			                }
			                else if (boxSelectionNodes.Contains(node))
			                {
			                    AddSelectedNode(node);
			                }
			                else
			                {
			                    // selection box doesn't overlap the node
			                    selectedNodes.Remove(node);
			                }
			            }
						
			            e.Use();
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
                    
                    // check to see if selection actually changed?
                    if (selectionBox.size.x > 0 && selectionBox.size.y > 0)
                    {
                        var tempList = new List<MerinoTreeElement>(selectedNodes);
                        selectedNodes = boxSelectionNodes;
                        selectedNodes = tempList;
                    }
                    
                    //TODO: autosave changes made to nodemap
                    break;
                }
            }
            		
            // clear selection box
            selectionBox.size = Vector2.zero;
            selectionBox.position = -Vector2.one;
            startSelectionBoxPos = selectionBox.position;
            shouldRepaint = true;
        }
        
        protected override void OnScrollWheel(Event e)
        {
            if (selectionBox.size != Vector2.zero) return;

            // get point to zoom in on based on mouse position
            Vector2 zoomCenter;
            zoomCenter.x = e.mousePosition.x / zoom / position.width;
            zoomCenter.y = e.mousePosition.y / zoom / position.height;
            zoomCenter *= zoom;

            HandleZoom(-e.delta.y * 0.01f, zoomCenter);
            e.Use();
        }

        protected override void OnKeyDown(Event e)
        {
            var node = GetNodeAt(e.mousePosition);

            switch (e.keyCode)
            {
                case KeyCode.F:
                {
                    // focus on selected nodes
                    if (selectedNodes.Count > 0)
                    {
                        FocusNode(selectedNodes.Select(x => x.id).ToList());
                        Repaint();
                    }
                    break;
                }
            }
        }

        #endregion
    }
}
