using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using Yarn;
using File = System.IO.File;
using EditorCoroutines;
using Random = System.Random;
// wow that's a lot of usings

namespace Merino
{

	class MerinoEditorWindow : EditorWindow
	{
		// sidebar tree view management stuff
		[NonSerialized] bool m_Initialized;
		[SerializeField] TreeViewState viewState; // Serialized in the window layout file so it survives assembly reloading
		[SerializeField] MultiColumnHeaderState m_MultiColumnHeaderState;
		SearchField m_SearchField;
		[SerializeField] MerinoTreeData treeData;
		[SerializeField] MerinoTreeView m_TreeView;
		public MerinoTreeView treeView {
			get { return m_TreeView; }
		}
		// double lastTabTime = 0.0; // this is a really bad hack
		
		// preferences
		static bool prefsLoaded = false;
		
		static string newFileTemplatePath = "NewFileTemplate.yarn";
		static string tempDataPath = "Assets/Merino/Editor/MerinoTempData.asset";
		
		static Color highlightComments = new Color(0.3f, 0.6f, 0.25f);
		static Color highlightCommands = new Color(0.8f, 0.5f, 0.1f);
		static Color highlightNodeOptions = new Color(0.8f, 0.4f, 0.6f);
		static Color highlightShortcutOptions = new Color(0.2f, 0.6f, 0.7f);
		// public static Color highlightVariables;
		
		// UI settings, will still get saved by Hidden EditorPrefs
		static bool stopOnDialogueEnd = true;
		static bool useAutoAdvance = false;
		static bool useAutosave = true;
		static float sidebarWidth = 180f;

		const int margin = 10;
		[SerializeField] Vector2 scrollPos;
		
		
		int zoomID = -1, zoomToLineNumber = -1, lastZoomToLineNumber = -1;
		double zoomToLineNumberTimestamp;
		const double zoomLineFadeTime = 1.0;
		
		bool resizingSidebar = false;
		
		float playPreviewHeight {
			get { return isDialogueRunning ? 180f : 0f; }
		}
		
		Rect sidebarSearchRect
		{
			get { return new Rect (0, margin, sidebarWidth, margin*2); }
		}
		
		Rect sidebarRect
		{
			get { return new Rect(0, margin*3, sidebarWidth, position.height-margin*5.5f); }
		}

		Rect sidebarResizeRect
		{
			get { return new Rect(sidebarWidth, 0, 5, position.height); }
		}

		Rect nodeEditRect
		{
			get { return new Rect( sidebarWidth, margin, position.width-sidebarWidth, position.height-margin*3.5f-playPreviewHeight);} // was height-30
		}
		
		Rect playPreviewRect
		{
			get { return new Rect( sidebarWidth, position.height-margin-playPreviewHeight, position.width-sidebarWidth-0.5f, playPreviewHeight-margin);}
		}

		Rect bottomToolbarRect
		{
			get { return new Rect(0, position.height - margin*2.5f, position.width, margin*2.5f); }
		}

		// misc resources
		Texture helpIcon, errorIcon;
		TextAsset currentFile;
		Font monoFont;
		
		// undo management
		double lastUndoTime;
		int moveCursorUndoID, moveCursorUndoIndex;
		[SerializeField] List<MerinoUndoLog> undoData = new List<MerinoUndoLog>();
		bool spaceWillIncrementUndo = false; // used just in Main Pane
		
		// error checking
		List<MerinoErrorLine> errorLog = new List<MerinoErrorLine>();
		
		// node management
		private List<int> DeleteList = new List<int>();
		int currentNodeIDEditing;
		
		// Yarn Spinner running stuff
		[NonSerialized] bool isDialogueRunning;
		MerinoVariableStorage varStorage;
		MerinoDialogueUI dialogueUI;
		Dialogue _dialogue;
		Dialogue dialogue {
			get {
				if (_dialogue == null) {
					// Create the main Dialogue runner, and pass our variableStorage to it
					varStorage = new MerinoVariableStorage();
					_dialogue = new Yarn.Dialogue ( varStorage );
					_dialogue.experimentalMode = false;
					dialogueUI = new MerinoDialogueUI();
					dialogueUI.consolasFont = monoFont;
					
					// Set up the logging system.
					_dialogue.LogDebugMessage = delegate (string message) {
						Debug.Log (message);
					};
					_dialogue.LogErrorMessage = delegate (string message) {
						PlaytestErrorLog (message);
					};
				}
				return _dialogue;
			}
		}
		
		// some help strings
		const string compileErrorHoverString = "{0}\n\n(DEBUGGING TIP: This line number is just Yarn's guess. Look before this point too.)\n\nLeft-click to dismiss.";
		

		#region EditorWindowStuff

		[MenuItem("Window/Merino (Yarn Editor)")]
		public static MerinoEditorWindow GetWindow ()
		{
			var window = GetWindow<MerinoEditorWindow>();
			window.titleContent = new GUIContent("Merino (Yarn)");
			window.Focus();
			window.Repaint();
			return window;
		}

		static void ResetEditorPrefsAll()
		{
			newFileTemplatePath = "NewFileTemplate.yarn";
			
			highlightComments = new Color(0.3f, 0.6f, 0.25f);
			highlightCommands = new Color(0.8f, 0.5f, 0.1f);
			highlightNodeOptions = new Color(0.8f, 0.4f, 0.6f);
			highlightShortcutOptions = new Color(0.2f, 0.6f, 0.7f);
			
			SaveEditorPrefs();

			stopOnDialogueEnd = true;
			useAutoAdvance = false;
			useAutosave = true;
			sidebarWidth = 180f;
			
			SaveHiddenEditorPrefs();
		}

		public static void LoadEditorPrefs()
		{
			if (EditorPrefs.HasKey("MerinoFirstRun") == false)
			{
				SaveEditorPrefs();
				EditorPrefs.SetBool("MerinoFirstRun", true);
			}
			newFileTemplatePath = EditorPrefs.GetString("MerinoTemplatePath", newFileTemplatePath);

			ColorUtility.TryParseHtmlString(EditorPrefs.GetString("MerinoHighlightCommands"), out highlightCommands);
			ColorUtility.TryParseHtmlString(EditorPrefs.GetString("MerinoHighlightComments"), out highlightComments);
			ColorUtility.TryParseHtmlString(EditorPrefs.GetString("MerinoHighlightNodeOptions"), out highlightNodeOptions);
			ColorUtility.TryParseHtmlString(EditorPrefs.GetString("MerinoHighlightShortcutOptions"), out highlightShortcutOptions);
		}

		public static void SaveEditorPrefs()
		{
			EditorPrefs.SetString("MerinoTemplatePath", newFileTemplatePath);
			
			EditorPrefs.SetString("MerinoHighlightCommands", "#"+ColorUtility.ToHtmlStringRGB(highlightCommands) );
			EditorPrefs.SetString("MerinoHighlightComments", "#"+ColorUtility.ToHtmlStringRGB(highlightComments) );
			EditorPrefs.SetString("MerinoHighlightNodeOptions", "#"+ColorUtility.ToHtmlStringRGB(highlightNodeOptions) );
			EditorPrefs.SetString("MerinoHighlightShortcutOptions", "#"+ColorUtility.ToHtmlStringRGB(highlightShortcutOptions) );

		}
		
		[PreferenceItem("Merino (Yarn)")]
		public static void MerinoPreferencesGUI()
		{
			// Load the preferences
			if (!prefsLoaded)
			{
				LoadEditorPrefs();
				prefsLoaded = true;
			}


			// Reset button
			if (GUILayout.Button("Reset Merino to default settings"))
			{
				ResetEditorPrefsAll();
				SaveEditorPrefs();
			}
			
			// Preferences GUI
			GUILayout.Label("File Handling", EditorStyles.boldLabel);
			EditorGUILayout.Space();
			GUILayout.Label("New File Template filepath (relative to /Resources/, omit .txt)");
			newFileTemplatePath = EditorGUILayout.TextField("/Resources/", newFileTemplatePath);

			GUILayout.Label("Syntax Highlighting Colors", EditorStyles.boldLabel);
			highlightCommands = EditorGUILayout.ColorField("<<Commands>>", highlightCommands);
			highlightComments = EditorGUILayout.ColorField("// Comments", highlightComments);
			highlightNodeOptions = EditorGUILayout.ColorField("[[NodeOptions]]", highlightNodeOptions);
			highlightShortcutOptions = EditorGUILayout.ColorField("-> ShortcutOptions", highlightShortcutOptions);

			// Save the preferences
			if (GUI.changed)
			{
				SaveEditorPrefs();
			}

		}
		
		void ResetMerino()
		{
			currentFile = null;
			viewState = null;
			m_Initialized = false;
			ForceStopDialogue();
			AssetDatabase.DeleteAsset(tempDataPath); // delete tempdata, or else it will just get reloaded again
			Selection.objects = new UnityEngine.Object[0]; // deselect all
			Undo.undoRedoPerformed -= OnUndo;
			InitIfNeeded(true);
		}

		static void LoadHiddenEditorPrefs()
		{
			if (EditorPrefs.HasKey("MerinoStopOn") == false)
			{
				SaveHiddenEditorPrefs(); // save defaults if not found
			}
			stopOnDialogueEnd = EditorPrefs.GetBool("MerinoStopOn");
			useAutoAdvance = EditorPrefs.GetBool("MerinoAutoAdvance");
			useAutosave = EditorPrefs.GetBool("MerinoAutosave");
			sidebarWidth = EditorPrefs.GetFloat("MerinoSidebarWidth");
		}

		static void SaveHiddenEditorPrefs()
		{
			EditorPrefs.SetBool("MerinoStopOn", stopOnDialogueEnd);
			EditorPrefs.SetBool("MerinoAutoAdvance", useAutoAdvance);
			EditorPrefs.SetBool("MerinoAutosave", useAutosave);
			EditorPrefs.SetFloat("MerinoSidebarWidth", sidebarWidth);
		}
		
		void InitIfNeeded (bool ignoreSelection=false)
		{
			if (m_Initialized) return;

			currentNodeIDEditing = -1;
			Undo.undoRedoPerformed += OnUndo;
			undoData.Clear();
			errorLog.Clear();
			
			// default highlight colors		
			LoadEditorPrefs();
			LoadHiddenEditorPrefs();
			
			// load font
			if (monoFont == null)
			{
				monoFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Merino/Editor/Fonts/Inconsolata-Regular.ttf");
			}
			
			// load help icon
			if (helpIcon == null)
			{
				helpIcon = EditorGUIUtility.IconContent("_Help").image;
			}
			
			// load error icon
			if (errorIcon == null)
			{
				errorIcon = EditorGUIUtility.Load("icons/d_console.erroricon.sml.png") as Texture;
			}
			
			// Check if it already exists (deserialized from window layout file or scriptable object)
			if (viewState == null)
				viewState = new TreeViewState();

			bool firstInit = m_MultiColumnHeaderState == null;
			var headerState = MerinoTreeView.CreateDefaultMultiColumnHeaderState(sidebarRect.width);
			if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_MultiColumnHeaderState, headerState))
				MultiColumnHeaderState.OverwriteSerializedFields(m_MultiColumnHeaderState, headerState);
			m_MultiColumnHeaderState = headerState;
				
			var multiColumnHeader = new MerinoMultiColumnHeader(headerState);
			if (firstInit)
				multiColumnHeader.ResizeToFit ();

			// detect temp data (e.g. when going into play mode and back)
			var possibleTempData = AssetDatabase.LoadAssetAtPath<MerinoTreeData>(tempDataPath);
			if (possibleTempData == null)
			{
				treeData = ScriptableObject.CreateInstance<MerinoTreeData>();
				AssetDatabase.CreateAsset(treeData, tempDataPath);
				AssetDatabase.SaveAssets();
			}
			else
			{
				treeData = possibleTempData;
			}

			// generate sidebar data structures
			var treeModel = new TreeModel<MerinoTreeElement>(GetData(null, true));
			m_TreeView = new MerinoTreeView(viewState, multiColumnHeader, treeModel);
			m_SearchField = new SearchField();
			m_SearchField.downOrUpArrowKeyPressed += m_TreeView.SetFocusAndEnsureSelectedItem;

			m_Initialized = true;

			if (!ignoreSelection)
			{
				OnSelectionChange();
			}
		}
		
		// called when something get selected in Project tab
		void OnSelectionChange ()
		{
			if (!m_Initialized)
				return;

			var possibleYarnFile = Selection.activeObject as TextAsset;
			if (possibleYarnFile != null && IsProbablyYarnFile(possibleYarnFile)) // possibleYarnFile != currentFile
			{
				// if we already have an Asset selected, make sure it's refreshed
				if (currentFile != null)
				{
					AssetDatabase.ImportAsset( AssetDatabase.GetAssetPath(currentFile));
				}

				// ok load this new file now?
				currentFile = possibleYarnFile;
				viewState.selectedIDs.Clear();
				ForceStopDialogue();
				m_TreeView.treeModel.SetData (GetData ());
				m_TreeView.Reload ();
			}
		}
		
		// This gets called many times a second
		public void Update()
		{
			// small delay before reimporting the asset (otherwise it's too annoying to constantly reimport the asset)
			if (EditorApplication.timeSinceStartup > lastSaveTime + 1.0 && currentFile != null)
			{
				AssetDatabase.ImportAsset( AssetDatabase.GetAssetPath(currentFile));
				lastSaveTime = EditorApplication.timeSinceStartup + 9999; // don't save again until SaveDataToFile resets the variable
			}
			
			if (isDialogueRunning || resizingSidebar || zoomToLineNumber > -1 || lastZoomToLineNumber > -1)
			{
				// MASSIVELY improves framerate, wow, amazing
				Repaint();
			}
		}

		void OnDestroy()
		{
			// if destroyed, make sure the text file got refreshed at least
			if (currentFile != null)
			{
				AssetDatabase.ImportAsset( AssetDatabase.GetAssetPath(currentFile));
			}
			
			Undo.undoRedoPerformed -= OnUndo;
			Undo.ClearUndo(treeData);
			
			undoData.Clear();
			
			ForceStopDialogue();
		}
		
		void OnUndo()
		{
			if (EditorApplication.timeSinceStartup < lastUndoTime + 0.005)
			{
				return;
			}
			lastUndoTime = EditorApplication.timeSinceStartup;
			
			// detect which nodes changed, if any
			// is the current treeData timestamp earlier than our undo log?... if so, then an undo probably happened
			for( int i=undoData.Count-1; i>0 && treeData.timestamp < undoData[i].time; i-- )
			{
				var recent = undoData[i];
				var treeDataNode = treeData.treeElements.First(x => x.id == recent.id);
				
				moveCursorUndoID = recent.id;
				
				int newIndex = CompareStringsAndFindDiffIndex(recent.bodyText, treeDataNode.nodeBody); // TODO: strip all "\" out?
				if (moveCursorUndoIndex == -1 || newIndex >= 0) // sometimes it suddenly spits out -1 when the logging is slightly off, so we need to ignore it in that case
				{
					moveCursorUndoIndex = newIndex;
				}
			}
			
			// TODO: on undo, select the undone node (if not already selected) and frame the undone part / line? (use SelectNodeAndScrollToLine() )
			
			// repaint tree view so names get updated
			m_TreeView.Reload();
			if (currentFile != null && useAutosave)
			{
				SaveDataToFile();
			}
		}
		#endregion
		
		#region LoadingAndSaving
		
		bool IsProbablyYarnFile(TextAsset textAsset)
		{
			if ( AssetDatabase.GetAssetPath(textAsset).EndsWith(".yarn.txt") && textAsset.text.Contains("---") && textAsset.text.Contains("===") && textAsset.text.Contains("title:") )
			{
				return true;
			}
			else
			{
				return false;
			}
		}
		
		IList<MerinoTreeElement> GetData (TextAsset source=null, bool isFromInit=false)
		{
			var treeElements = new List<MerinoTreeElement>();
			var root = new MerinoTreeElement("Root", -1, 0);
			root.children = new List<TreeElement>();
			treeElements.Add(root);
			
			// extract data from the text file, but only if it's not about entering playmode
			if (source == null && currentFile != null )
			{
				source = currentFile;
			}
			
			// hack hack hack: if we're calling GetData from init, then ignore playmode currentFile
			if (isFromInit && EditorApplication.isPlayingOrWillChangePlaymode)
			{
				source = null;
			}

			if (source != null) {
				AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(source));
				//var format = YarnSpinnerLoader.GetFormatFromFileName(AssetDatabase.GetAssetPath(currentFile)); // TODO: add JSON and ByteCode support?
				var nodes = YarnSpinnerLoader.GetNodesFromText(source.text, NodeFormat.Text);
				var parents = new Dictionary<MerinoTreeElement, string>();
				foreach (var node in nodes)
				{
					// clean some of the stuff to help prevent file corruption
					string cleanName = CleanYarnField(node.title, true);
					string cleanBody = CleanYarnField(node.body);
					string cleanTags = CleanYarnField(node.tags, true);
					string cleanParent = string.IsNullOrEmpty(node.parent) ? "" : CleanYarnField(node.parent, true);
					// write data to the objects
					var newItem = new MerinoTreeElement( cleanName, 0, treeElements.Count);
					newItem.nodeBody = cleanBody;
					newItem.nodePosition = new Vector2Int(node.position.x, node.position.y);
					newItem.nodeTags = cleanTags;
					if (string.IsNullOrEmpty(cleanParent) || cleanParent == "Root")
					{
						root.children.Add(newItem);
					}
					else
					{
						parents.Add(newItem, cleanParent); // have to assign parents in a second pass
					}
					treeElements.Add(newItem);
				}
				// second pass: now that all nodes have been created, we can finally assign parents
				foreach (var kvp in parents )
				{
					var parent = treeElements.Where(x => x.name == kvp.Value).ToArray();
					if (parent.Length == 0)
					{
						Debug.LogErrorFormat("Merino couldn't assign parent for node {0}: can't find a parent called {1}", kvp.Key.name, kvp.Value);
					}
					else
					{
						kvp.Key.parent = parent[0];
						if (kvp.Key.parent.children == null)
						{
							kvp.Key.parent.children = new List<TreeElement>();
						}
						kvp.Key.parent.children.Add(kvp.Key);
					}
				}
				// if there's already parent data then I don't really know what the depth value is used for (a cache to speed up GUI drawing?)
				// but I think we're supposed to do this thing so let's do it
				TreeElementUtility.UpdateDepthValues(root);
			}
			else 
			{ 
				// see if we can load temp data
				if (treeData != null && treeData.treeElements != null && treeData.treeElements.Count > 0)
				{
					return treeData.treeElements;
				}
				
				// otherwise, load default data from template
				var defaultData = Resources.Load<TextAsset>(newFileTemplatePath);
				if (defaultData != null)
				{
					return GetData(defaultData);
				}
				else
				{ // oops, couldn't find the new file template!
					Debug.LogErrorFormat("Merino couldn't load default data for a new Yarn file! Looked for /Resources/{0}.txt ... by default, it is in Assets/Merino/Editor/Resources/NewFileTemplate.yarn.txt and the preference is set to [NewFileTemplate.yarn]", newFileTemplatePath);
					return null;
				}
				
			}

			treeData.treeElements = treeElements;
			return treeData.treeElements;
		}

		string CleanYarnField(string inputString, bool extraClean=false)
		{
			if (extraClean)
			{
				return inputString.Replace("===", " ").Replace("---", " ").Replace("title:", " ").Replace("tags:", " ").Replace("position:", " ").Replace("colorID:", " ");
			}
			else
			{
				return inputString.Replace("===", " ").Replace("---", " ");
			}
		}

		// used internally for playtest preview, but also by SaveDataToFile
		string SaveNodesAsString()
		{
			if (treeData == null)
			{
				Debug.LogError("Merino TreeData got corrupted somehow! Trying to reinitialize... but if you can reproduce this, please file a bug report at https://github.com/radiatoryang/merino/issues");
				m_Initialized = false;
				InitIfNeeded();
			}
			
			// gather data
			ValidateNodeTitles();
			var nodeInfo = new List<YarnSpinnerLoader.NodeInfo>();
			
			// grab nodes based on visible order in the hierarchy tree view (sorting)
			
			// first, in order to properly export, we need to expand everything
			var previousExpanded = treeView.GetExpanded();
			treeView.ExpandAll();
			// then grab the nodes
			var treeNodes = treeView.GetRows().Select(x => treeView.treeModel.Find(x.id)).ToArray(); // treeData.treeElements; // m_TreeView.treeModel.root.children;
			// then set the expanded nodes back to what they were
			treeView.SetExpanded(previousExpanded);
			
			// save data to string
			foreach (var item in treeNodes)
			{
				// skip the root
				if (item.depth == -1)
				{
					continue;
				}
				
				var itemCasted = (MerinoTreeElement) item;
				var newNodeInfo = new YarnSpinnerLoader.NodeInfo();

				newNodeInfo.title = itemCasted.name;
				newNodeInfo.body = itemCasted.nodeBody;
				newNodeInfo.tags = itemCasted.nodeTags;
				var newPosition = new YarnSpinnerLoader.NodeInfo.Position();
				newPosition.x = itemCasted.nodePosition.x;
				newPosition.y = itemCasted.nodePosition.y;
				newNodeInfo.position = newPosition;
				newNodeInfo.parent = itemCasted.parent.name;

				nodeInfo.Add(newNodeInfo);
			}

			return YarnSpinnerFileFormatConverter.ConvertNodesToYarnText(nodeInfo);
		}

		// writes data to the file
		double lastSaveTime;
		void SaveDataToFile()
		{
			if (currentFile != null)
			{
				File.WriteAllText(AssetDatabase.GetAssetPath(currentFile), SaveNodesAsString() );
				EditorUtility.SetDirty(currentFile);
				lastSaveTime = EditorApplication.timeSinceStartup;
			}
		}
		#endregion
		
		#region NodeManagement

		void AddNewNode()
		{
			var newID = m_TreeView.treeModel.GenerateUniqueID();
			var newNode = new MerinoTreeElement("NewNode" + newID.ToString(), 0, newID);
			newNode.nodeBody = "Write stuff here.";
			m_TreeView.treeModel.AddElement(newNode, m_TreeView.treeModel.root, 0);
			m_TreeView.FrameItem(newID);
			m_TreeView.SetSelection( new List<int>() {newID} );
			SaveDataToFile();
		}

		public void AddNodeToDelete(int id)
		{
			if (!EditorUtility.DisplayDialog("Delete Node?",
				"Are you sure you want to delete " + m_TreeView.treeModel.Find(id).name + "?", "Delete", "Cancel"))
			{
				return;
			}

			DeleteList.Add(id);
		}

		public void AddNodeToDelete(IList<int> ids)
		{
			if (ids.Count == 1)
			{
				AddNodeToDelete(ids[0]);
				return;
			}
			
			if (EditorUtility.DisplayDialog("Delete Nodes?",
				"Are you sure you want to delete " + ids.Count + " nodes?", "Delete", "Cancel"))
			{
				DeleteList.AddRange(ids);
			}
		}

		public void DeleteNodes()
		{
			if (DeleteList.Count <= 0)
			{
				return;
			}

			// orphans are children of deleted nodes; if you delete their parent without reparenting them, the orphans won't get saved
			// because they no longer have a parent in the treeview (and the save data relies on what's visible in the tree view)
			// ... so, let's reparent them to their nearest parent
			var orphans = new List<TreeElement>(); 
			foreach (var id in DeleteList)
			{
				var children = m_TreeView.treeModel.Find(id).children;
				if (m_TreeView.treeModel.Find(id) != null && children != null)
				{
					orphans.AddRange(children);
				}
			}
			foreach (var child in orphans)
			{
				var oldParent = child.parent;
				
				// keep searching for new parents
				while (DeleteList.Contains(child.parent.id))
				{
					child.parent = child.parent.parent; // eventually, this will end up at root, which can never be deleted
				}
				// make sure the new parent knows about it too
				oldParent.children.Remove(child);
				if (child.parent.children == null)
				{
					child.parent.children = new List<TreeElement>();
					
				}
				child.parent.children.Add( child );
			}
			
			// ok now delete the nodes + remove any from the current selection
			m_TreeView.treeModel.RemoveElements(DeleteList);
			foreach (var id in DeleteList)
			{
				if (viewState.selectedIDs.Contains(id))
				{
					viewState.selectedIDs.Remove(id);
				}
			}
		
			SaveDataToFile();
			DeleteList.Clear();
			TreeElementUtility.UpdateDepthValues(m_TreeView.treeModel.root);
		}
		
		// ensure unique node titles, very important for YarnSpinner
		void ValidateNodeTitles()
		{
			var treeNodes = treeData.treeElements; // m_TreeView.treeModel.root.children;
			// validate data: ensure unique node names
			var nodeTitles = new Dictionary<int,string>(); // index, newTitle
			bool doRenaming = false;
			for (int i=0;i<treeNodes.Count;i++)
			{
				// if there's a node already with that name, then append unique suffix
				if (nodeTitles.Values.Contains(treeNodes[i].name))
				{
					nodeTitles.Add(i, treeNodes[i].name + "_" + Path.GetRandomFileName().Split('.')[0]);
					doRenaming = true;
				}
				else
				{ // otherwise, business as usual
					nodeTitles.Add(i, treeNodes[i].name);
				}
			}
			if (doRenaming)
			{
				string renamedNodes = "Merino found nodes with duplicate names (which aren't allowed for Yarn) and renamed them. This might break some of your scripting, you can undo it. The following nodes were renamed: ";
				Undo.RecordObject(treeData, "Merino: AutoRename");
				foreach (var kvp in nodeTitles)
				{
					if (treeData.treeElements[kvp.Key].name != kvp.Value)
					{
						renamedNodes += string.Format("\n* {0} > {1}", treeNodes[kvp.Key].name, kvp.Value);
						treeData.treeElements[kvp.Key].name = kvp.Value;
					}
				}
				EditorUtility.SetDirty(treeData);
				Debug.LogWarning(renamedNodes + "\n\n");
				// repaint tree view so names get updated
				m_TreeView.Reload();
			}
		}
		#endregion
		
		#region PlaytestPreview
		void PlaytestFrom(string startPassageName, bool reset=true)
		{
			if (reset)
			{
				errorLog.Clear();
				dialogue.UnloadAll();
				varStorage.ResetToDefaults();
				try
				{
					var program = SaveNodesAsString();
					if (!string.IsNullOrEmpty(program))
					{
						dialogue.LoadString(program);
					}
				}
				catch (Exception ex)
				{
					PlaytestErrorLog(ex.Message);
					return;
				}
				
			}
			this.StopAllCoroutines();

			// use EditorCoroutines to run the dialogue
			this.StartCoroutine(RunDialogue(startPassageName));
		}
		
		void ForceStopDialogue()
		{
			isDialogueRunning = false;
			if (dialogue != null)
			{
				dialogue.Stop();
			}

			this.StopAllCoroutines();
		}

		// logs errors from the playtest engine and Yarn Loader
		public void PlaytestErrorLog(string message)
		{
			string fileName = "unknown";
			string nodeName = "unknown";
			int lineNumber = -1;
			
			// detect file name
			if (currentFile != null)
			{
				fileName = currentFile.name;
			} 
			else if (message.Contains("file"))
			{
				fileName = message.Split(new string[] {"file"}, StringSplitOptions.None)[1].Split(new string[] {" ", ":"}, StringSplitOptions.None)[1];
			}
			
			// detect node name
			if (message.Contains("node"))
			{
				nodeName = message.Split(new string[] {"node"}, StringSplitOptions.None)[1].Split(new string[] {" ", ":"}, StringSplitOptions.None)[1];
				
				// detect line numbers, if any, by grabbing the first digit after nodeName
				string numberLog = "";
				for( int index = message.IndexOf(nodeName); index < message.Length; index++)
				{
					if (Char.IsDigit(message[index]))
					{
						numberLog += message[index];
					}
					else if ( numberLog.Length > 0) // did we hit a non-number, after already hitting numbers? then stop
					{
						break;
					}
				}

				int.TryParse(numberLog, out lineNumber);
			}
			
			// also output to Unity console
			Debug.LogError(message);
			// Debug.LogFormat("{0} {1} {2}", fileName, nodeName, lineNumber);
			var nodeRef = treeData.treeElements.Where(x => x.name == nodeName).ToArray();
			errorLog.Add( new MerinoErrorLine(message, fileName, nodeRef.Length > 0 ? nodeRef[0].id : -1, Mathf.Max(0, lineNumber)));
		}

		// used by the playtest toolbar View Node Source button
		int GuessLineNumber(int nodeID, string lineText)
		{
			var node = treeView.treeModel.Find(nodeID);
			// this is a really bad way of doing it, but Yarn Spinner DialogueRunner doesn't offer any access to line numbers
			if (node != null && node.nodeBody.Contains(lineText))
			{
				var lines = node.nodeBody.Split('\n');
				for (int i = 0; i < lines.Length; i++)
				{
					if (lines[i].Contains(lineText))
					{
						return i + 1;
					}
				}
				return -1;
			}
			else
			{
				if (node == null)
				{
					Debug.LogWarning("Merino couldn't find node ID " + nodeID.ToString() + "... it might've been deleted or the Yarn file might be corrupted.");
				}

				return -1;
			}
		}

		// this is basically just ripped from YarnSpinner/DialogueRunner.cs
		IEnumerator RunDialogue (string startNode = "Start")
        {        
            // Mark that we're in conversation.
            isDialogueRunning = true;

            // Signal that we're starting up.
            //  yield return this.StartCoroutine(this.dialogueUI.DialogueStarted());

            // Get lines, options and commands from the Dialogue object,
            // one at a time.
            foreach (Yarn.Dialogue.RunnerResult step in dialogue.Run(startNode))
            {
	            dialogueUI.currentNode = dialogue.currentNode;

                if (step is Yarn.Dialogue.LineResult) {

                    // Wait for line to finish displaying
                    var lineResult = step as Yarn.Dialogue.LineResult;
	                yield return this.StartCoroutine(this.dialogueUI.RunLine(lineResult.line, useAutoAdvance));
//	                while (dialogueUI.inputContinue == false)
//	                {
//		                yield return new WaitForSeconds(0.01f);
//	                }

                } else if (step is Yarn.Dialogue.OptionSetResult) {

                    // Wait for user to finish picking an option
                    var optionSetResult = step as Yarn.Dialogue.OptionSetResult;
	                dialogueUI.RunOptions(optionSetResult.options, optionSetResult.setSelectedOptionDelegate);
	                while (dialogueUI.inputOption < 0)
	                {
		                yield return new WaitForSeconds(0.01f);
	                }

                } else if (step is Yarn.Dialogue.CommandResult) {

                    // Wait for command to finish running

                    var commandResult = step as Yarn.Dialogue.CommandResult;

//                    if (DispatchCommand(commandResult.command.text) == true) {
//                        // command was dispatched
//                    } else {
	                yield return this.StartCoroutine( dialogueUI.RunCommand(commandResult.command, useAutoAdvance));
//                    }


                } else if(step is Yarn.Dialogue.NodeCompleteResult) {

                    // Wait for post-node action
                    // var nodeResult = step as Yarn.Dialogue.NodeCompleteResult;
                    // yield return this.StartCoroutine (this.dialogueUI.NodeComplete (nodeResult.nextNode));
                }
            }
	        Debug.Log("Merino: reached the end of the dialogue.");
	        
            // No more results! The dialogue is done.
            this.dialogueUI.DialogueComplete ();
	        while (stopOnDialogueEnd==false)
	        {
		        yield return new WaitForSeconds(0.01f);
	        }

            // Clear the 'is running' flag. We do this after DialogueComplete returns,
            // to allow time for any animations that might run while transitioning
            // out of a conversation (ie letterboxing going away, etc)
            isDialogueRunning = false;
	        Repaint();
        }
		#endregion
		
		void OnGUI ()
		{
			InitIfNeeded();

			DrawSidebarSearch (sidebarSearchRect);
			DrawSidebar (sidebarRect);
			ResizeSidebar();

			if (viewState != null)
			{
				DrawMainPane(nodeEditRect);
			}

			if (dialogueUI != null && isDialogueRunning)
			{
				DrawPlaytestToolbar(playPreviewRect);
				// if not pressed, then show the normal play preview
				dialogueUI.OnGUI(playPreviewRect);
			}

			DrawBottomToolBar (bottomToolbarRect);
		}

		const int sidebarWidthClamp = 50;
		
		// from https://answers.unity.com/questions/546686/editorguilayout-split-view-resizable-scroll-view.html
		private void ResizeSidebar(){
			EditorGUIUtility.AddCursorRect( sidebarResizeRect,MouseCursor.SplitResizeLeftRight);
         
			// start resizing
			if( Event.current.type == EventType.MouseDown && sidebarResizeRect.Contains(Event.current.mousePosition))
			{
				resizingSidebar = true;
			}
			
			// do resize
			if(resizingSidebar){
				sidebarWidth = Mathf.Clamp(Event.current.mousePosition.x - 10, sidebarWidthClamp, position.width-sidebarWidth);
			//	cursorChangeRect.Set(cursorChangeRect.x,currentScrollViewHeight,cursorChangeRect.width,cursorChangeRect.height);
			}

			// stop resizing
			if (Event.current.rawType == EventType.MouseUp)
			{
				resizingSidebar = false;
				SaveHiddenEditorPrefs();
			}
		}

		void DrawPlaytestToolbar(Rect rect)
		{
			// toolbar
			GUILayout.BeginArea(rect);
			var toolbarStyle = new GUIStyle(EditorStyles.toolbar);
			toolbarStyle.alignment = TextAnchor.MiddleCenter;
			EditorGUILayout.BeginHorizontal(toolbarStyle, GUILayout.ExpandWidth(true));

			// jump to node button, but only if dialogue is already running
			var jumpOptions = dialogue.allNodes.ToList();
			var currentJumpIndex = jumpOptions.IndexOf(dialogue.currentNode);
			bool dialogueEnded = false;
			if (dialogue.currentNode == null)
			{ // if there is no current node, then tell them that
				dialogueEnded = true;
				currentJumpIndex = 0;
				jumpOptions.Insert(0, "<Stopped> Jump to Node?...");
			}
			var newJumpIndex = EditorGUILayout.Popup(
				new GUIContent("", "the node you're currently playing; you can also jump to any other node"), 
				currentJumpIndex, 
				jumpOptions.Select( 
					x => x.StartsWith("<Stopped>") ? new GUIContent(x) : new GUIContent("Node: " + x)
				).ToArray(), 
				EditorStyles.toolbarPopup, 
				GUILayout.Width(160)
			);
			if (currentJumpIndex != newJumpIndex)
			{
				if ( !dialogueEnded || newJumpIndex > 0) {
					PlaytestFrom(jumpOptions[newJumpIndex], false);
				}
			}
			GUILayout.Space(4);
			
			// current node button
			GUI.enabled = !dialogueEnded;
			if (dialogue.currentNode != null && GUILayout.Button(new GUIContent("View Node Source", string.Format("click to see Yarn script code for this node")), EditorStyles.toolbarButton))
			{
				var matchingNode = treeData.treeElements.Find(x => x.name == dialogue.currentNode);
				if (matchingNode != null)
				{
					SelectNodeAndZoomToLine(matchingNode.id, GuessLineNumber(matchingNode.id, dialogueUI.displayStringFull) );
				}
				else if ( dialogue != null && dialogue.currentNode != null )
				{
					Debug.LogWarning("Marino couldn't find the node " + dialogue.currentNode + "... it might've been deleted or the Yarn file is corrupted.");
				}
			}
			GUI.enabled = true;

			GUILayout.FlexibleSpace();
			
			// begin some settings
			EditorGUI.BeginChangeCheck();
			var smallToggleStyle = new GUIStyle();
			smallToggleStyle.fontSize = 10;
			smallToggleStyle.alignment = TextAnchor.MiddleLeft;
			if (EditorGUIUtility.isProSkin)
			{
				smallToggleStyle.normal.textColor = Color.gray;
				
			}
			// auto advance button
			useAutoAdvance = EditorGUILayout.ToggleLeft(new GUIContent("AutoAdvance", "automatically advance dialogue, with no user input, until there's a choice"), useAutoAdvance, smallToggleStyle, GUILayout.Width(100));
			GUILayout.FlexibleSpace();
			// stop on dialogue end button
			stopOnDialogueEnd = EditorGUILayout.ToggleLeft(new GUIContent("CloseOnEnd", "when dialogue terminates, stop and close playtest session automatically"), stopOnDialogueEnd, smallToggleStyle, GUILayout.Width(100));
			if (EditorGUI.EndChangeCheck())
			{
				SaveHiddenEditorPrefs(); // remember new settings
			}
			
			// stop button
			var backupColor = GUI.backgroundColor;
			GUI.backgroundColor = Color.red;
			if (GUILayout.Button(new GUIContent("Close", "click to force stop the playtest preview and close it"), EditorStyles.toolbarButton))
			{
				ForceStopDialogue();
			}
			GUI.backgroundColor = backupColor;
			
			// close out toolbar
			EditorGUILayout.EndHorizontal();
			GUILayout.EndArea();
		}
		
		void DrawSidebarSearch (Rect rect)
		{
			treeView.searchString = m_SearchField.OnGUI (rect, treeView.searchString);
		}

		void DrawSidebar (Rect rect)
		{
			float BUTTON_HEIGHT = 20;
			var buttonRect = rect;
			buttonRect.height = BUTTON_HEIGHT;

			bool addNewNode = false;
			EditorGUI.BeginChangeCheck();
			addNewNode = GUI.Button(buttonRect, "+ NEW NODE");
			if (EditorGUI.EndChangeCheck())
			{
//				Undo.RecordObject(treeData, "Merino: add new node");
				if (addNewNode)
				{
					AddNewNode();
				}
			}
			rect.y += BUTTON_HEIGHT;
			rect.height -= BUTTON_HEIGHT;
			m_TreeView.OnGUI(rect);
		}
				
		void DrawMainPane(Rect rect)
		{
			GUILayout.BeginArea(rect);
			
			GUILayout.BeginHorizontal();
			
			// autosave / save button
			if (currentFile != null)
			{
				EditorGUI.BeginChangeCheck();
				useAutosave = EditorGUILayout.Toggle(new GUIContent("", "if enabled, will automatically save every change"), useAutosave, GUILayout.Width(16));
				GUILayout.Label(new GUIContent("AutoSave?   ", "if enabled, will automatically save every change"), GUILayout.Width(0), GUILayout.ExpandWidth(true), GUILayout.MaxWidth(80) );
				if (EditorGUI.EndChangeCheck())
				{
					SaveHiddenEditorPrefs();
				}
				if (!useAutosave && GUILayout.Button( new GUIContent("Save", "save all changes to the current .yarn.txt file"), GUILayout.MaxWidth(60)))
				{
					SaveDataToFile();
					AssetDatabase.ImportAsset( AssetDatabase.GetAssetPath(currentFile));
				}
			}
			
			// save as button
			if (GUILayout.Button( new GUIContent("Save As...", "save all changes as a new .yarn.txt file"), GUILayout.MaxWidth(80)))
			{
				string defaultPath = Application.dataPath + "/";
				string defaultName = "NewYarnStory";
				if (currentFile != null)
				{
					defaultPath = Application.dataPath.Substring(0, Application.dataPath.Length - 6) + AssetDatabase.GetAssetPath(currentFile);
					defaultName = currentFile.name.Substring(0, currentFile.name.Length - 5);
				}
				string fullFilePath = EditorUtility.SaveFilePanel("Merino: save yarn.txt", Path.GetDirectoryName(defaultPath), defaultName, "yarn.txt");
				if (fullFilePath.Length > 0)
				{
					File.WriteAllText(fullFilePath, "");
					AssetDatabase.Refresh();
					currentFile = AssetDatabase.LoadAssetAtPath<TextAsset>( "Assets" + fullFilePath.Substring(Application.dataPath.Length));
					SaveDataToFile();
					AssetDatabase.ImportAsset( AssetDatabase.GetAssetPath(currentFile));
				}
			}

			// NEW FILE BUTTON
			if (GUILayout.Button(new GUIContent("New File", "will throw away any unsaved changes, and reset Merino to a new blank file"), GUILayout.MaxWidth(80)))
			{
				if (EditorUtility.DisplayDialog("Merino: New File?",
					"Are you sure you want to create a new file? All unsaved work will be lost.", "Create New File", "Cancel"))
				{
					ResetMerino();
					return;
				}
			}
			GUILayout.Space(10);

			EditorGUI.BeginChangeCheck();
			var newFile = (TextAsset)EditorGUILayout.ObjectField(currentFile, typeof(TextAsset), false);
			if (EditorGUI.EndChangeCheck())
			{
				currentFile = newFile;
				if (currentFile != null && IsProbablyYarnFile(currentFile))
				{
					m_TreeView.treeModel.SetData(GetData());
					m_TreeView.Reload();
				}
				else if (currentFile != null)
				{
					Debug.LogWarningFormat(currentFile, "Merino: the TextAsset at {0} doesn't seem to be a valid .yarn.txt file. Can't load it. Sorry.", AssetDatabase.GetAssetPath(currentFile));
					currentFile = null;
				}
				else
				{
					currentFile = null;
					ResetMerino();
				}
			}
			GUILayout.FlexibleSpace();
			// help and documentation, with short and long buttons
			if ( (position.width <= 800 && GUILayout.Button(new GUIContent(helpIcon, "click to open Merino help page / documentation in web browser"), EditorStyles.label, GUILayout.Width(24)) )
				|| (position.width > 800 && GUILayout.Button(new GUIContent(" Help & Documentation", helpIcon, "click to open Merino help page / documentation in web browser"), GUILayout.Width(160))) )
			{
				Help.BrowseURL("https://github.com/radiatoryang/merino/wiki");
			}
			GUILayout.EndHorizontal();

//			bool forceSave = false;
			int idToPreview = -1;
			if (viewState.selectedIDs.Count > 0)
			{
				scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
				
				foreach (var id in viewState.selectedIDs)
				{
					// start node container
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.BeginVertical(EditorStyles.helpBox);
					
					// NODE HEADER ======================================================================
					EditorGUILayout.BeginHorizontal();
					
					// add "Play" button
					if (GUILayout.Button(new GUIContent("▶", "click to playtest this node"), GUILayout.Width(24) ) )
					{
						idToPreview = id;
					}
					
					// node title
					var nameStyle = new GUIStyle( EditorStyles.textField );
					nameStyle.font = monoFont;
					nameStyle.fontSize = 16;
					nameStyle.fixedHeight = 20f;
					string newName = EditorGUILayout.TextField(m_TreeView.treeModel.Find(id).name, nameStyle);
					GUILayout.FlexibleSpace();
					
					// delete button
					if (GUILayout.Button("Delete Node", GUILayout.Width(100)))
					{
						AddNodeToDelete(id);
					}
					EditorGUILayout.EndHorizontal();

					
					// NODE BODY ======================================================================
					var backupContentColor = GUI.contentColor;
					string passage = m_TreeView.treeModel.Find(id).nodeBody;
					float height = EditorStyles.textArea.CalcHeight(new GUIContent(passage), rect.width);
					
					// start preparing to draw the body
					int lineDigits = -1;
					int totalLineCount = -1;
					int[] lineToCharIndex; // used to save charIndex for the start of each line number, so we can later calculate rects for these line numbers if we have to
					string lineNumbers = AddLineNumbers(passage, out lineToCharIndex, out lineDigits, out totalLineCount);
					var bodyStyle = new GUIStyle( EditorStyles.textArea );
					bodyStyle.font = monoFont;
					bodyStyle.margin = new RectOffset(lineDigits * 12 + 10, 4, 4, 4); // make room for the line numbers!!!
					
					// at around 250-300+ lines, Merino was giving error messages and line numbers broke: "String too long for TextMeshGenerator. Cutting off characters."
					// because Unity EditorGUI TextArea has a limit of 16382 characters, extra-long nodes must be chunked into multiple TextAreas (let's say, every 200 lines, just to be safe)
					const int chunkSize = 200;
					// split passage and lineNumbers into lines
					var linebreak = new string[] {"\n"};
					string[] passageLines = passage.Split(linebreak, StringSplitOptions.None);
					string[] numberLines = lineNumbers.Split(linebreak, StringSplitOptions.None);
					
					// recombine into lines chunks of 200 lines
					int chunkCount = Mathf.CeilToInt(1f * passageLines.Length / chunkSize);
					string[] passageChunks = new string[chunkCount];
					string[] numberChunks = new string[chunkCount];
					for (int i = 0; i < passageLines.Length; i+=chunkSize)
					{
						int chunkIndex = Mathf.CeilToInt(1f * i / chunkSize);
						passageChunks[chunkIndex] = string.Join( linebreak[0], passageLines.Skip(i).Take(chunkSize).ToArray() );
						numberChunks[chunkIndex] = string.Join( linebreak[0], numberLines.Skip(i).Take(chunkSize).ToArray() );
					}
					
					// draw chunks as TextAreas, each with their own highlighting and line number overlays
					var newBodies = new string[chunkCount];
					for( int chunkIndex=0; chunkIndex<passageChunks.Length; chunkIndex++) {
						int chunkStart = chunkIndex * chunkSize; // line number this chunk starts at
						int chunkEnd = (chunkIndex + 1) * chunkSize; // line number this chunk ends at
						
						GUI.contentColor = new Color ( 0f, 0f, 0f, 0.16f ); // the text you type is actually invisible
						string controlName = "TextArea" + newName + chunkIndex.ToString();
						GUI.SetNextControlName ( controlName );
						
						// draw text area
						int nextControlID = GUIUtility.GetControlID(FocusType.Passive) + 1;
						newBodies[chunkIndex] = EditorGUILayout.TextArea(passageChunks[chunkIndex], bodyStyle, GUILayout.Height(0f), GUILayout.ExpandHeight(true), GUILayout.MaxHeight(height));
						GUI.contentColor = backupContentColor;
						var bodyRect = GUILayoutUtility.GetLastRect(); // we need the TextArea rect for the line number and syntax highlight overlays
						
						// stuff for manual keyboard tab support, but also any per-word or per-line features
//						var te = (TextEditor)GUIUtility.GetStateObject( typeof (TextEditor), GUIUtility.keyboardControl );
						// we have to use reflection to access TextEditors for EditorGUI TextAreas, but it's worth it
						var te = typeof(EditorGUI).GetField("activeEditor", BindingFlags.Static | BindingFlags.NonPublic).GetValue(null) as TextEditor;
						
						if (zoomID == id && zoomToLineNumber > chunkStart && zoomToLineNumber < chunkEnd)
						{
							// focus control on this TextArea, so that we can use TextEditor to zoom to a line number
							GUI.FocusControl(controlName);
							EditorGUI.FocusTextInControl(controlName);
							GUIUtility.keyboardControl = nextControlID;
						}
						
						// line number style
						GUIStyle highlightOverlay = new GUIStyle();
						highlightOverlay.border = bodyStyle.border;
						highlightOverlay.padding = bodyStyle.padding;
						highlightOverlay.font = monoFont;
						highlightOverlay.normal.textColor = (EditorGUIUtility.isProSkin ? Color.white : Color.black) * 0.45f;
						highlightOverlay.richText = true;
						highlightOverlay.wordWrap = true;
						// line number positioning (just slightly to the left)
						Rect linesRect = new Rect(bodyRect);
						linesRect.x -= lineDigits * 12;
						// draw the line numbers
						GUI.Label(linesRect, numberChunks[chunkIndex], highlightOverlay);
						
						// syntax highlight via label overlay
						Rect lastRect = new Rect(bodyRect);
//						lastRect.x += 2;
//						lastRect.y += 1f;
//						lastRect.width -= 6;
//						lastRect.height -= 1;
						highlightOverlay.normal.textColor = (EditorGUIUtility.isProSkin ? Color.white : Color.black) * 0.8f;
						string syntaxHighlight = DoSyntaxMarch(newBodies[chunkIndex]); // inserts richtext <color> tags to do highlighting
						GUI.Label(lastRect, syntaxHighlight, highlightOverlay); // drawn on top of actual TextArea
					
						// special functions that rely on TextEditor: undo spacing and character-based positioning (error bubbles, inline syntax highlights)
						if (te != null && GUIUtility.keyboardControl == te.controlID)
						{						
							// undo support: move back keyboard cursor (caret) to undo point... minor quality of life that's actually kind of major
							if (moveCursorUndoID == id && moveCursorUndoIndex >= 0)
							{
								te.cursorIndex = moveCursorUndoIndex;
								te.selectIndex = moveCursorUndoIndex;
								// moveCursorUndo* will get blanked out after the foreach node loop
							}

							// detect whether to increment the undo group based on typing whitespace / word breaks
							var eventCurrent = Event.current;
							if (spaceWillIncrementUndo && eventCurrent.isKey && (eventCurrent.keyCode == KeyCode.Space || eventCurrent.keyCode == KeyCode.Tab || eventCurrent.keyCode == KeyCode.Return))
							{
								Undo.IncrementCurrentGroup();
								spaceWillIncrementUndo = false;
							}

							// if user presses something other than SPACE, then let whitespace increment undo again
							if (!spaceWillIncrementUndo && eventCurrent.isKey && eventCurrent.keyCode != KeyCode.Space)
							{
								spaceWillIncrementUndo = true;
							}

							// now, anything that has to do with counting lines or word-level alignment
							
							// first, display any error bubbles associated with line numbers for this node chunk
							// ... which is surprisingly complicated

							// important: have to clamp the error's line number here! e.g. error might say line 201 even though only 199 lines total displayed
							// also must clamp now before the Where(), because that chunk range check needs to account for the clamp
							var errors = errorLog.Select(e => {
								e.lineNumber = Mathf.Clamp(e.lineNumber, 0, lineToCharIndex.Length-1); // (why length-1? clamp range is inclusive)
								return e;
							}).Where(e => e.nodeID == id && e.lineNumber > chunkStart && e.lineNumber < chunkEnd).ToArray(); // grab errors only for this nodeID && in this chunk

							// ok, um, because we're chunking the textBody into multiple textAreas, the original lineToCharIndex doesn't go to the proper offset
							// (lineToCharIndex used passage, not passageChunks[]... unless we subtract all previous chunk char lengths too... thus, accounting for all previous chunks)
							int chunkCharOffset = 0;
							for (int c = 0; c < chunkIndex; c++)
							{
								chunkCharOffset += newBodies[c].Length + 1; // add +1 for "\n" between the chunks
							}

							// prep the rest of our data for drawing error bubbles on line numbers...
							var errorLinesToIndices = errors.Select(e => lineToCharIndex[e.lineNumber] - chunkCharOffset).ToArray(); // change errors' line numbers into string index
							var indicesToRects = CalculateTextEditorIndexToRect(te, bodyStyle, errorLinesToIndices); // change errors' string index to rect
							for (int i = 0; i < errors.Length; i++)
							{
								// place error bubble near line number
								Rect errorRect = new Rect(bodyRect.x - 20, indicesToRects[i].y+2, bodyRect.width + 20, 22);
								var backupBGColor = GUI.backgroundColor;
								GUI.backgroundColor = new Color(0.9f, 0.3f, 0.25f, 0.62f);
								GUI.Box( errorRect, "", EditorStyles.helpBox); // shaded line bg highlight
								errorRect = new Rect(bodyRect.x - 20, indicesToRects[i].y+4, 32, 32);
								EditorGUIUtility.AddCursorRect( errorRect,MouseCursor.Zoom);
								// user can press the button to dismiss the error
								if (GUI.Button(errorRect, new GUIContent(errorIcon, string.Format(compileErrorHoverString, errors[i].message)), EditorStyles.label))
								{
									errorLog.Remove(errorLog.First(e => e.message == errors[i].message)); // have to use LINQ to find the original ErrorLine object
								}

								GUI.backgroundColor = backupBGColor;
							}
							
							// lastly, if we're zooming to a line number, then let's scroll to it
							if (lastZoomToLineNumber > -1 && zoomID == id )
							{
								// double-check we should be rendering it in this chunk though
								int clampedZoomLine = Mathf.Clamp(lastZoomToLineNumber-1, 0, lineToCharIndex.Length - 1);
								int charCursorIndex = lineToCharIndex[clampedZoomLine] - chunkCharOffset;
								if (clampedZoomLine > chunkStart && clampedZoomLine < chunkEnd)
								{
									var zoomRect = CalculateTextEditorIndexToRect(te, bodyStyle, new int[] {charCursorIndex});

									// if we haven't scrolled to the currect line yet, then do it
									if (zoomToLineNumber > -1)
									{
										te.cursorIndex = charCursorIndex;
										te.selectIndex = charCursorIndex;
										scrollPos = new Vector2(0f, zoomRect[0].y - nodeEditRect.height * 0.5f); // center line in middle of rect, if possible
										zoomToLineNumber = -1;
									}
									
									// highlight the line we zoomed to, and fade out slowly
									if (lastZoomToLineNumber > -1 && EditorApplication.timeSinceStartup - zoomToLineNumberTimestamp < zoomLineFadeTime)
									{
										var backupColor = GUI.color;
										GUI.color = new Color(0.9f, 0.45f, 0.3f, 1f - (float)((EditorApplication.timeSinceStartup - zoomToLineNumberTimestamp)/ zoomLineFadeTime) );
										GUI.DrawTexture( new Rect(bodyRect.x, zoomRect[0].y, bodyRect.width, 22), EditorGUIUtility.whiteTexture );
										GUI.color = backupColor;
									}
								}
								
							}

						} // end of if( TextEditor != null )
					} // end of textAreas
					
					// combine all body texts into a single string for saving
					var newBody = string.Join("\n", newBodies);

					// playtest preview button at bottom of node
					if (GUILayout.Button(new GUIContent("▶ Playtest", "click to playtest this node"), GUILayout.Width(80) ) )
					{
						idToPreview = id;
					}
 
					// close out node container				
					EditorGUILayout.EndVertical();
					EditorGUILayout.Space();
					EditorGUILayout.Separator();
					
					// did user edit something?
					if (EditorGUI.EndChangeCheck() )
					{
						// remember last edited node, for the "playtest this" button in lower-right corner
						currentNodeIDEditing = id;
						
						// undo begin
						Undo.RecordObject(treeData, "Merino > " + newName );
						
						// actually commit the data now
						m_TreeView.treeModel.Find(id).name = newName;
						m_TreeView.treeModel.Find(id).nodeBody = newBody;
						treeData.editedID = id;
						treeData.timestamp = EditorApplication.timeSinceStartup;
						
						// log the undo data
						undoData.Add( new MerinoUndoLog(id, EditorApplication.timeSinceStartup, newBody) );
						
						// save after commit if we're autosaving
						if (currentFile != null && useAutosave)
						{
							SaveDataToFile();
						}
						
						// repaint tree view so names get updated
						m_TreeView.Reload();
					}
				} // end foreach selected node ID

				DeleteNodes();
				moveCursorUndoID = -1;
				moveCursorUndoIndex = -1;
				
				// fail-safe, just in case something goes wrong with the zoomToLineNumber thing
				if (EditorApplication.timeSinceStartup - zoomToLineNumberTimestamp > zoomLineFadeTime)
				{
					zoomToLineNumber = -1;
					lastZoomToLineNumber = -1;
				}
				
				GUI.enabled = true;
				EditorGUILayout.EndScrollView();
				
				// if we only have one node selected, then let's just say that's the node we're working with
				if (viewState.selectedIDs.Count == 1)
				{
					currentNodeIDEditing = viewState.selectedIDs[0];
				}
				
				// detect if we need to do play preview
				if (idToPreview > -1)
				{
					PlaytestFrom( m_TreeView.treeModel.Find(idToPreview).name, !isDialogueRunning);
				}
			}
			else
			{
				if ( currentFile == null ) { EditorGUILayout.HelpBox(" To edit a Yarn.txt file, select it in your Project tab OR use the TextAsset object picker above.\n ... or, just work with the default template here, and then click [Save As].", MessageType.Info);}
				EditorGUILayout.HelpBox(" Select node(s) from the sidebar on the left.\n - Left-Click:  select\n - Left-Click + Shift:  select multiple\n - Left-Click + Ctrl / Left-Click + Command:  add / remove from selection", MessageType.Info);
			}

			GUILayout.EndArea();
		}
		
		#region MainPaneUtility

		// I can't believe I finally got this to work, wow
		// only needed for GUILayout.TextArea
//		string KeyboardTabSupport(string text, TextEditor te)
//		{
//			if ( GUIUtility.keyboardControl == te.controlID && Event.current.type != EventType.Layout && (Event.current.keyCode == KeyCode.Tab || Event.current.character == '\t') )
//			{
//				int cursorIndex = te.cursorIndex;
//				GUI.FocusControl("TextArea");
//				if (text.Length > te.cursorIndex && EditorApplication.timeSinceStartup > lastTabTime + 0.2)
//				{
//					lastTabTime = EditorApplication.timeSinceStartup;
//					text = text.Insert(te.cursorIndex, "\t");
//					te.cursorIndex = cursorIndex + 1;
//					te.selectIndex = te.cursorIndex;
//				}
//
//				Event.current.Use();
//				GUI.FocusControl("TextArea");
//			}
//			return text;
//		}

		string DoSyntaxMarch(string text)
		{
			var textLines = text.Split('\n');
			for (int i = 0; i < textLines.Length; i++)
			{
				var newColor = CheckSyntax(textLines[i]);
				if (newColor != Color.white)
				{
					textLines[i] = string.Format("<color=#{0}>{1}</color>", ColorUtility.ToHtmlStringRGB(newColor), textLines[i]);
				}
			}

			return string.Join("\n", textLines);
		}

		Color CheckSyntax (string syntax )
		{
			string newSyntax = syntax.Replace("\t", "").TrimEnd(' ').TrimStart(' '); // cleanup string
			if ( newSyntax.StartsWith ( "//" ) )
			{
				return highlightComments;
			} else if (newSyntax.StartsWith("->") )
			{
				return highlightShortcutOptions;
			} else if (newSyntax.StartsWith("[["))
			{
				return highlightNodeOptions;
			}else if (newSyntax.StartsWith("<<"))
			{
				return highlightShortcutOptions;
			}
			else
			{
				return Color.white;
			}
		}

		// given a long string with line breaks, it will tranpose line numbers to the start of each line (and hide the rest of the body text with invisible rich text Color)
		string AddLineNumbers(string input, out int[] charIndices, out int digits, out int totalLineCount)
		{
			var lines = input.Split(new string[] {"\n"}, StringSplitOptions.None );
			totalLineCount = lines.Length;
			digits = Mathf.CeilToInt(1f * lines.Length / 10).ToString().Length + 1; // significant digits, so we know how much space to pad
			
			// all the body after the line number is hidden with invisible richtext color tags, ensuring that wordwrapping still works the same
			string invisibleBegin = "<color=#00000000>";
			string invisibleEnd = "</color>";
			
			// to mark lines with GUI elements later, we need to remember the character index offset for each line (applies to original input string, not input+lineNumbers)... see CalculateTextEditorIndexToRect()
			charIndices = new int[totalLineCount];
			int totalCharLengthWithoutNumbers = 0; 
			
			// ok begin processing each line now...
			for (int i = 0; i < lines.Length; i++)
			{
				// save caret cursor offset for this line... IMPORTANT: tabs and line breaks are 1 cursor space, NOT 2
				charIndices[i] = totalCharLengthWithoutNumbers;
				string lineFixedForCaret = lines[i].Replace("\t", " ");
				totalCharLengthWithoutNumbers += lineFixedForCaret.Length + 1; // +2 is for the "\n"
				
				// generate line numbers
				string lineDisplay = (i+1).ToString(); // line numbers start from 1
				lineDisplay = lineDisplay.PadLeft(digits);
				
				// make sure the line will be long enough
				if (lines[i].Length < digits)
				{
					lines[i] = lines[i].PadRight(digits);
				}
				
				// add line number to line
				lines[i] = lineDisplay + invisibleBegin + lines[i].Remove(0, digits) + invisibleEnd;
			}

			return string.Join("\n", lines);
		}
		
		// given a TextEditor and indices, find the GUI Vector2 screen positions for each character index (or word)
		Rect[] CalculateTextEditorIndexToRect(TextEditor te, GUIStyle textFieldStyle, int[] textIndex, bool selectWholeWord = false)
		{
			// backup TextEditor caret position, because we're about to move it a lot
			var backupCursor = te.cursorIndex;
			var backupSelect = te.selectIndex;

			// move TextEditor caret to each index in [text], and construct Rect for it
			var rects = new Rect[textIndex.Length];
			for (int i = 0; i < textIndex.Length; i++)
			{
				te.cursorIndex = textIndex[i];
				te.selectIndex = textIndex[i];
				if (selectWholeWord)
				{
					te.SelectCurrentWord();
				}

				Vector2 cursorPixelPos = textFieldStyle.GetCursorPixelPosition(te.position, new GUIContent(te.text), te.cursorIndex);
				Vector2 selectPixelPos = textFieldStyle.GetCursorPixelPosition(te.position, new GUIContent(te.text), te.selectIndex);
				rects[i] = new Rect(selectPixelPos.x - textFieldStyle.border.left - 2f, selectPixelPos.y - textFieldStyle.border.top, cursorPixelPos.x, cursorPixelPos.y);
			}

			// revert to backups
			te.cursorIndex = backupCursor;
			te.selectIndex = backupSelect;

			return rects;
		}

		void DrawBottomToolBar (Rect rect)
		{
			GUILayout.BeginArea (rect);

			using (new EditorGUILayout.HorizontalScope (isDialogueRunning ? EditorStyles.label : EditorStyles.helpBox))
			{

				var style = GUI.skin.button; //EditorStyles.miniButton;
				
				if (GUILayout.Button("Expand All", style))
				{
					treeView.ExpandAll ();
				}

				if (GUILayout.Button("Collapse All", style))
				{
					treeView.CollapseAll ();
				}

				GUILayout.FlexibleSpace();

				if (errorLog != null && errorLog.Count > 0)
				{
					var error = errorLog[errorLog.Count - 1];
					var node = treeView.treeModel.Find(error.nodeID);
					if (GUILayout.Button(new GUIContent( node == null ? " ERROR!" : " ERROR: " + node.name + ":" + error.lineNumber.ToString(), errorIcon, node == null ? "" : string.Format("{2}\n\nclick to open node {0} at line {1}", node.name, error.lineNumber, error.message )), style, GUILayout.MaxWidth(position.width * 0.31f) ))
					{
						SelectNodeAndZoomToLine(error.nodeID, error.lineNumber);
					}
				}

				GUILayout.FlexibleSpace ();
				
				// playtest button, based on the last node you touched
				if ( !isDialogueRunning && currentNodeIDEditing > -1 && treeView.treeModel.Find(currentNodeIDEditing) != null && GUILayout.Button("▶ Playtest node " + treeView.treeModel.Find(currentNodeIDEditing).name, style))
				{
					PlaytestFrom( treeView.treeModel.Find(currentNodeIDEditing).name );
				}
			}

			GUILayout.EndArea();
		}

		// a lot of the logic for this is handled in OnGUI > DrawMainPane, this just sets variables to get read elsewhere
		void SelectNodeAndZoomToLine(int nodeID, int lineNumber)
		{
			viewState.selectedIDs.Clear();
			viewState.selectedIDs.Add(nodeID);
			// treeView.SetSelection(new List<int>() { nodeID });
			
			// grab the node, count the lines, and guess the line number
			var node = treeView.treeModel.Find(nodeID);
			if (node == null) return;

			zoomID = nodeID;
			zoomToLineNumber = lineNumber;
			lastZoomToLineNumber = lineNumber;
			zoomToLineNumberTimestamp = EditorApplication.timeSinceStartup;
			
			// very important! we need to deselect any text fields before we zoom
			GUI.FocusControl(null);
		}
		
		/// <summary>
		/// Compare two strings and return the index of the first difference.  Return -1 if the strings are equal.
		/// </summary>
		/// <param name="s1"></param>
		/// <param name="s2"></param>
		/// <returns></returns>
		int CompareStringsAndFindDiffIndex(string s1, string s2)
		{
			int index = 0;
			int min = Math.Min(s1.Length, s2.Length);
			while (index < min && s1[index] == s2[index]) 
				index++;

			return (index == min && s1.Length == s2.Length) ? -1 : index;
		}
		
		[SerializeField]
		public struct MerinoUndoLog
		{
			public int id;
			public double time;
			public string bodyText;

			public MerinoUndoLog(int id, double time, string bodyText)
			{
				this.id = id;
				this.time = time;
				this.bodyText = bodyText;
			}
		}

		[SerializeField]
		public struct MerinoErrorLine
		{
			public string fileName, message;
			public int lineNumber, nodeID;

			public MerinoErrorLine(string message, string fileName, int nodeID, int lineNumber=-1)
			{
				this.message = message;
				this.fileName = fileName;
				this.nodeID = nodeID;
				this.lineNumber = lineNumber;
			}
		}
		#endregion
	}

	// based a bit on YarnSpinner/ExampleDialogueUI
	public class MerinoDialogueUI
	{
		float textSpeed = 0.01f;
		public bool inputContinue = false;
		public int inputOption = -1;

		public string currentNode;
		string displayString;
		public string displayStringFull;
		bool showContinuePrompt = false;
		string[] optionStrings = new string[0];
		Yarn.OptionChooser optionChooser;
		bool useConsolasFont = false;

		public Font consolasFont;

		public IEnumerator RunLine(Yarn.Line line, bool autoAdvance)
		{
			displayStringFull = line.text;
			optionStrings = new string[0];
			// display dialog
            if (textSpeed > 0.0f) {
                // Display the line one character at a time
                var stringBuilder = new StringBuilder ();
	            inputContinue = false;
                foreach (char c in line.text) {
                    float timeWaited = 0f;
                    stringBuilder.Append (c);
                    displayString = stringBuilder.ToString ();
                    while ( timeWaited < textSpeed )
                    {
	                    timeWaited += textSpeed;
                        // early out / skip ahead
                        if ( inputContinue ) { break; }
	                    yield return new WaitForSeconds(timeWaited);
                    }
                    if ( inputContinue ) { displayString = line.text; break; }
                }
            } else {
              // Display the line immediately if textSpeed == 0
                displayString = line.text;
           }

			inputContinue = false;

            // Show the 'press any key' prompt when done, if we have one
			showContinuePrompt = true;

            // Wait for any user input
            while (inputContinue == false && autoAdvance == false)
            {
	            yield return new WaitForSeconds(0.01f);
            }

            // Hide the text and prompt
			showContinuePrompt = false;
		}
		
		public IEnumerator RunCommand (Yarn.Command command, bool autoAdvance)
		{
			optionStrings = new string[0];
			displayString = "(Yarn command: <<" + command.text + ">>)";
			inputContinue = false;
			useConsolasFont = true;
			showContinuePrompt = true;
			while (inputContinue == false && autoAdvance == false)
			{
				yield return new WaitForSeconds(0.01f);
			}

			showContinuePrompt = false;
			useConsolasFont = false;
		}
		
		public void RunOptions (Yarn.Options optionsCollection, Yarn.OptionChooser optionChooser)
		{
			// Display each option in a button, and make it visible
			optionStrings = new string[optionsCollection.options.Count];
			for(int i=0; i<optionStrings.Length; i++)
			{
				optionStrings[i] = optionsCollection.options[i];
			}

			inputOption = -1;
			this.optionChooser = optionChooser;
		}
		
//		public IEnumerator NodeComplete(string nextNode)
//		{
//			yield return new WaitForSeconds(0.1f);
//		}
//		
		public void DialogueComplete()
		{
			displayString = "";
		}
		
		public void OnGUI(Rect rect)
		{
			// background button for clicking to continue			
			var newRect = new Rect(rect);
			newRect.y += 20;
			GUILayout.BeginArea( newRect, EditorStyles.helpBox);
			// main stuff
			GUI.enabled = optionStrings.Length == 0;
			if (GUILayout.Button("", GUILayout.ExpandHeight(true), GUILayout.ExpandWidth(true)))
			{
				inputContinue = true;
			}
			GUILayout.EndArea();
			GUI.enabled = true;
			
			// display Yarn line here
			GUILayout.BeginArea( newRect );
			var passageStyle = new GUIStyle(GUI.skin.box);
			if (useConsolasFont)
			{
				passageStyle.font = consolasFont;
			}
			passageStyle.fontSize = 18;
			passageStyle.normal.textColor = EditorStyles.boldLabel.normal.textColor;
			passageStyle.padding = new RectOffset(8,8,8,8);
			passageStyle.richText = true;
			passageStyle.alignment = TextAnchor.UpperLeft;
			float maxHeight = passageStyle.CalcHeight(new GUIContent(displayString), rect.width);
			GUILayout.Label(displayString, passageStyle, GUILayout.Height(0), GUILayout.ExpandHeight(true), GUILayout.MaxHeight(maxHeight), GUILayout.ExpandWidth(true));
			
			// show continue prompt
			if (showContinuePrompt)
			{
				Rect promptRect = GUILayoutUtility.GetLastRect();
				var bounce = Mathf.Sin((float)EditorApplication.timeSinceStartup * 6f) > 0;
				promptRect.x += promptRect.width-24-(bounce ? 0 : 4);
				promptRect.y += promptRect.height-24;
				promptRect.width = 20;
				promptRect.height = 20;
				passageStyle.border = new RectOffset(0,0,0,0);
				passageStyle.padding = new RectOffset(0,0,0,0);
				passageStyle.alignment = TextAnchor.MiddleCenter;
				passageStyle.wordWrap = false;
				passageStyle.normal.background = null;
				GUI.Box(promptRect, "▶", passageStyle);
			}
			
			// show choices
			var buttonStyle = new GUIStyle(GUI.skin.button);
			buttonStyle.fontSize = 16;
			for (int i = 0; i < optionStrings.Length; i++)
			{
				if (GUILayout.Button(optionStrings[i], buttonStyle))
				{
					inputOption = i;
					optionChooser(inputOption);
					optionStrings = new string[0];
				}
			}
			GUILayout.EndArea();
		}
	}
	

	// this is pretty much just YarnSpinner/ExampleVariableStorage with all the MonoBehaviour stuff taken out
	public class MerinoVariableStorage : VariableStorage
	{
		/// Where we actually keeping our variables
		Dictionary<string, Yarn.Value> variables = new Dictionary<string, Yarn.Value> ();
	
		/// A default value to apply when the object wakes up, or
		/// when ResetToDefaults is called
		[System.Serializable]
		public class DefaultVariable
		{
			/// Name of the variable
			public string name;
			/// Value of the variable
			public string value;
			/// Type of the variable
			public Yarn.Value.Type type;
		}
	
		/// Our list of default variables, for debugging.
		DefaultVariable[] defaultVariables = new DefaultVariable[0];
	
		/// Reset to our default values when the game starts
		public MerinoVariableStorage ()
		{
			ResetToDefaults ();
		}
	
		/// Erase all variables and reset to default values
		public void ResetToDefaults ()
		{
			Clear ();
	
			// For each default variable that's been defined, parse the string
			// that the user typed in in Unity and store the variable
			foreach (var variable in defaultVariables) {
				
				object value;
	
				switch (variable.type) {
				case Yarn.Value.Type.Number:
					float f = 0.0f;
					float.TryParse(variable.value, out f);
					value = f;
					break;
	
				case Yarn.Value.Type.String:
					value = variable.value;
					break;
	
				case Yarn.Value.Type.Bool:
					bool b = false;
					bool.TryParse(variable.value, out b);
					value = b;
					break;
	
				case Yarn.Value.Type.Variable:
					// We don't support assigning default variables from other variables
					// yet
					Debug.LogErrorFormat("Can't set variable {0} to {1}: You can't " +
						"set a default variable to be another variable, because it " +
						"may not have been initialised yet.", variable.name, variable.value);
					continue;
	
				case Yarn.Value.Type.Null:
					value = null;
					break;
	
				default:
					throw new System.ArgumentOutOfRangeException ();
	
				}
	
				var v = new Yarn.Value(value);
	
				SetValue ("$" + variable.name, v);
			}
		}
			
		/// Set a variable's value
		public void SetNumber (string variableName, float value)
		{
			// Copy this value into our list
			variables[variableName] = new Yarn.Value(value);
		}
	
		/// Get a variable's value
		public float GetNumber (string variableName)
		{
			// If we don't have a variable with this name, return the null value
			if (variables.ContainsKey(variableName) == false)
				return -1f;
		
			return variables [variableName].AsNumber;
		}
	
		/// Set a variable's value
		public void SetValue (string variableName, Yarn.Value value)
		{
			// Copy this value into our list
			variables[variableName] = new Yarn.Value(value);
		}
	
		/// Get a variable's value
		public Yarn.Value GetValue (string variableName)
		{
			// If we don't have a variable with this name, return the null value
			if (variables.ContainsKey(variableName) == false)
				return Yarn.Value.NULL;
			
			return variables [variableName];
		}
	
		/// Erase all variables
		public void Clear ()
		{
			variables.Clear ();
		}
			
	}


	internal class MerinoMultiColumnHeader : MultiColumnHeader
	{
		Mode m_Mode;
		//Texture helpIcon = EditorGUIUtility.IconContent("_Help").image;
		
		public enum Mode
		{
			LargeHeader,
			DefaultHeader,
			MinimumHeaderWithoutSorting
		}

		public MerinoMultiColumnHeader(MultiColumnHeaderState state)
			: base(state)
		{
			mode = Mode.DefaultHeader;
		}

		public Mode mode
		{
			get
			{
				return m_Mode;
			}
			set
			{
				m_Mode = value;
				switch (m_Mode)
				{
					case Mode.LargeHeader:
						canSort = true;
						height = 37f;
						break;
					case Mode.DefaultHeader:
						canSort = true;
						height = DefaultGUI.minimumHeight;
						break;
					case Mode.MinimumHeaderWithoutSorting:
						canSort = false;
						height = DefaultGUI.minimumHeight;
						break;
				}
			}
		}
		
		public override void OnGUI(Rect rect, float xScroll)
		{
			// add extra "clear sorting" button if sorting is active
			if (state.sortedColumnIndex > -1)
			{
				var clearSortRect = new Rect(rect);
				clearSortRect.width = 18;
				clearSortRect.height = clearSortRect.width;
				rect.x += clearSortRect.width;
				rect.width -= clearSortRect.width;
				if (GUI.Button(clearSortRect, new GUIContent("x", "no sort / clear column sorting"), EditorStyles.miniButton))
				{
					state.sortedColumnIndex = -1;
				}
			}

			// draw rest of the header
			base.OnGUI(rect, xScroll);
		}

		protected override void ColumnHeaderGUI (MultiColumnHeaderState.Column column, Rect headerRect, int columnIndex)
		{
			// Default column header gui
			base.ColumnHeaderGUI(column, headerRect, columnIndex);

			// Add additional info for large header
			if (mode == Mode.LargeHeader)
			{
				// Show example overlay stuff on some of the columns
				if (columnIndex > 2)
				{
					headerRect.xMax -= 3f;
					var oldAlignment = EditorStyles.largeLabel.alignment;
					EditorStyles.largeLabel.alignment = TextAnchor.UpperRight;
					GUI.Label(headerRect, 36 + columnIndex + "%", EditorStyles.largeLabel);
					EditorStyles.largeLabel.alignment = oldAlignment;
				}
			}
		}
	}

}
