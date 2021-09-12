using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// An interface for allowing the behaviour to be "used" by the player

namespace Archaic.Core.Utilities
{
    public interface IUseable
    {
        /// <summary>
        /// Can be currently used/interacted with
        /// </summary>
        bool Interactable { get; set; }

        /// <summary>
        /// Begin using this interactable
        /// </summary>
        /// <returns>False if the item cannot be used right now</returns>
        bool StartUse(Transform user);

        /// <summary>
        /// Stop using this interactable
        /// </summary>
        void StopUse();

        /// <summary>
        /// Tooltip to display on screen when able to be used
        /// </summary>
        string Tooltip { get; }
    }
}