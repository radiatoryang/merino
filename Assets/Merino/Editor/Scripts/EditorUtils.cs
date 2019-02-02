using UnityEditor;
using UnityEngine;

namespace Merino
{
	public static class EditorUtils
	{
		/// <summary>
		/// Returns true if a EditorWindow exists of the given type.
		/// </summary>
		public static bool HasWindow<TWindow>() where TWindow : EditorWindow
		{
			var instances = Resources.FindObjectsOfTypeAll<TWindow>();
			return instances.Length > 0;
		}
	}
}