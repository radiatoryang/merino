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
					_monoFont = AssetDatabase.LoadAssetAtPath<Font>(MerinoCore.LocateMerinoFolder( relativeToProjectFolder:true ) + "/Editor/Fonts/Inconsolata-Regular.ttf");
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

		public static GUIStyle SmallMonoTextStyle
		{
			get 
			{
				var smallMonoStyle = new GUIStyle(EditorStyles.helpBox);

				smallMonoStyle.font = monoFont;
				smallMonoStyle.fontSize = 11;
				smallMonoStyle.normal.textColor *= 0.69f;
				smallMonoStyle.focused.textColor = smallMonoStyle.normal.textColor;
				smallMonoStyle.active.textColor = smallMonoStyle.normal.textColor;
				smallMonoStyle.hover.textColor = smallMonoStyle.normal.textColor;
				smallMonoStyle.richText = false;

				return smallMonoStyle;
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

		public static GUIStyle ButtonLeft
		{
			get {
				var buttonStyle = new GUIStyle(EditorStyles.miniButtonLeft);
				buttonStyle.fontSize = 11;
				buttonStyle.alignment = TextAnchor.MiddleLeft;
				return buttonStyle;
			}
		}

		public static GUIStyle ButtonMid
		{
			get {
				var buttonStyle = new GUIStyle(EditorStyles.miniButtonMid);
				buttonStyle.fontSize = 11;
				return buttonStyle;
			}
		}

		public static GUIStyle ButtonRight
		{
			get {
				var buttonStyle = new GUIStyle(EditorStyles.miniButtonRight);
				buttonStyle.fontSize = 11;
				return buttonStyle;
			}
		}

		public static GUIStyle DropdownRightOverlay
		{
			get {
				var buttonStyle = GUI.skin.GetStyle("IN Dropdown");
				buttonStyle.fontSize = 11;
				buttonStyle.normal.textColor = GUI.skin.button.normal.textColor;
				buttonStyle.focused.textColor = GUI.skin.button.focused.textColor;
				buttonStyle.hover.textColor = GUI.skin.button.hover.textColor;
				buttonStyle.padding = new RectOffset(4,4,0,0);		
				return buttonStyle;
			}
		}


		public static GUIStyle GetBodyStyle(int lineDigits)
		{
			var bodyStyle = new GUIStyle(EditorStyles.textArea);
			
			bodyStyle.font = monoFont;
			bodyStyle.margin = new RectOffset(lineDigits * 12 + 10, 4, 4, 4); // make room for the line numbers!!!
			bodyStyle.richText = false;

			// fix for Unity 2019.1 ... thanks Richard Pieterse!
			var defaultStyle = new GUIStyle();
			bodyStyle.fontSize = defaultStyle.fontSize;
			
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
