using UnityEditor;
using UnityEngine;

// disable warning about PreferenceItem being obsolete... will update later
#pragma warning disable 618

namespace Merino
{
	public static class MerinoPrefs 
	{
		//editor prefs
		public static bool useWindowsLineEnding = false;
		public static string lineEnding { get { return useWindowsLineEnding ? "\r\n" : "\n"; } }
		public static string newFileTemplatePath = "NewFileTemplate.yarn";
		public static Color highlightComments = new Color(0.3f, 0.6f, 0.25f);
		public static Color highlightCommands = new Color(0.8f, 0.5f, 0.1f);
		public static Color highlightNodeOptions = new Color(0.8f, 0.4f, 0.6f);
		public static Color highlightShortcutOptions = new Color(0.2f, 0.6f, 0.7f);
		// public static Color highlightVariables;
		public static bool useYarnSpinnerExperimentalMode = false;
		public static bool validateNodeTitles = true;
		public static int tabSize = 0;
		public static bool useTabbedBackspace { get { return tabSize > 0; } }

		//hidden prefs
		public static bool stopOnDialogueEnd = true;
		public static bool useAutoAdvance = false;
		public static bool useAutosave = true;
		public static float sidebarWidth = 180f;
		public static bool showSyntaxReference = true;
		public enum PlaytestScope { AllFiles, SameFile, NodeOnly }
		public static PlaytestScope playtestScope = PlaytestScope.AllFiles;
		public static bool allowLinksAcrossFiles = false;

		
		//window specific
		private static bool prefsLoaded = false;
		private static Vector2 scrollPos;
		internal static LoggingLevel loggingLevel = LoggingLevel.Warning;

		[InitializeOnLoadMethod]
		public static void InitializePrefs()
		{
			LoadEditorPrefs();
			LoadHiddenPrefs();
		}
		
		[PreferenceItem("Merino")]
		public static void PreferencesGUI()
		{
			// Load the preferences
			if (!prefsLoaded)
			{
				LoadEditorPrefs();
				prefsLoaded = true;
			}

			scrollPos = EditorGUILayout.BeginScrollView(scrollPos);
			
			// Preferences GUI
			GUILayout.Label("File Handling", EditorStyles.boldLabel); // =============================================================

			GUILayout.Label("New File Template filepath (relative to /Resources/, omit .txt)");
			newFileTemplatePath = EditorGUILayout.TextField("/Resources/", newFileTemplatePath);
			loggingLevel = (LoggingLevel) EditorGUILayout.EnumPopup("Logging Level", loggingLevel);

			// 23 Jan 2019: in reponse to GitHub issue #16, let user disable node validation? (even though I don't really see the point...)
			validateNodeTitles = EditorGUILayout.ToggleLeft(" Validate and correct duplicate node titles", validateNodeTitles);
			
			// 5 May 2019: user-configurable line endings, fix for issue #26 https://github.com/radiatoryang/merino/issues/26
			useWindowsLineEnding = EditorGUILayout.ToggleLeft(@" Use Windows line endings [\r\n]? false = [\n]", useWindowsLineEnding);

			GUILayout.Space(16);
			GUILayout.Label("Experimental / kinda buggy", EditorStyles.boldLabel); // =============================================================

			// 2 Feb 2019: added "tab size" setting, in response to issue #20
			GUILayout.Label("Tab Size replaces all tabs with spaces, but for now it's buggy!\ndon't hold TAB or press TAB too fast, or else it can't catch up");
			tabSize = EditorGUILayout.IntField("Size (<= 0: keep tabs)", tabSize );
			
			// 14 Oct 2018: commented out experimental mode, it seems to have the same parser problem as before: can't read "---" sentinel, keeps looking for node header data  -RY
			useYarnSpinnerExperimentalMode = EditorGUILayout.ToggleLeft("Use Yarn Spinner's experimental ANTLR parser", useYarnSpinnerExperimentalMode);

			GUILayout.Space(16);
			GUILayout.Label("Syntax Highlighting Colors", EditorStyles.boldLabel); // =============================================================
			highlightCommands = EditorGUILayout.ColorField("<<Commands>>", highlightCommands);
			highlightComments = EditorGUILayout.ColorField("// Comments", highlightComments);
			highlightNodeOptions = EditorGUILayout.ColorField("[[NodeOptions]]", highlightNodeOptions);
			highlightShortcutOptions = EditorGUILayout.ColorField("-> ShortcutOptions", highlightShortcutOptions);
			
			EditorGUILayout.EndScrollView();
			
			// Reset button
			if (GUILayout.Button("Use Defaults", GUILayout.Width(120)))
			{
				ResetPrefs();
			}
			
			// Save the preferences
			if (GUI.changed)
			{
				SaveEditorPrefs();
			}
		}
		
		#region Reset Methods

		static void ResetPrefs()
		{
			ResetEditorPrefs();
			ResetHiddenPrefs();
		}

		static void ResetEditorPrefs()
		{
			newFileTemplatePath = "NewFileTemplate.yarn";
			loggingLevel = LoggingLevel.Warning;
			useYarnSpinnerExperimentalMode = false;
			validateNodeTitles = true;
			useWindowsLineEnding = false;
			tabSize = 0;
			
			highlightComments = new Color(0.3f, 0.6f, 0.25f);
			highlightCommands = new Color(0.8f, 0.5f, 0.1f);
			highlightNodeOptions = new Color(0.8f, 0.4f, 0.6f);
			highlightShortcutOptions = new Color(0.2f, 0.6f, 0.7f);
			
			SaveEditorPrefs();
		}

		static void ResetHiddenPrefs()
		{
			stopOnDialogueEnd = true;
			useAutoAdvance = false;
			useAutosave = true;
			sidebarWidth = 180f;
			showSyntaxReference = true;
			playtestScope = PlaytestScope.AllFiles;
			allowLinksAcrossFiles = false;
			
			SaveHiddenPrefs();
		}

		#endregion

		#region Load Methods

		static void LoadEditorPrefs()
		{
			if (EditorPrefs.HasKey("MerinoFirstRun") == false)
			{
				SaveEditorPrefs();
				EditorPrefs.SetBool("MerinoFirstRun", true);
			}
			newFileTemplatePath = EditorPrefs.GetString("MerinoTemplatePath", newFileTemplatePath);
			loggingLevel = (LoggingLevel) EditorPrefs.GetInt("MerinoLoggingLevel", (int)LoggingLevel.Warning);
			useYarnSpinnerExperimentalMode = EditorPrefs.GetBool("MerinoExperimentalMode", useYarnSpinnerExperimentalMode);
			validateNodeTitles = EditorPrefs.GetBool("MerinoValidateNodeTitles", validateNodeTitles );
			useWindowsLineEnding = EditorPrefs.GetBool("MerinoUseWindowsLinesEnding", useWindowsLineEnding );
			tabSize = EditorPrefs.GetInt("MerinoTabSize", tabSize);

			ColorUtility.TryParseHtmlString(EditorPrefs.GetString("MerinoHighlightCommands"), out highlightCommands);
			ColorUtility.TryParseHtmlString(EditorPrefs.GetString("MerinoHighlightComments"), out highlightComments);
			ColorUtility.TryParseHtmlString(EditorPrefs.GetString("MerinoHighlightNodeOptions"), out highlightNodeOptions);
			ColorUtility.TryParseHtmlString(EditorPrefs.GetString("MerinoHighlightShortcutOptions"), out highlightShortcutOptions);
		}
		
		static void LoadHiddenPrefs()
		{
			if (EditorPrefs.HasKey("MerinoStopOn") == false)
			{
				SaveHiddenPrefs(); // save defaults if not found
			}
			stopOnDialogueEnd = EditorPrefs.GetBool("MerinoStopOn");
			useAutoAdvance = EditorPrefs.GetBool("MerinoAutoAdvance");
			useAutosave = EditorPrefs.GetBool("MerinoAutosave");
			sidebarWidth = EditorPrefs.GetFloat("MerinoSidebarWidth");
			showSyntaxReference = EditorPrefs.GetBool("MerinoShowSyntax");
			playtestScope = (PlaytestScope)EditorPrefs.GetInt("MerinoPlaytestScope");
			allowLinksAcrossFiles = EditorPrefs.GetBool("MerinoAllowCrossFile");
		}

		#endregion

		#region Save Methods

		public static void SaveEditorPrefs()
		{
			EditorPrefs.SetString("MerinoTemplatePath", newFileTemplatePath);
			EditorPrefs.SetInt("MerinoLoggingLevel", (int) loggingLevel);
			EditorPrefs.SetBool("MerinoExperimentalMode", useYarnSpinnerExperimentalMode);
			EditorPrefs.SetBool("MerinoValidateNodeTitles", validateNodeTitles );
			EditorPrefs.SetBool("MerinoWindowsLineEnding", useWindowsLineEnding );
			EditorPrefs.SetInt("MerinoTabSize", tabSize );
			
			EditorPrefs.SetString("MerinoHighlightCommands", "#"+ColorUtility.ToHtmlStringRGB(highlightCommands) );
			EditorPrefs.SetString("MerinoHighlightComments", "#"+ColorUtility.ToHtmlStringRGB(highlightComments) );
			EditorPrefs.SetString("MerinoHighlightNodeOptions", "#"+ColorUtility.ToHtmlStringRGB(highlightNodeOptions) );
			EditorPrefs.SetString("MerinoHighlightShortcutOptions", "#"+ColorUtility.ToHtmlStringRGB(highlightShortcutOptions) );
		}

		public static void SaveHiddenPrefs()
		{
			EditorPrefs.SetBool("MerinoStopOn", stopOnDialogueEnd);
			EditorPrefs.SetBool("MerinoAutoAdvance", useAutoAdvance);
			EditorPrefs.SetBool("MerinoAutosave", useAutosave);
			EditorPrefs.SetFloat("MerinoSidebarWidth", sidebarWidth);
			EditorPrefs.SetBool("MerinoShowSyntax", showSyntaxReference);
			EditorPrefs.SetInt("MerinoPlaytestScope", (int)playtestScope );
			EditorPrefs.SetBool("MerinoAllowCrossFile", allowLinksAcrossFiles );
		}

		#endregion
	}
}
