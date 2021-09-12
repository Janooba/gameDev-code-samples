using Archaic.Maxim.Data;
using Archaic.Maxim.Inventory;
using Rewired.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Magazine item class (for weapons)
// Allows combining in the inventory to load ammo
// into a magazine, or from one mag to another

namespace Archaic.Maxim.Data
{
    [CreateAssetMenu(menuName = "Maxim/Data/Magazine Data", order = 500)]
    [Serializable]
    public class MagazineData : ItemData
    {
        public AmmoData ammoType;

        public override bool CanCombineWith(RuntimeItem fromItem, RuntimeItem toItem)
        {
            // ammo to mag
            if (fromItem.data is AmmoData ammo && toItem.data is MagazineData mag)
            {
                return mag.ammoType == ammo;
            }
            // mag to mag
            else if (fromItem.data is MagazineData fromMag && toItem.data is MagazineData toMag)
            {
                return fromMag.ammoType == toMag.ammoType;
            }
            else
                return false;
        }

        public override void Combine(RuntimeItem ammo, RuntimeItem magazine)
        {
            int totalCount = ammo.count + magazine.count;

            int leftOver = 0;
            if (totalCount > magazine.data.MaxStack)
            {
                leftOver = totalCount - magazine.data.MaxStack;
                magazine.count = magazine.data.MaxStack;
            }
            else
                magazine.count = totalCount;

            ammo.count = leftOver;
        }
    }
}