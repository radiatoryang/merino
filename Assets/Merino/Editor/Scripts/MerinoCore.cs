using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Merino
{
    internal static class MerinoCore
    {
        public static void ReimportFiles(bool forceReimportAll = false)
        {
            MerinoTreeData data = MerinoTreeData.Instance;
            
            if (forceReimportAll)
            {
                foreach (var file in data.currentFiles)
                {
                    AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(file));
                }
            }
            else if (data.dirtyFiles.Count > 0 )
            {
                foreach (var file in data.dirtyFiles)
                {
                    AssetDatabase.ImportAsset(AssetDatabase.GetAssetPath(file));
                }
            }

            data.dirtyFiles.Clear();
        }

        // TODO: eventually, add more data on what's dirty about it (what nodes modified? when it was last modified? etc)
        public static void MarkFileDirty(TextAsset dirtyFile)
        {
            MerinoTreeData data = MerinoTreeData.Instance;

            if (data.dirtyFiles.Contains(dirtyFile) == false)
            {
                data.dirtyFiles.Add(dirtyFile);
            }
        }


	    public static double LastSaveTime;
	    
        public static void SaveDataToFiles()
        {
            MerinoTreeData data = MerinoTreeData.Instance;

            if (data.currentFiles.Count > 0 )
            {
                foreach (var file in data.currentFiles)
                {
	                if (data.fileToNodeID.ContainsKey(file))
	                {
		                File.WriteAllText(AssetDatabase.GetAssetPath(file), SaveFileNodesAsString(data.fileToNodeID[file]));
		                EditorUtility.SetDirty(file);
		                LastSaveTime = EditorApplication.timeSinceStartup;
	                }
	                else
	                {
		                MerinoDebug.Log(LoggingLevel.Warning, file.name + " has not been mapped to a NodeID and cannot be saved, reload the file and try again.");
	                }
                }
            }
        }

		// used for file saving
		public static string SaveFileNodesAsString(int fileNodeID)
		{
			MerinoTreeData data = MerinoTreeData.Instance;
			
			var nodeInfoList = new List<YarnSpinnerLoader.NodeInfo>();
			var toTraverse = new List<int>() {fileNodeID};
			var filterList = new List<int>();

			while (toTraverse.Count > 0)
			{
				if (filterList.Contains(toTraverse[0]) == false)
				{
					filterList.Add(toTraverse[0]);
					var node = MerinoTreeData.GetNode(toTraverse[0]);
					if (node != null && node.hasChildren)
					{
						toTraverse.AddRange(node.children.Select(x => x.id));
					}
				}
				toTraverse.RemoveAt(0);
			}
			
			// export these nodes
			//todo: move back over to linq, we were getting null ref exception so using this for the time being
			var treeNodes = new List<MerinoTreeElement>();
			foreach (var merinoTreeElement in data.treeElements)
			{
				if (filterList.Contains(merinoTreeElement.id))
				{
					treeNodes.Add(merinoTreeElement);
				}
			}

			// save data to string
			foreach (var treeNode in treeNodes)
			{
				// skip the root, and skip any non-node nodes
				if (treeNode.depth == -1 || treeNode.leafType != MerinoTreeElement.LeafType.Node) continue;

				nodeInfoList.Add(TreeNodeToYarnNode(treeNode));
			}
				
			return YarnSpinnerFileFormatConverter.ConvertNodesToYarnText(nodeInfoList);
		}

		// used internally for playtest preview
		public static string SaveAllNodesAsString()
		{	
			// gather data
			if (MerinoPrefs.validateNodeTitles) 
			{
				ValidateNodeTitles();
			}
			
			MerinoTreeData data = MerinoTreeData.Instance;
			var nodeInfo = new List<YarnSpinnerLoader.NodeInfo>();

			// save data to string
			//todo: move back over to linq, we were getting null ref exception so using this for the time being
			var treeNodes = new List<MerinoTreeElement>();
			foreach (var merinoTreeElement in data.treeElements)
			{
				if (merinoTreeElement.leafType == MerinoTreeElement.LeafType.Node)
				{
					treeNodes.Add(merinoTreeElement);
				}
			}
			
			foreach (var treeNode in treeNodes)
			{
				// skip the root
				if (treeNode.depth == -1)
				{
					continue;
				}

				nodeInfo.Add( TreeNodeToYarnNode(treeNode) );
			}

			return YarnSpinnerFileFormatConverter.ConvertNodesToYarnText(nodeInfo);
		}

		private static YarnSpinnerLoader.NodeInfo TreeNodeToYarnNode(MerinoTreeElement treeNode)
		{
			var info = new YarnSpinnerLoader.NodeInfo();

			info.title = treeNode.name;
			info.body = treeNode.nodeBody;
			info.tags = treeNode.nodeTags;
			
			var newPosition = new YarnSpinnerLoader.NodeInfo.Position
			{
				x = treeNode.nodePosition.x, 
				y = treeNode.nodePosition.y
			};
			info.position = newPosition;
			
			if (((MerinoTreeElement) treeNode.parent).leafType != MerinoTreeElement.LeafType.File)
			{
				info.parent = treeNode.parent.name;
			}

			return info;
		}

		// ensure unique node titles, very important for YarnSpinner
		private static void ValidateNodeTitles(List<MerinoTreeElement> nodes = null)
		{
			if (!MerinoPrefs.validateNodeTitles) return;

			if (nodes == null) // if null, then let's just use all currently loaded nodes
			{
				nodes = MerinoTreeData.Instance.treeElements;
			}
			
			// make sure we're not doing this to any folder or file nodes, ONLY YARN NODES
			nodes = nodes.Where(x => x.leafType == MerinoTreeElement.LeafType.Node).ToList();

			// validate data: ensure unique node names
			var nodeTitles = new Dictionary<int,string>(); // index, newTitle
			var duplicateCount = new Dictionary<string, int>(); // increment counter for each duplicate name, and use for rename suffix
			bool foundDuplicate = false;
			for (int i=0;i<nodes.Count;i++)
			{
				// if there's a node already with that name, then append unique suffix
				if (nodeTitles.Values.Contains(nodes[i].name))
				{
					// count duplicates
					if (!duplicateCount.ContainsKey(nodes[i].name))
					{
						duplicateCount.Add(nodes[i].name, 2);
					}
					
					// when we make a new name, we have to ensure it's unique...
					if (nodeTitles.ContainsKey(nodes[i].id) == false)
					{
						nodeTitles.Add(nodes[i].id, nodes[i].name);
					}
					
					nodeTitles[nodes[i].id] = nodes[i].name + "_" + duplicateCount[nodes[i].name]++;
					foundDuplicate = true;
				} // but if there's not already a node with that name, we should still make a note of it
				else if (nodeTitles.ContainsKey(nodes[i].id) == false)
				{
					nodeTitles.Add(nodes[i].id, nodes[i].name);
				}
	
			}
			
			if (foundDuplicate)
			{
				MerinoTreeData data = MerinoTreeData.Instance;
			
				string renamedNodes = "Merino found nodes with duplicate names (which aren't allowed for Yarn) and renamed them. This might break node links, you can undo it. The following nodes were renamed: ";
				Undo.RecordObject(data, "Merino: AutoRename");
				
				foreach (var kvp in nodeTitles)
				{
					if (data.treeElements[kvp.Key].name != kvp.Value)
					{
						renamedNodes += string.Format("\n* {0} > {1}", data.treeElements[kvp.Key].name, kvp.Value);
						data.treeElements[kvp.Key].name = kvp.Value;
					}
				}
				EditorUtility.SetDirty(data);
				MerinoDebug.Log(LoggingLevel.Warning, renamedNodes);
				//todo: repaint MerinoEditorWindow tree view so names get updated
				
				// this is bad, but we're gonna do some recursion here, just to make extra sure there's STILL no duplicates...
				ValidateNodeTitles(data.treeElements);
			}
			else if (MerinoPrefs.useAutosave)
			{
				SaveDataToFiles();
			}
			
		}
	    
	    
    }
}
