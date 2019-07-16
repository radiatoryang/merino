using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace Merino {
    /// <summary>
    /// Abstract class for all Merino EditorWindows.
    /// </summary>
    public abstract class MerinoEventWindow : EventWindow
    {
        public abstract void Refresh();
    }
}
