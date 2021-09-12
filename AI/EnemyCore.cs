using Archaic.Maxim.Characters;
using Archaic.Maxim.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Sirenix.OdinInspector;
using Archaic.Maxim.Player;
using UnityEngine.AI;
using System.Linq;
using Archaic.Core.Extensions;
using Archaic.Core;

// Core enemy ai. Gives access to many helper functions and sets up a lot of the core data

namespace Archaic.Maxim.AI
{
    [RequireComponent(typeof(Actor))]
    public class EnemyCore : SavedBehaviour
    {
        public bool IsActive;
        public bool IsBlind;

        private Actor actor;
        public Actor Actor
        {
            get
            {
                if (!actor)
                    actor = GetComponent<Actor>();

                return actor;
            }
            private set
            {
                actor = value;
            }
        }
        [ShowInInspector]
        public Actor Target { get; protected set; }

        [InlineEditor]
        public EnemyCoreData coreData => Actor.Data as EnemyCoreData;

        protected virtual bool IsIdle { get; set; }

        public bool IsInitialized { get; protected set; }

        private float timeLastBarked = 0f;
        private float TimeSinceLastBarked => Time.time - timeLastBarked;
        private float nextBarkDelay = 1f;
        public virtual bool CanBark => IsIdle && (Actor.HasHealthController && Actor.Health.IsAlive) && TimeSinceLastBarked > nextBarkDelay;

        protected bool isPlayerLocked;
        protected NavMeshAgent agent;
        protected float randomSeed;

        [SerializeField]
        protected Vector3 spawnPosition;

        public override string InstanceID => $"{gameObject.scene.name}:{gameObject.name}:{(int)spawnPosition.x}.{(int)spawnPosition.y}.{(int)spawnPosition.z}";

        protected virtual void OnValidate()
        {
            if (!Actor) Actor = GetComponent<Actor>();

            spawnPosition = transform.position;
        }

        public virtual bool Initialize()
        {
            Actor = GetComponent<Actor>();
            agent = GetComponent<NavMeshAgent>();
            randomSeed = Random.Range(-1000, 1000);
            spawnPosition = transform.position;

            // Baked in sound triggers
            if (Actor.HasHealthController)
            {
                Actor.Health.RecievedDamage += Health_RecievedDamage;
                Actor.Health.BecameDead += (context, e) =>
                {
                    PlayAudio(coreData.sounds.die);
                    agent.isStopped = true;
                };
            }

            PlayerController.PlayerLockChanged += PlayerController_PlayerLockChanged;

            LoadState();

            return IsInitialized;
        }

        private void PlayerController_PlayerLockChanged(bool isLocked)
        {
            isPlayerLocked = isLocked;
        }

        public virtual void GoToPosition(Transform transform)
        {
            GoToPosition(transform.position);
        }

        public virtual void GoToPosition(Vector3 worldPosition)
        {
            if (!IsInitialized)
                Initialize();

            if (agent)
                agent.SetDestination(worldPosition);
        }

        protected virtual void Update()
        {
            if (!IsActive)
                return;

            if (isPlayerLocked)
                return;

            // Audio
            if (CanBark)
            {
                Bark();
            }
        }

        /// <summary>Checks if this actor can see the target actor. Will check based off the "head" value of actors</summary>
        /// <param name="target">the target Actor</param>
        /// <returns>True if the actor is within line of sight. False if the actor is too far or blocked.</returns>
        protected bool CheckLineOfSight(Actor target, float fov = 180f)
        {
            if (!target)
                return false;

            if (IsBlind)
                return false;

            if (Vector3.Angle(Actor.head.forward, target.head.position - Actor.head.position) > fov)
                return false;

            RaycastHit hit;
            Ray ray = new Ray(Actor.head.position, (target.head.position - Actor.head.position).normalized);

            if (Physics.Raycast(ray, out hit, coreData.maxDistance, GlobalData.Instance.LayermaskData.AIDetection))
            {
                Debug.DrawLine(ray.origin, hit.point, Color.red);

                // Get the actor of the hit collider (it might not be the target)
                Actor hitActor = hit.collider.GetComponentInThisOrParent<Actor>();
                return hitActor == target;
            }
            else
            {
                Debug.DrawRay(ray.origin, ray.direction, Color.red);
                return false;
            }
        }

        /// <summary>Checks if this actor can see the target GameObject. Will check based off the "head" value of actor</summary>
        /// <param name="target">the target GameObject</param>
        /// <returns>True if the GameObject is within line of sight. False if the GameObject is too far or blocked.</returns>
        protected bool CheckLineOfSight(GameObject target, float fov = 180f)
        {
            if (!target)
                return false;

            if (IsBlind)
                return false;

            if (Vector3.Angle(Actor.head.forward, target.transform.position - Actor.head.position) > fov)
                return false;

            RaycastHit hit;

            if (Physics.Linecast(Actor.head.position, target.transform.position, out hit, GlobalData.Instance.LayermaskData.AIDetection))
            {
                Debug.DrawLine(Actor.head.position, hit.point, Color.red);

                return target.transform.GetComponentsInChildren<Collider>().Contains(hit.collider);
            }
            else
            {
                Debug.DrawLine(Actor.head.position, target.transform.position, Color.red);
                return true;
            }
        }

        public void SetTarget(Actor actor)
        {
            if (!actor)
                return;

            Target = actor;
        }

        public void TargetPlayer()
        {
            if (!IsInitialized)
                Initialize();

            SetTarget(PlayerController.I.Actor);
        }

        /// <summary>Sets the AI Target to the closest Actor with the given faction that is within line of sight.</summary>
        /// <param name="faction">The faction to check for</param>
        /// <returns>Whether a target was found or not</returns>
        protected bool FindTarget(CharacterData.CharacterFaction faction = CharacterData.CharacterFaction.Player)
        {
            // Get all actors with the given faction ordered by distance (closest are preferred)
            var foundActors = Actor.AllActors
                .Where(x => x.Data.faction == faction)
                .OrderBy(x => Vector3.Distance(x.head.position, Actor.head.position))
                .ToArray();

            foreach (var actor in foundActors)
            {
                if (CheckLineOfSight(actor))
                {
                    Target = actor;
                    break;
                }
            }

            return Target != null;
        }

        private float timeLastPlayedHurtSound = 0f;
        private float TimeSinceLastPlayedHurtSound => Time.time - timeLastPlayedHurtSound;
        protected virtual void Health_RecievedDamage(object sender, HealthController.RecievedDamagedEventArgs args)
        {
            if (TimeSinceLastPlayedHurtSound > coreData.sounds.hurtCooldown)
            {
                PlayAudio(coreData.sounds.hurt);
                timeLastPlayedHurtSound = Time.time;
            }
        }

        protected void PlayAudio(SECTR_AudioCue cue)
        {
            SECTR_AudioSystem.Play(cue, Actor.chest, Vector3.zero, false);
        }

        protected SECTR_AudioCueInstance PlayLoop(SECTR_AudioCue cue)
        {
            return SECTR_AudioSystem.Play(cue, Actor.chest, Vector3.zero, true);
        }

        protected void Bark()
        {
            PlayAudio(coreData.sounds.idle);
            timeLastBarked = Time.time;
            nextBarkDelay = Random.Range(coreData.sounds.idleBarkTime.x, coreData.sounds.idleBarkTime.y);
        }

        private void OnDrawGizmos()
        {
            if (agent && agent.hasPath && Application.isPlaying)
            {
                var path = agent.path;
                Vector3 p1 = agent.nextPosition;
                Vector3 p2 = agent.nextPosition;
                for (int i = 0; i < path.corners.Length; i++)
                {
                    p2 = path.corners[i];
                    Debug.DrawLine(p1, p2, Color.green);
                    p1 = p2;
                }
            }
        }

        // For Unity Events

        public void SetActive(bool isActive)
        {
            if (!IsInitialized)
                Initialize();

            IsActive = isActive;
        }

        public void SetBlind(bool isBlind)
        {
            if (!IsInitialized)
                Initialize();

            IsBlind = isBlind;
        }


        [Button]
        public override void LoadState()
        {
            if (ProfileManager.LoadedProfile.enemyData == null)
                return;

            if (ProfileManager.LoadedProfile.enemyData.ContainsKey(InstanceID))
            {
                var data = ProfileManager.LoadedProfile.enemyData[InstanceID];

                IsActive = data.isActive;
                IsBlind = data.isBlind;

                if (Actor.HasHealthController)
                {
                    if (data.health <= 0)
                        Actor.Health.Die(Actor.Health.MaxHealth);
                    else
                        Actor.Health.CurrentHealth = data.health;
                }

                transform.position = data.positionData.position;
                transform.rotation = data.positionData.rotation;

                if (agent) agent.Warp(data.positionData.position);

                if (!string.IsNullOrEmpty(data.targetName))
                    Target = GameObject.Find(data.targetName)?.GetComponent<Actor>();
            }
        }

        [Button]
        public override void SaveState()
        {
            ProfileManager.LoadedProfile.SetEnemyData(InstanceID, this);
        }
    }
}