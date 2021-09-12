using Sirenix.OdinInspector;
using Sirenix.Serialization;
using Sirenix.Utilities;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using QFSW.QC;

#if UNITY_EDITOR
using Sirenix.Utilities.Editor;
#endif

// This is the "singleton" item database.

namespace Archaic.Maxim.Data
{
    [CreateAssetMenu(fileName ="ItemDatabase", menuName ="Maxim/Data/Item Database")]
    public class ItemDatabase : GlobalConfig<ItemDatabase>
    {
        static ItemDatabase _instance = null;
        public static new ItemDatabase Instance
        {
            get
            {
                if (!_instance)
                    _instance = Resources.Load<ItemDatabase>("Data/ItemDatabase");

                if (!_instance && GameInfo.I)
                    _instance = GameInfo.I.itemDatabase;

                if (!_instance)
                    Debug.LogError("Could not find ItemDatabase scriptableObject");

                return _instance;
            }
        }

        [ListDrawerSettings(OnTitleBarGUI = "DrawNoteRefresh")]
        public List<NoteData> Notes;

        public List<KeyData> Keys;
        public List<MiscItemData> Misc;
        public List<HealingData> Heal;
        public List<AmmoData> Ammo;
        public List<MagazineData> Magazines;

        [ListDrawerSettings(OnTitleBarGUI = "DrawItemRefresh")]
        public List<ItemData> AllItems;

        public NoteData GetNoteByID(string ID)
        {
            return Notes.Where(x => x.Id == ID).FirstOrDefault();
        }

        public ItemData GetItemByID(string ID)
        {
            return AllItems.Where(x => x.Id == ID).FirstOrDefault();
        }

        // Commands
        [Command("Database.ListAllItems", "Lists all items in the item database by ID")]
        public static string[] ListAllItems()
        {
            return Instance.AllItems
                .Where(x => !string.IsNullOrEmpty(x.Id))
                .Select(x => x.Id)
                .ToArray();
        }

#if UNITY_EDITOR
        private void DrawNoteRefresh()
        {
            if (SirenixEditorGUI.ToolbarButton(EditorIcons.Refresh))
            {
                string[] guids = AssetDatabase.FindAssets("t:NoteData");

                foreach (string guid in guids)
                {
                    var note = AssetDatabase.LoadAssetAtPath<NoteData>(AssetDatabase.GUIDToAssetPath(guid));

                    if (!Notes.Contains(note))
                        Notes.Add(note);
                }

                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
            }
        }

        private void DrawItemRefresh()
        {
            if (SirenixEditorGUI.ToolbarButton(EditorIcons.Refresh))
            {
                string[] guids = AssetDatabase.FindAssets("t:ItemData");

                foreach (string guid in guids)
                {
                    var item = AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid));

                    if (!AllItems.Contains(item))
                        AllItems.Add(item);

                    if (item is KeyData && !Keys.Contains(item))
                        Keys.Add(item as KeyData);

                    if (item is MiscItemData && !Misc.Contains(item))
                        Misc.Add(item as MiscItemData);

                    if (item is HealingData && !Heal.Contains(item))
                        Heal.Add(item as HealingData);

                    if (item is AmmoData && !Ammo.Contains(item))
                        Ammo.Add(item as AmmoData);

                    if (item is MagazineData && !Magazines.Contains(item))
                        Magazines.Add(item as MagazineData);
                }

                EditorUtility.SetDirty(this);
                AssetDatabase.SaveAssets();
            }
        }
#endif
    }
}