using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEditor.IMGUI.Controls;

namespace Merino
{
	public class MerinoData : ScriptableObject
	{
		#region Singleton Behaviour

		private static MerinoData instance;
		public static MerinoData Instance
		{
			get
			{
				if (instance != null)
				{
					return instance;
				}

				var tempPath = MerinoCore.GetTempDataPath();
				
				// attempt to get instance from disk.
				var possibleTempData = AssetDatabase.LoadAssetAtPath<MerinoData>(tempPath);
				if (possibleTempData != null && possibleTempData.treeElements != null)
				{
					instance = possibleTempData;
					return instance;
				}
				
				// no instance exists, create a new instance.
				instance = CreateInstance<MerinoData>();
				AssetDatabase.CreateAsset(instance, tempPath);
				AssetDatabase.SaveAssets();
				return instance;
			}
		}

		#endregion
	
		[SerializeField] private int editedID = -1;
		internal static int EditedID
		{
			get { return Instance.editedID; }
			set { Instance.editedID = value; }
		}
		
		[SerializeField] private double timestamp;
		internal static double Timestamp
		{
			get { return Instance.timestamp; }
			set { Instance.timestamp = value; }
		}
		
		[SerializeField] private TreeViewState viewState = new TreeViewState();
		internal static TreeViewState ViewState
		{
			get { return Instance.viewState; }
			set { Instance.viewState = value; }
		}
		
		[SerializeField] private List<TextAsset> currentFiles = new List<TextAsset>();
		internal static List<TextAsset> CurrentFiles
		{
			get { return Instance.currentFiles; }
			set { Instance.currentFiles = value; }
		}
		
		[SerializeField] private List<TextAsset> dirtyFiles = new List<TextAsset>();
		internal static List<TextAsset> DirtyFiles
		{
			get { return Instance.dirtyFiles; }
			set { Instance.dirtyFiles = value; }
		}

		[SerializeField] private Dictionary<TextAsset, int> fileToNodeID = new Dictionary<TextAsset, int>();
		internal static Dictionary<TextAsset, int> FileToNodeID
		{
			get { return Instance.fileToNodeID; }
			set { Instance.fileToNodeID = value; }
		}

		[SerializeField] private List<MerinoTreeElement> treeElements = new List<MerinoTreeElement>();
		internal static List<MerinoTreeElement> TreeElements
		{
			get { return Instance.treeElements; }
			set { Instance.treeElements = value; }
		}

		#region Util Methods

		internal static MerinoTreeElement GetNode(int id)
		{
			return Instance.treeElements.Find(x => x.depth != -1 && x.id == id);
		}

		internal static MerinoTreeElement GetNode(string name)
		{
			return Instance.treeElements.Find(x => x.depth != -1 && x.name == name);
		}

		#endregion
	}
}
