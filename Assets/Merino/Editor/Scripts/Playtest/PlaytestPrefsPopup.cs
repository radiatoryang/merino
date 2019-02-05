using UnityEditor;
using UnityEngine;

namespace Merino
{
	public class PlaytestPrefsPopup : PopupWindowContent
	{
		private readonly bool docked;
		
		public PlaytestPrefsPopup(bool docked)
		{
			this.docked = docked;
		}
		
		public override Vector2 GetWindowSize()
		{
			return new Vector2(150, 40);
		}

		public override void OnGUI(Rect rect)
		{
			EditorGUI.BeginChangeCheck();
			
			// playtesting "hidden" prefs
			MerinoPrefs.useAutoAdvance = EditorGUILayout.ToggleLeft(new GUIContent("Auto Advance", "Automatically advance dialogue with no user input, until there's a choice."), MerinoPrefs.useAutoAdvance);
			GUI.enabled = !docked; //disable prefs that aren't applicable when docked
			MerinoPrefs.stopOnDialogueEnd = EditorGUILayout.ToggleLeft(new GUIContent("Close On End", "When dialogue is complete, close the playtest session automatically. This doesn't apply if the window is docked."), MerinoPrefs.stopOnDialogueEnd);
			GUI.enabled = true;
			
			if (EditorGUI.EndChangeCheck()) 
				MerinoPrefs.SaveHiddenPrefs(); // remember new settings

			if (Event.current.type == EventType.MouseDown)
				Event.current.Use();

		}

		public override void OnClose()
		{
			EditorWindow.GetWindow<MerinoPlaytestWindow>().blockMouseInput = true;
		}
	}
}