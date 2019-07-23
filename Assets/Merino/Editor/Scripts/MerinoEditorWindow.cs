using System.Collections;
using System;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Merino
{
	class MerinoEditorWindow : MerinoEventWindow
	{
		// sidebar tree view management stuff
		[NonSerialized] bool m_Initialized;

		[SerializeField] MultiColumnHeaderState m_MultiColumnHeaderState;
		SearchField m_SearchField;
		[SerializeField] MerinoTreeView m_TreeView;

		public MerinoTreeView treeView {
			get { return m_TreeView; }
		}

		// used for keyboard tab / space replacement
		double lastTabTime = 0.0; 
		bool moveCursorAfterTab = false;

		const int margin = 10;
		[SerializeField] Vector2 scrollPos;
		
		
		int zoomID = -1, zoomToLineNumber = -1, lastZoomToLineNumber = -1;
		double zoomToLineNumberTimestamp;
		const double zoomLineFadeTime = 1.0;
		
		bool resizingSidebar = false;

		public static Action OnFileLoaded;
		public static Action OnFileUnloaded;
		
		Rect sidebarSearchRect
		{
			get { return new Rect (0, 0, MerinoPrefs.sidebarWidth, 18); }
		}
		
		Rect sidebarRect
		{
			get { return new Rect(margin/2, 20, MerinoPrefs.sidebarWidth, position.height-margin*5); }
		}

		Rect sidebarResizeRect
		{
			get { return new Rect(MerinoPrefs.sidebarWidth+3, 0, 5, position.height); }
		}
		
		Rect toolbarRect
		{
			get { return new Rect(MerinoPrefs.sidebarWidth, 0, position.width - MerinoPrefs.sidebarWidth, 18); } // was height-30
		}

		Rect nodeEditRect
		{
			get { return new Rect( MerinoPrefs.sidebarWidth+margin, 18, position.width-MerinoPrefs.sidebarWidth-margin*1.5f, position.height-margin*4.2f); }
		}

		Rect bottomToolbarRect
		{
			get { return new Rect(0, position.height - margin*2.5f, position.width, margin*2.5f); }
		}

		// misc resources
		Texture helpIcon, errorIcon, folderIcon, textIcon, deleteIcon, resetIcon;
		
		// undo management
		double lastUndoTime;
		int moveCursorUndoID, moveCursorUndoIndex;
		[SerializeField] List<MerinoUndoLog> undoData = new List<MerinoUndoLog>();
		bool spaceWillIncrementUndo = false; // used just in Main Pane
		
		// error checking
		public static List<MerinoErrorLine> errorLog = new List<MerinoErrorLine>();
		
		// node management
		private List<int> DeleteList = new List<int>();
		int currentNodeIDEditing, currentNodeWordCountCache;
		
		// some help strings
		const string compileErrorHoverString = "{0}\n\n(DEBUGGING TIP: This line number is just Yarn's guess. Look before this point too.)\n\nLeft-click to dismiss.";

		//todo: Move into MerinoCore.cs when merged into Develop
		#region TempPath Hotfix
		
		/// <summary>
		/// Delete all Merino temp data instances in the project.
		/// </summary>
		public static void CleanupTempData()
		{
			var tempData = Resources.FindObjectsOfTypeAll<MerinoData>();
			foreach (var data in tempData)
			{
				var path = AssetDatabase.GetAssetPath(data);
				if (!string.IsNullOrEmpty(path)) //don't attempt to delete ghost(?) scriptable objects
					AssetDatabase.DeleteAsset(path);
			}
		}

		/// <summary>
		/// Returns the path of the Merino folder, based on the location of MerinoEditorWindow.cs since that should always be in there.
		/// </summary>
		public static string LocateMerinoFolder()
		{
			string[] results = Directory.GetFiles(Application.dataPath, "MerinoEditorWindow.cs", SearchOption.AllDirectories);
			if (results.Length > 0)
			{
				var parent = Directory.GetParent(results[0]);
				while (parent.Name != "Merino")
					parent = parent.Parent;

				return parent.FullName;
			}

			return null;
		}

		/// <summary>
		/// Returns the path Merino temp data should live.
		/// </summary>
		public static string GetTempDataPath()
		{
			var path = LocateMerinoFolder(); //find folder in project...
			path += "\\Editor\\MerinoTempData.asset"; //append on the path for temp data;
			path = path.Substring(path.IndexOf("Assets")); //remove path before the assets folder
			return (path);
		}

		#endregion
		
		#region EditorWindowStuff

		public const string windowTitle = " Merino (Yarn Editor)";
		const string syntaxReference = "YARN API:  [[GoToThisNodeName]]   [[OptionText|NodeName]]   ->ShortcutOptionText\n<<if $var is 1>>   <<elseif $var = 2>>   <<else>>  <<endif>>    <<set $var to 1>>";

		[MenuItem("Window/Merino/Main Editor")]
		static void MenuItem_GetWindow()
		{
			GetWindow<MerinoEditorWindow>(windowTitle, true);
		}

		public static MerinoEditorWindow GetEditorWindow(bool focus=true) {
			return GetWindow<MerinoEditorWindow>(windowTitle, focus);
		}

		void OnEnable() {
			// GetEditorWindow().titleContent = new GUIContent( windowTitle, MerinoEditorResources.Node );
		}
		
		void ResetMerino()
		{
			MerinoData.CurrentFiles.Clear();
			MerinoData.FileToNodeID.Clear();
			MerinoData.DirtyFiles.Clear();
			if (OnFileUnloaded != null)
				OnFileUnloaded();
			
			MerinoData.ViewState = null;
			AssetDatabase.DeleteAsset(MerinoCore.GetTempDataPath() ); // delete tempdata, or else it will just get reloaded again
			Selection.objects = new UnityEngine.Object[0]; // deselect all
			Undo.undoRedoPerformed -= OnUndo;
			
			m_Initialized = false;
			InitIfNeeded(true);
		}

		// TODO: need to move the rest of these icons to MerinoEditorResources?
		void InitIcons()
		{
			if (helpIcon == null) 
				helpIcon = EditorGUIUtility.IconContent("_Help").image;
			
			if (errorIcon == null) 
				errorIcon = EditorGUIUtility.Load("icons/d_console.erroricon.sml.png") as Texture;
			
			if (folderIcon == null) 
				folderIcon = EditorGUIUtility.FindTexture("Folder Icon");
			
			if (textIcon == null)
				textIcon = EditorGUIUtility.IconContent("TextAsset Icon").image;
			
			if (deleteIcon == null)
				deleteIcon = EditorGUIUtility.Load("icons/d_TreeEditor.Trash.png") as Texture;
			
			if (resetIcon == null)
			{
				string path =  EditorGUIUtility.isProSkin ? "icons/d_animation.prevkey.png" : "icons/animation.prevkey.png";
				resetIcon = EditorGUIUtility.Load(path) as Texture;
			}

			// TODO: put this type of check into the getters / setters in MerinoEditorResources?
			if ( MerinoEditorResources.PageNew == null ) {
				MerinoEditorResources.GenerateResourcesScript();
				MerinoEditorResources.UpdateTextureReferences( MerinoEditorResources.Instance );
			}
		}
		
		void InitIfNeeded (bool ignoreSelection=false)
		{
			if (m_Initialized) return;

			currentNodeIDEditing = -1;
			Undo.undoRedoPerformed += OnUndo;
			undoData.Clear();
			errorLog.Clear();
			
			InitIcons();

			// Check if viewstate already exists (deserialized from window layout file or scriptable object)
			if (MerinoData.ViewState == null) 
			{
				MerinoData.ViewState = new TreeViewState();
			}

			bool firstInit = m_MultiColumnHeaderState == null;
			var headerState = MerinoTreeView.CreateDefaultMultiColumnHeaderState(sidebarRect.width);
			if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_MultiColumnHeaderState, headerState))
				MultiColumnHeaderState.OverwriteSerializedFields(m_MultiColumnHeaderState, headerState);
			m_MultiColumnHeaderState = headerState;
				
			var multiColumnHeader = new MerinoMultiColumnHeader(headerState);
			if (firstInit)
				multiColumnHeader.ResizeToFit ();

			// generate sidebar data structures
			var treeModel = new TreeModel<MerinoTreeElement>(MerinoCore.GetData());
			m_TreeView = new MerinoTreeView(MerinoData.ViewState, multiColumnHeader, treeModel);
			m_TreeView.treeChanged += OnTreeChanged;
			
			m_SearchField = new SearchField();
			m_SearchField.downOrUpArrowKeyPressed += m_TreeView.SetFocusAndEnsureSelectedItem;

			m_Initialized = true;

			if (!ignoreSelection)
			{
				OnSelectionChange();
			}
		}

		double lastTreeChangeTime;
		void OnTreeChanged()
		{
			lastTreeChangeTime = EditorApplication.timeSinceStartup;
			// just in case, let's update all the cachedParentIDs
			UpdateCachedParentIDs();
		}

		void UpdateCachedParentIDs () {
			foreach ( var node in MerinoData.TreeElements ) {
				if ( node != null && node.depth > 0 && node.leafType == MerinoTreeElement.LeafType.Node ) {
					node.cachedParentID = treeView.treeModel.GetAncestors(node.id)[0];
				}
			}
		}

		// called when something get selected in Project tab
		void OnSelectionChange ()
		{
			if (!m_Initialized) return;

			// TODO: commented out until we figure out what this UX should actually be
//			var possibleYarnFile = Selection.activeObject as TextAsset;
//			if (possibleYarnFile != null && IsProbablyYarnFile(possibleYarnFile)) // possibleYarnFile != currentFile
//			{
//				// if we already have an Asset selected, make sure it's refreshed
//				if (currentFile != null)
//				{
//					AssetDatabase.ImportAsset( AssetDatabase.GetAssetPath(currentFile));
//				}
//
//				// ok load this new file now?
//				currentFile = possibleYarnFile;
//				ViewState.selectedIDs.Clear();
//				ForceStopDialogue();
//				m_TreeView.treeModel.SetData (GetData ());
//				m_TreeView.Reload ();
//			}
		}

		// This gets called 10 times a second, good for low priority stuff
		public void OnInspectorUpdate()
		{
			if (!m_Initialized) return;

			// if there are no nodes selected, let's still process deleted nodes
			var viewState = MerinoData.ViewState;
			if (viewState != null && viewState.selectedIDs != null && viewState.selectedIDs.Count == 0)
			{
				DeleteNodes();
			}
			
			// small delay before saving after OnTreeChanged
			if (MerinoPrefs.useAutosave && MerinoData.CurrentFiles.Count > 0 && EditorApplication.timeSinceStartup > lastTreeChangeTime + 0.5)
			{
				MerinoCore.SaveDataToFiles();
				MerinoCore.ReimportFiles(true); // we have no idea which node ID got changed, so we just have to reimport all files at this point
				lastTreeChangeTime = EditorApplication.timeSinceStartup + 99999;
			}
			
			// small delay before reimporting the asset (otherwise it's too annoying to constantly reimport the asset)
			if (EditorApplication.timeSinceStartup > MerinoCore.LastSaveTime + 1.0 && MerinoData.CurrentFiles.Count > 0)
			{
				MerinoCore.ReimportFiles();
				MerinoCore.LastSaveTime = EditorApplication.timeSinceStartup + 99999; // don't save again until SaveDataToFile resets the variable
			}

			// calculate and cache wordcount
			if ( currentNodeIDEditing > -1 && treeView != null && treeView.treeModel != null && treeView.treeModel.Find(currentNodeIDEditing) != null && treeView.treeModel.Find(currentNodeIDEditing).leafType == MerinoTreeElement.LeafType.Node ) {
				currentNodeWordCountCache = GetWordCount( treeView.treeModel.Find(currentNodeIDEditing).nodeBody );
			} else {
				currentNodeWordCountCache = -1;
			}
		}

		// This gets called many times a second, for real-time interaction / smoother animation / feel stuff
		public void Update()
		{
			if (resizingSidebar || zoomToLineNumber > -1 || lastZoomToLineNumber > -1)
			{
				// MASSIVELY improves framerate, wow, amazing
				Repaint();
			}
		}

		void OnDestroy()
		{
			// if destroyed, make sure the text file got refreshed at least
			if (MerinoData.CurrentFiles.Count > 0)
			{
				MerinoCore.ReimportFiles(true);
			}
			
			Undo.undoRedoPerformed -= OnUndo;
			Undo.ClearUndo(MerinoData.Instance);
			
			undoData.Clear();
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
			for( int i=undoData.Count-1; i>0 && MerinoData.Timestamp < undoData[i].time; i-- )
			{
				var recent = undoData[i];
				var treeDataNode = MerinoData.TreeElements.First(x => x.id == recent.id);
				
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
			if (MerinoData.CurrentFiles.Count > 0 && MerinoPrefs.useAutosave)
			{
				MerinoCore.SaveDataToFiles();
			}
		}
		#endregion
		
		#region LoadingAndSaving

		[MenuItem("Assets/Create/Yarn.txt Script")]
		public static void ProjectTabCreateNewYarnFile()
		{
			var defaultData = GetDefaultData();
			ProjectWindowUtil.CreateAssetWithContent("NewYarnFile.yarn.txt", defaultData != null ? defaultData.text : "title: Start\n---\nWrite your story here.\n===");
		}

		void LoadFile()
		{
			// Fix OpenFilePanelWithFilters not behaving properly on OSX
			// https://github.com/radiatoryang/merino/issues/25
#if UNITY_EDITOR_WIN
			string addFilePath = EditorUtility.OpenFilePanelWithFilters("Load a Yarn.txt script in Assets folder...", Application.dataPath, new string[] {"Yarn.txt scripts", "yarn.txt"});
#else
			string addFilePath = EditorUtility.OpenFilePanel("Load a Yarn.txt script in Assets folder...", Application.dataPath, "txt");
#endif
			if (!string.IsNullOrEmpty(addFilePath))
			{
				if (!addFilePath.StartsWith(Application.dataPath))
				{
					EditorUtility.DisplayDialog("Merino: invalid file", addFilePath + " is not in your Unity project's Assets folder! Cannot load it.", "Sorry");
				}
				else
				{
					// remove project folder path from selected path (+1 = trailing slash)
					addFilePath = "Assets/" + addFilePath.Substring(Application.dataPath.Length + 1);
					LoadYarnFileAtFullPath(addFilePath, true); // add all found files to currentFiles
					m_TreeView.treeModel.SetData(MerinoCore.GetData());
					m_TreeView.Reload();

					if (OnFileLoaded != null)
						OnFileLoaded();
				}
			}
		}

		void LoadFolder()
		{
			string addPath = EditorUtility.OpenFolderPanel("Load Yarn.txt scripts in Assets folder...", Application.dataPath, "");
			if (!string.IsNullOrEmpty(addPath))
			{
				if (!addPath.StartsWith(Application.dataPath))
				{
					EditorUtility.DisplayDialog("Merino: invalid folder", addPath + " is not in your Unity project's Assets folder! Cannot load it.", "Sorry");
				}
				else
				{
					// remove project folder path from selected path (+1 = trailing slash)
					addPath = addPath == Application.dataPath ? "Assets" : "Assets/" + addPath.Substring( Application.dataPath.Length + 1);
					LoadYarnFilesAtPath(addPath); // add all found files to currentFiles
					m_TreeView.treeModel.SetData(MerinoCore.GetData());
					m_TreeView.Reload();

					if (OnFileLoaded != null)
						OnFileLoaded();
				}
			}
		}

		void CreateNewYarnFile() { // for the menu items and callbacks
			CreateNewYarnFile("NewYarnFile");
		}
		
		public void CreateNewYarnFile(string defaultName = "NewYarnFile", string fileContents = "")
		{
			string defaultPath = Application.dataPath + "/";
			if (MerinoData.CurrentFiles.Where( file => file != null).Count() > 0)
			{
				var lastFile = MerinoData.CurrentFiles.Where( file => file != null).Last();
				defaultPath = Application.dataPath.Substring(0, Application.dataPath.Length - 6) + AssetDatabase.GetAssetPath(lastFile );
				defaultName = lastFile.name.Substring(0, lastFile.name.Length - 5); // -5 because ignore ".yarn" at end of file name
			}
			string fullFilePath = EditorUtility.SaveFilePanel("Merino: save yarn.txt in Assets folder...", Path.GetDirectoryName(defaultPath), defaultName, "yarn.txt");
			if (fullFilePath.Length > 0)
			{
				if (!fullFilePath.StartsWith(Application.dataPath))
				{
					EditorUtility.DisplayDialog("Merino: invalid save location", "Cannot save new file at " + fullFilePath + " because it is not in your Unity project's Assets folder!", "Sorry");
				}
				else if (!fullFilePath.EndsWith(".yarn.txt"))
				{
					EditorUtility.DisplayDialog("Merino: invalid file extension", "Cannot save new file at " + fullFilePath + " must be saved with a .yarn.txt file extension.", "Sorry");
				} 
				else
				{
					// v0.5.4, 9 July 2019: bug on MacOS results in warning dialog 'You cannot save this file with extension ".txt" at the end of the name. The required extension is ".yarn.txt".'
					// and if user clicks "Use .yarn.txt" then the file name results in something absurd like "NewYarnFile.yarn.txt.yarn.yarn.txt"... if clicks "Use both" then results in "NewYarnFile.yarn.txt.yarn.txt.yarn.txt"
					// so these are hacks to correct the file name
					if ( fullFilePath.EndsWith(".yarn.txt.yarn.yarn.txt") ) {
						fullFilePath = fullFilePath.Substring(0, fullFilePath.Length - ".yarn.yarn.txt".Length);
					}
					if ( fullFilePath.EndsWith(".yarn.txt.yarn.txt.yarn.txt") ) {
						fullFilePath = fullFilePath.Substring(0, fullFilePath.Length - ".yarn.txt.yarn.txt".Length);
					}
					if ( fullFilePath.EndsWith(".yarn.yarn.txt") ) { // user tries to correct by removing extension, clicks "use .yarn.txt"
						fullFilePath = fullFilePath.Substring(0, fullFilePath.Length - ".yarn.yarn.txt".Length) + ".yarn.txt";
					}
					if ( fullFilePath.EndsWith(".yarn.txt.yarn.txt") ) { // user tries to correct by removing extension, clicks "use both"
						fullFilePath = fullFilePath.Substring(0, fullFilePath.Length - ".yarn.txt".Length);
					}

					if ( string.IsNullOrEmpty(fileContents) && GetDefaultData() != null) {
						fileContents = GetDefaultData().text;
					} 

					File.WriteAllText(fullFilePath, fileContents);
					AssetDatabase.Refresh();
					var newFile = AssetDatabase.LoadAssetAtPath<TextAsset>("Assets" + fullFilePath.Substring(Application.dataPath.Length));
					AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(newFile));
					LoadYarnFileAtFullPath(AssetDatabase.GetAssetPath(newFile), true);
					m_TreeView.treeModel.SetData(MerinoCore.GetData());
					m_TreeView.Reload();

					var fileID = MerinoData.FileToNodeID[newFile];
					m_TreeView.FrameItem( fileID );
					m_TreeView.SetExpanded(fileID, true);
					m_TreeView.SetSelection(new List<int>() { fileID });

					if (OnFileLoaded != null)
						OnFileLoaded();
				}
			}
		}
		
		static TextAsset GetDefaultData()
		{
			var defaultData = Resources.Load<TextAsset>(MerinoPrefs.newFileTemplatePath);
			if (defaultData == null)
			{
				MerinoDebug.Log(LoggingLevel.Warning, "Merino couldn't find the new file template at Resources/" + MerinoPrefs.newFileTemplatePath + ". Double-check the file exists there, or you can override this path in EditorPrefs.");
				return null;
			}
			return defaultData;
		}
		
		static bool IsProbablyYarnFile(TextAsset textAsset)
		{
			// v0.5.4, 9 June 2019: commented-out stricter .yarn.txt file checks, since Merino theoretically lets you delete all the nodes from the file
			// for the discussion, see https://github.com/radiatoryang/merino/issues/28
			// - RY
			if ( AssetDatabase.GetAssetPath(textAsset).EndsWith(".yarn.txt") ) // && textAsset.text.Contains("---") && textAsset.text.Contains("===") && textAsset.text.Contains("title:") )
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		TextAsset LoadYarnFileAtFullPath(string path, bool isRelativePath=false)
		{
			var newFile = AssetDatabase.LoadAssetAtPath<TextAsset>( isRelativePath ? path : "Assets" + path.Substring(Application.dataPath.Length) );
			if (MerinoData.CurrentFiles.Contains(newFile) == false)
			{
				MerinoData.CurrentFiles.Add(newFile);
				EditorUtility.SetDirty( MerinoData.Instance );
			}
			else
			{
				MerinoDebug.Log(LoggingLevel.Warning, "Merino: file at " + path + " is already loaded!");
			}

			return newFile;
		}
		
		TextAsset[] LoadYarnFilesAtPath(string path)
		{
			// use Unity's AssetDatabase search function to find TextAssets, then convert GUIDs to files, and add unique items to currentFiles
			var guids = !string.IsNullOrEmpty(path) ? AssetDatabase.FindAssets("t:TextAsset", new string[] {path} ) : AssetDatabase.FindAssets("t:TextAsset");
			var files = guids.Select(AssetDatabase.GUIDToAssetPath )
				.Where( x => x.Contains("Editor")==false )
				.Select( AssetDatabase.LoadAssetAtPath<TextAsset>)
				.Where( IsProbablyYarnFile )
				.ToArray();
			foreach (var file in files)
			{
				if (MerinoData.CurrentFiles.Contains(file)==false )
				{
					MerinoData.CurrentFiles.Add(file);
				}
			}

			if (files.Length == 0)
			{
				EditorUtility.DisplayDialog("Merino: no Yarn.txt files found", "No valid Yarn.txt files were found at the path " + path, "Close");
			}

			EditorUtility.SetDirty( MerinoData.Instance );
			return files;
		}

		#endregion
		
		#region NodeManagement

		// used by GenericMenu
		void AddNewNode()
		{
			AddNewNode(null);
		}

		public List<MerinoTreeElement> AddNewNode(IList<int> parents=null, bool focusWindow = true)
		{
			// Debug.Log( string.Join(", ", parents.Select( x => x.ToString()).ToArray() ) );

			if (MerinoData.CurrentFiles.Count == 0)
			{
				return null;
			}

			if (parents == null || parents.Count == 0)
			{
				var parentSearch = currentNodeIDEditing > 0 && m_TreeView.treeModel.Find(currentNodeIDEditing) != null
					? m_TreeView.treeModel.Find(currentNodeIDEditing)
					: m_TreeView.treeModel.Find(MerinoData.FileToNodeID[MerinoData.CurrentFiles[0]]);

				while (parentSearch.leafType == MerinoTreeElement.LeafType.Node)
				{
					parentSearch = (MerinoTreeElement) parentSearch.parent;
				}

				parents = new List<int>() {parentSearch.id };
			}

			int newID = -1;
			var newNodes = new List<MerinoTreeElement>();
			foreach (var parentID in parents)
			{
				var parent = treeView.treeModel.Find(parentID);
				
				newID = m_TreeView.treeModel.GenerateUniqueID();

				// just to be safe, also run a check of all existing node names
				int nameID = newID;
				var newNodeName = "NewNode" + nameID.ToString();
				while ( MerinoData.TreeElements.Find( node => node.name == newNodeName ) != null ) {
					nameID++;
					newNodeName = "NewNode" + nameID.ToString();
				}

				var newNode = new MerinoTreeElement(newNodeName, 0, newID);
				newNode.nodeBody = "Write here.\n";
				newNode.cachedParentID = parent.id;
				m_TreeView.treeModel.AddElement(newNode, parent, parent.children != null ? parent.children.Count : 0);
				if ( focusWindow ) {
					m_TreeView.FrameItem(newID);
					var newSelect = m_TreeView.GetSelection().ToList();
					newSelect.Add( newID );
					m_TreeView.SetSelection( newSelect );
				}
				newNodes.Add(newNode);
			}

			// if creating only one node, then prompt user rename it (like Unity's Project tab)
			if (parents.Count == 1 && focusWindow)
			{
				var newViewItem = m_TreeView.GetRows().Where(x => x.id == newID).First();
				m_TreeView.BeginRename(newViewItem);
			}

			if (MerinoPrefs.useAutosave)
			{
				MerinoCore.SaveDataToFiles();
			}

			return newNodes;
		}

		public void AddNodeToDelete(int id, bool fileWasDeletedOverride = false)
		{
			if (m_TreeView.treeModel.Find(id).leafType == MerinoTreeElement.LeafType.Node)
			{
				if (!EditorUtility.DisplayDialog("Delete Node?",
					"Are you sure you want to delete " + m_TreeView.treeModel.Find(id).name + "?", "Delete", "Cancel"))
				{
					return;
				}
			}
			else
			{
				if ( fileWasDeletedOverride ) 
				{
					EditorUtility.DisplayDialog("Loaded file was deleted and/or cannot be found.", "The file " + m_TreeView.treeModel.Find(id).name + " was deleted. It will be unloaded from Merino.", "Ok, understood.");
				} else if (!EditorUtility.DisplayDialog("Unload File?",
					"Are you sure you want to unload file " + m_TreeView.treeModel.Find(id).name + "? Unsaved changes will be lost.", "Unload", "Cancel"))
				{
					return;
				}
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
			
			// TODO: this isn't working?
			// Undo.RecordObject(treeData, "Merino: Delete Nodes");
			
			m_TreeView.treeModel.RemoveElements(DeleteList);
			var newDeleteList = new List<int>();
			foreach (var id in DeleteList)
			{
				// remove from selection
				if (MerinoData.ViewState.selectedIDs.Contains(id))
				{
					MerinoData.ViewState.selectedIDs.Remove(id);
				}
				
				// remove any files from file lists
				if (MerinoData.FileToNodeID.ContainsValue(id))
				{
					// remove any nodes that claimed this file node as a parent
					var nodes = MerinoData.TreeElements.Where( node => node.cachedParentID == id );
					foreach ( var node in nodes ) {
						newDeleteList.Add(node.id);
						MerinoData.TreeElements.Remove(node); // I don't actually understand when it gets removed though
					}

					var fileToRemove = MerinoData.FileToNodeID.Where(x => x.Value == id).Select(x => x.Key).First();
					MerinoData.FileToNodeID.Remove(fileToRemove);
					MerinoData.CurrentFiles.Remove(fileToRemove);
					MerinoData.DirtyFiles.Remove(fileToRemove);

					if (OnFileUnloaded != null)
						OnFileUnloaded();
				} 
			}

			DeleteList.Clear();
			DeleteList.AddRange(newDeleteList);

			TreeElementUtility.UpdateDepthValues(m_TreeView.treeModel.root);
			
			if (MerinoPrefs.useAutosave)
			{
				MerinoCore.SaveDataToFiles();
			}
		}

		// TODO: there's a duplicate of this functionality in MerinoData.GetFileParent... should reconcile
		MerinoTreeElement GetFileParent(MerinoTreeElement treeNode)
		{
			return treeNode.leafType == MerinoTreeElement.LeafType.Node ? m_TreeView.treeModel.GetAncestors(treeNode.id).Select( id => m_TreeView.treeModel.Find(id)).Where(x => x.leafType == MerinoTreeElement.LeafType.File).First() : treeNode;
		}

		TextAsset GetTextAssetForNode( MerinoTreeElement treeNode )
		{
			var fileParent = GetFileParent(treeNode);
			return MerinoData.CurrentFiles.First(x => MerinoData.FileToNodeID[x] == fileParent.id);
		}
		
		
		#endregion
		
		#region PlaytestPreview
		
		// logs errors from the playtest engine and Yarn Loader
		public void PlaytestErrorLog(string message)
		{
			string fileName = "unknown";
			string nodeName = "unknown";
			int lineNumber = -1;
			
			// detect file name
			if (message.Contains("file"))
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
			MerinoDebug.Log(LoggingLevel.Error, message);
			var nodeRef = MerinoData.TreeElements.Where(x => x.name == nodeName).ToArray();
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
					MerinoDebug.Log(LoggingLevel.Warning, "Merino couldn't find node ID " + nodeID.ToString() + "... it might've been deleted or the Yarn file might be corrupted.");
				}

				return -1;
			}
		}

		#endregion
		
		void OnGUI ()
		{
			InitIfNeeded();

			DrawSidebarSearch (sidebarSearchRect);
			DrawSidebar (sidebarRect);
			ResizeSidebar();

			if (MerinoData.ViewState != null)
			{
				DrawMainToolbar(toolbarRect);
				DrawMainPane(nodeEditRect);
			}

			DrawBottomToolBar(bottomToolbarRect);
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
				MerinoPrefs.sidebarWidth = Mathf.Clamp(Event.current.mousePosition.x - 10, sidebarWidthClamp, position.width-MerinoPrefs.sidebarWidth);
			//	cursorChangeRect.Set(cursorChangeRect.x,currentScrollViewHeight,cursorChangeRect.width,cursorChangeRect.height);
			}

			// stop resizing
			if (Event.current.rawType == EventType.MouseUp)
			{
				resizingSidebar = false;
				MerinoPrefs.SaveHiddenPrefs();
			}
		}
		
		void DrawSidebarSearch (Rect rect)
		{
			GUILayout.BeginArea( rect, EditorStyles.toolbar );

			// CREATE menu
			bool showDropdown = EditorGUILayout.DropdownButton(new GUIContent(" Create", MerinoEditorResources.PageNew, "create new Yarn.txt files and new Yarn nodes"), FocusType.Passive, EditorStyles.toolbarDropDown, GUILayout.MaxWidth(67));
			var dropdownRect = GUILayoutUtility.GetLastRect();

			if (showDropdown)
			{
				var createMenu = new GenericMenu();
				createMenu.AddItem(new GUIContent("Yarn.txt File"), false, CreateNewYarnFile);
				if (MerinoData.CurrentFiles.Count > 0)
				{
					createMenu.AddItem(new GUIContent("New Yarn Node"), false, AddNewNode);
				}
				else
				{
					createMenu.AddDisabledItem( new GUIContent("New Yarn Node"), false);
				}

				var menuRect = new Rect(dropdownRect);
				menuRect.y += 16;
				createMenu.DropDown(menuRect);
			}

			// search bar
			rect.x += dropdownRect.width+12;
			rect.width -= dropdownRect.width+12;
			treeView.searchString = m_SearchField.OnGUI (rect, treeView.searchString);
			GUILayout.EndArea();
		}

		void DrawSidebar (Rect rect)
		{
//			if (currentFiles.Count > 0)
//			{
//				float BUTTON_HEIGHT = 20;
//				var buttonRect = rect;
//				buttonRect.height = BUTTON_HEIGHT;
//				bool addNewNode = false;
//				EditorGUI.BeginChangeCheck();
//				addNewNode = GUI.Button(buttonRect, "+ New Node");
//				if (EditorGUI.EndChangeCheck())
//				{
//					if (addNewNode)
//					{
//						AddNewNode();
//					}
//				}
//				rect.y += BUTTON_HEIGHT;
//				rect.height -= BUTTON_HEIGHT;
//			}
			m_TreeView.OnGUI(rect);
		}

		void DrawMainToolbar(Rect rect)
		{
			GUILayout.BeginArea(rect);
			
			GUILayout.BeginHorizontal( EditorStyles.toolbar );
			
			if ( FluidGUIButtonIcon(" + Folder ", folderIcon, "Load all .yarn.txt files in a folder (and its subfolders) into Merino", EditorStyles.toolbarButton, GUILayout.Height(18), GUILayout.MaxWidth(70) ))
			{
				LoadFolder();
			}

			if ( FluidGUIButtonIcon(" + File", textIcon, "Load a single .yarn.txt file into Merino", EditorStyles.toolbarButton, GUILayout.Height(18), GUILayout.MaxWidth(55) ))
			{
				LoadFile();
			}
			
			if (MerinoData.CurrentFiles.Count > 0 )
			{
				// UNLOAD BUTTON, formerly known as the NEW FILE BUTTON
				if ( FluidGUIButtonIcon("Unload All", resetIcon, "will unload all files, throw away any unsaved changes, and reset Merino", EditorStyles.toolbarButton, GUILayout.Height(18), GUILayout.MaxWidth(80) ) )
				{
					if (EditorUtility.DisplayDialog("Merino: Unload all files?",
						"Are you sure you want to unload all files? All unsaved work will be lost.", "Unload all files", "Cancel"))
					{
						ResetMerino();
						return;
					}
				}
				
				GUILayout.FlexibleSpace();
				
				// autosave / save button
				if (!MerinoPrefs.useAutosave && FluidGUIButtonIcon( " Save All", MerinoEditorResources.Save, "save all changes to all files", EditorStyles.toolbarButton, GUILayout.Height(18), GUILayout.MaxWidth(70)))
				{
					MerinoCore.SaveDataToFiles();
					MerinoCore.ReimportFiles(true);
				}
				GUILayout.Space(4);
				EditorGUI.BeginChangeCheck();
				MerinoPrefs.useAutosave = EditorGUILayout.ToggleLeft(new GUIContent("Autosave?", "if enabled, will automatically save every change"), MerinoPrefs.useAutosave, EditorStyles.miniLabel, GUILayout.Width(80));
				GUILayout.Space(4);
				MerinoPrefs.showSyntaxReference = EditorGUILayout.ToggleLeft(new GUIContent("Show API?", "if enabled, will show a little cheat sheet of common Yarn script commands"), MerinoPrefs.showSyntaxReference, EditorStyles.miniLabel, GUILayout.Width(100) );
				if (EditorGUI.EndChangeCheck())
				{
					MerinoPrefs.SaveHiddenPrefs();
				}
			}
			
			GUILayout.FlexibleSpace();
			
			// help and documentation, with short and long buttons
			if ( FluidGUIButtonIcon(" Help & Docs", helpIcon, "open Merino help page / documentation in web browser", EditorStyles.toolbarButton, GUILayout.Width(90) ) )
			{
				Help.BrowseURL("https://github.com/radiatoryang/merino/wiki");
			}
			GUILayout.EndHorizontal();
			GUILayout.EndArea();
		}
		
				
		void DrawMainPane(Rect rect)
		{
			GUILayout.BeginArea(rect);
			GUILayout.Space(4);

//			bool forceSave = false;
			// int idToPreview = -1;
			int idToZoomTo = -1;
			if (MerinoData.ViewState.selectedIDs.Count > 0)
			{ 
				// optional: show syntax reference
				if ( MerinoPrefs.showSyntaxReference ) {
					EditorGUILayout.SelectableLabel( syntaxReference, MerinoStyles.SmallMonoTextStyle );
				}

				scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
				
				foreach (var id in MerinoData.ViewState.selectedIDs)
				{
					// if MerinoData.ViewState has been corrupted, then try to purge corrupt tree elements
					if (m_TreeView.treeModel.Find(id) == null) {
						MerinoData.ViewState.selectedIDs.Remove(id);
						EditorGUILayout.EndScrollView();
						break;
					}

					// DRAW FILE NODE ==================================================================
					if (m_TreeView.treeModel.Find(id).leafType == MerinoTreeElement.LeafType.File)
					{
						EditorGUILayout.BeginVertical(EditorStyles.helpBox);

						EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
						EditorGUILayout.SelectableLabel( m_TreeView.treeModel.Find(id).name, EditorStyles.boldLabel );
						if (GUILayout.Button(new GUIContent(" Unload File", resetIcon), GUILayout.Width(110)))
						{
							AddNodeToDelete(id);
							EditorGUILayout.EndHorizontal();
							EditorGUILayout.EndVertical();
							continue;
						}
						EditorGUILayout.EndHorizontal();

						EditorGUILayout.BeginHorizontal();
						var fileNode = m_TreeView.treeModel.Find(id);
						var textAsset = fileNode != null ? GetTextAssetForNode( fileNode ) : null;

						// if the text asset got deleted, then unload it automatically
						// if ( textAsset == null ) {
						// 	AddNodeToDelete(id, true);
						// 	EditorGUILayout.EndHorizontal();
						// 	EditorGUILayout.EndVertical();
						// 	continue;
						// }
						
						if ( textAsset != null ) {
							// if the file got renamed in Project tab, then we can refresh the node name here, at least
							if (textAsset.name != m_TreeView.treeModel.Find(id).name)
							{
								m_TreeView.treeModel.Find(id).name = textAsset.name;
								m_TreeView.Reload();
							}
							
							GUI.enabled = false;
							EditorGUILayout.ObjectField(textAsset, typeof(TextAsset), false);
							GUI.enabled = true;
							EditorGUILayout.SelectableLabel( AssetDatabase.GetAssetPath(textAsset) );
							EditorGUILayout.EndHorizontal();

							// if ( !MerinoPrefs.useAutosave) {
								EditorGUILayout.HelpBox("Editing .yarn.txt files directly is disabled. To write, select a node in the left sidebar.", MessageType.Warning);
							// }
							GUI.enabled = false; //MerinoPrefs.useAutosave;
							// EditorGUI.BeginChangeCheck();
							string newText = EditorGUILayout.TextArea(textAsset.text, MerinoStyles.GetBodyStyle(0) );
							// if ( EditorGUI.EndChangeCheck() ) {
							// 	File.WriteAllText( AssetDatabase.GetAssetPath(textAsset), newText );
							// 	MerinoData.Timestamp = EditorApplication.timeSinceStartup;
							// 	MerinoCore.MarkFileDirty( textAsset );
							// 	// save after commit if we're autosaving
							// 	if (MerinoData.CurrentFiles.Count > 0 && MerinoPrefs.useAutosave)
							// 	{
							// 		MerinoCore.SaveDataToFiles();
							// 		// TODO: how to update from text file?
							// 		MerinoCore.GetDataFromFile( textAsset, MerinoData.FileToNodeID[textAsset]);
							// 	}
							// }
							GUI.enabled = true;
						} else {
							EditorGUILayout.LabelField("Couldn't find the Text Asset; the file has been deleted or moved outside of the Assets folder.");
							EditorGUILayout.EndHorizontal();
							if ( GUILayout.Button("Save All + try to recover data and save as new .yarn.txt file") ) {
								MerinoCore.SaveDataToFiles();
							}
						}
						
						EditorGUILayout.EndVertical();
						EditorGUILayout.Space();
						continue;
					}

					// DRAW YARN NODE ===================================================================
					
					// start node container
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.BeginVertical(EditorStyles.helpBox);
					
					// NODE HEADER ======================================================================
					EditorGUILayout.BeginHorizontal();
					
					// add "Play" button
					string oldName = m_TreeView.treeModel.Find(id).name;
					DrawPlaytestButton(id, oldName, true, false );
					
					// node title
					string newName = EditorGUILayout.TextField(oldName, MerinoStyles.NameStyle);
					newName = MerinoCore.CleanNodeTitle(newName); // in v0.6, we strip forbidden characters from node titles

					// v0.6, button to focus on node in nodemap
					DrawNodemapButton( id, newName, EditorStyles.miniButton );

// node position now visible in nodemap button tooltip
// #if MERINO_DEVELOPER
// 					// display node position
// 					GUILayout.Label( "Pos: " + m_TreeView.treeModel.Find(id).nodePosition.ToString() );
// #endif
					
					// display file parent
					var fileParent = GetFileParent(m_TreeView.treeModel.Find(id));
					if ( FluidGUIButtonIcon(fileParent.name.Length > 20 ? " View File" : " " + fileParent.name + ".txt", textIcon, "click to view " + fileParent.name + ".txt", EditorStyles.miniButton, GUILayout.Width(0), GUILayout.MaxWidth(200), GUILayout.Height(20) ) )
					{
						idToZoomTo = fileParent.id;
					}

					GUILayout.FlexibleSpace();
					
					// delete button
					if ( GUILayout.Button(new GUIContent(deleteIcon, "click to delete this node"), GUILayout.Width(26), GUILayout.Height(20) ) )
					{
						AddNodeToDelete(id);
					}
					EditorGUILayout.EndHorizontal();

					
					// NODE BODY ======================================================================
					var backupContentColor = GUI.contentColor;
					string passage = DoKeyboardTabReplacement( m_TreeView.treeModel.Find(id).nodeBody );
					float height = EditorStyles.textArea.CalcHeight(new GUIContent(passage), rect.width);
					
					// start preparing to draw the body
					int lineDigits = -1;
					int totalLineCount = -1;
					int[] lineToCharIndex; // used to save charIndex for the start of each line number, so we can later calculate rects for these line numbers if we have to
					string lineNumbers = AddLineNumbers(passage, out lineToCharIndex, out lineDigits, out totalLineCount);
					
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
					bool forceSave = false; // a hack so that DoKeyboardTabReplacement() can force a save
					for( int chunkIndex=0; chunkIndex<passageChunks.Length; chunkIndex++) {
						int chunkStart = chunkIndex * chunkSize; // line number this chunk starts at
						int chunkEnd = (chunkIndex + 1) * chunkSize; // line number this chunk ends at
						
						GUI.contentColor = new Color ( 0f, 0f, 0f, 0.16f ); // the text you type is actually invisible
						string controlName = "TextArea" + newName + chunkIndex.ToString();
						GUI.SetNextControlName ( controlName );
						
						// draw text area
						int nextControlID = GUIUtility.GetControlID(FocusType.Passive) + 1;
						newBodies[chunkIndex] = EditorGUILayout.TextArea(passageChunks[chunkIndex], MerinoStyles.GetBodyStyle(lineDigits), GUILayout.Height(0f), GUILayout.ExpandHeight(true), GUILayout.MaxHeight(height));
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
					
						// line number positioning (just slightly to the left)
						Rect linesRect = new Rect(bodyRect);
						linesRect.x -= lineDigits * 12;
						// draw the line numbers
						GUI.Label(linesRect, numberChunks[chunkIndex], MerinoStyles.GetHighlightStyle(lineDigits, 0.45f));
						
						// syntax highlight via label overlay
						Rect lastRect = new Rect(bodyRect);
						// for v0.5.4, I explored a "display whitespace characters" option, but unfortunately IMGUI doesn't recognize the characters as spaces, so wordwrap gets broken
						// so for now, this is unused, pending further research or need.  -RY, 9 July 2019
						// .Replace(' ', '·')
						string syntaxHighlight = DoSyntaxMarch(newBodies[chunkIndex]); // inserts richtext <color> tags to do highlighting
						GUI.Label(lastRect, syntaxHighlight, MerinoStyles.GetHighlightStyle(lineDigits, 0.8f)); // drawn on top of actual TextArea
					
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


                            // NEW APPROACH TO HANDLING KEYBOARD TABS: replace all tab characters with equivalent spaces, and move caret forward

							// TODO: this is still very hacky, because I still don't know how exactly to time when the tab character gets inserted into TextArea
							// so for now, this is just approximating the tab count with a timed delay... works OK as long as you don't hold down TAB?
                            if (MerinoPrefs.tabSize > 0 && Event.current.type != EventType.Layout)
                            {
								// move keyboard caret forward
                                if (moveCursorAfterTab)
                                {
                                    te.cursorIndex += MerinoPrefs.tabSize - 1;
                                    te.selectIndex = te.cursorIndex;
                                    forceSave = true;
                                    moveCursorAfterTab = false;
                                }

                                // keyboard tab replacement:
                                te.text = DoKeyboardTabReplacement(te.text);
                                if ((Event.current.keyCode == KeyCode.Tab || Event.current.character == '\t') && te.text.Length > te.cursorIndex && EditorApplication.timeSinceStartup > lastTabTime + 0.1)
                                {
                                    lastTabTime = EditorApplication.timeSinceStartup;
                                    moveCursorAfterTab = true;
                                    forceSave = true;
                                }

                                // keyboard tabbed backspace: delete X increments of consecutive spaces backwards
                                if (MerinoPrefs.useTabbedBackspace && Event.current.keyCode == KeyCode.Backspace && EditorApplication.timeSinceStartup > lastTabTime + 0.1)
                                {
                                    if ( te.cursorIndex-MerinoPrefs.tabSize >= 0 && String.IsNullOrWhiteSpace( te.text.Substring(te.cursorIndex-MerinoPrefs.tabSize, MerinoPrefs.tabSize) ) ) {
										te.selectIndex -= MerinoPrefs.tabSize-1;
										te.DeleteSelection();
										newBodies[chunkIndex] = te.text;
									}
                                    lastTabTime = EditorApplication.timeSinceStartup;
                                    forceSave = true;
                                }
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
							var errorLinesToIndices = errors.Select(e => lineToCharIndex[e.lineNumber-1] - chunkCharOffset).ToArray(); // change errors' line numbers into string index
							var indicesToRects = CalculateTextEditorIndexToRect(te, MerinoStyles.GetBodyStyle(lineDigits), errorLinesToIndices); // change errors' string index to rect
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
									var zoomRect = CalculateTextEditorIndexToRect(te, MerinoStyles.GetBodyStyle(lineDigits), new int[] {charCursorIndex});

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

					// bottom of node container: playtest button and +add new node button
					GUILayout.BeginHorizontal();
					// if (GUILayout.Button(new GUIContent("▶ Playtest", "click to playtest this node"), GUILayout.Width(80) ) )
					// {
					// 	idToPreview = id;
					// }
					if (FluidGUIButtonIcon(" Add New Node", MerinoEditorResources.NodeAdd, "click to add a new node with the same parent (sibling)", GUI.skin.button, GUILayout.Height(18), GUILayout.Width(120) ) )
					{
						AddNewNode( new List<int> {treeView.treeModel.Find(id).parent.id} );
					}
					GUILayout.FlexibleSpace();
					DrawPlaytestButton( id );
					GUILayout.EndHorizontal();
					
					// close out node container				
					EditorGUILayout.EndVertical();
					EditorGUILayout.Space();
					EditorGUILayout.Separator();
					
					// did user edit something?
					if (EditorGUI.EndChangeCheck() || forceSave )
					{
						// remember last edited node, for the "playtest this" button in lower-right corner
						currentNodeIDEditing = id;
						
						// undo begin
						Undo.RecordObject(MerinoData.Instance, "Merino > " + newName );
						
						// actually commit the data now
						m_TreeView.treeModel.Find(id).name = newName;
						m_TreeView.treeModel.Find(id).nodeBody = newBody;
						MerinoData.EditedID = id;
						MerinoData.Timestamp = EditorApplication.timeSinceStartup;
						MerinoCore.MarkFileDirty( GetTextAssetForNode( m_TreeView.treeModel.Find(id) ) );
						EditorUtility.SetDirty( MerinoData.Instance );
						
						// log the undo data
						undoData.Add( new MerinoUndoLog(id, EditorApplication.timeSinceStartup, newBody) );
						
						// save after commit if we're autosaving
						if (MerinoData.CurrentFiles.Count > 0 && MerinoPrefs.useAutosave)
						{
							MerinoCore.SaveDataToFiles();
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
				if (MerinoData.ViewState.selectedIDs.Count == 1)
				{
					currentNodeIDEditing = MerinoData.ViewState.selectedIDs[0];
				}
				
				// detect if we need to do play preview
				// if (idToPreview > -1)
				// {
				// 	MerinoPlaytestWindow.PlaytestFrom( m_TreeView.treeModel.Find(idToPreview).name);
				// }
				
				// detect if we need to zoom to a different node ID (can't zoom while in the foreach, that modifies the collection)
				if (idToZoomTo > -1)
				{
					SelectNodeAndZoomToLine( idToZoomTo, -1);
					idToZoomTo = -1;
				}
			}
			else
			{
				DrawDefaultHelpBox(rect);
			}

			GUILayout.EndArea();
		}
		
		#region MainPaneUtility

		void DrawDefaultHelpBox(Rect rect)
		{
			var helpRect = new Rect(rect);
			helpRect.x = margin*2;
			helpRect.y = margin*2;
			helpRect.width -= margin * 4;
			helpRect.height /= 2;
			GUILayout.BeginArea(helpRect);
				
			if (MerinoData.CurrentFiles.Count == 0)
			{
				EditorGUILayout.HelpBox(
					" To write anything, you must load at least one file.\n" +
					" - Click the [Create] dropdown, or click the [+ Folder] or [+ File] button.\n" +
					" - For info and advice, click [Help & Docs].",
					MessageType.Info
				);
				GUILayout.Space(8);
				GUILayout.BeginHorizontal();
				if ( GUILayout.Button( new GUIContent(" Create File", MerinoEditorResources.PageNew, "Create a new .yarn.txt file and load it into Merino"), GUILayout.Height(18), GUILayout.Width(100) ))
				{
					CreateNewYarnFile();
				}
				GUILayout.Space(8);
				if ( GUILayout.Button( new GUIContent(" Add Folder ", folderIcon, "Load all .yarn.txt files in a folder (and its subfolders) into Merino"), GUILayout.Height(18), GUILayout.Width(105) ))
				{
					LoadFolder();
				}
				GUILayout.Space(8);
				if ( GUILayout.Button( new GUIContent(" Add File", textIcon, "Load a single .yarn.txt file into Merino"), GUILayout.Height(18), GUILayout.Width(80) ))
				{
					LoadFile();
				}
				GUILayout.EndHorizontal();
					
			}
			else
			{
				EditorGUILayout.HelpBox(
					" Select node(s) from the sidebar on the left.\n - Left-Click:  select\n - Left-Click + Shift:  select multiple\n - Left-Click + Ctrl / Left-Click + Command:  add / remove from selection",
					MessageType.Info);
			}
				
			GUILayout.EndArea();
		}

        string DoKeyboardTabReplacement(string text)
        {
			if ( MerinoPrefs.tabSize > 0 ) {
				string tabReplace = new string(' ', MerinoPrefs.tabSize);
				text = text.Replace( "\t", tabReplace );
			}
            return text;
		}

		string DoSyntaxMarch(string text)
		{
			var textLines = text.Split('\n');
			for (int i = 0; i < textLines.Length; i++)
			{
				var newColor = CheckSyntax(textLines[i]);
				string cleanTextLine = SanitizeRichText(textLines[i]);
				if (newColor != Color.white)
				{
					string hexColor = ColorUtility.ToHtmlStringRGB(newColor);
					textLines[i] = string.Format("<color=#{0}>{1}</color>", hexColor, cleanTextLine );
				}
				else
				{
					textLines[i] = cleanTextLine;
				}
			}

			return string.Join("\n", textLines);
		}

		Color CheckSyntax (string syntax )
		{
			string newSyntax = syntax.Replace("\t", "").TrimEnd(' ').TrimStart(' '); // cleanup string
			if ( newSyntax.StartsWith ( "//" ) )
			{
				return MerinoPrefs.highlightComments;
			} else if (newSyntax.StartsWith("->") )
			{
				return MerinoPrefs.highlightShortcutOptions;
			} else if (newSyntax.StartsWith("[["))
			{
				return MerinoPrefs.highlightNodeOptions;
			}else if (newSyntax.StartsWith("<<"))
			{
				return MerinoPrefs.highlightShortcutOptions;
			}
			else
			{
				return Color.white;
			}
		}

		const int fluidGUIThreshold = 500;
		bool FluidGUIButtonIcon(string buttonTitle, Texture buttonIcon, string buttonTooltip, GUIStyle buttonStyle, params GUILayoutOption[] longParams)
		{
			return (position.width-MerinoPrefs.sidebarWidth <= fluidGUIThreshold && GUILayout.Button(new GUIContent(buttonIcon, buttonTooltip), buttonStyle, GUILayout.Height(20), GUILayout.Width(24)))
			       || (position.width-MerinoPrefs.sidebarWidth > fluidGUIThreshold && GUILayout.Button(new GUIContent(buttonTitle, buttonIcon, buttonTooltip), buttonStyle, longParams) );
		}

		string SanitizeRichText(string lineText)
		{
			// look for user-generated rich text tags, and replace the "<" and ">" angle brackets with harmless look-alikes
			return lineText.Replace('<', '‹').Replace('>', '›');
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
				lines[i] = lineDisplay + invisibleBegin + SanitizeRichText(lines[i].Remove(0, digits)) + invisibleEnd;
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

			using (new EditorGUILayout.HorizontalScope (EditorStyles.helpBox))
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
					if (GUILayout.Button(new GUIContent( node == null ? error.message : node.name + ":" + error.lineNumber.ToString(), errorIcon, node == null ? error.message : string.Format("{2}\n\nclick to open node {0} near line {1}", node.name, error.lineNumber, error.message )), EditorStyles.miniButtonLeft, GUILayout.MaxWidth(position.width * 0.31f), GUILayout.Height(15) ))
					{
						if (node != null)
						{
							SelectNodeAndZoomToLine(error.nodeID, error.lineNumber);
						}
						else
						{
							EditorUtility.DisplayDialog("Merino Error Message!", "Merino error message:\n\n" + error.message, "Close");
						}
					} // dismiss error dialog
					if (GUILayout.Button("x", EditorStyles.miniButtonRight ) ) {
						errorLog.Clear();
					}
				} 

				GUILayout.FlexibleSpace ();
				
				// contextual buttons, based on the last node you touched or edited
				if (currentNodeIDEditing > -1 
					&& treeView.treeModel.Find(currentNodeIDEditing) != null 
				    && treeView.treeModel.Find(currentNodeIDEditing).leafType == MerinoTreeElement.LeafType.Node
				) 
				{
					string nodeName = treeView.treeModel.Find(currentNodeIDEditing).name;
					DrawNodemapButton( currentNodeIDEditing, nodeName, GUI.skin.button );
					DrawPlaytestButton( currentNodeIDEditing, nodeName );

					// lower-right corner is reserved for resizing the window, so don't put buttons here
					// word count, based on last node you touched
					if ( currentNodeWordCountCache > -1 ) {
						var wcStyle = new GUIStyle( GUI.skin.label );
						wcStyle.fontSize = 8;
						wcStyle.alignment = TextAnchor.MiddleRight;
						GUILayout.Label( currentNodeWordCountCache.ToString() + " words  ", wcStyle, GUILayout.Width(55), GUILayout.Height(20) );
					}
				}
			}

			GUILayout.EndArea();
		}

		void DrawNodemapButton(int nodeID, string nodeName, GUIStyle style) { 
			if ( FluidGUIButtonIcon(
				" Map: " + nodeName, 
				MerinoEditorResources.Nodemap, 
				"click to focus on node " + nodeName + " in the nodemap\nnode position: " + m_TreeView.treeModel.Find(nodeID).nodePosition.ToString(),
				style,
				GUILayout.Height(20) ) ) 
			{
				var nodemap = MerinoNodemapWindow.GetNodemapWindow();
				nodemap.FocusNode(currentNodeIDEditing);
				nodemap.SetSelectedNode(currentNodeIDEditing);
			}
		}

		// also used in node map, but mostly in the editor window still
		public static void DrawPlaytestButton(int playtestNodeID, string nodeName = null, bool iconButton = false, bool includeDropdown = true, float dropdownFlexWidth = 78 ) {
			if ( string.IsNullOrEmpty(nodeName) )
				nodeName = MerinoData.GetNode(playtestNodeID).name;

			string label = "▶";
			if ( !iconButton ) {
				label = "▶ " + nodeName;
			}
			var content = new GUIContent(label, "click to playtest " + nodeName );
			switch ( MerinoPrefs.playtestScope ) {
				case MerinoPrefs.PlaytestScope.AllFiles:
					content.tooltip += " (and also include all currently loaded nodes)";
				break;
				case MerinoPrefs.PlaytestScope.SameFile:
					content.tooltip += " (and also include all other nodes in the same .yarn.txt file)";
				break;
				case MerinoPrefs.PlaytestScope.NodeOnly:
					content.tooltip += " (playtest only this node, and no other nodes)";
				break;
			}
			if (GUILayout.Button(content, !includeDropdown ? GUI.skin.button : MerinoStyles.ButtonLeft, GUILayout.MinWidth(22), GUILayout.Width( 22 ), GUILayout.MaxWidth( iconButton ? 22 : MerinoStyles.ButtonLeft.CalcSize(content).x), GUILayout.Height(20) ))
			{
				MerinoPlaytestWindow.PlaytestFrom( nodeName, MerinoCore.GetPlaytestParentID(playtestNodeID) );
			}

			if ( includeDropdown ) {
				bool oldGUIChanged = GUI.changed;
				GUILayout.Box("", MerinoStyles.ButtonRight, GUILayout.Width(dropdownFlexWidth), GUILayout.Height(20) );
				var enumRect = GUILayoutUtility.GetLastRect();
				enumRect.x += 4;
				enumRect.width -= 8;
				enumRect.y += 2;
				var newPrefs = (MerinoPrefs.PlaytestScope)EditorGUI.EnumPopup(enumRect, MerinoPrefs.playtestScope, MerinoStyles.DropdownRightOverlay );
				// if ( EditorGUI.EndChangeCheck() ) {
				if ( newPrefs != MerinoPrefs.playtestScope ) {
					MerinoPrefs.playtestScope = newPrefs;
					MerinoPrefs.SaveHiddenPrefs();
				}
				// }
				GUI.changed = oldGUIChanged;
			}
		}

		public static int GetWordCount(string text) {
        	MatchCollection collection = Regex.Matches(text, @"[\S]+");
        	return collection.Count;
		}

		// a lot of the logic for this is handled in OnGUI > DrawMainPane, this just sets variables to get read elsewhere
		public void SelectNodeAndZoomToLine(int nodeID, int lineNumber)
		{
			MerinoData.ViewState.selectedIDs.Clear();
			MerinoData.ViewState.selectedIDs.Add(nodeID);
			// treeView.SetSelection(new List<int>() { nodeID });

			// as of v0.5.3, we have a separate playtest editorWindow; if user clicks "View Source" without an open text editorWindow, it throws null reference
			if ( treeView == null || treeView.treeModel == null ) return; 
			
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

		public override void Refresh()
		{
			Repaint();
		}
	}
}
