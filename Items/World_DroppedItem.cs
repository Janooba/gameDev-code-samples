using Archaic.Core;
using Archaic.Core.Extensions;
using Archaic.Core.Utilities;
using Archaic.Maxim.Data;
using Archaic.Maxim.Inventory;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;

// An item that was dropped on the ground

namespace Archaic.Maxim.World
{
    public class World_DroppedItem : Trigger, IUseable
    {
        [SerializeField]
        private bool interactable = true;
        public bool Interactable
        {
            get => interactable;
            set => interactable = value;
        }

        public string Tooltip => "Pick Up";
        public ItemData item;
        public int count;
        public bool hasPhysics;
        [ShowIf("@item is WeaponData")]
        public MagazineData includedMagazine;
        protected override Color gizmoColour => new Color(0, 1, 1);

        [SerializeField, HideInInspector]
        private GameObject itemModel;
        [SerializeField, HideInInspector]
        private GameObject itemGlint;

        private bool isPickedUp = false;

        /// <summary> Spawns a dropped item in the world. Used mostly by inventory. </summary>
        public static World_DroppedItem SpawnDroppedItem(string id, int count, Vector3 position, Quaternion rotation, bool hasPhysics = false, float spawnRadius = 0f, string magId = "")
        {
            GameObject instance = new GameObject($"DroppedItem:{id}");

            Vector2 offset = Random.insideUnitCircle * spawnRadius;
            position += new Vector3(offset.x, 0f, offset.y);

            instance.transform.SetPositionAndRotation(position, rotation);

            var droppedItem = instance.AddComponent<World_DroppedItem>();

            droppedItem.item = ItemDatabase.Instance.GetItemByID(id);
            droppedItem.count = count;
            droppedItem.hasPhysics = hasPhysics;

            if (!string.IsNullOrEmpty(magId))
                droppedItem.includedMagazine = ItemDatabase.Instance.GetItemByID(magId) as MagazineData;

            return droppedItem;
        }

        protected override void Start()
        {
            base.Start();

            if (!rbody)
                rbody = GetComponent<Rigidbody>();

            if (rbody)
                rbody.isKinematic = !hasPhysics;
        }

        public void Initialize(bool isNew)
        {
            if (isNew)
                SaveState();

            SpawnItemModel();
            var trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = item.defaultPickupRadius;

            if (!rbody)
                rbody = GetComponent<Rigidbody>();

            if (rbody)
                rbody.isKinematic = !hasPhysics;

            SpawnItemGlint();
        }

        public bool StartUse(Transform user)
        {
            if (item.ShowDetailsOnPickup)
                ItemDisplay.Instance?.ShowItem(item);

            GameInfo.I.Player.Inventory.GiveItem(item, count, includedMagazine);

            isPickedUp = true;
            SaveState();

            Destroy(gameObject);
            return true;
        }

        public void StopUse() { }

        public override void SaveState()
        {
            if (isPickedUp)
                ProfileManager.LoadedProfile.RemoveDroppedItem(InstanceID);
            else
            {
                string magId = includedMagazine == null ? "" : includedMagazine.Id;
                ProfileManager.LoadedProfile.AddDroppedItem(InstanceID, item.Id, count, magId, transform.position, transform.rotation, gameObject.scene.name);
            }
        }

        public override void LoadState()
        {
            if (!ProfileManager.LoadedProfile.DroppedItemExists(InstanceID))
            {
                Destroy(gameObject);
            }

            Profile.ItemData data = ProfileManager.LoadedProfile.itemsDropped[item.Id];
            if (string.IsNullOrEmpty(data.id))
            {
                hasPhysics = data.hasPhysics;

                if (!rbody)
                    rbody = GetComponent<Rigidbody>();

                if (rbody)
                    rbody.isKinematic = !hasPhysics;
            }
        }

        private void SpawnItemGlint()
        {
            itemGlint = Instantiate(item.ModelPrefab, transform, false);
            itemGlint.hideFlags = HideFlags.DontSave;

            foreach (var renderer in itemGlint.GetComponentsInThisOrChildren<Renderer>())
            {
                Material[] matArray = renderer.materials;

                for (int i = 0; i < matArray.Length; i++)
                {
                    if (item.GlintMaterial)
                        matArray[i] = item.GlintMaterial;
                    else
                        matArray[i] = GlobalData.Instance.EffectData.itemGlintMaterial;
                }

                renderer.materials = matArray;
            }
        }
        private void SpawnItemModel()
        {
            if (item.ModelPrefab)
                itemModel = Instantiate(item.ModelPrefab, transform, false);
            else
            {
                itemModel = GameObject.CreatePrimitive(PrimitiveType.Cube);
                itemModel.transform.SetParent(transform);
                itemModel.transform.SetPositionAndRotation(transform.position, Quaternion.identity);
                itemModel.transform.localScale = Vector3.one * 0.1f;
            }
        }
    }
}