using System;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Merino
{

	[Serializable]
	internal class MerinoTreeElement : TreeElement
	{
	//	public float floatValue1, floatValue2, floatValue3;
	//	public Material material;
		public Color nodeColor;
		public Vector2Int nodePosition;
		//public string nodeTitle = "";
		public string nodeTags = "";
		public string nodeBody = "";
		public bool enabled;

		public MerinoTreeElement (string name, int depth, int id) : base (name, depth, id)
		{
			enabled = true;
		}
	}
}
