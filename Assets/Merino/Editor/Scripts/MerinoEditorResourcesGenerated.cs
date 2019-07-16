using UnityEngine;
#pragma warning disable 649

namespace Merino
{
    internal partial class MerinoEditorResources : ScriptableObject
    {
        [SerializeField] private EditorTexture node;
        [SerializeField] private EditorTexture node_add;
        [SerializeField] private EditorTexture node_delete;
        [SerializeField] private EditorTexture node_edit;
        [SerializeField] private EditorTexture nodemap;
        [SerializeField] private EditorTexture page;
        [SerializeField] private EditorTexture page_new;
        [SerializeField] private EditorTexture save;

        public static Texture2D Node { get { return Instance.node.Texture2D; } }
        public static Texture2D NodeAdd { get { return Instance.node_add.Texture2D; } }
        public static Texture2D NodeDelete { get { return Instance.node_delete.Texture2D; } }
        public static Texture2D NodeEdit { get { return Instance.node_edit.Texture2D; } }
        public static Texture2D Nodemap { get { return Instance.nodemap.Texture2D; } }
        public static Texture2D Page { get { return Instance.page.Texture2D; } }
        public static Texture2D PageNew { get { return Instance.page_new.Texture2D; } }
        public static Texture2D Save { get { return Instance.save.Texture2D; } }
    }
}
