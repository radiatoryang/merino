using System;
using System.Collections;
using System.Linq;
using System.Text;
using Merino.EditorCoroutines;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

namespace Merino
{
	public class MerinoPlaytestWindow : EventWindow
	{
		Yarn.Dialogue dialogue;
		MerinoVariableStorage varStorage;
		Yarn.OptionChooser optionChooser;
		
		string displayString, displayStringFull;
		string[] optionStrings = new string[0];
		
		public bool blockMouseInput; // prevents mouse input to progress text for a single frame
		bool inputContinue;
		int inputOption = -1;
		
		bool showContinuePrompt;
		bool useConsolasFont;
		
		Rect prefButtonRect; // used to determine the height of the toolbar

		const float textSpeed = 0.01f;
		const string windowTitle = "Merino Playtest";
		const string popupControl = "nodeJumpPopup";

		bool IsDialogueRunning
		{
			get { return dialogue.currentNode != null; }
		}

		void Awake()
		{
			// create the main Dialogue runner, and pass our variableStorage to it
			varStorage = new MerinoVariableStorage();
			dialogue = new Yarn.Dialogue(varStorage);
			
			// setup the logging system.
			dialogue.LogDebugMessage = message => MerinoDebug.Log(LoggingLevel.Verbose, "[Merino] " + message);
//			dialogue.LogErrorMessage = message => PlaytestErrorLog(message);
			dialogue.LogErrorMessage = message => MerinoDebug.Log(LoggingLevel.Error, "[Merino] " + message);
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

		[DidReloadScripts] // make sure the window is closed after recompiling to prevent errors
		public static void ForceStop()
		{
			if (EditorUtils.HasWindow<MerinoPlaytestWindow>())
			{
				var window = GetWindow<MerinoPlaytestWindow>();
				window.ForceStop_Internal();
			}
		}

		void ForceStop_Internal()
		{
			if (dialogue != null)
			{
				dialogue.Stop();
			}
			this.StopAllCoroutines();
			Close();
		}

		public static void PlaytestFrom(string startPassageName)
		{
			var window = GetWindow<MerinoPlaytestWindow>(windowTitle, true);
			window.PlaytestFrom_Internal(startPassageName, !window.IsDialogueRunning);
		}

		void PlaytestFrom_Internal(string startPassageName, bool reset = true)
		{
			if (reset)
			{
				MerinoEditorWindow.errorLog.Clear();
				dialogue.UnloadAll();
				varStorage.ResetToDefaults();
				
				try
				{
					var program = MerinoCore.SaveAllNodesAsString();
					if (!string.IsNullOrEmpty(program))
					{
						dialogue.LoadString(program);
					}
				}
				catch (Exception ex)
				{
//					PlaytestErrorLog(ex.Message);
					return;
				}
			}
			
			this.StopAllCoroutines();
			this.StartCoroutine(RunDialogue(startPassageName));
		}

		#endregion

		#region Drawing GUI Methods

		void DrawToolbar(Event e)
		{
			EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);

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
					PlaytestFrom(jumpOptions[newJumpIndex]);
			}
			GUILayout.Space(6);
			
			// view node source button
			GUI.enabled = IsDialogueRunning; //disable if dialogue isn't running
			if (GUILayout.Button(new GUIContent("View Node Source", "Click to see Yarn script code for this node."), EditorStyles.toolbarButton))
			{
				throw new System.NotImplementedException();
//				// attempt to get current node
//				var matchingNode = MerinoTreeData.Instance.GetNode(dialogue.currentNode);
//				if (matchingNode != null)
//				{
//					// display in yarn editor window
//					var w = GetWindow<MerinoEditorWindow>(MerinoEditorWindow.windowTitle, true);
//					w.SelectNodeAndZoomToLine(matchingNode.id, GuessLineNumber(matchingNode.id, displayStringFull) );
//				}
//				else
//				{
//					if (MerinoPrefs.loggingLevel >= LoggingLevel.Warning)
//						Debug.LogWarningFormat("[Merino] Couldn't find the node {0}. It might've been deleted or the Yarn file is corrupted.", dialogue.currentNode);
//				}
			}
			GUI.enabled = true;
			GUILayout.FlexibleSpace();

			// playtesting preferences popup
			if (GUILayout.Button(new GUIContent("Preferences"), EditorStyles.toolbarDropDown))
			{
				PopupWindow.Show(prefButtonRect, new PlaytestPrefsPopup());
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

		#endregion

		#region Playtest Methods

		//todo: can we add possibly add a wait before continuing text while autoadvance is active if an option is not the next result?
		IEnumerator RunDialogue(string startNode = "Start")
        {        
            // Get lines, options and commands from the Dialogue object, one at a time.
            foreach (Yarn.Dialogue.RunnerResult step in dialogue.Run(startNode))
            {
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
	        if (MerinoPrefs.loggingLevel >= LoggingLevel.Info)
				Debug.Log("[Merino] Reached the end of the dialogue.");
	        
            // No more results! The dialogue is done.
	        yield return new WaitUntil(() => MerinoPrefs.stopOnDialogueEnd);
	        ForceStop_Internal();
        }
		
		IEnumerator RunLine(Yarn.Line line)
		{
			displayStringFull = line.text;
			optionStrings = new string[0];
			
			// display dialog
//			if (textSpeed > 0.0f) 
//			{
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
//			}
//			else 
//			{
//				displayString = line.text;
//			}

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
		
        void RunOptions(Yarn.Options optionsCollection, Yarn.OptionChooser optionChooser)
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
	}
}
