// Copyright (c) Meta Platforms, Inc. and affiliates.

using UnityEditor;
using UnityEngine;

namespace PhantoUtils
{
    /// <summary>
    /// Just adds a visible comment to a game object.
    /// </summary>
    public class CommentComponent : MonoBehaviour
    {
        [SerializeField, Multiline(4)] internal string comment;

        public string Comment => comment;

#if UNITY_EDITOR
        [CustomEditor(typeof(CommentComponent))]
        public class CommentComponentEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                GUILayout.Label("// Comment:");
                var commentComponent = target as CommentComponent;
                commentComponent.comment = GUILayout.TextArea(commentComponent.comment);

                if (GUI.changed)
                {
                    EditorUtility.SetDirty(commentComponent);
                }
            }
        }
#endif
    }
}
