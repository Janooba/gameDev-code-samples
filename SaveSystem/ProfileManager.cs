using System;
using System.IO;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEngine;

using Sirenix.OdinInspector;
using Sirenix.Serialization;

using Archaic.Core;
using Archaic.Maxim.Player;
using Archaic.Maxim.World;
using Archaic.Maxim.Entities;

using QFSW.QC;

// Handles saving and loading profile data to and from file
// LoadedProfile is always kept up to date, but only saved to file at Save Stations

namespace Archaic.Maxim.Data
{
    public class ProfileManager : MonoBehaviour
    {
        [ShowInInspector]
        private static Profile[] loadedProfiles;
        public static Profile[] LoadedProfiles
        {
            get
            {
                return loadedProfiles;
            }
        }

        private static Profile loadedProfile;
        [HideInInspector]
        public static Profile LoadedProfile
        {
            get
            {
                return loadedProfile;
            }
        }

        public static bool useDevProfile = false;

        public static DateTime timeStartedPlaying = DateTime.Now;

        private static string SavePath => $"{Application.persistentDataPath}/SaveData/";

        #region Public API
        public static void Initialize() {}

        /// <summary> Creates a brand new player profile. </summary>
        public static void CreateNewProfile(Profile template = null)
        {
            Profile newProfile;
            
            if (template != null)
                newProfile = template;
            else
                newProfile = GlobalData.Instance.Profiles.GetNewFromTemplate();

            newProfile.identifier = UnityEngine.Random.Range(100000, 1000000).ToString();
            newProfile.totalTimePlayed = 0;

            newProfile.InitializeDictionaries();

            loadedProfile = newProfile;
        }

        /// <summary> Creates a new save slot with the current loaded data </summary>
        public static void SaveNew()
        {
            UpdateCurrentSave();
            SaveToFile(loadedProfile);
        }

        /// <summary> Overwrites the given file by creating a new file, then deleting the old one </summary>
        public static void OverwriteSave(string fileToOverwrite)
        {
            UpdateCurrentSave();
            SaveToFile(loadedProfile);

            DeleteSave(fileToOverwrite);
        }

        /// <summary>
        /// Loads information into the profile from the given file
        /// NOTE: Actual game-state loading is handled by the GameLogic
        /// </summary>
        public static void LoadSave(string filename)
        {
            Profile profile;

            if (LoadFromFile(filename, out profile))
            {
                loadedProfile = profile;
            }
        }

        /// <summary>
        /// Deletes the save with the given file name plus extension
        /// </summary>
        /// <param name="filename">file name plus extension</param>
        public static void DeleteSave(string filename)
        {
            string path = SavePath + filename;
            File.Delete(path);
        }

        /// <summary>
        /// Finds all save files in the save directory and returns them as a dictionary 
        /// of [filename, Profile]
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, Profile> GetAllSaves()
        {
            Dictionary<string, Profile> profiles = new Dictionary<string, Profile>();

            if (!Directory.Exists(SavePath))
                return profiles;

            string[] fileNames = Directory.GetFiles(SavePath, "*.json");
            foreach (var file in fileNames)
            {
                Profile profile;
                string fileName = Path.GetFileName(file);
                if (LoadFromFile(fileName, out profile))
                {
                    profiles.Add(fileName, profile);
                }
            }

            return profiles;
        }

        #endregion Public API

        /// <summary>
        /// Gathers all information needed that isn't updated by itself
        /// </summary>
        private static void UpdateCurrentSave()
        {
            PlayerController.I.WeaponController.SaveHandgun();
            PlayerController.I.WeaponController.SaveHeldWeapon();
            PlayerController.I.WeaponController.SaveSlungWeapon();

            // Enemies
            var enemies = GameObject.Find("Enemies")?.GetComponentsInChildren<AI.EnemyCore>(true);
            foreach (var enemy in enemies)
            {
                enemy.SaveState();
            }

            // Audio environments
            var audioEnviros = FindObjectsOfType<SECTR_AudioEnvironmentArea>();
            foreach (var enviro in audioEnviros)
            {
                if (enviro.Active)
                {
                    LoadedProfile.activeSoundscape = enviro.name;
                }
            }

            // Save everything else that can be saved
            var savedBehaviours = GameObject.FindObjectsOfType<SavedBehaviour>();
            foreach (var savedObject in savedBehaviours)
            {
                savedObject.SaveState();
            }

            // Save dropped items
            foreach (var drop in FindObjectsOfType<World_DroppedItem>())
            {
                drop.SaveState();
            }

            var spawnPoint = FindObjectOfType<PlayerSpawn>();

            LoadedProfile.savedScene = spawnPoint.gameObject.scene.name;
            LoadedProfile.savedPosition = PlayerController.I.Character.transform.position;
            LoadedProfile.savedRotation = PlayerController.I.Character.transform.rotation;
            LoadedProfile.isIndoors = PlayerController.I.IsIndoors;
            LoadedProfile.health = PlayerController.I.Actor.Health.CurrentHealth;

            LoadedProfile.weatherIntensity = Weather.Instance.intensity;
            LoadedProfile.weatherType = Weather.Instance.weatherType;

            // Dialogue system
            LoadedProfile.dialogue = PixelCrushers.SaveSystem.Serialize(PixelCrushers.SaveSystem.RecordSavedGameData());

            PlayerController.I.Inventory.SaveToProfile();
        }

        /// <summary>
        /// Saves the provided profile to file.
        /// </summary>
        /// <param name="profile"></param>
        private static void SaveToFile(Profile profile)
        {
            string fileName = $"{profile.identifier}_{DateTime.Now.ToString("yyyyMMdd_hhmmss")}.json";
            string path = SavePath + fileName;

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                profile.totalTimePlayed += (float) (DateTime.Now - timeStartedPlaying).TotalSeconds;
                profile.timeSaved = DateTime.Now;
                timeStartedPlaying = DateTime.Now;

                byte[] bytes = SerializationUtility.SerializeValue<Profile>(profile, DataFormat.JSON);
                File.WriteAllBytes(path, bytes);
            }
            catch (Exception e)
            {
                Debug.LogError(e.Message);
            }
        }

        /// <summary>
        /// Loads a specified profile into data
        /// </summary>
        /// <param name="fileName">File name plus extension</param>
        /// <param name="profile">The profile, if loaded</param>
        /// <returns>True if a profile was found and successfully loaded</returns>
        private static bool LoadFromFile(string fileName, out Profile profile)
        {
            string path = SavePath + fileName;
            profile = new Profile();

            if (!File.Exists(path))
            {
                return false;
            }
            else
            {
                try
                {
                    byte[] bytes = File.ReadAllBytes(path);
                    Profile profileData = SerializationUtility.DeserializeValue<Profile>(bytes, DataFormat.JSON);

                    if (profileData == null)
                        throw new FormatException("Profile was not the right format.");

                    if (profileData.profileVersion < Profile.CurrentVersion)
                        throw new FormatException("Profile is of an older version.");

                    profile = profileData;

                    return true;
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message);
                    return false;
                }
            }
        }

        #region Console Commands

        [Command("Saves.Load", "Loads a save with the given filename. Use Saves.Get to get all file names")]
        private void CMD_LoadSave(string filename)
        {
            if (File.Exists(SavePath + filename))
                LoadSave(filename);
            else
                Debug.LogWarning($"Save with filename: {filename} does not exist");
        }

        [Command("Saves.ForceSave", "UNSTABLE! Forces the game to create a new save with current data.")]
        private void CMD_ForceSave()
        {
            if (loadedProfile == null)
            {
                Debug.LogWarning("No game loaded to save");
                return;
            }

            SaveNew();
        }

        [Command("Saves.Get", "Returns all save file names")]
        private string[] CMD_GetSaveFiles()
        {
            var dict = GetAllSaves();
            return dict.Keys.ToArray();
        }

        #endregion
    }
}