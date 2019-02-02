using UnityEditor;
using UnityEngine;

namespace Merino
{
	public class PlaytestPrefsPopup : PopupWindowContent
	{
		public override Vector2 GetWindowSize()
		{
			return new Vector2(150, 40);
		}

		public override void OnGUI(Rect rect)
		{
			EditorGUI.BeginChangeCheck();
			
			// playtesting "hidden" prefs
			MerinoPrefs.useAutoAdvance = EditorGUILayout.ToggleLeft(new GUIContent("Auto Advance", "Automatically advance dialogue with no user input, until there's a choice."), MerinoPrefs.useAutoAdvance);
			MerinoPrefs.stopOnDialogueEnd = EditorGUILayout.ToggleLeft(new GUIContent("Close On End", "When dialogue is complete, stop and close the playtest session automatically."), MerinoPrefs.stopOnDialogueEnd);
		
			if (EditorGUI.EndChangeCheck()) 
				MerinoPrefs.SaveHiddenPrefs(); // remember new settings

			if (Event.current.type == EventType.MouseDown)
			{
				Event.current.Use();
			}

		}

		public override void OnClose()
		{
			EditorWindow.GetWindow<MerinoPlaytestWindow>().blockMouseInput = true;
		}
	}
}