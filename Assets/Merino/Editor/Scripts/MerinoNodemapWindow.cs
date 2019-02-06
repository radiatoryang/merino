using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Merino
{
    public class MerinoNodemapWindow : MerinoEditorWindow
    {
        public const string windowTitle = "Merino (Nodemap)";
        
        [MenuItem("Window/Merino/Nodemap", priority = 1)]
        static void MenuItem_GetWindow()
        {
            GetWindow<MerinoNodemapWindow>(windowTitle, true);
        }

        void OnGUI()
        {
            
        }

        public override void Refresh()
        {
            Repaint();
        }
    }
}
