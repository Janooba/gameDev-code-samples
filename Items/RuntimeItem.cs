using Archaic.Maxim.Data;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// The runtime item that exists in the players inventory

namespace Archaic.Maxim.Inventory
{
    [Serializable]
    public class RuntimeItem
    {
        public ItemData data;
        public int count;

        public RuntimeItem(ItemData data, int count = 1)
        {
            this.data = data;
            this.count = count;
        }

        public Profile.ItemData GetSavedData()
        {
            if (!data)
                return default(Profile.ItemData);

            return new Profile.ItemData
            {
                id = data.Id,
                count = this.count
            };
        }
    }
}