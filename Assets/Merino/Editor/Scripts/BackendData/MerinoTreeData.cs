using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls;

namespace Merino
{
	public class MerinoTreeData : ScriptableObject
	{
		#region Singleton Behaviour

		private static MerinoTreeData instance;

		public static MerinoTreeData Instance
		{
			get
			{
				if (instance != null)
				{
					return instance;
				}
				
				// attempt to get instance from disk.
				var possibleTempData = AssetDatabase.LoadAssetAtPath<MerinoTreeData>(MerinoPrefs.tempDataPath);
				if (possibleTempData != null && possibleTempData.treeElements != null)
				{
					instance = possibleTempData;
					return instance;
				}
				
				// no instance exists, create a new instance.
				instance = CreateInstance<MerinoTreeData>();
				AssetDatabase.CreateAsset(instance, MerinoPrefs.tempDataPath);
				AssetDatabase.SaveAssets();
				return instance;
			}
		}

		#endregion
	
		public int editedID = -1;
		public double timestamp;
		
		public List<TextAsset> currentFiles = new List<TextAsset>();
		public List<TextAsset> dirtyFiles = new List<TextAsset>();
		public Dictionary<TextAsset, int> fileToNodeID = new Dictionary<TextAsset, int>();
		
		public TreeViewState viewState = new TreeViewState();

		[SerializeField] private List<MerinoTreeElement> m_TreeElements = new List<MerinoTreeElement> ();
		internal List<MerinoTreeElement> treeElements
		{
			get { return m_TreeElements; }
			set { m_TreeElements = value; }
		}
		
		//todo: can we move this to a more appropriate class, like a utils class or something. partial class? idk just don't overload this file
		internal static MerinoTreeElement GetElement(int id)
		{
			return Instance.treeElements.Find(x => x.depth != -1 && x.id == id);
		}
	}
}
