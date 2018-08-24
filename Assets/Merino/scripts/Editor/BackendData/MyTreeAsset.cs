using System.Collections.Generic;
using UnityEngine;
using UnityEditor.TreeViewExamples;

namespace Merino
{
	
	[CreateAssetMenu (fileName = "TreeDataAsset", menuName = "Tree Asset", order = 1)]
	public class MyTreeAsset : ScriptableObject
	{
		[SerializeField] List<MerinoTreeElement> m_TreeElements = new List<MerinoTreeElement> ();

		internal List<MerinoTreeElement> treeElements
		{
			get { return m_TreeElements; }
			set { m_TreeElements = value; }
		}

		void Awake ()
		{
			if (m_TreeElements.Count == 0)
				m_TreeElements = MyTreeElementGenerator.GenerateRandomTree(160);
		}
	}
}
