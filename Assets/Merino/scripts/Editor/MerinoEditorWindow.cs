using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Net;
using System.Text;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Security.Permissions;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Experimental.PlayerLoop;
using UnityEngine.UI;
using UnityEngine.Windows;
using Yarn;
using Yarn.Unity;
using File = System.IO.File;
using EditorCoroutines;
using TreeEditor;
using Directory = System.IO.Directory;
using Random = System.Random;

namespace Merino
{

	[HelpURL("http://example.com/docs/MyComponent.html")]
	class MerinoEditorWindow : EditorWindow
	{
		[NonSerialized] bool m_Initialized;
		[SerializeField] TreeViewState viewState; // Serialized in the window layout file so it survives assembly reloading
		[SerializeField] MultiColumnHeaderState m_MultiColumnHeaderState;
		[SerializeField] bool useAutosave = false;
		bool doubleClickOpensFile = true;
		
		[NonSerialized] float sidebarWidth = 180f;
		float playPreviewHeight
		{
			get { return isDialogueRunning ? 180f : 0f; }
		}
		[SerializeField] Vector2 scrollPos;
		// double lastTabTime = 0.0; // this is a really bad hack
		
		SearchField m_SearchField;
		[SerializeField] MerinoTreeView m_TreeView;
		public MerinoTreeView treeView
		{
			get { return m_TreeView; }
		}
		[SerializeField] MerinoTreeData treeData;
	//	MyTreeAsset m_MyTreeAsset;

		Texture helpIcon;
		TextAsset currentFile;
		Font monoFont;
		
		// better undo support
		double lastUndoTime;
		int moveCursorUndoID, moveCursorUndoIndex;
		[SerializeField] List<MerinoUndoLog> undoData = new List<MerinoUndoLog>();
		
		// Yarn Spinner running stuff
		MerinoDialogueUI dialogueUI;
		Dialogue _dialogue;
		Dialogue dialogue {
			get {
				if (_dialogue == null) {
					// Create the main Dialogue runner, and pass our variableStorage to it
					_dialogue = new Yarn.Dialogue ( new MerinoVariableStorage() );
					dialogueUI = new MerinoDialogueUI();
					dialogueUI.consolasFont = monoFont;
					
					// Set up the logging system.
					_dialogue.LogDebugMessage = delegate (string message) {
						Debug.Log (message);
					};
					_dialogue.LogErrorMessage = delegate (string message) {
						Debug.LogError (message);
					};
				}
				return _dialogue;
			}
		}

		[MenuItem("Window/Merino (Yarn Editor)")]
		public static MerinoEditorWindow GetWindow ()
		{
			var window = GetWindow<MerinoEditorWindow>();
			window.titleContent = new GUIContent("Merino (Yarn)");
			window.Focus();
			window.Repaint();
			return window;
		}

		[OnOpenAsset]
		public static bool OnOpenAsset (int instanceID, int line)
		{
//			var myTreeAsset = EditorUtility.InstanceIDToObject (instanceID) as MyTreeAsset;
//			if (myTreeAsset != null)
//			{
//				var window = GetWindow ();
//				window.SetTreeAsset(myTreeAsset);
//				return true;
//			}
			var myTextAsset = EditorUtility.InstanceIDToObject(instanceID) as TextAsset;
			if (myTextAsset != null)
			{
				var window = GetWindow ();
				return window.SetTreeAsset(myTextAsset);
			}
			return false; // we did not handle the open
		}

		bool SetTreeAsset (TextAsset myTextAsset)
		{
			if (doubleClickOpensFile && IsProbablyYarnFile(myTextAsset))
			{
				currentFile = myTextAsset;
				m_Initialized = false;
				return true;
			}
			else
			{
				return false;
			}
		}

		Rect multiColumnTreeViewRect
		{
			get { return new Rect(10, 30, sidebarWidth, position.height-40); }
		}

		Rect toolbarRect
		{
			get { return new Rect (10f, 10f, sidebarWidth, 20f); }
		}

		Rect nodeEditRect
		{
			get { return new Rect( sidebarWidth+15f, 10, position.width-sidebarWidth-20, position.height-20-playPreviewHeight);} // was height-30
		}
		
		Rect playPreviewRect
		{
			get { return new Rect( sidebarWidth+15f, position.height-10-playPreviewHeight, position.width-sidebarWidth-15, playPreviewHeight-10);}
		}

		Rect bottomToolbarRect
		{
			get { return new Rect(20f, position.height - 18f, position.width - 40f, 16f); }
		}

		void InitIfNeeded (bool ignoreSelection=false)
		{
			if (m_Initialized) return;

			Undo.undoRedoPerformed += OnUndo;
			
			// load font
			if (monoFont == null)
			{
				monoFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Merino/Fonts/Inconsolata-Regular.ttf");
			}
			
			// load help icon
			if (helpIcon == null)
			{
				helpIcon = EditorGUIUtility.IconContent("_Help").image;
			}
			
			// Check if it already exists (deserialized from window layout file or scriptable object)
			if (viewState == null)
				viewState = new TreeViewState();

			bool firstInit = m_MultiColumnHeaderState == null;
			var headerState = MerinoTreeView.CreateDefaultMultiColumnHeaderState(multiColumnTreeViewRect.width);
			if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_MultiColumnHeaderState, headerState))
				MultiColumnHeaderState.OverwriteSerializedFields(m_MultiColumnHeaderState, headerState);
			m_MultiColumnHeaderState = headerState;
				
			var multiColumnHeader = new MyMultiColumnHeader(headerState);
			if (firstInit)
				multiColumnHeader.ResizeToFit ();

			treeData = ScriptableObject.CreateInstance<MerinoTreeData>();
			undoData.Clear();
			var treeModel = new TreeModel<MerinoTreeElement>(GetData());
				
			m_TreeView = new MerinoTreeView(viewState, multiColumnHeader, treeModel);

			m_SearchField = new SearchField();
			m_SearchField.downOrUpArrowKeyPressed += m_TreeView.SetFocusAndEnsureSelectedItem;

			m_Initialized = true;

			if (!ignoreSelection)
			{
				OnSelectionChange();
			}
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
				var treeDataNode = treeData.treeElements.Where(x => x.id == recent.id).First();
				
				moveCursorUndoID = recent.id;
				
				int newIndex = CompareStringsAndFindDiffIndex(recent.bodyText, treeDataNode.nodeBody); // TODO: strip all "\" out?
				if (moveCursorUndoIndex == -1 || newIndex >= 0) // sometimes it suddenly spits out -1 when the logging is slightly off, so we need to ignore it in that case
				{
					moveCursorUndoIndex = newIndex;
				}
			}
			
			// repaint tree view so names get updated
			m_TreeView.Reload();
			if (currentFile != null && useAutosave)
			{
				SaveDataToFile();
			}
		}


		IList<MerinoTreeElement> GetData ()
		{
//			if (m_MyTreeAsset != null && m_MyTreeAsset.treeElements != null && m_MyTreeAsset.treeElements.Count > 0)
//				return m_MyTreeAsset.treeElements;


			var treeElements = new List<MerinoTreeElement>();
			var root = new MerinoTreeElement("Root", -1, 0);
			treeElements.Add(root);
			
			// extract data from the text file
			if (currentFile != null)
			{
				AssetDatabase.Refresh();
				//var format = YarnSpinnerLoader.GetFormatFromFileName(AssetDatabase.GetAssetPath(currentFile));
				var nodes = YarnSpinnerLoader.GetNodesFromText(currentFile.text, NodeFormat.Text);
				foreach (var node in nodes)
				{
					// clean some of the stuff to help prevent file corruption
					string cleanName = CleanYarnField(node.title, true);
					string cleanBody = CleanYarnField(node.body);
					string cleanTags = CleanYarnField(node.tags, true);
					// write data to the objects
					var newItem = new MerinoTreeElement( cleanName ,0,treeElements.Count);
					newItem.nodeBody = cleanBody;
					newItem.nodePosition = new Vector2Int(node.position.x, node.position.y);
					newItem.nodeTags = cleanTags;
					treeElements.Add(newItem);
				}
			}
			else 
			{ // generate default data
				var start = new MerinoTreeElement("Start", 0, 1);
				start.nodeBody = "This is the Start node. Write the beginning of your Yarn story here." +
				                 "\n// You can make comments with '//' and the player won't see it." +
				                 "\n\n// There's two ways to do choices in Yarn. The most basic way is to link to other nodes like this:" +
				                 "\n[[Go see more examples|Start_MoreExamples]]\n[[Actually, let's restart this node again|Start]]" +
				                 "\n\n// IMPORTANT: node options are only offered at the end of the passage\nDo you want to read more about Yarn features?";
								 

				var start2 = new MerinoTreeElement("Start_MoreExamples", 0, 2);
				start2.nodeBody = "This node is called Start_MoreExamples.\nThe second way to do choices in Yarn is with 'shortcut options' like this:" +
				                  "\n\n->This is option 1\n\tYou selected option 1." +
				                  "\n->This is option 2\n\tYou selected option 2.\n\t<<set $didOption2 to true>>" +
				                  "\n\nBased on choices, you can set variables, and then check those variables later.\n<<if $didOption2 is true>>\nBy checking $didOption2, I remember you chose option 2!\n<<else>>\nI can't detect a variable $didOption2, so that means you chose option 1\n<<endif>>" +
				                  "\n\nDo you want to go back to Start now?\n-> Yes, send me back to Start.\n\t[[Start]]\n-> No thanks, I want to stop.\n\nOk here's the end, good luck!";
				
				treeElements.Add(start);
				treeElements.Add(start2);
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
			var treeNodes = treeData.treeElements; // m_TreeView.treeModel.root.children;
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

		/// <summary>
		/// Checks to see if TextAsset is a probably valid .yarn.txt file, or if it's just a random text file
		/// </summary>
		/// <param name="textAsset"></param>
		/// <returns></returns>
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

		void DeleteNode(int id)
		{
			m_TreeView.treeModel.RemoveElements( new List<int>() {id});
			if (viewState.selectedIDs.Contains(id))
			{
				viewState.selectedIDs.Remove(id);
			}
			SaveDataToFile();
		}

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

		void OnGUI ()
		{
			InitIfNeeded();

			SearchBar (toolbarRect);
			DoTreeView (multiColumnTreeViewRect);

			if (viewState != null)
			{
				DrawMainPane(nodeEditRect);
			}

			if (dialogueUI != null && isDialogueRunning)
			{
				PreviewToolbar(playPreviewRect);
				// if not pressed, then show the normal play preview
				dialogueUI.OnGUI(playPreviewRect);
			}

		//	BottomToolBar (bottomToolbarRect);
		}

		[SerializeField] bool stopOnDialogueEnd = true;
		[SerializeField] bool autoAdvance = false;
		void PreviewToolbar(Rect rect)
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
				GUILayout.Width(200)
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
			if (GUILayout.Button(new GUIContent("View Node Source", "click to see Yarn script for this node"), EditorStyles.toolbarButton))
			{
				var matchingNode = treeData.treeElements.Find(x => x.name == dialogue.currentNode);
				if (matchingNode != null)
				{
					viewState.selectedIDs.Clear();
					viewState.selectedIDs.Add(matchingNode.id);
				}
			}
			GUI.enabled = true;

			GUILayout.FlexibleSpace();
			// stop on dialogue end
			autoAdvance = EditorGUILayout.ToggleLeft(new GUIContent("Auto Advance?", "automatically advance dialogue, with no user input, until there's a choice"), autoAdvance, GUILayout.Width(120));
			GUILayout.FlexibleSpace();
			// stop on dialogue end
			stopOnDialogueEnd = EditorGUILayout.ToggleLeft(new GUIContent("Close On End?", "when dialogue terminates, stop and close playtest session automatically"), stopOnDialogueEnd, GUILayout.Width(120));
			// stop button
			var backupColor = GUI.backgroundColor;
			GUI.backgroundColor = Color.red;
			if (GUILayout.Button(new GUIContent("Close", "click to force stop the playtest preview and close it"), EditorStyles.toolbarButton))
			{
				ForceStopDialogue();
			}

			GUI.backgroundColor = backupColor;
			EditorGUILayout.EndHorizontal();
			GUILayout.EndArea();
		}
		

		void SearchBar (Rect rect)
		{
			treeView.searchString = m_SearchField.OnGUI (rect, treeView.searchString);
		}

		void DoTreeView (Rect rect)
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
//				if (addNewNode)
//				{
					AddNewNode();
//				}
			}
			rect.y += BUTTON_HEIGHT;
			rect.height -= BUTTON_HEIGHT;
			m_TreeView.OnGUI(rect);
		}

		void ResetMerino()
		{
			currentFile = null;
			viewState = null;
			m_Initialized = false;
			ForceStopDialogue();
			Selection.objects = new UnityEngine.Object[0]; // deselect all
			Undo.undoRedoPerformed -= OnUndo;
			InitIfNeeded(true);
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

		void PlaytestFrom(string startPassageName, bool reset=true)
		{
			if (reset)
			{
				dialogue.UnloadAll();
				dialogue.LoadString(SaveNodesAsString());
			}
			this.StopAllCoroutines();

			// use EditorCoroutines to run the dialogue
			this.StartCoroutine(RunDialogue(startPassageName));
		}

		// this is basically just ripped from YarnSpinner/DialogueRunner.cs
		[NonSerialized] bool isDialogueRunning;
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
	                yield return this.StartCoroutine(this.dialogueUI.RunLine(lineResult.line, autoAdvance));
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
	                yield return this.StartCoroutine( dialogueUI.RunCommand(commandResult.command, autoAdvance));
//                    }


                } else if(step is Yarn.Dialogue.NodeCompleteResult) {

                    // Wait for post-node action
                    var nodeResult = step as Yarn.Dialogue.NodeCompleteResult;
                    // yield return this.StartCoroutine (this.dialogueUI.NodeComplete (nodeResult.nextNode));
                }
            }
	        Debug.Log("Merino: reached the end of the dialogue.");
	        
            // No more results! The dialogue is done.
            // yield return this.StartCoroutine (this.dialogueUI.DialogueComplete ());
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

		bool spaceWillIncrementUndo = false;

		void DrawMainPane(Rect rect)
		{
			GUILayout.BeginArea(rect);
			
			GUILayout.BeginHorizontal();
			if (currentFile != null)
			{
				useAutosave = EditorGUILayout.Toggle(new GUIContent("", "if enabled, will automatically save every change"), useAutosave, GUILayout.Width(16));
				GUILayout.Label(new GUIContent("AutoSave?   ", "if enabled, will automatically save every change"), GUILayout.Width(0), GUILayout.ExpandWidth(true), GUILayout.MaxWidth(80) );
				if (!useAutosave && GUILayout.Button( new GUIContent("Save", "save all changes to the current .yarn.txt file"), GUILayout.MaxWidth(60)))
				{
					SaveDataToFile();
					AssetDatabase.ImportAsset( AssetDatabase.GetAssetPath(currentFile));
				}
			}
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
				ResetMerino();
				return;
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
			// help and documentation
			if (GUILayout.Button(new GUIContent(helpIcon, "click to open Merino help / documentation in web browser"), EditorStyles.label, GUILayout.Width(24)) )
			{
				Help.BrowseURL("https://github.com/radiatoryang/merino/wiki");
			}
			GUILayout.EndHorizontal();

			int idToDelete = -1;
//			bool forceSave = false;
			int idToPreview = -1;
			if (viewState.selectedIDs.Count > 0)
			{
				scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
				// GUI.enabled = !isDialogueRunning; // if dialogue is running, don't let them edit anything
				
				foreach (var id in viewState.selectedIDs)
				{
					// start node container
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.BeginVertical(EditorStyles.helpBox);
					
					// NODE HEADER
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
						idToDelete = id;
					}
					EditorGUILayout.EndHorizontal();
					
					
					// NODE BODY
					var backupContentColor = GUI.contentColor;
					GUI.contentColor = GUI.enabled==false ? backupContentColor : new Color ( 0f, 0f, 0f, 0.16f );
					string passage = m_TreeView.treeModel.Find(id).nodeBody;
					float height = EditorStyles.textArea.CalcHeight(new GUIContent(passage), rect.width);
					
					// stuff for manual keyboard tab support
//					var te = (TextEditor)GUIUtility.GetStateObject( typeof (TextEditor), GUIUtility.keyboardControl );
					var te = typeof(EditorGUI)
						.GetField("activeEditor", BindingFlags.Static | BindingFlags.NonPublic)
						.GetValue(null) as TextEditor;
					GUI.SetNextControlName ( "TextArea" + newName );
					
					// draw the body
					var bodyStyle = new GUIStyle( EditorStyles.textArea );
					bodyStyle.font = monoFont;
					string newBody = EditorGUILayout.TextArea(passage, bodyStyle, GUILayout.Height(0f), GUILayout.ExpandHeight(true), GUILayout.MaxHeight(height));
					GUI.contentColor = backupContentColor;
					
					// only run the fancier stuff if we're not in playtesting mode
					if (GUI.enabled)
					{
						// manual tab support for GUILayout.TextArea... no longer needed for EditorGUILayout.TextArea
//						string oldBody = newBody;
//						newBody = KeyboardTabSupport(newBody, te);
//						if (oldBody.Length != newBody.Length)
//						{
//							forceSave = true;
//						}

						if ( moveCursorUndoID == id && moveCursorUndoIndex >= 0 && te != null && GUIUtility.keyboardControl == te.controlID )
						{
							te.cursorIndex = moveCursorUndoIndex;
							te.selectIndex = moveCursorUndoIndex;
							// moveCursorUndo* will get blanked out after the foreach loop
						}
						
						// detect whether to increment the undo group
						if (te != null && GUIUtility.keyboardControl == te.controlID)
						{
							var e = Event.current;
							if (spaceWillIncrementUndo && e.isKey && (e.keyCode == KeyCode.Space || e.keyCode == KeyCode.Tab || e.keyCode == KeyCode.Return))
							{
								Undo.IncrementCurrentGroup();
								spaceWillIncrementUndo = false;
							}

							if (!spaceWillIncrementUndo && e.isKey && e.keyCode != KeyCode.Space)
							{
								spaceWillIncrementUndo = true;
							}
						}

						// syntax highlight via label overlay
						Rect lastRect = GUILayoutUtility.GetLastRect();
						lastRect.x += 2;
						lastRect.y += 1f;
						lastRect.width -= 6;
						lastRect.height -= 1;
						string syntaxHighlight = DoSyntaxMarch(newBody);

						GUIStyle highlightOverlay = new GUIStyle();
						highlightOverlay.font = monoFont;
						highlightOverlay.normal.textColor = (EditorGUIUtility.isProSkin ? Color.white : Color.black) * 0.8f;
						highlightOverlay.richText = true;
						highlightOverlay.wordWrap = true;
						
						GUI.Label(lastRect, syntaxHighlight, highlightOverlay);
					}

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
						// undo stuff
						Undo.RecordObject(treeData, "Merino > " + newName );
						
						m_TreeView.treeModel.Find(id).name = newName;
						m_TreeView.treeModel.Find(id).nodeBody = newBody;
						treeData.editedID = id;
						treeData.timestamp = EditorApplication.timeSinceStartup;
						
						// log the undo data
						undoData.Add( new MerinoUndoLog(id, EditorApplication.timeSinceStartup, newBody) );
						
						if (currentFile != null && useAutosave)
						{
							SaveDataToFile();
						}
						
						// repaint tree view so names get updated
						m_TreeView.Reload();
					}
				}

				// delete the node with this ID
				if (idToDelete > -1)
				{
					DeleteNode(idToDelete);
				}

				moveCursorUndoID = -1;
				moveCursorUndoIndex = -1;
				
				GUI.enabled = true;
				EditorGUILayout.EndScrollView();
				
				// detect if we need to do play preview
				if (idToPreview > -1)
				{
					PlaytestFrom( m_TreeView.treeModel.Find(idToPreview).name);
				}
			}
			else
			{
				if ( currentFile == null ) { EditorGUILayout.HelpBox(" To edit a Yarn.txt file, select it in your Project tab OR use the TextAsset object picker above.\n ... or, just work with the blank default template here, and then click [Save As].", MessageType.Info);}
				EditorGUILayout.HelpBox(" Select node(s) from the sidebar on the left.\n - Left-Click:  select\n - Left-Click + Shift:  select multiple\n - Left-Click + Ctrl / Left-Click + Command:  add / remove from selection", MessageType.Info);
			}

			GUILayout.EndArea();
		}

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
				return new Color(0.3f, 0.6f, 0.25f);
			} else if (newSyntax.StartsWith("->") )
			{
				return new Color(0.8f, 0.5f, 0.1f);
			} else if (newSyntax.StartsWith("[["))
			{
				return new Color(0.8f, 0.4f, 0.6f);
			}else if (newSyntax.StartsWith("<<"))
			{
				return new Color(0f, 0.6f, 0.7f);
			}
			else
			{
				return Color.white;
			}
		}

		void BottomToolBar (Rect rect)
		{
			GUILayout.BeginArea (rect);

			using (new EditorGUILayout.HorizontalScope ())
			{

				var style = "miniButton";
				if (GUILayout.Button("Expand All", style))
				{
					treeView.ExpandAll ();
				}

				if (GUILayout.Button("Collapse All", style))
				{
					treeView.CollapseAll ();
				}

				GUILayout.FlexibleSpace();

				GUILayout.Label (currentFile != null ? AssetDatabase.GetAssetPath (currentFile) : string.Empty);

				GUILayout.FlexibleSpace ();

				if (GUILayout.Button("Set sorting", style))
				{
					var myColumnHeader = (MyMultiColumnHeader)treeView.multiColumnHeader;
					myColumnHeader.SetSortingColumns (new int[] {4, 3, 2}, new[] {true, false, true});
					myColumnHeader.mode = MyMultiColumnHeader.Mode.LargeHeader;
				}


				GUILayout.Label ("Header: ", "minilabel");
				if (GUILayout.Button("Large", style))
				{
					var myColumnHeader = (MyMultiColumnHeader) treeView.multiColumnHeader;
					myColumnHeader.mode = MyMultiColumnHeader.Mode.LargeHeader;
				}
				if (GUILayout.Button("Default", style))
				{
					var myColumnHeader = (MyMultiColumnHeader)treeView.multiColumnHeader;
					myColumnHeader.mode = MyMultiColumnHeader.Mode.DefaultHeader;
				}
				if (GUILayout.Button("No sort", style))
				{
					var myColumnHeader = (MyMultiColumnHeader)treeView.multiColumnHeader;
					myColumnHeader.mode = MyMultiColumnHeader.Mode.MinimumHeaderWithoutSorting;
				}

				GUILayout.Space (10);
				
				if (GUILayout.Button("values <-> controls", style))
				{
					treeView.showControls = !treeView.showControls;
				}
			}

			GUILayout.EndArea();
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
		
		// This gets called many times a second
		public void Update()
		{
			// small delay before reimporting the asset (otherwise it's too annoying to constantly reimport the asset)
			if (EditorApplication.timeSinceStartup > lastSaveTime + 1.0 && currentFile != null)
			{
				AssetDatabase.ImportAsset( AssetDatabase.GetAssetPath(currentFile));
				lastSaveTime = EditorApplication.timeSinceStartup + 9999; // don't save again until SaveDataToFile resets the variable
			}
			
			if (isDialogueRunning)
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
			
			DestroyImmediate(treeData,true);
			undoData.Clear();
			
			ForceStopDialogue();
		}
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

	// based a bit on YarnSpinner/ExampleDialogueUI
	public class MerinoDialogueUI
	{
		float textSpeed = 0.01f;
		public bool inputContinue = false;
		public int inputOption = -1;

		public string currentNode;
		string displayString;
		bool showContinuePrompt = false;
		string[] optionStrings = new string[0];
		Yarn.OptionChooser optionChooser;
		bool useConsolasFont = false;

		public Font consolasFont;

		public IEnumerator RunLine(Yarn.Line line, bool autoAdvance)
		{
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
//		public IEnumerator DialogueComplete()
//		{
//			yield return new WaitForSeconds(0.1f);
//			displayString = "";
//		}
		
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


	internal class MyMultiColumnHeader : MultiColumnHeader
	{
		Mode m_Mode;

		public enum Mode
		{
			LargeHeader,
			DefaultHeader,
			MinimumHeaderWithoutSorting
		}

		public MyMultiColumnHeader(MultiColumnHeaderState state)
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
						height = DefaultGUI.defaultHeight;
						break;
					case Mode.MinimumHeaderWithoutSorting:
						canSort = false;
						height = DefaultGUI.minimumHeight;
						break;
				}
			}
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
