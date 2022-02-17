
using Archaic.Core.Extensions;
using Archaic.Maxim;
using Archaic.Maxim.Characters;
using Archaic.Maxim.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Sirenix.OdinInspector;

// Basic Melee ai without using a state machine
// Moves towards target, and attacks when within range
// Animation drives the actual damage bit

namespace Archaic.Maxim.AI
{
    [RequireComponent(typeof(Actor))]
    public class Enemy_Melee : EnemyCore
    {
        public MeleeEnemyData Data => coreData as MeleeEnemyData;

        public EnemyEvents events;
        public bool useAttackAnimation = false;
        public SphereCollider hurtZone;
        public Collider playerCollider;
        public Collider[] hitZones;

        private Vector3 currentTargetPos;
        private Vector2 smoothDeltaPosition = Vector2.zero;
        private Vector2 velocity;

        private float timeLastDamaged = 0f;
        private float TimeSinceLastDamaged => Time.time - timeLastDamaged;
        private bool IsStunned => TimeSinceLastDamaged < Data.stunTime;

        private Collider[] hurtTargets = new Collider[4];


        private float timeLastAttacked = 0f;
        private float TimeSinceLastAttacked => Time.time - timeLastAttacked;
        private bool IsWithinAttackRange => Vector3.Distance(Target.transform.position, transform.position) < Data.attackDistance;
        private bool CanAttack => (events && !events.IsBusy) && TimeSinceLastAttacked > Data.attackSpeed && IsWithinAttackRange;

        protected override bool IsIdle => !Target;

        private SECTR_AudioCueInstance runLoop;

        private void Start()
        {
            Initialize();
        }

        private void OnDisable()
        {
            runLoop.Stop(true);
        }

        public override bool Initialize()
        {
            if (Actor.HasHealthController)
            {
                Actor.Health.RecievedDamage += Health_RecievedDamage;
                Actor.Health.BecameDead += Health_BecameDead;
            }

            if (events)
            {
                events.Event_Footstep += PlayFootstepSound;
                events.Event_AttackHit += DealDamage;
            }

            base.Initialize();

            IsInitialized = true;
            return IsInitialized;
        }

        private void Health_BecameDead(object sender, HealthController.RecievedDamagedEventArgs e)
        {
            Actor.animator.SetTrigger("TookDamage");
            Actor.animator.SetBool("IsDead", true);
            playerCollider.enabled = false;
            foreach (var collider in hitZones)
            {
                collider.enabled = false;
            }
            runLoop.Stop(false);
        }

        protected override void Health_RecievedDamage(object sender, HealthController.RecievedDamagedEventArgs args)
        {
            base.Health_RecievedDamage(sender, args);
            Actor.animator.SetTrigger("TookDamage");

            Actor.animator.SetInteger("FlinchIndex", Random.Range(0, Data.flinchAnimCount));
            agent.isStopped = true;
            timeLastDamaged = Time.time;

            if (!IsBlind && !Target && args.FromActor)
                Target = args.FromActor;

            runLoop.Stop(false);
        }

        protected override void Update()
        {
            base.Update();

            if (!IsInitialized)
                return;

            if (Actor.HasHealthController && !Actor.Health.IsAlive)
                return;

            if (!IsActive)
                return;

            if (agent)
                agent.isStopped = isPlayerLocked;

            if (isPlayerLocked)
                return;

            if (!IsStunned && !events.IsBusy)
            {
                if (!Target)
                {
                    FindTarget();
                }
                else
                {
                    UpdateTargetPosition();
                    HandleAttacks();
                }
            }

            UpdateMovement();
        }

        // Sets agent destination if the target moves too far from known destination
        // for optimization so path isn't calculated every tick
        private void UpdateTargetPosition()
        {
            if (currentTargetPos != Target.transform.position || agent.isStopped)
            {
                agent.SetDestination(Target.transform.position);
                agent.isStopped = false;
                currentTargetPos = Target.transform.position;

                if (Vector3.Distance(agent.destination, transform.position) > Data.minDistance)
                {
                    if (!runLoop.Active)
                        runLoop = SECTR_AudioSystem.Play(Data.sounds.runloop, Actor.chest, Vector3.zero, true);
                }
                else
                    runLoop.Stop(false);
            }
        }

        // Handles animator movement values as well as rotation
        private void UpdateMovement()
        {
            velocity = new Vector2(
                agent.transform.InverseTransformDirection(agent.velocity).x,
                agent.transform.InverseTransformDirection(agent.velocity).z);

            if (Target && velocity.sqrMagnitude <= 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(Vector3.ProjectOnPlane(Target.transform.position - transform.position, Vector3.up), Vector3.up);
            }

            if (Actor.animator)
            {
                // Update animation parameters
                //animator.SetBool("move", shouldMove);
                Actor.animator.SetFloat("Strafe", velocity.x);
                Actor.animator.SetFloat("Forward", velocity.y);
            }
        }
        
        private void HandleAttacks()
        {
            if (CanAttack)
            {
                Attack();
            }
        }

        [Button]
        private void Attack()
        {
            if (events && useAttackAnimation)
                events.SetBusyTrue();

            // Starts animation which triggers damage, or deals it instantly
            if (useAttackAnimation)
                Actor.animator.SetTrigger("Attack");
            else
                DealDamage();

            PlayAudio(Data.sounds.attack);
            runLoop.Stop(false);

            timeLastAttacked = Time.time;
        }

        private void OnAnimatorMove()
        {
            if (Actor.animator.applyRootMotion)
                agent.nextPosition = transform.position;
            else
                transform.position = agent.nextPosition;
        }

        private void PlayFootstepSound()
        {
            if (Data.sounds.footstep)
            {
                PlayAudio(Data.sounds.footstep);
                return;
            }

            Ray ray = new Ray(transform.position + (Vector3.up * 0.1f), Vector3.down);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 1f, ~LayerMask.NameToLayer("Default")))
            {
                var physData = PhysDataManager.GetData(hit.GetMaterial(), hit.collider.material, true);

                // for liquid splash sounds
                if (Actor.GetLiquidDepth() == Trigger_WetZone.Depth.Shallow)
                {
                    var puddleData = PhysDataManager.GetData("Puddle");
                    SECTR_AudioSystem.Play(puddleData.sfxLandCue, transform.position, false);

                    var sound = SECTR_AudioSystem.Play(physData.sfxFootStepCue, transform, Vector3.zero, false);
                    sound.Volume = sound.Volume * 0.5f;
                }
                else
                    SECTR_AudioSystem.Play(physData.sfxFootStepCue, transform, Vector3.zero, false);
            }
        }

        private void DealDamage()
        {
            hurtTargets = Physics.OverlapSphere(hurtZone.transform.position, hurtZone.radius, ~LayerMask.NameToLayer("Player"));
            for (int i = 0; i < hurtTargets.Length; i++)
            {
                if (hurtTargets[i] == null)
                    continue;

                var col = hurtTargets[i];

                Actor hitActor = col.GetComponentInParent<Actor>();
                if (hitActor)
                {
                    if (hitActor == Actor)
                        continue;

                    if (hitActor.Data.faction == CharacterData.CharacterFaction.Enemy)
                        continue;

                    if (hitActor.HasHealthController)
                    {
                        var hitPosition = hurtTargets[i].ClosestPointOnBounds(hurtZone.transform.position);

                        hitActor.Health.TakeDamage(new DamagePacket()
                        {
                            FlatDmg = Data.attackDamage,
                            From = Actor,
                            Type = DamageType.Blunt,
                            HitCollider = hurtTargets[i],
                            HitPosition = hitPosition,
                            HitDirection = (hurtZone.transform.position - hitPosition).normalized
                        });

                        PlayAudio(Data.sounds.attackContact);
                    }
                }
            }
        }
    }
}
