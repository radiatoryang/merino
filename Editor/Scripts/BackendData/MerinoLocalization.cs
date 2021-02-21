using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using Yarn.Unity;

namespace Merino {
    public class MerinoLocalization
    {
        const string popupControl = "languagePopup";

        public static void DrawLanguageSelectDropdownGUI(GUIStyle dropdownStyle, bool isAudioLanguage=false)
        {
            var jumpOptions = (isAudioLanguage ? ProjectSettings.AudioProjectLanguages : ProjectSettings.TextProjectLanguages);
            int currentJumpIndex = 0; 
            if ( (isAudioLanguage && string.IsNullOrEmpty(MerinoPrefs.audioLanguage)) || (!isAudioLanguage && string.IsNullOrEmpty(MerinoPrefs.textLanguage) ))
            { 
                if ( isAudioLanguage ) {
                    if ( ProjectSettings.AudioProjectLanguages.Count == 0) {
                        jumpOptions.Insert(0, "(no languages defined)");
                    } else {
                        SetLanguageAudio( ProjectSettings.AudioProjectLanguageDefault );
                    }
                } else {
                    if ( ProjectSettings.TextProjectLanguages.Count == 0) {
                        jumpOptions.Insert(0, "(no languages defined)");
                    } else {
                        SetLanguageText( ProjectSettings.TextProjectLanguageDefault );
                    }
                }
            } else {
                currentJumpIndex = jumpOptions.IndexOf(isAudioLanguage ? MerinoPrefs.audioLanguage : MerinoPrefs.textLanguage);
            }
            // 21 Feb 2021 - disabled this, can't figure out why this keeps causing problems?
            // jumpOptions.Add("(Add languages...)");

            GUI.SetNextControlName(popupControl);
            int newJumpIndex = EditorGUILayout.Popup(
                new GUIContent("", "add new languages in in Preferences > Yarn Spinner"), 
                currentJumpIndex, 
                jumpOptions.Select(x => new GUIContent(x) ).ToArray(), 
                dropdownStyle, 
                GUILayout.Width(160));

            if (currentJumpIndex != newJumpIndex)
            {
                if ( jumpOptions[newJumpIndex] == "(Add languages...)" ) {
                    ShowYarnSpinnerProjectSettings();
                    newJumpIndex = 0;
                } else if ( jumpOptions[newJumpIndex].Contains("(") == false ) {
                    if ( isAudioLanguage ) {
                        SetLanguageAudio( jumpOptions[newJumpIndex] );
                    } else {
                        SetLanguageText( jumpOptions[newJumpIndex] );
                    }
                }
            }

        }

        /// <summary>
        /// Displays the Yarn Spinner project settings. Invoked from the
        /// menu created in <see cref="CreateLanguageMenu"/>.
        /// </summary>
        static void ShowYarnSpinnerProjectSettings()
        {
            SettingsService.OpenProjectSettings("Project/Yarn Spinner");
        }

        public static void SetLanguageText(object textLanguage) {
            MerinoPrefs.textLanguage = (string)textLanguage;
            MerinoPrefs.SaveHiddenPrefs();
        }

        public static void SetLanguageAudio(object audioLanguage) {
            MerinoPrefs.audioLanguage = (string)audioLanguage;
            MerinoPrefs.SaveHiddenPrefs();
        }
    }

}
