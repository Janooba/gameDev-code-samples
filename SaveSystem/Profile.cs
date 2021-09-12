using Archaic.Maxim.World;
using Sirenix.OdinInspector;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Profile data class for saving and loading

namespace Archaic.Maxim.Data
{
    [Serializable]
    public class Profile
    {
        [Serializable]
        public struct TransformData
        {
            public Vector3 position;
            public Quaternion rotation;
            public Vector3 velocity;
            public Vector3 angularVelocity;
        }

        [Serializable]
        public struct ItemData 
        {
            public string id;
            public int count;
            public bool hasPhysics;
            public string magId;
            public string scene;
            public TransformData positionData;
        }

        [Serializable]
        public struct EnemyData
        {
            public string instanceId;
            public string scene;
            public bool gameObjectActive;
            public bool isActive;
            public bool isBlind;
            public int health;
            public string targetName;
            public TransformData positionData;
        }

        public static int CurrentVersion => 1;

        public int profileVersion;

        public string identifier;

        [Title("Player")]
        public string niceLocation;
        public string savedScene;
        public Vector3 savedPosition;
        public Quaternion savedRotation;
        public bool isIndoors;
        public int health;

        [Title("Inventory")]
        public bool hasBaton;
        public bool hasFlashlight;
        public bool hasHandgun;
        public bool hasSling;

        public Player.WeaponController.EquippedItemType lastEquippedSlot;

        public List<string> keys = new List<string>();
        public List<string> notes = new List<string>();

        [TableMatrix, ShowInInspector]
        public ItemData[] inventory = new ItemData[9];

        [Title("Weapon Data")]
        public string handgunMag;
        public int handgun_loadedAmmo;
        [Space]
        public string heldWeapon;
        public string heldWeaponMag;
        public int held_loadedAmmo;
        [Space]
        public string slungWeapon;
        public string slungWeaponMag;
        public int slung_loadedAmmo;

        [Tooltip("In Minutes"), TitleGroup("Meta")]
        public float totalTimePlayed;

        [ShowInInspector, LabelText("Time Saved"), DisplayAsString, TitleGroup("Meta")]
        public string TimeSavedAsString => timeSaved.ToString("MMM d yyyy");
        public DateTime timeSaved;

        public int HoursPlayed => Mathf.FloorToInt(MinutesPlayed / 60);
        public int MinutesPlayed => Mathf.FloorToInt(totalTimePlayed / 60);

        [Title("World")]
        public string activeSoundscape;
        public Weather.WeatherType weatherType;
        public float weatherIntensity;
        [ShowInInspector] public Dictionary<string, bool> worldFlags;
        [ShowInInspector] public Dictionary<string, bool> doorLockedStates;
        [ShowInInspector] public Dictionary<string, bool> itemsPickedUp;
        [ShowInInspector] public Dictionary<string, ItemData> itemsDropped;
        [ShowInInspector] public Dictionary<string, EnemyData> enemyData;
        [ShowInInspector] public Dictionary<string, TransformData> physPositions;

        public string dialogue;

        public void InitializeDictionaries()
        {
            if (worldFlags == null)
                worldFlags = new Dictionary<string, bool>();

            if (doorLockedStates == null)
                doorLockedStates = new Dictionary<string, bool>();

            if (itemsPickedUp == null)
                itemsPickedUp = new Dictionary<string, bool>();

            if (itemsDropped == null)
                itemsDropped = new Dictionary<string, ItemData>();

            if (enemyData == null)
                enemyData = new Dictionary<string, EnemyData>();

            if (physPositions == null)
                physPositions = new Dictionary<string, TransformData>();
        }

        public bool GetWorldFlag(string flagKey, bool defaultVal)
        {
            return GetDictFlag(flagKey, ref worldFlags, defaultVal);
        }

        public void SetWorldFlag(string flagKey, bool val)
        {
            SetDictFlag(flagKey, val, ref worldFlags);
        }

        public bool GetDoorLocked(string flagKey, bool defaultVal)
        {
            return GetDictFlag(flagKey, ref doorLockedStates, defaultVal);
        }

        public void SetDoorLocked(string flagKey, bool val)
        {
            SetDictFlag(flagKey, val, ref doorLockedStates);
        }

        public bool GetItemPickedUp(string flagKey)
        {
            return GetDictFlag(flagKey, ref itemsPickedUp, false);
        }

        public void SetItemPickedUp(string flagKey, bool val)
        {
            SetDictFlag(flagKey, val, ref itemsPickedUp);
        }

        public void AddDroppedItem(string flagKey, string id, int count, string magId, Vector3 position, Quaternion rotation, string scene)
        {
            ItemData data = new ItemData
            {
                id = id,
                count = count,
                magId = magId,
                positionData = new TransformData
                {
                    position = position,
                    rotation = rotation
                },
                scene = scene
            };

            SetDictFlag(flagKey, data, ref itemsDropped);
        }

        public bool DroppedItemExists(string flagKey)
        {
            return itemsDropped.ContainsKey(flagKey);
        }

        public void RemoveDroppedItem(string flagKey)
        {
            itemsDropped.Remove(flagKey);
        }

        public void SetEnemyData(string flagKey, AI.EnemyCore enemy)
        {
            EnemyData data = new EnemyData
            {
                instanceId = enemy.InstanceID,
                scene = enemy.gameObject.scene.name,
                gameObjectActive = enemy.gameObject.activeSelf,
                isActive = enemy.IsActive,
                isBlind = enemy.IsBlind,
                health = enemy.Actor.HasHealthController ? enemy.Actor.Health.CurrentHealth : 1,
                targetName = enemy.Target?.name,
                positionData = new TransformData
                {
                    position = enemy.transform.position,
                    rotation = enemy.transform.rotation
                }
            };

            SetDictFlag(flagKey, data, ref enemyData);
        }

        // Generic way to access the various data dictionaries

        private T GetDictFlag<T>(string flagKey, ref Dictionary<string, T> dictionary, T defaultVal)
        {
            if (dictionary == null)
                dictionary = new Dictionary<string, T>();

            if (dictionary.ContainsKey(flagKey))
                return dictionary[flagKey];
            else
                return defaultVal;
        }

        private void SetDictFlag<T>(string flagKey, T val, ref Dictionary<string, T> dictionary)
        {
            if (dictionary == null)
                dictionary = new Dictionary<string, T>();

            if (dictionary.ContainsKey(flagKey))
                dictionary[flagKey] = val;
            else
                dictionary.Add(flagKey, val);
        }
    }
}