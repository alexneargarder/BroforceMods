using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityModManagerNet;

namespace Utility_Mod
{
    public class Dropdown
    {
        #region Fields and Properties

        private Vector2 scrollViewVector;
        public Rect dropDownRect;
        public string[] list;
        public string ToolTip;

        public int indexNumber;
        public bool show;

        #endregion

        #region Constructor

        public Dropdown(float x, float y, float width, float height, string[] options, int setIndexNumber, bool setShow = false)
        {
            dropDownRect = new Rect(x, y, width, height);
            list = options;
            this.indexNumber = setIndexNumber;

            scrollViewVector = Vector2.zero;
            show = setShow;
        }

        #endregion

        #region GUI Rendering

        public void OnGUI(UnityModManager.ModEntry modEntry)
        {
            if (GUILayout.Button("", GUILayout.Width(dropDownRect.width)))
            {
                if (!show)
                {
                    show = true;
                }
                else
                {
                    show = false;
                }
            }
            Rect lastRect = GUILayoutUtility.GetLastRect();
            dropDownRect.x = lastRect.x;  // + lastRect.width + offsetX;
            dropDownRect.y = lastRect.y;


            if (show)
            {
                scrollViewVector = GUI.BeginScrollView(new Rect((dropDownRect.x), (dropDownRect.y + 25), dropDownRect.width, dropDownRect.height), scrollViewVector, new Rect(0, 0, dropDownRect.width, Mathf.Max(dropDownRect.height, (list.Length * 25))));

                GUI.Box(new Rect(0, 0, dropDownRect.width, Mathf.Max(dropDownRect.height, (list.Length * 25))), "");

                for (int index = 0; index < list.Length; index++)
                {

                    if (GUI.Button(new Rect(0, (index * 25), dropDownRect.width, 25), ""))
                    {
                        show = false;
                        indexNumber = index;
                    }

                    GUI.Label(new Rect(5, (index * 25), dropDownRect.width, 25), list[index]);

                }

                GUI.EndScrollView();
            }
            else
            {
                GUI.Label(new Rect((dropDownRect.x + 5), dropDownRect.y, 300, 25), new GUIContent( list[indexNumber], this.ToolTip ) );
            }
        }

        #endregion
    }
}
