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
		public string nodeBody = "";
		public LeafType leafType; // needed for file / folder support in Merino's hierarchy view
		public Vector2Int nodePosition;

		public MerinoTreeElement (string name, int depth, int id) : base (name, depth, id)
		{
		}
	}
}
