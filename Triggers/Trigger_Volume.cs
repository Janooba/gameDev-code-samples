using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using Sirenix.OdinInspector;
using Archaic.Maxim.Data;

// A simple script used to trigger UnityEvents on trigger events.

namespace Archaic.Core.Utilities
{
    public class Trigger_Volume : Trigger
    {
        public bool triggerOnce;
        [ShowIf("triggerOnce")] public bool saveState;
        public UnityEvent onFirstEnter;
        public UnityEvent onEnter;
        public UnityEvent onExit;
        public UnityEvent onLastExit;

        private List<GameObject> objectsInside = new List<GameObject>();
        private bool hasBeenTriggered = false;

        protected override void Start()
        {
            base.Start();
            LoadState();
        }

        public void ForceTrigger()
        {
            onFirstEnter.Invoke();
            onEnter.Invoke();
            hasBeenTriggered = true;
            if (triggerOnce)
            {
                if (saveState) SaveState();
                Destroy(gameObject);
            }
        }

        private void OnTriggerEnter(Collider other)
        {
            if (DoesTagMatch(other))
            {
                hasBeenTriggered = true;

                if (objectsInside.Count == 0)
                    onFirstEnter.Invoke();

                onEnter.Invoke();
                if (triggerOnce)
                {
                    if (saveState) SaveState();
                    Destroy(gameObject);
                }
                objectsInside.Add(other.gameObject);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            if (objectsInside.Contains(other.gameObject))
            {
                objectsInside.Remove(other.gameObject);
                onExit.Invoke();

                if (objectsInside.Count == 0)
                    onLastExit.Invoke();
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
                Destroy(gameObject);
            }
        }
    }
}