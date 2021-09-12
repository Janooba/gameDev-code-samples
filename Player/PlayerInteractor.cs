using Archaic.Core.Extensions;
using Archaic.Core.Utilities;
using Archaic.Maxim.Data;

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// Handles interacting with the world, mostly the Use key, but also
// provides information on what is being looked at for other script uses

namespace Archaic.Maxim.Player
{
    public class PlayerInteractor : MonoBehaviour
    {
        public struct AimInfo
        {
            public GameObject aimedObject;
            public Vector3 aimedPosition;
            public Vector3 aimedNormal;
            public float aimedDistance;
        }

        public bool IsActive = false;

        public LayerMask interactionLayerMask;

        private PlayerCharacterInputs inputs;

        public void Initialize()
        {
            
        }

        public void HandleUpdate(ref PlayerCharacterInputs inputs)
        {
            if (!IsActive)
                return;

            this.inputs = inputs;

            HandleInput();

            aimedInfoStale = true;
        }

        private void HandleInput()
        {
            if (GameInfo.Input.GetButtonDown(RewiredConsts.Action.Interaction.Use))
                SendUseEvent();
        }

        private AimInfo aimedInfo;
        public bool aimedInfoStale;
        public AimInfo GetAimedInfo()
        {
            if (!aimedInfoStale)
                return aimedInfo;

            aimedInfo = new AimInfo();

            RaycastHit hit;

            if (Physics.Raycast(GameInfo.I.CenterScreenRay, out hit, 1000, interactionLayerMask, QueryTriggerInteraction.Collide))
            {
                aimedInfo.aimedObject = hit.collider.gameObject;
                aimedInfo.aimedPosition = hit.point;
                aimedInfo.aimedNormal = hit.normal;
                aimedInfo.aimedDistance = Vector3.Distance(GameInfo.I.CenterScreenRay.origin, hit.point);
            }
            else
            {
                aimedInfo.aimedPosition = GameInfo.I.CenterScreenRay.GetPoint(1000);
                aimedInfo.aimedNormal = Vector3.up;
                aimedInfo.aimedDistance = 0f;
            }

            return aimedInfo;
        }

        private void SendUseEvent()
        {
            AimInfo info = GetAimedInfo();
            if (info.aimedObject == null)
                return;

            if (Vector3.Distance(info.aimedPosition, GameInfo.I.LiveCamera.transform.position) > GlobalData.Instance.Player.useDistance)
                return;

            IUseable[] useables = info.aimedObject.GetComponentsInThisOrParent<IUseable>();

            foreach (var useable in useables)
            {
                if (useable.Interactable)
                    useable.StartUse(transform);
            }
        }
    }
}