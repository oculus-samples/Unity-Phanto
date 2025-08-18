// (c) Meta Platforms, Inc. and affiliates. Confidential and proprietary.

using UnityEngine;

namespace Phanto
{
    /// <summary>
    ///     This class manages the GUI visibility for the game
    /// </summary>
    public class PhantoGUI : MonoBehaviour
    {
        [SerializeField] private GameObject GuiParent;

        public void ToggleVisible()
        {
            GuiParent.SetActive(!GuiParent.activeSelf);
        }
    }
}
