using Archaic.Core.Utilities;
using Archaic.Core.Extensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Archaic.Maxim.Data;
using UnityEngine.UIElements;

// Uses the trigger class to create a useable button for the player
// Invoked by the players "PlayerInteractor" script

namespace Archaic.Maxim.World
{
    public class Trigger_Button : Trigger, IUseable
    {
        public string Tooltip => string.IsNullOrEmpty(tooltip) ? "Use" : tooltip;
        public bool canUse = true;
        [Tooltip("Defaults to \"Use\" if blank")]
        public string tooltip = "";
        protected override Color gizmoColour => new Color(0, 0, 1);

        public UnityEvent onStart;
        public UnityEvent onStop;
        public bool Interactable { get => canUse; set => canUse = value; }

        protected override void Start()
        {
            base.Start();
            LoadState();
        }

        public bool StartUse(Transform user)
        {
            if (!canUse)
                return false;

            onStart.Invoke();

            return true;
        }

        public void StopUse()
        {
            onStop.Invoke();
        }

        public void Unlock()
        {
            canUse = true;
            SaveState();
        }

        public void Lock()
        {
            canUse = false;
            SaveState();
        }

        public override void SaveState()
        {
            ProfileManager.LoadedProfile.SetWorldFlag($"{InstanceID}:CanUse", canUse);
        }

        public override void LoadState()
        {
            canUse = ProfileManager.LoadedProfile.GetWorldFlag($"{InstanceID}:CanUse", canUse);
        }
    }
}