using System;
using UnityEngine;

namespace Merino
{
	[Serializable]
	internal class MerinoTreeElement : TreeElement
	{
		public enum LeafType
		{
			Node,
			File,
			Folder
		}
		
		public string nodeTags = "";
		[TextArea(5,10)]
		public string nodeBody = "";
		public LeafType leafType; // needed for file / folder support in Merino's hierarchy view
		public Vector2Int nodePosition;

		public Rect nodeRect
		{
			get { return new Rect(nodePosition.x, nodePosition.y, 100, 40); }
		}

		public MerinoTreeElement (string name, int depth, int id) : base (name, depth, id)
		{
		}
	}
}
