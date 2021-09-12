using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Provides an API for the behaviour to save and load data.
// SaveState is automatically called upon scene switch or scene save

namespace Archaic.Core
{
    public abstract class SavedBehaviour : MonoBehaviour
    {
        public virtual string InstanceID => $"{gameObject.scene.name}:{gameObject.name}:{(int)transform.position.x}.{(int)transform.position.y}.{(int)transform.position.z}";

        public abstract void LoadState();
        public abstract void SaveState();
    }
}