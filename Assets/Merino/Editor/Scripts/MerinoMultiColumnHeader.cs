using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Merino
{
    
    internal class MerinoMultiColumnHeader : MultiColumnHeader
    {
        Mode m_Mode;
        //Texture helpIcon = EditorGUIUtility.IconContent("_Help").image;
		
        public enum Mode
        {
            LargeHeader,
            DefaultHeader,
            MinimumHeaderWithoutSorting
        }

        public MerinoMultiColumnHeader(MultiColumnHeaderState state)
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
                        height = DefaultGUI.minimumHeight;
                        break;
                    case Mode.MinimumHeaderWithoutSorting:
                        canSort = false;
                        height = DefaultGUI.minimumHeight;
                        break;
                }
            }
        }
		
        public override void OnGUI(Rect rect, float xScroll)
        {
            // add extra "clear sorting" button if sorting is active
            if (state.sortedColumnIndex != -1)
            {
                var clearSortRect = new Rect(rect);
                clearSortRect.width = 18;
                clearSortRect.height = clearSortRect.width;
                rect.x += clearSortRect.width;
                rect.width -= clearSortRect.width;
                if (GUI.Button(clearSortRect, new GUIContent("x", "no sort / clear column sorting"), EditorStyles.miniButton))
                {
                    state.sortedColumnIndex = -1;
                }
            }

            // draw rest of the header
            base.OnGUI(rect, xScroll);
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