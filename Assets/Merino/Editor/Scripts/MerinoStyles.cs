using UnityEditor;
using UnityEngine;

namespace Merino
{
	
	public static class MerinoStyles
	{
		private static Font _monoFont;
		public static Font monoFont
		{
			get
			{
				if (_monoFont == null)
				{
					_monoFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Merino/Editor/Fonts/Inconsolata-Regular.ttf");
				}

				return _monoFont;
			}
		}

		public static GUIStyle ToolbarStyle
		{
			get
			{
				var toolbarStyle = new GUIStyle(EditorStyles.toolbar);
			
				toolbarStyle.alignment = TextAnchor.MiddleCenter;
	
				return toolbarStyle;
			}
		}

		public static GUIStyle SmallToggleStyle
		{
			get
			{
				var smallToggleStyle = new GUIStyle();
			
				smallToggleStyle.fontSize = 10;
				smallToggleStyle.alignment = TextAnchor.MiddleLeft;
				if (EditorGUIUtility.isProSkin) smallToggleStyle.normal.textColor = Color.gray;
	
				return smallToggleStyle;
			}
		}

		public static GUIStyle NameStyle
		{
			get
			{
				var nameStyle = new GUIStyle(EditorStyles.textField);
			
				nameStyle.font = monoFont;
				nameStyle.fontSize = 16;
				nameStyle.fixedHeight = 20f;
			
				return nameStyle;
			}
		}
		
		public static GUIStyle ButtonStyle
		{
			get
			{
				var buttonStyle = new GUIStyle(GUI.skin.button);
				buttonStyle.fontSize = 16;
				
				return buttonStyle;
			}
		}

		public static GUIStyle GetBodyStyle(int lineDigits)
		{
			var bodyStyle = new GUIStyle(EditorStyles.textArea);
			
			bodyStyle.font = monoFont;
			bodyStyle.margin = new RectOffset(lineDigits * 12 + 10, 4, 4, 4); // make room for the line numbers!!!
			bodyStyle.richText = false;
			
			return bodyStyle;
		}
		
		public static GUIStyle GetHighlightStyle(int lineDigits, float textColorMultiplier)
		{
			var highlightStyle = new GUIStyle();
			var bodyStyle = GetBodyStyle(lineDigits);
			
			highlightStyle.border = bodyStyle.border;
			highlightStyle.padding = bodyStyle.padding;
			highlightStyle.font = monoFont;
			highlightStyle.normal.textColor = (EditorGUIUtility.isProSkin ? Color.white : Color.black) * textColorMultiplier;
			highlightStyle.richText = true;
			highlightStyle.wordWrap = true;
			
			return highlightStyle;
		}

		public static GUIStyle GetPassageStyle(bool useConsolasFont, bool isContinuePrompt = false)
		{
			var passageStyle = new GUIStyle(GUI.skin.box);
			
			if (useConsolasFont) passageStyle.font = monoFont;
			passageStyle.fontSize = 18;
			passageStyle.normal.textColor = EditorStyles.boldLabel.normal.textColor;
			passageStyle.richText = true;

			if (!isContinuePrompt)
			{
				passageStyle.padding = new RectOffset(8, 8, 8, 8);
				passageStyle.alignment = TextAnchor.UpperLeft;
			}
			else
			{
				passageStyle.border = new RectOffset(0, 0, 0, 0);
				passageStyle.padding = new RectOffset(0, 0, 0, 0);
				passageStyle.alignment = TextAnchor.MiddleCenter;
				passageStyle.wordWrap = false;
				passageStyle.normal.background = null;
			}

			return passageStyle;
		}
	}
}
