﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace HierarchyDecorator
    {
    [System.Serializable]
    internal class ComponentType : IComparable<ComponentType>
        {
        //Off by default to stop a huge spam of every component
        public bool shown = false;
        public string name;
        public Type type;

        public ComponentType(Type type)
            {
            this.type = type;
            this.name = type.Name;
            }

        public void UpdateType(Type type)
            {
            this.type = type;
            this.name = type.Name;
            }

        public int CompareTo(ComponentType other)
            {
            if (other == null)
                return 1;

            return name.CompareTo (other.name);
            }
        }

    // Create a new type of Settings Asset.
    internal class HierarchyDecoratorSettings : ScriptableObject, ISerializationCallbackReceiver
        {
        // --- Global Settings ---
        public bool showComponents = true;
        public bool showLayers = true;
        public bool showActiveToggles = true;

        public GlobalStyle globalStyle;

        #region Collections

        /// <summary>
        /// Collection of all prefixes used for custom hierarchy overlays
        /// </summary>
        public List<HierarchyStyle> prefixes;

        /// <summary>
        /// Collection of custom <see cref="GUIStyle"/>'s used for the hierarchy designs
        /// </summary>
        public List<GUIStyle> styles = new List<GUIStyle> ();

        ////Default prefixes. Add or remove any that you dislike or want
        //private readonly List<HierarchyStyle> importantPrefixes = new List<HierarchyStyle> ()
        //    {
        //    new HierarchyStyle ("=",  new Color (150f / 255f, 150f / 255f, 150f / 255f, 1), "Header"),
        //    new HierarchyStyle ("-" , new Color (178f / 255f, 178f / 255f, 178f / 255f, 1), "Toolbar"),
        //    new HierarchyStyle ("+" , new Color (63f / 255f, 188f / 255f, 200f / 255f, 1)),
        //    };

        private readonly List<HierarchyStyle> importantPrefixes = new List<HierarchyStyle> ()
            {
            new HierarchyStyle ("=" , "Header"),
            new HierarchyStyle ("-" , "Toolbar"),
            new HierarchyStyle ("+" ),
            };

        /// <summary>
        /// Components shown in the inspector
        /// </summary>
        public Dictionary<string, ComponentType> shownComponents = new Dictionary<string, ComponentType> ();

        /// <summary>
        /// List of all components
        /// Required for easier handling of serialization
        /// </summary>
        public List<ComponentType> components;

        private static Type[] allTypes;
        #endregion

        #region Creation

        /// <summary>
        /// Load the asset for settings, or create one if it doesn't already exist
        /// </summary>
        /// <returns>The loaded settings</returns>
        internal static HierarchyDecoratorSettings GetOrCreateSettings()
            {
            //Find the asset 
            HierarchyDecoratorSettings settings = AssetDatabase.LoadAssetAtPath<HierarchyDecoratorSettings> (Constants.SETTINGS_ASSET_PATH);

            //Create the asset if it doesn't exist
            if (settings == null)
                {
                //Create and setup defaults
                settings = CreateInstance<HierarchyDecoratorSettings> ();
                settings.SetDefaults ();

                if (!Directory.Exists (Constants.SETTINGS_ASSET_FOLDER))
                    Directory.CreateDirectory (Constants.SETTINGS_ASSET_FOLDER);

                //Create and save
                AssetDatabase.CreateAsset (settings, Constants.SETTINGS_ASSET_PATH);
                AssetDatabase.SaveAssets ();

                Debug.Log ($"Hiearchy Decorator found no previous settings, creating one at {Constants.SETTINGS_ASSET_PATH}.");
                }
            return settings;
            }

        /// <summary>
        /// Convert into serialized object for handling GUI
        /// </summary>
        /// <returns>Serialized version of the settings</returns>
        internal static SerializedObject GetSerializedSettings()
            {
            return new SerializedObject (GetOrCreateSettings ());
            }

        /// <summary>
        /// Setup defaults for the new settings asset
        /// </summary>
        internal void SetDefaults()
            {
            //Setup styles
            prefixes = importantPrefixes;
            styles = new List<GUIStyle> ()
                {
                CreateGUIStyle ("Header",       EditorStyles.boldLabel),
                CreateGUIStyle ("Toolbar",      EditorStyles.toolbarButton),
                CreateGUIStyle ("Grid Centered",EditorStyles.centeredGreyMiniLabel),
                };

            //Specifics for defaults
            var toolbar = prefixes[1];
            toolbar.SetAlignment (TextAnchor.MiddleLeft);
            toolbar.SetFontSize (10);
            toolbar.SetStyle(FontStyle.Normal);

            // - Grid Centered - 
            var grid = styles[2];
            grid.border = new RectOffset (15, 15, 15, 15);
            grid.stretchWidth = true;

            //Apply final settings to all styles
            //foreach (HierarchyStyle prefix in prefixes)
            //    prefix.SetLineStyle (LineStyle.BOTTOM);

            UpdateSettings ();
            }

        #endregion

        #region GUIStyles

        /// <summary>
        /// Get a GUI style from the custom style list
        /// </summary>
        /// <param name="name">The name of the style to find</param>
        /// <returns>Returns the gui style found, or the bold label as default if one is not</returns>
        public GUIStyle GetGUIStyle(string name)
            {
            GUIStyle style = styles.SingleOrDefault (s => s.name == name);
            return style ?? EditorStyles.boldLabel;
            }

        private GUIStyle CreateGUIStyle(string name, GUIStyle styleBase = null)
            {
            //Generally optimistic settings
            return new GUIStyle (styleBase)
                {
                name = name,

                stretchHeight = false,
                stretchWidth = false,

                fontSize = 11,
                fontStyle = FontStyle.Bold,

                fixedHeight = 0,
                fixedWidth = 0,

                alignment = TextAnchor.MiddleLeft,
                };
            }

        #endregion

        //This is just to get psudeo-type selection working
        //Can pass them back to the dictionary when required
        public void OnBeforeSerialize()
            {
            UpdateSettings ();
            }

        public void OnAfterDeserialize()
            {
            
            }

        //This is used just to update the hiearchy when settings are changed
        private void OnValidate()
            {
            EditorApplication.RepaintHierarchyWindow ();
            }

        public void UpdateSettings()
            {
            if (globalStyle == null)
                globalStyle = new GlobalStyle ();

            //Reflection for component types
            if (allTypes == null)
                allTypes = ReflectionUtility.GetTypesFromAllAssemblies (typeof (Component));

            bool hasMissing = (components == null) || (components.Count < allTypes.Length);

            //Quick fix for now to stop duplication issues
            if (components.Count > allTypes.Length)
                {
                components = new List<ComponentType> ();
                hasMissing = true;
                }

            for (int i = 0; i < allTypes.Length; i++)
                {
                var type = allTypes[i];

                if (hasMissing)
                    {
                    if (components == null)
                        components = new List<ComponentType> ();

                    components.Add (new ComponentType (type));
                    }

                if (components[i].type == null)
                    components[i].UpdateType (type);

                if (!shownComponents.ContainsKey (name))
                    shownComponents.Add (name, components[i]);
                }
            }
        }

  
    }
