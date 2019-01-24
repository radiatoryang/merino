using System.Collections.Generic;
using UnityEngine;
using UnityEditor.TreeViewExamples;
using UnityEditor.IMGUI.Controls;

namespace Merino
{
	
	public class MerinoTreeData : ScriptableObject
	{
		public int editedID = -1;
		public double timestamp;
		[SerializeField] List<MerinoTreeElement> m_TreeElements = new List<MerinoTreeElement> ();
		[SerializeField] public List<TextAsset> currentFiles = new List<TextAsset>();
		[SerializeField] public Dictionary<TextAsset, int> fileToNodeID = new Dictionary<TextAsset, int>();
		[SerializeField] public TreeViewState viewState = new TreeViewState();

		internal List<MerinoTreeElement> treeElements
		{
			get { return m_TreeElements; }
			set { m_TreeElements = value; }
		}

	}
}
