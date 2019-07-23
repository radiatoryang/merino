using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Merino.EditorCoroutines;

namespace Merino
{
    public class MerinoNodemapWindow : MerinoEventWindow
    {
        public const string windowTitle = " Merino (Nodemap)";
        private const string popupControl = "currentFilePopup";

        [NonSerialized] private bool shouldRepaint; //use for "delayed" repaints, call Repaint directly for instant refresh
        // [NonSerialized] private bool forceUpdateCurrentFile;
        [NonSerialized] private string previousFileName;

        double lastTimestampUpdate;

        // for ease of use, int key = file node ID... *not* MerinoData.CurrentFiles index
        //Dictionary<int, List<MerinoTreeElement>> nodeManifest = new Dictionary<int, List<MerinoTreeElement>>();
        [SerializeField] NodeManifest nodeManifest = new NodeManifest(); 

        [System.Serializable] class NodeManifest : SerializableDictionary<int, ManifestList> { }

        [SerializeField] List<int> allNodes = new List<int>(); 

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
        [NonSerialized] MerinoTreeElement currentLinkingNode;
        Vector2 currentLinkTarget;

        // selection box
        Rect selectionBox;
        Vector2 startSelectionBoxPos = -Vector2.one;
        List<MerinoTreeElement> boxSelectionNodes;

        [MenuItem("Window/Merino/Nodemap")]
        static void MenuItem_GetWindow()
        {
            GetWindow<MerinoNodemapWindow>(windowTitle, true).UpdateNodeManifest();
        }

        public static MerinoNodemapWindow GetNodemapWindow() {
            var window = GetWindow<MerinoNodemapWindow>(windowTitle, true);
            window.UpdateNodeManifest();
			return window;
		}

        private void OnEnable()
        {
            // GetNodemapWindow().titleContent = new GUIContent( windowTitle, MerinoEditorResources.Nodemap);
            MerinoEditorWindow.OnFileLoaded += FileLoadedHandler;
            MerinoEditorWindow.OnFileUnloaded += FileUnloadedHandler;
            currentLinkingNode = null;
            UpdateNodeManifest();
        }

        private void OnDisable()
        {
            MerinoEditorWindow.OnFileLoaded -= FileLoadedHandler;
            MerinoEditorWindow.OnFileUnloaded -= FileUnloadedHandler;
        }

        void OnGUI()
        {
            wantsMouseMove = currentLinkingNode != null;
            // ValidateNodeManifest(); 
            DrawNodemap();
            DrawToolbar(Event.current);
            
            if (Event.current.type == EventType.Repaint)
            {
                // draw selection box
                if (startSelectionBoxPos.x >= 0 && startSelectionBoxPos.y >= 0)
                    GUI.Box(selectionBox, "", GUI.skin.FindStyle("SelectionRect"));
            }

            DrawHelpbox();

            HandleEvents(Event.current);
            
            if (shouldRepaint)
            {
                Repaint();
                shouldRepaint = false;
            }
        }

        void OnInspectorGUI () {
            if ( lastTimestampUpdate < MerinoData.Timestamp ) {
                UpdateNodeManifest();
            }
        }

        void ValidateNodeManifest() {
            lastTimestampUpdate = MerinoData.Timestamp;

            // Debug.Log("ValidateNodeManifest: " + nodeManifest.Count);

            // validate all keys
            var removeKeys = new List<int>();
            foreach ( var kvp in nodeManifest ) {
                if ( MerinoData.GetNode( kvp.Key ) == null ) {
                    removeKeys.Add(kvp.Key);
                }
            }
            foreach ( var nullKey in removeKeys ) {
                nodeManifest.Remove( nullKey );
            }
            if ( removeKeys.Count > 0) {
                UpdateAllNodes();
            }
        }

        public void UpdateNodeManifest() {
            ValidateNodeManifest();

            // ok, now reload all children into the manifest
            var keys = nodeManifest.Keys.ToList();
            foreach ( var key in keys ) {
                 // Debug.Log( nodeManifest != null && nodeManifest.Count > 0 && nodeManifest.ContainsKey(key) && nodeManifest[key].internalList != null );
                nodeManifest[key].internalList = new List<int>( MerinoData.GetAllCachedChildren( key ).Select( c => c.id).ToList() );
            }

            UpdateAllNodes();
        }

        void UpdateAllNodes () {
            allNodes.Clear();
            foreach ( var kvp in nodeManifest ) {
                allNodes.AddRange( kvp.Value.internalList );
            }
        }

        private void DrawHelpbox()
        {
            if ( nodeManifest.Count > 0 ) {
                GUILayout.BeginArea( new Rect(10, 24, 192, 64));
                if ( currentLinkingNode != null) {
                    EditorGUILayout.HelpBox( "Left-click: Link node [" + currentLinkingNode.name + "] to another node.", MessageType.Info );
                } else {
#if UNITY_EDITOR_WIN
                    EditorGUILayout.HelpBox( "Left-click (drag): Move nodes\nDouble-click: Edit node\nScroll: Zoom in and out\nLMB + Alt: Pan view\nRMB: Pan view", MessageType.Info );
#else
                    EditorGUILayout.HelpBox( "Left-click (drag): Move nodes\nDouble-click: Edit node\nScroll: Zoom in and out\nLMB + Option: Pan view\nRMB: Pan view", MessageType.Info );
#endif
                }
                GUILayout.EndArea();
            } else {
                GUILayout.BeginArea( new Rect(position.width/2 - 128, position.height/2 - 48, 256, 96) );
                if ( MerinoData.CurrentFiles.Count == 0) {
                    EditorGUILayout.HelpBox( "No files / nodes are loaded into Merino yet, so there's nothing to display.", MessageType.Warning, true);
                } else {
                    EditorGUILayout.HelpBox( "Files have been loaded into Merino. Use the dropdown in the top-left to show nodes.", MessageType.Warning, true);
                }
                var content = new GUIContent( "Open Merino (Yarn Editor) Window", MerinoEditorResources.Node, "click to open the main Merino editor window" );
                if ( GUILayout.Button(content) ) {
                    MerinoEditorWindow.GetEditorWindow();
                }
                GUILayout.EndArea();
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
            Handles.color = EditorGUIUtility.isProSkin ? new Color(1,1,1,0.06f) : new Color(0, 0, 0, 0.25f);
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
            Handles.color = EditorGUIUtility.isProSkin ? new Color(1,1,1,0.1f) : new Color(0,0,0, 0.4f);
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
            // if in link / connect mode, then draw that too
            if ( currentLinkingNode != null ) {
                DrawLineConnection( currentLinkingNode.nodeRect.center, currentLinkTarget );
            }

            foreach ( var manifest in nodeManifest ) {
                var currentNodes = nodeManifest[manifest.Key];
                for (int i = 0; i < currentNodes.Count; i++)
                {
                    var node = MerinoData.GetNode(currentNodes[i]);
                    if (node == null || node.depth == -1 || node.leafType != MerinoTreeElement.LeafType.Node) continue; // skip root node and non-yarn node nodes
                    var connectedNodes = GetConnectedNodes(node);

                    foreach (var connectedNode in connectedNodes)
                    {

                        if ( selectedNodes.Contains(node) ) {
                            Handles.color = new Color( 0.25f, 0.6f, 1f, 1f);
                        } else if ( selectedNodes.Contains( connectedNode )) {
                            Handles.color = new Color( 0.9f, 0.95f, 1f, 1f);
                        } else {
                            Handles.color = new Color( 0.5f, 0.5f, 0.5f, 1f);
                        }

                        var offset = node.nodePosition.x < connectedNode.nodePosition.x ? Vector2.left * 10f : Vector2.right * 10f;
                        offset += node.nodePosition.y < connectedNode.nodePosition.y ? Vector2.up * 5f : Vector2.down * 5f;
                        DrawLineConnection( 
                            node.nodeRect.center + offset, 
                            connectedNode.nodeRect.RayIntersectToCenter(node.nodeRect.center, 15f) + offset, 
                            Mathf.Clamp01( Mathf.Min(
                                Mathf.Abs(node.nodePosition.x - connectedNode.nodePosition.x), 
                                Mathf.Abs(node.nodePosition.y - connectedNode.nodePosition.y)
                            ) / 60f) 
                        );          
                    }
                    Handles.color = Color.white;
                }
            }
        }
        
        void DrawLineConnection( Vector2 start, Vector2 end, float curvature = 1f) {
            var offset = start.y > end.y ? Vector2.down : Vector2.up;
            offset += start.x > end.x ? Vector2.left : Vector2.right;
            var points = Handles.MakeBezierPoints( start + scrollPos, end + scrollPos, start + scrollPos + offset * 55 * curvature, end + scrollPos, 16);
            Handles.DrawAAPolyLine( 6f, points);
            DrawConnectTriangle( points[15], points[15] - points[14]);
        }


        void DrawConnectTriangle( Vector2 center, Vector2 direction ) {
            const float triSize = 8f;
            direction.Normalize();
            var width = Vector2.Perpendicular( direction );
            direction *= 1.38f;
            Handles.DrawAAConvexPolygon( center + direction * triSize, center - (direction - width) * triSize, center - (direction + width) * triSize );
        }
        
        const int colorCount = 10;
        private void DrawNodes()
        {
            int colorIndex = 0;
            var oldBGColor = GUI.backgroundColor;
            foreach ( var manifest in nodeManifest ) {
                var currentNodes = nodeManifest[manifest.Key];
                var filename = MerinoData.GetNode(manifest.Key).name;
                for (int i = 0; i < currentNodes.Count; ++i)
                {
                    var node = MerinoData.GetNode(currentNodes[i]);
                    if (node.depth == -1 || node.leafType != MerinoTreeElement.LeafType.Node) continue; // skip root node and non-yarn node nodes

                    Rect windowRect = new Rect(node.nodeRect);
                    windowRect.position += scrollPos;

                    bool isSelected = false;
                    foreach (var selectedNode in selectedNodes)
                    {
                        if (selectedNode.id == node.id)
                        {
                            isSelected = true;
                            break;
                        }
                    }

                    string displayName = node.name;

                    GUIStyle style = new GUIStyle( GUI.skin.GetStyle("flow node 0") );
                    GUI.backgroundColor = Color.HSVToRGB( 1f * colorIndex / colorCount, 0.1f, 1f);
                    if ( isSelected ) {
                        GUI.backgroundColor = oldBGColor;
                        style = new GUIStyle( GUI.skin.GetStyle("flow node 1 on") );
                    } // highlight current playtest node
                    else if ( MerinoPlaytestWindow.CurrentNode != null && MerinoPlaytestWindow.CurrentNode == node.name && (MerinoPlaytestWindow.lastFileParent <= 0 || MerinoPlaytestWindow.lastFileParent == manifest.Key) ) {
                        GUI.backgroundColor = Color.HSVToRGB( 1f * colorIndex / colorCount, 0.5f + Mathf.PingPong(System.Convert.ToSingle(EditorApplication.timeSinceStartup), 0.5f), 1f);
                        Repaint();
                    } // highlight start nodes
                    else if ( node.name.Contains("Start") ) {
                        GUI.backgroundColor = Color.HSVToRGB( 1f * colorIndex / colorCount, 0.25f, 1f);
                    } 

                    // cache node size
                    node.cachedSize = Mathf.CeilToInt(Mathf.Max(100f, GUI.skin.GetStyle("flow node 0").CalcSize( new GUIContent(displayName)).x + 8f));

                    style.alignment = TextAnchor.UpperCenter;
                    if ( nodeManifest.Count > 1 ) {
                        displayName = "<size=8>" + filename + "</size>\n" + displayName;
                        style.richText = true;
                        style.padding.top -= 2;
                    }

                    var isThereBrokenNode = GetConnectedNodes(node, false, true).Where( n => n == null).Count() > 0;
                    if (isThereBrokenNode ) {
                        GUI.Box(windowRect, new GUIContent(displayName, MerinoEditorResources.Error, "This node has a broken link.\n- check your node names\n- the other node could be in a different file"), style);
                    } else {
                        GUI.Box(windowRect, displayName, style);
                    }

                    // if node is selected, show playtest button
                    if (isSelected && currentLinkingNode == null) {
                        var buttonRect = new Rect(windowRect);
                        buttonRect.y -= 20;
                        buttonRect.height = 20;
                        GUILayout.BeginArea( buttonRect );
                        GUILayout.BeginHorizontal();
                        MerinoEditorWindow.DrawPlaytestButton( node.id, node.name, true, true, buttonRect.width - 24 );
                        GUILayout.EndHorizontal();
                        GUILayout.EndArea();
                    }

                    // node body preview
                    windowRect.x += 5;
                    windowRect.y += nodeManifest.Count > 1 ? 35 : 25;
                    windowRect.width -= 10;
                    windowRect.height -= nodeManifest.Count > 1 ? 40 : 30;
                    GUI.Box( windowRect, node.nodeBody.Substring(0, Mathf.Min(128, node.nodeBody.Length)), MerinoStyles.SmallMonoTextStyle );
                
                    // if node is selected, show link button
                    if ( isSelected && currentLinkingNode == null ) {
                        windowRect.x += 10;
                        windowRect.y += windowRect.height;
                        windowRect.width -= 20;
                        windowRect.height = 20;
                        if ( GUI.Button( windowRect, "+ New Link", EditorStyles.miniButton) ) {
                            OnRightClickStartConnectNode( node );
                        }
                    }
                
                }
                colorIndex = (colorIndex+1) % colorCount;
            }
            GUI.backgroundColor = oldBGColor;
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
                    if ( EditorGUILayout.DropdownButton( new GUIContent(nodeManifest.Count + " Files Loaded"), FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.Width(120) ) ) {
                        var menu = new GenericMenu();

                        var fileOptions = GetCurrentFileNames();
                        foreach ( var newFilename in fileOptions ) {
                            var file = MerinoData.CurrentFiles.Find(x => x.name == newFilename);
                            var fileNode = MerinoData.TextAssetToNode(file);
                            menu.AddItem( new GUIContent(newFilename), nodeManifest.ContainsKey(fileNode.id), ToggleFileToManifest, fileNode.id );
                        }

                        menu.DropDown( GUILayoutUtility.GetLastRect() );
                    }

                    GUILayout.Space(2); //small space to mimic unity editor

                    if ( GUILayout.Button("Refresh", EditorStyles.toolbarButton) ) {
                        var refreshList = nodeManifest.Keys.ToList();
                        foreach ( var key in refreshList ) {
                            nodeManifest[key].internalList.Clear();
                        }
                        allNodes.Clear();
                        UpdateNodeManifest();
                    }

                    if ( GUILayout.Button("Reset Node Map", EditorStyles.toolbarButton) ) {
                        scrollPos = Vector2.zero;
                        zoom = 1f;
                        nodeManifest.Clear();
                        allNodes.Clear();
                        lastTimestampUpdate = 0.0;
                    }

                //    var fileOptions = GetCurrentFileNames();
                //    int currentCurrentFile = 0;
                //    if (currentFile != null)
                //    {
                //        currentCurrentFile = fileOptions.IndexOf(currentFile.name);
                //    }
                //    else
                //    {
                //        if (!string.IsNullOrEmpty(previousFileName)) // was the previous file not deleted
                //        {
                //            if (fileOptions.Contains(previousFileName)) //is the file still loaded
                //            {
                //                currentCurrentFile = fileOptions.IndexOf(previousFileName);
                //            }
                //            else
                //            {
                //                currentCurrentFile = fileOptions.IndexOf(fileOptions[0]); // reset to 0 index
                //            }
                //        }
                //        else
                //        {
                //            currentCurrentFile = fileOptions.IndexOf(fileOptions[0]); //reset to 0 index
                //        }
                //    }
               
                //     GUI.SetNextControlName(popupControl);

                //     int newCurrentFile = EditorGUILayout.Popup( 
                //         currentCurrentFile, 
                //         fileOptions.ToArray(), EditorStyles.toolbarDropDown);
                
                //     if (currentCurrentFile != newCurrentFile)
                //     {
                //         // change current file to new file
                //         currentFile = MerinoData.CurrentFiles.Find(x => x.name == fileOptions[newCurrentFile]);
                //         currentNodes = MerinoData.GetAllCachedChildren( MerinoData.TextAssetToNode(currentFile).id );
                //         shouldRepaint = true;
                //     }

                    GUILayout.FlexibleSpace();

                    if (!MerinoPrefs.useAutosave && GUILayout.Button( new GUIContent(" Save All", MerinoEditorResources.Save, "save all changes to all files"), EditorStyles.toolbarButton, GUILayout.Height(18), GUILayout.MaxWidth(70)))
                    {
                        MerinoCore.SaveDataToFiles();
                        MerinoCore.ReimportFiles(true);
                    }

                    EditorGUI.BeginChangeCheck();
                    MerinoPrefs.useAutosave = EditorGUILayout.ToggleLeft(new GUIContent("Autosave?", "if enabled, will automatically save every change"), MerinoPrefs.useAutosave, EditorStyles.miniLabel, GUILayout.Width(80));
                    GUILayout.Space(4);
                    MerinoPrefs.allowLinksAcrossFiles = EditorGUILayout.ToggleLeft( new GUIContent("Show Links Across Different Files", "If enabled, the node map will show links between nodes in different files. If disabled, it will mark them as broken."), MerinoPrefs.allowLinksAcrossFiles, EditorStyles.miniLabel );
                    if ( EditorGUI.EndChangeCheck() ) {
                        MerinoPrefs.SaveHiddenPrefs();
                    }

                    GUILayout.FlexibleSpace();

                    // if ( GUILayout.Button("Unwrap Nodes", EditorStyles.toolbarButton) ) {
                    //     this.StartCoroutine( UnknotNodes() );
                    // }
                } else {
                    GUILayout.FlexibleSpace();
                }

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

        void ToggleFileToManifest(object fileNodeID) {
            AddOrToggleFileToManifest( (int)fileNodeID );
        }

        void AddOrToggleFileToManifest( int id ) {
            AddSetFileToManifest( id, !nodeManifest.ContainsKey(id) );
        }

        void AddSetFileToManifest(int id, bool state, bool updateManifest=true) {
            if ( !state && nodeManifest.ContainsKey(id) ) {
                nodeManifest.Remove(id);
            } else if (state && !nodeManifest.ContainsKey(id) ){
                nodeManifest.Add(id, new ManifestList() );
            }

            if ( updateManifest ) {
                UpdateNodeManifest();
            }
        }

        public override void Refresh()
        {
            Repaint();
        }

        // cleanup nodes using simple force-directed graph algorithm
        // adapted from pseudocode in "Simple Algorithms for Network Visualization" by MJ McGuffin
        // https://pdfs.semanticscholar.org/9f0f/5a1507b83f96bcedbf2b8971fde21948b086.pdf
        IEnumerator UnknotNodes() {
            // this is o(n^2) but we expect only ~100 nodes at most in a yarn file imo

            foreach (var manifest in nodeManifest ) {
                Vector2 center = Vector2.zero;

                var nodes = nodeManifest[manifest.Key].internalList.Select( nodeID => MerinoData.GetNode(nodeID)).Where( node => node.depth > 0 && node.leafType == MerinoTreeElement.LeafType.Node ).ToList();

                var neighbors = new Dictionary<MerinoTreeElement, List<MerinoTreeElement>>(nodes.Count);
                var forces = new Dictionary<MerinoTreeElement, Vector2>(nodes.Count);
                foreach ( var node in nodes ) {
                    neighbors.Add( node, GetConnectedNodes(node, false) );
                    // forces.Add( node, Vector2.zero );
                }

                // randomly place nodes based on their connections
                int width = Mathf.CeilToInt( Mathf.Sqrt(nodes.Count));
                for( int i=0; i<nodes.Count; i++) {
                    nodes[i].nodePosition = Vector2Int.RoundToInt(center + UnityEngine.Random.insideUnitCircle.normalized * 200f * width / Mathf.Max(1, neighbors[nodes[i]].Count) );
                }
                // put 1-neighbor nodes next to their neighbor
                for (int i=0; i<nodes.Count; i++) {
                    if ( neighbors[nodes[i]].Count == 1) {
                        nodes[i].nodePosition = neighbors[nodes[i]][0].nodePosition;
                        nodes[i].nodePosition += Vector2Int.RoundToInt( new Vector2( nodes[i].nodePosition.x, nodes[i].nodePosition.y ).normalized * 50f);
                    }
                }
                
                // how many iterations to run
                for (int iterations=0; iterations<500; iterations++) {
                    forces.Clear();
                    foreach ( var node in nodes) {
                        forces.Add( node, Vector2.zero );
                    }

                    // repulsion between all pairs
                    for(int n1=0; n1<nodes.Count-1; n1++) {
                        for (int n2=n1+1; n2<nodes.Count; n2++) { // n2=n1+1 because only once per pair
                            var rawDistance = new Vector2(nodes[n2].nodePosition.x - nodes[n1].nodePosition.x, nodes[n2].nodePosition.y - nodes[n1].nodePosition.y);
                            if ( rawDistance.sqrMagnitude > 200000f && (neighbors[nodes[n1]].Count == 0 || neighbors[nodes[n2]].Count == 0) ) {
                                continue;
                            }
                            Vector2 forceFinal = Vector3.zero;
                            forceFinal = rawDistance.normalized * 69000f / Mathf.Max(1f, rawDistance.sqrMagnitude);
                            forces[nodes[n1]] -= forceFinal;
                            forces[nodes[n2]] += forceFinal;
                        }
                    }

                    // attraction between linked nodes and second degree neighbors
                    for (int n1=0; n1<nodes.Count; n1++) {
                        var myNeighbors = neighbors[nodes[n1]];
                        // Debug.Log( nodes[n1].name + " has " + myNeighbors.Count);
                        for( int n2=0; n2<myNeighbors.Count; n2++) {
                            // if ( nodes[n1].id >= myNeighbors[n2].id ) { 
                            //     continue; // only apply attraction once per pair though
                            // }
                            var rawDistance = new Vector2(myNeighbors[n2].nodePosition.x - nodes[n1].nodePosition.x, myNeighbors[n2].nodePosition.y - nodes[n1].nodePosition.y);
                            Vector2 forceFinal = rawDistance.normalized * Mathf.Log10( Mathf.Max(1f, rawDistance.magnitude - 300f) );
                            var neighborPull = Mathf.Pow(Mathf.Max(1, 4 - myNeighbors.Count), 4);
                            forces[nodes[n1]] += forceFinal * neighborPull;

                            if ( nodes.Contains(myNeighbors[n2])) { // don't change neighbors if they're not part of the selection
                                forces[myNeighbors[n2]] -= forceFinal * neighborPull * 2;
                            }

                        }
                    }

                    // apply force to node positions
                    float iterationEnergy = 0f;
                    const float deltaTime = 1.6f;
                    const float maxDisplacement = 100f;
                    foreach (var node in nodes) {
                        forces[node] *= deltaTime;
                        if ( (forces[node]).sqrMagnitude > maxDisplacement * maxDisplacement ) {
                            forces[node] = forces[node].normalized * maxDisplacement;
                        }
                        node.nodePosition = Vector2Int.RoundToInt( forces[node] + node.nodePosition );
                        iterationEnergy += forces[node].sqrMagnitude;
                    }

                    if ( iterations % 10 == 0) {
                        Repaint();
                        HandleZoom( -999f, Vector2.one * 0.5f );
                        yield return new WaitForSeconds(0.01f);
                    }

                    if ( iterationEnergy < 5f ) {
                        Repaint();
                        continue;
                    }
                }
            }
            Repaint();
            EditorUtility.SetDirty( MerinoData.Instance ); // commit changes to temp data
        }
        
        MerinoTreeElement GetNodeAt(Vector2 point)
        {
            foreach ( var manifest in nodeManifest) {
                var currentNodes = nodeManifest[manifest.Key];
                for (int i = currentNodes.Count - 1; i >= 0; i--) // reverse for loop so we get the nodes on top first
                {
                    var node = MerinoData.GetNode(currentNodes[i]);
                    if (node.depth == -1 || node.leafType != MerinoTreeElement.LeafType.Node) continue; // skip root node and non-yarn node nodes

                    var rect = node.nodeRect;
                    rect.position += scrollPos;
                    if (rect.Contains(point / zoom)) 
                        return node;
                }
            }

            return null;
        }
        
        public void FocusNode(int id)
        {
            var node = MerinoData.GetNode(id);
            AddSetFileToManifest( MerinoData.GetFileParent(id).id, true );
            // currentNodes = MerinoData.GetAllCachedChildren( MerinoData.TextAssetToNode(currentFile).id );
            
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
            var fileParents = new List<int>();
            var nodes = new List<MerinoTreeElement>();
            foreach (var id in ids)
            {
                var found = MerinoData.GetNode(id);                
                if (found != null && found.leafType == MerinoTreeElement.LeafType.Node) {
                    nodes.Add(found);
                    var fParent = MerinoData.GetFileParent( id ).id;
                    if ( !fileParents.Contains(fParent) ) {
                        fileParents.Add(fParent);
                    }
                }
            }

            // foreach file parent, load nodes
            foreach ( var fParentID in fileParents ) {
                AddSetFileToManifest( fParentID, state:true, updateManifest:false );
            }
            UpdateNodeManifest(); // only update node manifest at the end

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

       public List<string> GetCurrentFileNames()
       {
           var list = new List<string>();

           foreach (var file in MerinoData.CurrentFiles)
               list.Add(file.name);

           return list;
       }
                
        List<MerinoTreeElement> GetConnectedNodes(MerinoTreeElement baseNode, bool findIncomingLinksToo = false, bool includeNullLinks = false)
        {
            List<MerinoTreeElement> connected = new List<MerinoTreeElement>();

            int fileID = MerinoData.GetFileParent(baseNode.id).id;

            // this shouldn't happen... this is bad... but I don't know when / why this happens
            if ( !nodeManifest.ContainsKey( fileID ) ) {
                ValidateNodeManifest();
                AddSetFileToManifest(fileID, true);
            }
            
            var currentNodes = nodeManifest[fileID];
            var nodeSearch = new List<MerinoTreeElement>();
            if ( findIncomingLinksToo ) {
                nodeSearch.AddRange( currentNodes.internalList.Select( id => MerinoData.GetNode(id)).Where( node => node.leafType == MerinoTreeElement.LeafType.Node ) );
            } else {
                nodeSearch.Add( baseNode );
            }
            // parse body for node names
            foreach ( var searchNode in nodeSearch ) {

                 // TODO: refactor to use regex
                string[] splitBody = searchNode.nodeBody.Split('[', ']');
                for (int i = 2; i < splitBody.Length; i = i + 4)
                {
                    // remove delimiter and text prior to it if applicable
                    int delimiter = splitBody[i].IndexOf('|');
                    if (delimiter != -1) splitBody[i] = splitBody[i].Remove(0, delimiter + 1);

                    // skip options which link to the same node
                    if (baseNode == searchNode && splitBody[i] == baseNode.name) continue;
                    
                    // get and add connected node to list of connections
                    var nodeList = (MerinoPrefs.allowLinksAcrossFiles ? allNodes : currentNodes.internalList ).Select( id => MerinoData.GetNode(id)).Where( node => node.name == splitBody[i] );
                    foreach ( var node in nodeList ) {
                        if (node != null ) {
                            if ( !connected.Contains(node) ) {
                                connected.Add(node); 
                            }
                        } else if ( includeNullLinks ) {
                            connected.Add(null);
                        }
                    }
                }
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
            
            UpdateNodeManifest();

            // if (currentFile != null) // the file was just unloaded, but not deleted
            //     previousFileName = currentFile.name; //we will attempt to recover the file
            // else
            //     previousFileName = null;
            
            // currentFile = null;
            // forceUpdateCurrentFile = true;
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
                        if ( currentLinkingNode != null ) {
                            // connect node with new node
                            currentLinkingNode.nodeBody += string.Format("\n[[Go to {0}|{0}]]", node.name);
                            currentLinkingNode = null;
                            EditorUtility.SetDirty( MerinoData.Instance );
                            UpdateNodeManifest();
                        }  
                        else if (GetAppendKeyDown())
                        {
                            // add or remove node from selection
                            if (selectedNodes.Contains(node))
                                selectedNodes.Remove(node);
                            else
                                AddSelectedNode(node);
                        }
                        else
                        {
                            // if double-click, then zoom to the node in the text editor...
                            if ( e.clickCount >= 2) {
                                MerinoEditorWindow.GetEditorWindow().SelectNodeAndZoomToLine(node.id, -1);
                            } else { // otherwise, select and move node in nodemap
                                if (!selectedNodes.Contains(node))
                                    SelectedNode = node;
                                
                                dragNode = node;
                            }
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
                case MouseButton.Right:
                {
                    var menu = new GenericMenu();
                    if ( node != null ) {
                        menu.AddItem( new GUIContent("Edit Node \"" + node.name + "\""), false, OnRightClickOpenNode, node);
                        menu.AddItem( new GUIContent("Playtest \"" + node.name + "\" + " + MerinoPrefs.playtestScope.ToString() ), false, OnRightClickOpenNode, node);
                        menu.AddItem( new GUIContent("Link \"" + node.name + "\" to..."), false, OnRightClickStartConnectNode, node);
                    } else {
                        var content = new GUIContent("Create New Node Here " + e.mousePosition.ToString() );
                        if ( MerinoData.CurrentFiles.Count > 0) {
                            menu.AddItem( content, false, OnRightClickCreateNode, e.mousePosition );
                        } else {
                            menu.AddDisabledItem( content, false );
                        }
                    }
                    menu.ShowAsContext();
                    break;
                }

            }
        }

        void OnRightClickOpenNode(object node) {
            var thisNode = (MerinoTreeElement)node;
            MerinoEditorWindow.GetEditorWindow().SelectNodeAndZoomToLine(thisNode.id, -1);
        }

        void OnRightClickPlaytest(object node) {
            var playNode = (MerinoTreeElement)node;
            MerinoPlaytestWindow.PlaytestFrom( playNode.name, MerinoCore.GetPlaytestParentID(playNode.id) );
        }

        void OnRightClickStartConnectNode (object node) {
            var startNode = (MerinoTreeElement)node;
            currentLinkingNode = startNode;
            currentLinkTarget = startNode.nodeRect.center / zoom - scrollPos;
            SetSelectedNode( startNode.id );
        }

        void OnRightClickCreateNode(object mousePosition ) {
            var createPos = (Vector2)mousePosition;

            var newNode = MerinoEditorWindow.GetEditorWindow(false).AddNewNode(null, false)[0];
            newNode.nodePosition = Vector2Int.RoundToInt( createPos / zoom - scrollPos );
            Focus();
            SetSelectedNode( newNode.id );
            EditorUtility.SetDirty( MerinoData.Instance );
            UpdateNodeManifest();
        }

        protected override void OnRawMouseMove(Event e)
        {
             // if linking / connecting a node, then draw a line to there too
            if ( currentLinkingNode != null) {
                // Debug.Log( e.mousePosition / zoom - scrollPos );
                // get node, and snap to it if close enough
                var node = GetNodeAt( e.mousePosition );
                if (node == null) {
                    currentLinkTarget = e.mousePosition / zoom - scrollPos;
                } else {
                    currentLinkTarget = node.nodeRect.center;
                }
                Repaint();
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

			            foreach (var id in allNodes)
			            {
                            var node = MerinoData.GetNode(id);
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
                        // save changes to nodemap
                        EditorUtility.SetDirty( MerinoData.Instance );

                        // if autosave is on, save changes
                        if ( MerinoPrefs.useAutosave ) {
                            MerinoCore.SaveDataToFiles();
                        }

                        // clean-up
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
