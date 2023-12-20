using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BroMakerLib.Loggers;
using UnityEngine;

namespace Captain_Ameribro_Mod
{
    public class Main
    {
        public const bool DEBUGTEXTURES = true;
        public static byte[] ExtractResource(string filename)
        {
            Assembly a = Assembly.GetExecutingAssembly();
            using (Stream resFilestream = a.GetManifestResourceStream(filename))
            {
                if (resFilestream == null) return null;
                byte[] ba = new byte[resFilestream.Length];
                resFilestream.Read(ba, 0, ba.Length);
                return ba;
            }
        }

        public static void checkAttached(GameObject gameObject)
        {
            BMLogger.Log("\n\n");
            Component[] allComponents;
            allComponents = gameObject.GetComponents(typeof(Component));
            foreach (Component comp in allComponents)
            {
                BMLogger.Log("attached: " + comp.name + " also " + comp.GetType());
            }
            BMLogger.Log("\n\n");
        }
    }
}
