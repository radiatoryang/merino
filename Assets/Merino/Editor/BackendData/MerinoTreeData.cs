using System.Collections.Generic;
using UnityEngine;
using UnityEditor.TreeViewExamples;

namespace Merino
{
	
	public class MerinoTreeData : ScriptableObject
	{
		public int editedID = -1;
		public double timestamp;
		[SerializeField] List<MerinoTreeElement> m_TreeElements = new List<MerinoTreeElement> ();

		internal List<MerinoTreeElement> treeElements
		{
			get { return m_TreeElements; }
			set { m_TreeElements = value; }
		}

	}
}
