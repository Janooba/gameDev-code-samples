using Archaic.Core.Extensions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using Sirenix.OdinInspector;

// Base trigger class, not super useful on its own
// Allows the use of Box, Mesh, and/or Sphere colliders with gizmos

namespace Archaic.Core.Utilities
{
    [RequireComponent(typeof(Rigidbody))]
    public class Trigger : SavedBehaviour
    {
        public string[] queryTags = { "Player" };
        public bool drawGizmo = true;

        protected Rigidbody rbody;
        protected BoxCollider boxCollider;
        protected MeshCollider meshCollider;
        protected SphereCollider sphereCollider;

        protected virtual Color gizmoColour => new Color(1, 1, 1, 0.5f);

        protected virtual void OnValidate()
        {
            Validate();
        }

        protected virtual void Start()
        {
            // Tags
            if (queryTags != null)
                queryTags = queryTags
                .Where(x => x != null)              // Removing any null entries
                .Where(x => x.Trim().Length > 0)    // Trimming empty space before and after tags
                .ToArray();

            Validate();
        }

        private void Validate()
        {
            if (!rbody)
                rbody = GetComponent<Rigidbody>();

            rbody.isKinematic = true;

            // Mesh
            if (!boxCollider)
                boxCollider = GetComponent<BoxCollider>();

            if (boxCollider)
                boxCollider.isTrigger = true;

            // Sphere
            if (!sphereCollider)
                sphereCollider = GetComponent<SphereCollider>();

            if (sphereCollider)
                sphereCollider.isTrigger = true;

            // Mesh
            if (!meshCollider)
                meshCollider = GetComponent<MeshCollider>();

            if (meshCollider)
            {
                meshCollider.convex = true;
                meshCollider.isTrigger = true;
            }
        }

        protected virtual void OnDrawGizmos()
        {
            if (!drawGizmo)
                return;

            Matrix4x4 originalMatrix = Gizmos.matrix;

            Gizmos.matrix = transform.localToWorldMatrix;

            Gizmos.color = gizmoColour;

            if (boxCollider)
            {
                Gizmos.DrawWireCube(boxCollider.center, boxCollider.size);

                Gizmos.color *= new Color(1, 1, 1, 0.5f);
                Gizmos.DrawCube(boxCollider.center, boxCollider.size);
            }

            if (sphereCollider)
            {
                Gizmos.DrawWireSphere(sphereCollider.center, sphereCollider.radius);

                Gizmos.color *= new Color(1, 1, 1, 0.5f);
                Gizmos.DrawSphere(sphereCollider.center, sphereCollider.radius);
            }

            if (meshCollider)
            {
                Gizmos.DrawWireMesh(meshCollider.sharedMesh);

                Gizmos.color *= new Color(1, 1, 1, 0.5f);
                Gizmos.DrawMesh(meshCollider.sharedMesh);
            }

            Gizmos.matrix = originalMatrix;
        }

        protected bool DoesTagMatch(Collider other)
        {
            bool matched = false;

            if (queryTags.Length == 0)
                matched = true;
            else
                for (int i = 0; i < queryTags.Length; i++)
                {
                    if (other.tag == queryTags[i].Trim())
                        matched = true;
                }
            return matched;
        }

        public override void LoadState() { }

        public override void SaveState() { }
    }
}