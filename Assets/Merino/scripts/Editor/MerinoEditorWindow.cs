using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Linq;
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

namespace Merino
{

	class MerinoEditorWindow : EditorWindow
	{
		[NonSerialized] bool m_Initialized;
		[SerializeField] TreeViewState viewState; // Serialized in the window layout file so it survives assembly reloading
		[SerializeField] MultiColumnHeaderState m_MultiColumnHeaderState;
		[SerializeField] bool useAutosave = false;
		bool doubleClickOpensFile = true;
		
		[NonSerialized] float sidebarWidth = 180f;
		[SerializeField] Vector2 scrollPos;
		double lastTabTime = 0.0; // this is a really bad hack
		
		SearchField m_SearchField;
		MerinoTreeView m_TreeView;
		MyTreeAsset m_MyTreeAsset;

		TextAsset currentFile;
		Font monoFont;
		
		// Yarn Spinner running stuff
		MerinoDialogueUI dialogueUI;
		Dialogue _dialogue;
		Dialogue dialogue {
			get {
				if (_dialogue == null) {
					// Create the main Dialogue runner, and pass our variableStorage to it
					_dialogue = new Yarn.Dialogue ( new MerinoVariableStorage() );
					dialogueUI = new MerinoDialogueUI();
					
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
			get { return new Rect(20, 30, sidebarWidth, position.height-60); }
		}

		Rect toolbarRect
		{
			get { return new Rect (20f, 10f, sidebarWidth, 20f); }
		}

		Rect nodeEditRect
		{
			get { return new Rect( sidebarWidth+40f, 10, position.width-sidebarWidth-70, position.height-30);}
		}

		Rect bottomToolbarRect
		{
			get { return new Rect(20f, position.height - 18f, position.width - 40f, 16f); }
		}

		public MerinoTreeView treeView
		{
			get { return m_TreeView; }
		}

		void InitIfNeeded ()
		{
			if (m_Initialized) return;
			
			// load font
			if (monoFont == null)
			{
				monoFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Merino/Fonts/Inconsolata-Regular.ttf");
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

			var treeModel = new TreeModel<MerinoTreeElement>(GetData());
				
			m_TreeView = new MerinoTreeView(viewState, multiColumnHeader, treeModel);

			m_SearchField = new SearchField();
			m_SearchField.downOrUpArrowKeyPressed += m_TreeView.SetFocusAndEnsureSelectedItem;

			m_Initialized = true;
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
				                 "\n\nThere's two ways to do choices in Yarn. The most basic way is to link to other nodes like this:" +
				                 "\n[[Go see more examples|Start_MoreExamples]]\n[[Actually, let's restart this node again|Start]]" +
				                 "\n\nIMPORTANT: node options are offered to player only at the END of the entire passage, like after this line:";
								 

				var start2 = new MerinoTreeElement("Start_MoreExamples", 0, 2);
				start2.nodeBody = "This node is called Start_MoreExamples.\nThe second way to do choices in Yarn is with 'shortcut options' like this:" +
				                  "\n\n->This is option 1\n\tThis happens if player selects option 1" +
				                  "\n->This is option 2\n\tThis happens if player selects option 2\n\t<<set $didOption2 to true>>" +
				                  "\n\n<<if $didOption2 is true>>\nYep, you definitely chose option 2!\n<<else>>\nThis happens if they chose option 1\n<<endif>>" +
				                  "\n\nDo you want to go back to Start now?\n-> Yes, send me back to Start.\n\t[[Start]]\n-> No thanks.\n\nOk that's the end, good luck!";
				
				treeElements.Add(start);
				treeElements.Add(start2);
			}
			

			return treeElements;
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

		string SaveDataAsString()
		{
			var nodeInfo = new List<YarnSpinnerLoader.NodeInfo>();
			var allTreeInfo = m_TreeView.treeModel.root.children;
			foreach (var item in allTreeInfo)
			{
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
		void SaveDataToFile()
		{
			if (currentFile != null)
			{
				File.WriteAllText(AssetDatabase.GetAssetPath(currentFile), SaveDataAsString() );
				EditorUtility.SetDirty(currentFile);
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
				currentFile = possibleYarnFile;
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
				DrawSelectedNodes(nodeEditRect);
			}

			if (dialogueUI != null)
			{
				dialogueUI.OnGUI(nodeEditRect);
			}

		//	BottomToolBar (bottomToolbarRect);
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
			if (GUI.Button(buttonRect, "+ NEW NODE"))
			{
				AddNewNode();
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
			InitIfNeeded();
		}

		void PlayFrom(string startPassageName)
		{
			if (isDialogueRunning)
			{
				return;
			}
			
			dialogue.LoadString( SaveDataAsString() );
			
			// use EditorCoroutines to run the dialogue
			this.StartCoroutine(RunDialogue(startPassageName));
		}

		// this is basically just ripped from YarnSpinner/DialogueRunner.cs
		bool isDialogueRunning;
		IEnumerator RunDialogue (string startNode = "Start")
        {
            // Mark that we're in conversation.
            isDialogueRunning = true;

            // Signal that we're starting up.
            yield return this.StartCoroutine(this.dialogueUI.DialogueStarted());

            // Get lines, options and commands from the Dialogue object,
            // one at a time.
            foreach (Yarn.Dialogue.RunnerResult step in dialogue.Run(startNode)) {

                if (step is Yarn.Dialogue.LineResult) {

                    // Wait for line to finish displaying
                    var lineResult = step as Yarn.Dialogue.LineResult;
                    yield return this.StartCoroutine (this.dialogueUI.RunLine (lineResult.line));

                } else if (step is Yarn.Dialogue.OptionSetResult) {

                    // Wait for user to finish picking an option
                    var optionSetResult = step as Yarn.Dialogue.OptionSetResult;
                    yield return this.StartCoroutine (
                        this.dialogueUI.RunOptions (
                        optionSetResult.options,
                        optionSetResult.setSelectedOptionDelegate
                    ));

                } else if (step is Yarn.Dialogue.CommandResult) {

                    // Wait for command to finish running

                    var commandResult = step as Yarn.Dialogue.CommandResult;

//                    if (DispatchCommand(commandResult.command.text) == true) {
//                        // command was dispatched
//                    } else {
                        yield return this.StartCoroutine (this.dialogueUI.RunCommand (commandResult.command));
//                    }


                } else if(step is Yarn.Dialogue.NodeCompleteResult) {

                    // Wait for post-node action
                    var nodeResult = step as Yarn.Dialogue.NodeCompleteResult;
                    yield return this.StartCoroutine (this.dialogueUI.NodeComplete (nodeResult.nextNode));
                }
            }

            // No more results! The dialogue is done.
            yield return this.StartCoroutine (this.dialogueUI.DialogueComplete ());

            // Clear the 'is running' flag. We do this after DialogueComplete returns,
            // to allow time for any animations that might run while transitioning
            // out of a conversation (ie letterboxing going away, etc)
            isDialogueRunning = false;
        }

		void DrawSelectedNodes(Rect rect)
		{
			GUILayout.BeginArea(rect);
			
			GUILayout.BeginHorizontal();
			if (currentFile != null)
			{
				useAutosave = EditorGUILayout.Toggle(new GUIContent("", "if enabled, will automatically save every change"), useAutosave, GUILayout.Width(16));
				GUILayout.Label(new GUIContent("AutoSave?   ", "if enabled, will automatically save every change"), GUILayout.Width(0), GUILayout.ExpandWidth(true), GUILayout.MaxWidth(80) );
				GUI.enabled = !useAutosave;
				if (GUILayout.Button( new GUIContent("Save", "save all changes to the current .yarn.txt file"), GUILayout.MaxWidth(60)))
				{
					SaveDataToFile();
					AssetDatabase.Refresh();
				}
				GUI.enabled = true;
			}
			if (GUILayout.Button( new GUIContent("Save As...", "save all changes as a new .yarn.txt file"), GUILayout.MaxWidth(100)))
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
					AssetDatabase.Refresh();
				}
			}

			if (GUILayout.Button(new GUIContent("New File", "will throw away any unsaved changes, and reset Merino to a new blank file"), GUILayout.MaxWidth(100)))
			{
				ResetMerino();
			}
			GUILayout.Space(10);
			//if ( currentFile != null ) { GUILayout.Label(EditorGUIUtility.FindTexture ("Folder Icon"), GUILayout.Width(24), GUILayout.Height(24));}
			//EditorGUILayout.SelectableLabel( currentFile != null ? AssetDatabase.GetAssetPath(currentFile) : "[no file loaded]", EditorStyles.whiteLargeLabel);
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
			GUILayout.EndHorizontal();

			int idToDelete = -1;
			bool forceSave = false;
			int idToPreview = -1;
			if (viewState.selectedIDs.Count > 0)
			{
				scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
				
				foreach (var id in viewState.selectedIDs)
				{
					// start node container
					EditorGUI.BeginChangeCheck();
					EditorGUILayout.BeginVertical(EditorStyles.helpBox);
					
					// NODE HEADER
					EditorGUILayout.BeginHorizontal();
					var nameStyle = new GUIStyle( EditorStyles.textField );
					nameStyle.font = monoFont;
					nameStyle.fontSize = 16;
					nameStyle.fixedHeight = 20f;
					string newName = EditorGUILayout.TextField(m_TreeView.treeModel.Find(id).name, nameStyle);
					if (GUILayout.Button("Delete", GUILayout.Width(100)))
					{
						idToDelete = id;
					}
					EditorGUILayout.EndHorizontal();
					
					// NODE BODY
					var te = (TextEditor)GUIUtility.GetStateObject( typeof (TextEditor), GUIUtility.keyboardControl );
					// save colors for later
//					var backupColor = GUI.color;
					var backupContentColor = GUI.contentColor;
//					var backupBackgroundColor = GUI.backgroundColor;
					
 
					//get the texteditor of the textarea to control selection
					GUI.contentColor = new Color ( 0f, 0f, 0f, 0.16f );
					string passage = m_TreeView.treeModel.Find(id).nodeBody;
					float height = EditorStyles.textArea.CalcHeight(new GUIContent(passage), rect.width);
					
					// stuff for tabs
					GUI.SetNextControlName ( "TextArea" );
					
					// draw the body
					var bodyStyle = new GUIStyle( EditorStyles.textArea );
					bodyStyle.font = monoFont;
					string newBody = GUILayout.TextArea(passage, bodyStyle, GUILayout.Height(0f), GUILayout.ExpandHeight(true), GUILayout.MaxHeight(height));
					
					// tab support
					string oldBody = newBody;
					newBody = KeyboardTabSupport(newBody, te);
					if (oldBody.Length != newBody.Length)
					{
						forceSave = true;
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
					highlightOverlay.normal.textColor = Color.white * 0.8f;
					highlightOverlay.richText = true;
					highlightOverlay.wordWrap = true;
					GUI.contentColor = backupContentColor;
					
					GUI.Label(lastRect, syntaxHighlight, highlightOverlay);
					
					// add "Play" button
					if (GUILayout.Button("Preview this", GUILayout.Width(100)))
					{
						idToPreview = id;
					}
 
					// close out node container				
					EditorGUILayout.EndVertical();
					EditorGUILayout.Space();
					EditorGUILayout.Separator();
					
					// did user edit something?
					if (EditorGUI.EndChangeCheck() || forceSave)
					{
						// TODO: fix this somehow? I guess the file isn't writing quickly enough for Undo to catch it
						if (currentFile != null)
						{
							Undo.RecordObject(currentFile, "Merino: edit file ");
						}
						m_TreeView.treeModel.Find(id).name = newName;
						m_TreeView.treeModel.Find(id).nodeBody = newBody;
						if (currentFile != null && useAutosave)
						{
							SaveDataToFile();
						}
					}
				}

				// delete the node with this ID
				if (idToDelete > -1)
				{
					DeleteNode(idToDelete);
				}

				EditorGUILayout.EndScrollView();
				
				// detect if we need to do play preview
				if (idToPreview > -1)
				{
					PlayFrom( m_TreeView.treeModel.Find(idToPreview).name);
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
		string KeyboardTabSupport(string text, TextEditor te)
		{
			if ( GUIUtility.keyboardControl == te.controlID && Event.current.type != EventType.Layout && (Event.current.keyCode == KeyCode.Tab || Event.current.character == '\t') )
			{
				int cursorIndex = te.cursorIndex;
				GUI.FocusControl("TextArea");
				if (text.Length > te.cursorIndex && EditorApplication.timeSinceStartup > lastTabTime + 0.2)
				{
					lastTabTime = EditorApplication.timeSinceStartup;
					text = text.Insert(te.cursorIndex, "\t");
					te.cursorIndex = cursorIndex + 1;
					te.selectIndex = te.cursorIndex;
				}

				Event.current.Use();
				GUI.FocusControl("TextArea");
			}
			return text;
		}

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
				return new Color(0.9f, 0.5f, 0.8f);
			}else if (newSyntax.StartsWith("<<"))
			{
				return Color.cyan;
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
	}

	// based a bit on YarnSpinner/ExampleDialogueUI
	public class MerinoDialogueUI
	{
		public IEnumerator DialogueStarted()
		{
			yield return 0;
		}
		
		public IEnumerator RunLine(Yarn.Line line)
		{
			yield return 0;
		}
		
		public IEnumerator RunCommand (Yarn.Command command)
		{
			yield return 0;
		}
		
		public IEnumerator RunOptions (Yarn.Options optionsCollection, Yarn.OptionChooser optionChooser)
		{
			yield return 0;
		}
		
		public IEnumerator NodeComplete(string nextNode)
		{
			yield return 0;
		}
		
		public IEnumerator DialogueComplete()
		{
			yield return 0;
		}
		
		public void OnGUI(Rect rect)
		{
			
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
		public DefaultVariable[] defaultVariables;
	
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
