using System.Collections;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Merino
{
    
    // based a bit on YarnSpinner/ExampleDialogueUI
    public class MerinoDialogueUI
    {
        float textSpeed = 0.01f;
        public bool inputContinue = false;
        public int inputOption = -1;

        public string currentNode;
        string displayString;
        public string displayStringFull;
        bool showContinuePrompt = false;
        string[] optionStrings = new string[0];
        Yarn.OptionChooser optionChooser;
        bool useConsolasFont = false;

        public Font consolasFont;

        public IEnumerator RunLine(Yarn.Line line, bool autoAdvance)
        {
            displayStringFull = line.text;
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
}