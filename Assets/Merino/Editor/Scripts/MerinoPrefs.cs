using UnityEditor;
using UnityEngine;

namespace Merino
{
	public static class MerinoPrefs 
	{
		//editor prefs
		public static string newFileTemplatePath = "NewFileTemplate.yarn";
		public static Color highlightComments = new Color(0.3f, 0.6f, 0.25f);
		public static Color highlightCommands = new Color(0.8f, 0.5f, 0.1f);
		public static Color highlightNodeOptions = new Color(0.8f, 0.4f, 0.6f);
		public static Color highlightShortcutOptions = new Color(0.2f, 0.6f, 0.7f);
		// public static Color highlightVariables;
		public static bool useYarnSpinnerExperimentalMode = false;
		public static bool validateNodeTitles = true;

		//hidden prefs
		public static bool stopOnDialogueEnd = true;
		public static bool useAutoAdvance = false;
		public static bool useAutosave = true;
		public static float sidebarWidth = 180f;
		
		//window specific
		private static bool prefsLoaded = false;
		private static Vector2 scrollPos;
		internal static LoggingLevel loggingLevel = LoggingLevel.Error;

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
			GUILayout.Label("File Handling", EditorStyles.boldLabel);
			GUILayout.Label("New File Template filepath (relative to /Resources/, omit .txt)");
			newFileTemplatePath = EditorGUILayout.TextField("/Resources/", newFileTemplatePath);
			loggingLevel = (LoggingLevel) EditorGUILayout.EnumPopup("Logging Level", loggingLevel);

			// 14 Oct 2018: commented out experimental mode, it seems to have the same parser problem as before: can't read "---" sentinel, keeps looking for node header data  -RY
			// useYarnSpinnerExperimentalMode = EditorGUILayout.ToggleLeft("Use Yarn Spinner's experimental ANTLR parser", useYarnSpinnerExperimentalMode);

			// 23 Jan 2019: in reponse to GitHub issue #16, let user disable node validation? (even though I don't really see the point...)
			validateNodeTitles = EditorGUILayout.ToggleLeft("Validate and correct duplicate node titles", validateNodeTitles);

			GUILayout.Label("Syntax Highlighting Colors", EditorStyles.boldLabel);
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
			loggingLevel = LoggingLevel.Error;
			useYarnSpinnerExperimentalMode = false;
			validateNodeTitles = true;
			
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
			loggingLevel = (LoggingLevel) EditorPrefs.GetInt("MerinoLoggingLevel", (int) LoggingLevel.Error);
			useYarnSpinnerExperimentalMode = EditorPrefs.GetBool("MerinoExperimentalMode", useYarnSpinnerExperimentalMode);
			validateNodeTitles = EditorPrefs.GetBool("MerinoValidateNodeTitles", validateNodeTitles );

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
		}

		#endregion

		#region Save Methods

		public static void SaveEditorPrefs()
		{
			EditorPrefs.SetString("MerinoTemplatePath", newFileTemplatePath);
			EditorPrefs.SetInt("MerinoLoggingLevel", (int) loggingLevel);
			EditorPrefs.SetBool("MerinoExperimentalMode", useYarnSpinnerExperimentalMode);
			EditorPrefs.SetBool("MerinoValidateNodeTitles", validateNodeTitles );
			
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
		}

		#endregion
	}
}
