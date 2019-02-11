using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Merino
{
    /// <summary>
    /// Abstract class for all Merino EditorWindows.
    /// </summary>
    public abstract class MerinoEditorWindow : EventWindow
    {
        public abstract void Refresh();
    }
}