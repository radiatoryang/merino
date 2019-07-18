// PluginUpdateCheck.cs
// By Adrienne Lombardo (@charblar)

using System;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;

// ignore warnings about unused vars
#pragma warning disable 649
// ignore warnings about WWW usage, we'll upgrade later
#pragma warning disable 618
// ignore warnings if displayReleaseNotes is false
#pragma warning disable 429
#pragma warning disable 162

namespace Merino
{
    [InitializeOnLoad]
    public class PluginUpdateCheck : EditorWindow
    {
        #region Configuration
    
        /// <summary>
        /// Formal title of the plugin, used for titles and menus.
        /// </summary>
        private const string pluginName = "Merino";
        
        /// <summary>
        /// Version of the plugin, this will be compared against the latest release on GitHub.
        /// </summary>
        private static readonly Version CurrentVersion = new Version("0.6.0");
        /// <summary>
        /// Github user/organization the repo belongs to.
        /// </summary>
        private const string repoUser = "radiatoryang";
        
        /// <summary>
        /// Name of the Github repository.
        /// </summary>
        private const string repoName = "merino";
        
        /// <summary>
        /// Should we display the release notes from the release in editor? Best used for 
        /// </summary>
        private const bool displayReleaseNotes = false;
    
        #endregion
    
        private bool checkUpdatesToggle = true;
        private Vector2 releaseNotesScrollPos;
    
        private static WWW www;
        private static ReleaseInfo latestReleaseInfo;
        private static Version latestVersion;
        private static string latestVersionKey;
        private static PluginUpdateCheck windowInstance;

        private static bool checkVersionComplete;
        private static bool forceCheckVersion;
    
        private const string lastestVersionUrl = "https://api.github.com/repos/" + repoUser + "/" + repoName + "/releases/latest";
        private const string releasesUrl = "https://github.com/" + repoUser + "/" + repoName + "/releases";
        private const string latestReleaseUrl = releasesUrl + "/latest";
    
        private const string ignoredVersionsKey = pluginName + ".IgnoredUpdates";
        private const string ignoreUpdatesKey = pluginName + ".IgnoreAllUpdates";
        
        private const string nextCheckTimeKey = pluginName + ".NextCheckTime";
        private const double nextCheckInteraval = 30.0; // in minutes
    
    
        struct ReleaseInfo
        {
            public string tag_name; // tag assigned to release
            public string body;     // body of the release
        }
        
        static PluginUpdateCheck()
        {
            EditorApplication.update += CheckVersion;
            EditorApplication.playModeStateChanged += PlayModeChangedHandler;
        }
    
        private void OnEnable()
        {
            AssemblyReloadEvents.beforeAssemblyReload += BeforeAssemblyReloadHandler;
        }
    
        private void OnDisable()
        {
            AssemblyReloadEvents.beforeAssemblyReload -= BeforeAssemblyReloadHandler;
        }
        
    
        private void OnGUI()
        {
            // version status banner
            if (NewerVersionAvailable())
                EditorGUILayout.HelpBox("There is a new version of " + pluginName + " available for download.", MessageType.Info);
            else
                EditorGUILayout.HelpBox(pluginName + " is up to date.", MessageType.Info);
                    
            // versions
            EditorGUILayout.LabelField("Current Version", "v" + CurrentVersion);
            EditorGUILayout.LabelField("Latest Version", latestReleaseInfo.tag_name, NewerVersionAvailable() ? EditorStyles.boldLabel : EditorStyles.label); //bold new version for emphasis 
    
            // release notes
            if (displayReleaseNotes && !string.IsNullOrEmpty(latestReleaseInfo.body))
            {
                releaseNotesScrollPos = EditorGUILayout.BeginScrollView(releaseNotesScrollPos);
                {
                    EditorGUILayout.HelpBox(latestReleaseInfo.body, MessageType.None);
                }
                EditorGUILayout.EndScrollView();
            }
            
            GUILayout.FlexibleSpace();
    
            // action buttons
            if (NewerVersionAvailable())
            {
                if (GUILayout.Button(new GUIContent("Download new version", latestReleaseUrl)))
                {
                    Application.OpenURL(latestReleaseUrl);
                }
                if (GUILayout.Button(new GUIContent("Skip new version", "Stop being notified about this release (" + latestReleaseInfo.tag_name + "), you will be notified about future releases.")))
                {
                    AddVersionToIgnored();
                    Close();
                }
            }
            else
            {
                if (GUILayout.Button(new GUIContent("Close")))
                {
                    Close();
                }
            }
    
            checkUpdatesToggle = EditorGUILayout.ToggleLeft(new GUIContent("Check for Updates", "Automatically check for new releases of " + pluginName), checkUpdatesToggle);
        }
        
        private void OnDestroy()
        {
            EditorPrefs.SetBool(ignoreUpdatesKey, !checkUpdatesToggle);
            
            if (windowInstance == this)
                windowInstance = null;
        }
            
        private static void MaybeOpenWindow()
        {
            //only allow one instance
            if (windowInstance != null) return;
    
            windowInstance = GetWindow<PluginUpdateCheck>(true, pluginName + " Update Check");
            
            // position/size window
            windowInstance.minSize = new Vector2(335, 360f);
            var rect = windowInstance.position;
            windowInstance.position = new Rect(50f, 50f, rect.width, 360f);
            
            windowInstance.checkUpdatesToggle = !EditorPrefs.GetBool(ignoreUpdatesKey, false);
        }
    
        private static void PlayModeChangedHandler(PlayModeStateChange mode)
        {
            // don't check for updates while in play mode
            if (mode == PlayModeStateChange.EnteredPlayMode)
            {
                EditorApplication.update -= CheckVersion;
            }
        }
        
        private void BeforeAssemblyReloadHandler()
        {
            // close before assembly reloads to prevent bugs/errors
            Close();
        }
        
        [MenuItem(pluginName + "/Check for Updates")]
        private static void CheckForUpdates()
        {
            forceCheckVersion = true;
            checkVersionComplete = false;
            EditorApplication.update += CheckVersion;
        }
    
        private static void CheckVersion()
        {
            // fetch new release info from github
            if (!checkVersionComplete && (!EditorPrefs.GetBool(ignoreUpdatesKey, false) || forceCheckVersion))
            {
                if (www == null) 
                {
                    if (EditorPrefs.HasKey(nextCheckTimeKey) && DateTime.UtcNow < UtcDateTimeFromStr(EditorPrefs.GetString(nextCheckTimeKey)) && !forceCheckVersion)
                    {
                        checkVersionComplete = true;
                        return;
                    }
                    
                    www = new WWW(lastestVersionUrl);
                }
                
                if (!www.isDone) return;
                
                // update next time to check for updates
                EditorPrefs.SetString(nextCheckTimeKey, UtcDateTimeToStr(DateTime.UtcNow.AddMinutes(nextCheckInteraval)));
    
                if (UrlSuccess(www))
                {
                    latestReleaseInfo = JsonUtility.FromJson<ReleaseInfo>(www.text);
                    
                    // parse latestVersion from release tag
                    if (!string.IsNullOrEmpty(latestReleaseInfo.tag_name)) 
                    {
                        try
                        {
                            latestVersion = new Version(Regex.Replace(latestReleaseInfo.tag_name, "[^0-9\\.]", string.Empty));
                            latestVersionKey = latestVersion.ToString();
                        }
                        catch
                        {
                            latestVersion = default(Version);
                            latestVersionKey = string.Empty;
                        }
                    }
                }
    
                www.Dispose();
                www = null;
    
                checkVersionComplete = true;
            }
    
            if ((!string.IsNullOrEmpty(latestVersionKey) && !IsVersionIgnored(latestVersionKey) && NewerVersionAvailable()) || forceCheckVersion)
            {
                MaybeOpenWindow();
            }
    
            EditorApplication.update -= CheckVersion;
        }
        
        private static bool UrlSuccess(WWW www)
        {
            try
            {
                // 404
                if (Regex.IsMatch(www.text, "not found", RegexOptions.IgnoreCase))
                {
                    Debug.Log("Failed to check updates for " + pluginName + ", 404 not found.");
                    return false;
                }
                
                // API rate limit exceeded, see https://developer.github.com/v3/#rate-limiting
                if (!string.IsNullOrEmpty(www.error))
                {
                    Debug.Log("Failed to check updates " + pluginName + ", rate limit exceeded for this IP address. As a result we won't check updates for another hour.");
                    EditorPrefs.SetString(nextCheckTimeKey, UtcDateTimeToStr(DateTime.UtcNow.AddMinutes(60.0)));
                        
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.Log("Failed to check updates for " + pluginName + ", " + ex);
                return false;
            }
    
            return true;
        }
        
        private static bool NewerVersionAvailable()
        {
            // "latest or greatest version" (shrug)
            return latestVersion > CurrentVersion;
        }
    
        #region Ignored Versions Methods
    
        private static bool IsVersionIgnored()
        {
            return IsVersionIgnored(latestVersionKey);
        }
        
        private static bool IsVersionIgnored(string version)
        {
            if (EditorPrefs.HasKey(ignoredVersionsKey))
            {
                var ignored = EditorPrefs.GetString(ignoredVersionsKey).Split(',');
                return ignored.Contains(version);
            }
    
            return false;
        }
    
        private static void AddVersionToIgnored()
        {
            AddVersionToIgnored(latestVersionKey);
        }
    
        private static void AddVersionToIgnored(string version)
        {
            if (string.IsNullOrEmpty(version)) return;
            
            //append to ignored versions
            EditorPrefs.SetString(ignoredVersionsKey, EditorPrefs.GetString(ignoredVersionsKey, String.Empty) + version + ",");
        }
    
        #endregion
    
        #region Util Methods
    
        private static DateTime UtcDateTimeFromStr(string str)
        {
            long utcTicks;
            
            
            if (string.IsNullOrEmpty(str) || !long.TryParse(str, out utcTicks))
                return DateTime.MinValue;
            
            return new DateTime(utcTicks, DateTimeKind.Utc);
        }
        
        private static string UtcDateTimeToStr(DateTime utcDateTime)
        {
            return utcDateTime.Ticks.ToString();
        }
    
        #endregion
    }
}
    
#pragma warning restore 618
#pragma warning restore 429
#pragma warning restore 162