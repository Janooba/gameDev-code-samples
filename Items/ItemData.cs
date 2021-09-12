using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Archaic.Maxim.Inventory;
using Archaic.Maxim.Characters;

// Base Item class

namespace Archaic.Maxim.Data
{
    [CreateAssetMenu(fileName = "New Item Data", menuName = "Maxim/Data/Item Data")]
    public class ItemData : ScriptableObject
    {
        public string Id;
        public string NiceName;
        [TextArea(3, 3)]
        public string Description;
        public int MaxStack = 1;
        public bool AllowZero;
        public bool ShowDetailsOnPickup;
        public bool Useable;
        public float defaultPickupRadius = 0.1f;

        public Texture2D icon;

        public SECTR_AudioCue PickupSound;

        private void OnValidate()
        {
            Id = Id.ToLower().Trim().Replace(' ', '_');
        }

        [PreviewField(height: 128)]
        public GameObject ModelPrefab;
        public Material GlintMaterial;

        public virtual bool CanUse(Actor user)
        {
            return false;
        }

        public virtual void Use(Actor user) {}

        public virtual bool CanCombineWith(RuntimeItem fromItem, RuntimeItem toItem)
        {
            return false;
        }

        public virtual void Combine(RuntimeItem fromItem, RuntimeItem toItem) {}
    }
}