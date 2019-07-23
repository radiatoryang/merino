using System;
using System.Collections;
using System.Linq;
using System.Text;
using Merino.EditorCoroutines;
using UnityEditor;
using UnityEngine;

namespace Merino
{
	//todo: mark if the dialogue loaded in the playtest window is "stale" and that the user should restart if they wish to see the latest changes.
	public class MerinoPlaytestWindow : EventWindow
	{
		[NonSerialized] Yarn.Dialogue dialogue;
		[NonSerialized] MerinoVariableStorage varStorage;
		[NonSerialized] Yarn.OptionChooser optionChooser;
		
		string displayString, displayStringFull;
		string[] optionStrings = new string[0];
		
		public bool blockMouseInput; // prevents mouse input to progress text for a single frame
		bool inputContinue;
		int inputOption = -1;
		
		bool showContinuePrompt;
		bool useConsolasFont;
		
		Rect prefButtonRect; // used to determine the height of the toolbar

		[NonSerialized] private bool validDialogue;
		
		private Texture errorIcon;

		const float textSpeed = 0.01f;
		const string windowTitle = "▶ Merino Playtest";
		const string popupControl = "nodeJumpPopup";

		public bool IsDialogueRunning
		{
			get
			{
				if (dialogue == null)
					return false;
				
				return dialogue.currentNode != null;
			}
		}

		// this is a cache for MerinoNodemapWindow visualization... otherwise, GetWindow opens the window, even if the dialogue isn't running
		public static string CurrentNode;
		public static int lastFileParent = -1;
		
		const int margin = 10;
		Rect bottomToolbarRect
		{
			get { return new Rect(0, position.height - margin * 2.5f, position.width, margin * 2.5f); }
		}

		private void OnEnable()
		{
			InitIfNeeded();
		}

		void OnDisable()
		{
			CurrentNode = null;
			lastFileParent = -1;
		}

		private void InitIfNeeded()
		{
			// create the main Dialogue runner, and pass our variableStorage to it
			varStorage = new MerinoVariableStorage();
			dialogue = new Yarn.Dialogue(varStorage);
			
			// setup the logging system.
			dialogue.LogDebugMessage = message => MerinoDebug.Log(LoggingLevel.Verbose, message);
			dialogue.LogErrorMessage = PlaytestErrorLog;

			CurrentNode = null;
			
			// icons
			if (errorIcon == null)
				errorIcon = EditorGUIUtility.Load("icons/d_console.erroricon.sml.png") as Texture;
		}

		private void Update()
		{
			if (IsDialogueRunning)
			{
				Repaint();
			}
		}

		void OnGUI()
		{
			DrawToolbar(Event.current);
			
			if (IsDialogueRunning)
			{
				DrawDialog();
			}
			
			DrawBottomToolBar(bottomToolbarRect);
			
			HandleEvents(Event.current);
		}
		
		#region EventWindow Methods

		protected override void OnMouseDown(Event e)
		{
			// HACK: prevent input when clicking out of the prefs popup for a frame
			if (blockMouseInput)
			{
				blockMouseInput = false;
				return;
			}
			
			if (e.button == MouseButton.Left)
			{
				inputContinue = true;
			}
		}

		#endregion
		
		#region Public Static Methods and their "Internal" versions

		public static MerinoPlaytestWindow GetPlaytestWindow(bool focus) {
			return GetWindow<MerinoPlaytestWindow>(windowTitle, focus);
		}

		public static void StopPlaytest(bool force = false)
		{
			if (EditorUtils.HasWindow<MerinoPlaytestWindow>())
			{
				var window = GetWindow<MerinoPlaytestWindow>();
				window.StopPlaytest_Internal(force);
			}
		}

		void StopPlaytest_Internal(bool force = false)
		{
			if (!IsDocked() || force)
			{
				if (dialogue != null)
				{
					dialogue.Stop();
				}
				this.StopAllCoroutines();
				validDialogue = false;
			
				Close();
			}
			CurrentNode = null;
		}

		public static void PlaytestFrom(string startPassageName, int onlyFromThisNodeID = -1)
		{
			var window = GetWindow<MerinoPlaytestWindow>(windowTitle, true);
			lastFileParent = onlyFromThisNodeID;
			window.PlaytestFrom_Internal(startPassageName, true, onlyFromThisNodeID );
		}

		void PlaytestFrom_Internal(string startPassageName, bool reset = true, int onlyFromThisNodeID = -1)
		{
			if (reset)
			{
				MerinoEditorWindow.errorLog.Clear();
				dialogue.Stop();
				dialogue.UnloadAll();
				varStorage.ResetToDefaults();
				
				try
				{
					var program = MerinoCore.SaveAllNodesAsString(onlyFromThisNodeID);
					if (!string.IsNullOrEmpty(program))
					{
						string filename = MerinoData.CurrentFiles.Count > 1 ? "<input>" : MerinoData.CurrentFiles[0].name;
						if ( onlyFromThisNodeID > 0 && MerinoData.GetNode(onlyFromThisNodeID).leafType == MerinoTreeElement.LeafType.File ) {
							filename = MerinoData.GetNode(onlyFromThisNodeID).name;
						}
						dialogue.LoadString(program, filename );
					}
				}
				catch (Exception ex)
				{
					validDialogue = false;
					PlaytestErrorLog(ex.Message);
					return;
				}
			}

			validDialogue = true;
			this.StopAllCoroutines();
			this.StartCoroutine(RunDialogue(startPassageName));
		}

		#endregion

		#region Drawing GUI Methods

		void DrawToolbar(Event e)
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

			if (validDialogue)
			{
				GUILayout.Space(2); //small space to mimic unity editor
			
				// jump to node button
				var jumpOptions = dialogue.allNodes.ToList();
				int currentJumpIndex = jumpOptions.IndexOf(dialogue.currentNode);
				if (!IsDialogueRunning)
				{ 
					// if there is no current node, then inform the user that
					currentJumpIndex = 0;
					jumpOptions.Insert(0, "<Stopped> Jump to Node?...");
				}
				GUI.SetNextControlName(popupControl);
				int newJumpIndex = EditorGUILayout.Popup(
					new GUIContent("", "The node you're currently playing; you can also jump to any other node."), 
					currentJumpIndex, 
					jumpOptions.Select(x => x.StartsWith("<Stopped>") ? new GUIContent(x) : new GUIContent("Node: " + x)).ToArray(), 
					EditorStyles.toolbarDropDown, 
					GUILayout.Width(160));
				if (currentJumpIndex != newJumpIndex)
				{
					if (IsDialogueRunning || newJumpIndex > 0)
					{
						PlaytestFrom_Internal(jumpOptions[newJumpIndex], false);
					}
				}
				GUILayout.Space(6);
			
				GUI.enabled = IsDialogueRunning; // disable if dialogue isn't running
				// attempt to get current node
				var matchingNode = MerinoData.GetNode(dialogue.currentNode);
				if ( lastFileParent > 0 ) { // if we know the file where the playtest started, we can be more specific
					matchingNode = MerinoData.GetAllCachedChildren(lastFileParent).Find( node => node.name == dialogue.currentNode );
					// ok if that search failed for some reason, then just give up and fallback
					if ( matchingNode == null ) {
						matchingNode = MerinoData.GetNode(dialogue.currentNode);
					}
				}
				var content = new GUIContent(" View Node Source", MerinoEditorResources.TextAsset, "Click to see Yarn script code for this node.");
				if (GUILayout.Button(content, EditorStyles.toolbarButton, GUILayout.Width(EditorStyles.toolbarButton.CalcSize(content).x - 40 ) ))
				{

					if (matchingNode != null)
					{
						// display in yarn editor window
						GetWindow<MerinoEditorWindow>(MerinoEditorWindow.windowTitle, true).
							SelectNodeAndZoomToLine(matchingNode.id, GuessLineNumber(matchingNode.id, displayStringFull));
					}
					else
					{
						MerinoDebug.LogFormat(LoggingLevel.Warning, "Merino culdn't find any node called {0}. It might've been deleted or the Yarn file is corrupted.", dialogue.currentNode);
					}
				}
				if (GUILayout.Button(new GUIContent(" View in Node Map", MerinoEditorResources.Nodemap, "Click to see this node in the node map window."), EditorStyles.toolbarButton))
				{
					if ( matchingNode != null ) 
					{
						MerinoNodemapWindow.GetNodemapWindow().FocusNode( matchingNode.id );
					} 
					else 
					{
						MerinoDebug.LogFormat(LoggingLevel.Warning, "Merino couldn't find any node called {0}. It might've been deleted or the Yarn file is corrupted.", dialogue.currentNode);
					}
				}
				GUI.enabled = true;
			}
		
			GUILayout.FlexibleSpace();

			// playtesting preferences popup
			if (GUILayout.Button(new GUIContent("Preferences"), EditorStyles.toolbarDropDown))
			{
				PopupWindow.Show(prefButtonRect, new PlaytestPrefsPopup(IsDocked()));
			}
			//grab popup button's rect do determine the height of the toolbar
			if (e.type == EventType.Repaint) prefButtonRect = GUILayoutUtility.GetLastRect();
			
			GUILayout.Space(2); //small space to mimic unity editor
			
			EditorGUILayout.EndHorizontal();

			// eat clicks
			if (e.type == EventType.MouseDown)
			{
				// clicked on toolbar
				if (e.mousePosition.y < prefButtonRect.height)
					e.Use();

				// clicked out of popup
				if (GUI.GetNameOfFocusedControl() == popupControl)
				{
					e.Use();
					GUI.FocusControl(null);
				}
			}
		}
		
		void DrawDialog()
		{
			// display yarn line
			var passageStyle = MerinoStyles.GetPassageStyle(useConsolasFont);
			float maxHeight = passageStyle.CalcHeight(new GUIContent(displayString), 600);
			GUILayout.Label(displayString, passageStyle, GUILayout.Height(0), GUILayout.ExpandHeight(true), GUILayout.MaxHeight(maxHeight), GUILayout.ExpandWidth(true));
			
			// show continue prompt
			if (showContinuePrompt)
			{
				Rect promptRect = GUILayoutUtility.GetLastRect();
				//handle continue prompt animation
				bool bounce = Mathf.Sin((float)EditorApplication.timeSinceStartup * 6f) > 0; 
				promptRect.x += promptRect.width - 24 - (bounce ? 0 : 4);
				promptRect.y += promptRect.height - 24;
				promptRect.width = 20;
				promptRect.height = 20;
				GUI.Box(promptRect, "▶", MerinoStyles.GetPassageStyle(useConsolasFont, true));
			}
			
			// show choices
			for (int i = 0; i < optionStrings.Length; i++)
			{
				if (GUILayout.Button(optionStrings[i], MerinoStyles.ButtonStyle))
				{
					inputOption = i;
					optionChooser(inputOption);
					optionStrings = new string[0];
				}
			}
		}
		
		void DrawBottomToolBar (Rect rect)
		{
			if (MerinoEditorWindow.errorLog == null || MerinoEditorWindow.errorLog.Count <= 0) return;
			
			GUILayout.BeginArea (rect);

			using (new EditorGUILayout.HorizontalScope (EditorStyles.helpBox))
			{
				var style = GUI.skin.button; //EditorStyles.miniButton;

				GUILayout.FlexibleSpace();

				if (MerinoEditorWindow.errorLog != null && MerinoEditorWindow.errorLog.Count > 0)
				{
					var error = MerinoEditorWindow.errorLog[MerinoEditorWindow.errorLog.Count - 1];
					var node = MerinoData.GetNode(error.nodeID);
					if (GUILayout.Button(new GUIContent( node == null ? " ERROR!" : " ERROR: " + node.name + ":" + error.lineNumber.ToString(), errorIcon, node == null ? error.message : string.Format("{2}\n\nclick to open node {0} at line {1}", node.name, error.lineNumber, error.message )), style, GUILayout.MaxWidth(position.width * 0.31f) ))
					{
						if (node != null)
						{
							GetWindow<MerinoEditorWindow>(MerinoEditorWindow.windowTitle).SelectNodeAndZoomToLine(error.nodeID, error.lineNumber);
						}
						else
						{
							EditorUtility.DisplayDialog("Merino Error Message!", "Merino error message:\n\n" + error.message, "Close");
						}
						StopPlaytest_Internal();
					}
				}

				GUILayout.FlexibleSpace ();
			}

			GUILayout.EndArea();
		}

		#endregion

		#region Playtest Methods

		IEnumerator RunDialogue(string startNode = "Start")
        {        
            // Get lines, options and commands from the Dialogue object, one at a time.
            foreach (Yarn.Dialogue.RunnerResult step in dialogue.Run(startNode))
            {
				CurrentNode = dialogue.currentNode;
                if (step is Yarn.Dialogue.LineResult) 
                {
                    // Wait for line to finish displaying
                    var lineResult = step as Yarn.Dialogue.LineResult;
	                yield return this.StartCoroutine(RunLine(lineResult.line));
                } 
                else if (step is Yarn.Dialogue.OptionSetResult) 
                {
                    // Wait for user to finish picking an option
                    var optionSetResult = step as Yarn.Dialogue.OptionSetResult;
	                RunOptions(optionSetResult.options, optionSetResult.setSelectedOptionDelegate);
	                yield return new WaitWhile(() => inputOption < 0);
                } 
                else if (step is Yarn.Dialogue.CommandResult) 
                {
                    // Wait for command to finish running
                    var commandResult = step as Yarn.Dialogue.CommandResult;
	                yield return this.StartCoroutine(RunCommand(commandResult.command));
                } 
            }
	        
			MerinoDebug.Log(LoggingLevel.Info, "Reached the end of the dialogue.");
			CurrentNode = null;
	        
            // No more results! The dialogue is done.
	        yield return new WaitUntil(() => MerinoPrefs.stopOnDialogueEnd);
			StopPlaytest_Internal();
        }
		
		IEnumerator RunLine(Yarn.Line line)
		{
			displayStringFull = line.text;
			optionStrings = new string[0];
			
			// Display the line one character at a time
			var stringBuilder = new StringBuilder();
			inputContinue = false;
			
			foreach (char c in line.text) 
			{
				float timeWaited = 0f;
				stringBuilder.Append(c);
				displayString = stringBuilder.ToString();
				while (timeWaited < textSpeed)
				{
					if (inputContinue) break; // early out / skip ahead

					timeWaited += textSpeed;
					yield return new WaitForSeconds(timeWaited);
				}

				if (inputContinue)
				{
					displayString = line.text; 
					break;
				}
			}

			showContinuePrompt = true;

			// Wait for user input
			inputContinue = false;
			yield return new WaitWhile(() => !inputContinue && !MerinoPrefs.useAutoAdvance);

			showContinuePrompt = false;
		}
		
        IEnumerator RunCommand(Yarn.Command command)
        {
            optionStrings = new string[0];
            displayString = "(Yarn command: <<" + command.text + ">>)";
	        
	        showContinuePrompt = true;
            useConsolasFont = true;
	        
	        // Wait for user input
	        inputContinue = false;
	        yield return new WaitUntil((() => inputContinue == false && MerinoPrefs.useAutoAdvance == false));

            showContinuePrompt = false;
            useConsolasFont = false;
        }
		
        private void RunOptions(Yarn.Options optionsCollection, Yarn.OptionChooser optionChooser)
        {
            optionStrings = new string[optionsCollection.options.Count];
	        
            for(int i = 0; i < optionStrings.Length; i++)
            {
                optionStrings[i] = optionsCollection.options[i];
            }

            inputOption = -1;
            this.optionChooser = optionChooser;
        }

		#endregion
		
		/// <summary>
		/// Logs errors from the playtest engine and Yarn Loader.
		/// </summary>
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
			
			var nodeRef = MerinoData.TreeElements.Find(x => x.name == nodeName);

			// v0.6, resolved: "todo: replace "<input>" in parsing errors with the name of the file instead."
			// if filename is default "<input>" then guess filename via cached parentID data in TreeElements
			if ( fileName == "<input>" && nodeRef != null ) {
				fileName = MerinoData.GetFileParent(nodeRef.id).name;
			}

			MerinoEditorWindow.errorLog.Add(new MerinoEditorWindow.MerinoErrorLine(message, fileName, nodeRef != null ? nodeRef.id : -1, Mathf.Max(0, lineNumber)));
			MerinoDebug.Log(LoggingLevel.Error, message);
		}
		
		/// <summary>
		/// Guesses the line number of a given line. Used for the View Node Source button.
		/// </summary>
		int GuessLineNumber(int nodeID, string lineText)
		{
			var node = MerinoData.GetNode(nodeID);
			
			// this is a really bad way of doing it, but Yarn Spinner's DialogueRunner doesn't offer any access to line numbers.
			if (node != null && node.nodeBody.Contains(lineText))
			{
				var lines = node.nodeBody.Split('\n');
				for (int i = 0; i < lines.Length; i++)
				{
					if (lines[i].Contains(lineText))
						return i + 1;
				}
				return -1;
			}

			if (node == null)
				MerinoDebug.LogFormat(LoggingLevel.Warning, "Couldn't find node ID {0}. It might've been deleted or the Yarn file might be corrupted.", nodeID);

			return -1;
		}
	}
}
