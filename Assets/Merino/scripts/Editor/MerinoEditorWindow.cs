using System;
using System.Collections.Generic;
using System.Net;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEditor.TreeViewExamples;

namespace Merino
{

	class MerinoEditorWindow : EditorWindow
	{
		[NonSerialized] bool m_Initialized;
		[SerializeField] TreeViewState viewState; // Serialized in the window layout file so it survives assembly reloading
		[SerializeField] MultiColumnHeaderState m_MultiColumnHeaderState;
		[SerializeField] bool doubleClickOpensFile = true;
		
		[NonSerialized] float sidebarWidth = 200f;
		
		SearchField m_SearchField;
		MerinoTreeView m_TreeView;
		MyTreeAsset m_MyTreeAsset;

		TextAsset currentFile;

		[MenuItem("Window/Merino (Yarn Editor)")]
		public static MerinoEditorWindow GetWindow ()
		{
			var window = GetWindow<MerinoEditorWindow>();
			window.titleContent = new GUIContent("Merino (Yarn)");
			window.Focus();
			window.Repaint();
			return window;
		}

		[OnOpenAsset]
		public static bool OnOpenAsset (int instanceID, int line)
		{
//			var myTreeAsset = EditorUtility.InstanceIDToObject (instanceID) as MyTreeAsset;
//			if (myTreeAsset != null)
//			{
//				var window = GetWindow ();
//				window.SetTreeAsset(myTreeAsset);
//				return true;
//			}
			var myTextAsset = EditorUtility.InstanceIDToObject(instanceID) as TextAsset;
			if (myTextAsset != null)
			{
				var window = GetWindow ();
				return window.SetTreeAsset(myTextAsset);
			}
			return false; // we did not handle the open
		}

		bool SetTreeAsset (TextAsset myTextAsset)
		{
			if (doubleClickOpensFile && IsProbablyYarnFile(myTextAsset))
			{
				currentFile = myTextAsset;
				m_Initialized = false;
				return true;
			}
			else
			{
				return false;
			}
		}

		Rect multiColumnTreeViewRect
		{
			get { return new Rect(20, 30, sidebarWidth, position.height-60); }
		}

		Rect toolbarRect
		{
			get { return new Rect (20f, 10f, sidebarWidth, 20f); }
		}

		Rect nodeEditRect
		{
			get { return new Rect( sidebarWidth+40f, 20f, position.width-sidebarWidth-70, position.height-40);}
		}

		Rect bottomToolbarRect
		{
			get { return new Rect(20f, position.height - 18f, position.width - 40f, 16f); }
		}

		public MerinoTreeView treeView
		{
			get { return m_TreeView; }
		}

		void InitIfNeeded ()
		{
			if (!m_Initialized)
			{
				// Check if it already exists (deserialized from window layout file or scriptable object)
				if (viewState == null)
					viewState = new TreeViewState();

				bool firstInit = m_MultiColumnHeaderState == null;
				var headerState = MerinoTreeView.CreateDefaultMultiColumnHeaderState(multiColumnTreeViewRect.width);
				if (MultiColumnHeaderState.CanOverwriteSerializedFields(m_MultiColumnHeaderState, headerState))
					MultiColumnHeaderState.OverwriteSerializedFields(m_MultiColumnHeaderState, headerState);
				m_MultiColumnHeaderState = headerState;
				
				var multiColumnHeader = new MyMultiColumnHeader(headerState);
				if (firstInit)
					multiColumnHeader.ResizeToFit ();

				var treeModel = new TreeModel<MerinoTreeElement>(GetData());
				
				m_TreeView = new MerinoTreeView(viewState, multiColumnHeader, treeModel);

				m_SearchField = new SearchField();
				m_SearchField.downOrUpArrowKeyPressed += m_TreeView.SetFocusAndEnsureSelectedItem;

				m_Initialized = true;
			}
		}
		
		IList<MerinoTreeElement> GetData ()
		{
//			if (m_MyTreeAsset != null && m_MyTreeAsset.treeElements != null && m_MyTreeAsset.treeElements.Count > 0)
//				return m_MyTreeAsset.treeElements;
			
			if ( currentFile != null && m_MyTreeAsset.treeElements != null && m_MyTreeAsset.treeElements.Count > 0)
				return m_MyTreeAsset.treeElements;

			// generate default data
			var treeElements = new List<MerinoTreeElement>(2);
			var root = new MerinoTreeElement("Root", -1, 0);
			var start = new MerinoTreeElement("Start", 0, 1);
			treeElements.Add(root);
			treeElements.Add(start);
			return treeElements;
		}

		/// <summary>
		/// Checks to see if TextAsset is a probably valid .yarn.txt file, or if it's just a random text file
		/// </summary>
		/// <param name="textAsset"></param>
		/// <returns></returns>
		bool IsProbablyYarnFile(TextAsset textAsset)
		{
			if ( AssetDatabase.GetAssetPath(textAsset).EndsWith(".yarn.txt") && textAsset.text.Contains("---") && textAsset.text.Contains("===") && textAsset.text.Contains("title:") )
			{
				return true;
			}
			else
			{
				return false;
			}
		}

		void OnSelectionChange ()
		{
			if (!m_Initialized)
				return;

			var possibleYarnFile = Selection.activeObject as TextAsset;
			if (possibleYarnFile != null && possibleYarnFile != currentFile && IsProbablyYarnFile(possibleYarnFile))
			{
				currentFile = possibleYarnFile;
				m_TreeView.treeModel.SetData (GetData ());
				m_TreeView.Reload ();
			}
		}

		void OnGUI ()
		{
			InitIfNeeded();

			SearchBar (toolbarRect);
			DoTreeView (multiColumnTreeViewRect);

			if (viewState != null && viewState.selectedIDs.Count > 0)
			{
				DrawSelectedNodes(nodeEditRect);
			}

			//BottomToolBar (bottomToolbarRect);
		}

		void SearchBar (Rect rect)
		{
			treeView.searchString = m_SearchField.OnGUI (rect, treeView.searchString);
		}

		void DoTreeView (Rect rect)
		{
			m_TreeView.OnGUI(rect);
		}

		void DrawSelectedNodes(Rect rect)
		{
			GUILayout.BeginArea(rect);

			

//			using (new EditorGUILayout.VerticalScope())
//			{
				
				foreach (var id in viewState.selectedIDs)
				{
				//	EditorGUILayout.BeginVertical();
					EditorGUILayout.TextField( m_TreeView.treeModel.Find(id).name);
					string passage = "blah blah blah\nlorem ipsum\nplaceholder";
					float height = EditorStyles.textArea.CalcHeight(new GUIContent(passage), rect.width);
					GUILayout.TextArea(passage, GUILayout.Height(0f), GUILayout.ExpandHeight(true),
						GUILayout.MaxHeight(height));
				//	EditorGUILayout.EndVertical();
					EditorGUILayout.Space();
					EditorGUILayout.Separator();
				}
				
//			}
			
			GUILayout.EndArea();
		}

		void BottomToolBar (Rect rect)
		{
			GUILayout.BeginArea (rect);

			using (new EditorGUILayout.HorizontalScope ())
			{

				var style = "miniButton";
				if (GUILayout.Button("Expand All", style))
				{
					treeView.ExpandAll ();
				}

				if (GUILayout.Button("Collapse All", style))
				{
					treeView.CollapseAll ();
				}

				GUILayout.FlexibleSpace();

				GUILayout.Label (currentFile != null ? AssetDatabase.GetAssetPath (currentFile) : string.Empty);

				GUILayout.FlexibleSpace ();

				if (GUILayout.Button("Set sorting", style))
				{
					var myColumnHeader = (MyMultiColumnHeader)treeView.multiColumnHeader;
					myColumnHeader.SetSortingColumns (new int[] {4, 3, 2}, new[] {true, false, true});
					myColumnHeader.mode = MyMultiColumnHeader.Mode.LargeHeader;
				}


				GUILayout.Label ("Header: ", "minilabel");
				if (GUILayout.Button("Large", style))
				{
					var myColumnHeader = (MyMultiColumnHeader) treeView.multiColumnHeader;
					myColumnHeader.mode = MyMultiColumnHeader.Mode.LargeHeader;
				}
				if (GUILayout.Button("Default", style))
				{
					var myColumnHeader = (MyMultiColumnHeader)treeView.multiColumnHeader;
					myColumnHeader.mode = MyMultiColumnHeader.Mode.DefaultHeader;
				}
				if (GUILayout.Button("No sort", style))
				{
					var myColumnHeader = (MyMultiColumnHeader)treeView.multiColumnHeader;
					myColumnHeader.mode = MyMultiColumnHeader.Mode.MinimumHeaderWithoutSorting;
				}

				GUILayout.Space (10);
				
				if (GUILayout.Button("values <-> controls", style))
				{
					treeView.showControls = !treeView.showControls;
				}
			}

			GUILayout.EndArea();
		}
	}


	internal class MyMultiColumnHeader : MultiColumnHeader
	{
		Mode m_Mode;

		public enum Mode
		{
			LargeHeader,
			DefaultHeader,
			MinimumHeaderWithoutSorting
		}

		public MyMultiColumnHeader(MultiColumnHeaderState state)
			: base(state)
		{
			mode = Mode.DefaultHeader;
		}

		public Mode mode
		{
			get
			{
				return m_Mode;
			}
			set
			{
				m_Mode = value;
				switch (m_Mode)
				{
					case Mode.LargeHeader:
						canSort = true;
						height = 37f;
						break;
					case Mode.DefaultHeader:
						canSort = true;
						height = DefaultGUI.defaultHeight;
						break;
					case Mode.MinimumHeaderWithoutSorting:
						canSort = false;
						height = DefaultGUI.minimumHeight;
						break;
				}
			}
		}

		protected override void ColumnHeaderGUI (MultiColumnHeaderState.Column column, Rect headerRect, int columnIndex)
		{
			// Default column header gui
			base.ColumnHeaderGUI(column, headerRect, columnIndex);

			// Add additional info for large header
			if (mode == Mode.LargeHeader)
			{
				// Show example overlay stuff on some of the columns
				if (columnIndex > 2)
				{
					headerRect.xMax -= 3f;
					var oldAlignment = EditorStyles.largeLabel.alignment;
					EditorStyles.largeLabel.alignment = TextAnchor.UpperRight;
					GUI.Label(headerRect, 36 + columnIndex + "%", EditorStyles.largeLabel);
					EditorStyles.largeLabel.alignment = oldAlignment;
				}
			}
		}
	}

}
