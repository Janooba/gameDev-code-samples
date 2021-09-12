using Archaic.Core.Utilities;
using Archaic.Core.Extensions;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Archaic.Maxim.Data;
using Sirenix.OdinInspector;
using Archaic.Maxim.Player;

// A more complex way to trigger UnityEvents.
// Only triggers if the player is within the trigger zone* AND looking at the target
// *subject to settings

namespace Archaic.Maxim.World
{
    public class Trigger_Look : Trigger
    {
        public Transform lookTarget;
        public bool saveState;
        public bool mustBeInsideTrigger;
        public float triggerAngle = 20f;

        public bool triggerWhenGlassesOff = true;
        public bool triggerWhenGlassesOn = true;

        protected override Color gizmoColour => new Color(1, 1, 1);

        public UnityEvent onTrigger;

        private List<GameObject> objectsInside = new List<GameObject>();
        private bool hasBeenTriggered = false;

        protected override void OnValidate()
        {
            base.OnValidate();

            if (!lookTarget)
            {
                lookTarget = new GameObject($"{name}:Target").transform;
                lookTarget.SetParent(transform);
                lookTarget.localPosition = Vector3.zero;
            }

            if (queryTags.Length == 0)
                queryTags = new string[] { "Player" };
        }

        private void Update()
        {
            if (!PlayerController.IsPlayerLoaded)
                return;

            if (hasBeenTriggered)
                return;

            if (mustBeInsideTrigger && objectsInside.Count == 0)
                return;

            var playerLookDir = PlayerController.I.CameraRig.Camera.transform.forward;
            var triggerDirection = lookTarget.position - PlayerController.I.CameraRig.Camera.transform.position;
            var angleBetween = Vector3.Angle(playerLookDir, triggerDirection);

            if (angleBetween < triggerAngle)
            {
                if (PlayerController.I.GlassesController.isWearingGlasses && triggerWhenGlassesOn)
                    Trigger();

                if (!PlayerController.I.GlassesController.isWearingGlasses && triggerWhenGlassesOff)
                    Trigger();
            }
        }

        public void Trigger()
        {
            hasBeenTriggered = true;
            onTrigger.Invoke();

            if (saveState) SaveState();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (DoesTagMatch(other))
            {
                objectsInside.Add(other.gameObject);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (objectsInside.Contains(other.gameObject))
            {
                objectsInside.Remove(other.gameObject);
            }
        }

        public override void SaveState()
        {
            ProfileManager.LoadedProfile.SetWorldFlag(InstanceID, hasBeenTriggered);
        }

        public override void LoadState()
        {
            if (ProfileManager.LoadedProfile.GetWorldFlag(InstanceID, false))
            {
                hasBeenTriggered = true;
            }
        }

        protected override void OnDrawGizmos()
        {
            base.OnDrawGizmos();

            if (!lookTarget)
                return;

            if (!drawGizmo)
                return;

            DebugExtension.DrawPoint(lookTarget.position, Color.magenta, 0.1f);
            Gizmos.color = Color.magenta * new Color(1, 1, 1, 0.3f);
            Gizmos.DrawLine(transform.position, lookTarget.position);
        }
    }
}