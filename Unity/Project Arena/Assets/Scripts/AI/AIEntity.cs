using System;
using AI.GoalMachine;
using AI.Layers.Actuators;
using AI.Layers.KnowledgeBase;
using AI.Layers.Memory;
using AI.Layers.Planners;
using AI.Layers.SensingLayer;
using AI.Layers.Statistics;
using Entity.Component;
using Logging;
using Managers.Mode;
using Others;
using Pickables;
using UnityEngine;
using Utils;

namespace AI
{
    /// <summary>
    /// Class representing an entity which is played by a bot.
    /// </summary>
    public class AIEntity : Entity.Entity, ILoggable
    {
        [SerializeField] private GameObject head;

        [SerializeField] private BotCharacteristics botParams = BotCharacteristics.Default;

        [SerializeField] private BotState botState;

        [SerializeField] private Entity.Entity enemy;


        private IGoalMachine goalMachine;
        private int killerId;

        // Time of the last position log.
        private float lastPositionLog;
        private bool loggedFirstRespawn;

        // Do I have to log?
        private bool loggingGame;
        private bool mustProcessDeath;

        private BotState BotState
        {
            get => botState;
            set => botState = value;
        }

        public override int Health
        {
            get => BotState.Health;
            protected set => BotState.Health = value;
        }

        public int MaxHealth => totalHealth;

        public TargetMemory TargetMemory { get; private set; }

        public TargetKnowledgeBase TargetKnowledgeBase { get; private set; }

        public DamageSensor DamageSensor { get; private set; }

        public SoundSensor SoundSensor { get; private set; }

        public MovementController MovementController { get; private set; }

        public SightSensor SightSensor { get; private set; }

        public SightController SightController { get; private set; }

        public PickupMemory PickupMemory { get; private set; }

        public PickupActivationEstimator PickupKnowledgeBase { get; private set; }

        public PickupPlanner PickupPlanner { get; private set; }

        public NavigationSystem NavigationSystem { get; private set; }

        public GunManager GunManager { get; private set; }

        public MapMemory MapMemory { get; private set; }

        public MapWanderPlanner MapWanderPlanner { get; private set; }


        public override bool IsAlive => isActiveAndEnabled && (Health > 0 || mustProcessDeath);
        public float FightingSkill => botParams.FightingSkill;
        public float GunMovementCorrectness => botParams.GunMovementCorrectness;
        public float FightBackWhenCollectingPickup => botParams.FightBackWhenCollectingPickup;
        public float DodgeRocketProbability => botParams.DodgeRocketProbability;
        
        public float CanSelectCoverProbability => botParams.CanSelectCoverProbability;

        public float UncorrectableAimDelayAverage => botParams.UncorrectableAimDelayAverage;
        public float CorrectableAimDelay => botParams.CorrectableAimDelay;
        public float AimingDispersionAngle => botParams.AimingDispersionAngle; 
        public float AcceptableShootingAngle => botParams.AcceptableShootingAngle; 
        public Recklessness Recklessness => botParams.Recklessness;

        private LoggingComponent loggingComponent;

        private void Update()
        {
            if (loggingGame && Time.time > lastPositionLog + 0.5)
            {
                var t = transform;
                var position = t.position;
                PositionInfoGameEvent.Instance.Raise(
                    new PositionInfo {x = position.x, z = position.z, dir = t.eulerAngles.y, entityID = entityID}
                );
                lastPositionLog = Time.time;
            }

            if (inGame)
            {
                if (!enemy.IsAlive)
                {
                    TargetMemory.Reset();
                    SoundSensor.Reset();
                    DamageSensor.Reset();
                }
                else
                {
                    TargetMemory.Update();
                    TargetKnowledgeBase.Update();
                }
                
                MapMemory.Update();
                // Important: order of update is important! Keep order of layers as described in thesis.
                PickupMemory.Update();
                PickupKnowledgeBase.Update();
                PickupPlanner.Update();
                goalMachine.Update();
            }
            loggingComponent.Update();
        }

        private void LateUpdate()
        {
            if (!inGame || !mustProcessDeath) return;
            mustProcessDeath = false;
            // Kill the entity.
            Die(killerId);
        }

        public void SetupLogging()
        {
            loggingGame = true;
            loggingComponent = new LoggingComponent(this);
        }

        // Prepares all the AI components
        private void PrepareComponents(GameManager gms, bool[] ag)
        {
            MovementController = new MovementController(this, botParams.Speed);
            SightController =
                new SightController(this, head, botParams.CameraSpeed, botParams.CameraAcceleration);
            SightSensor = new SightSensor(head, botParams.MaxRange, botParams.FOV);
            MapMemory = new MapMemory(this, gms);
            MapWanderPlanner = new MapWanderPlanner(this);
            TargetMemory = new TargetMemory(
                this,
                enemy,
                botParams.MemoryWindow
            );
            TargetKnowledgeBase = new TargetKnowledgeBase(
                this,
                enemy,
                botParams.DetectionWindow,
                botParams.TimeBeforeReaction
            );
            DamageSensor = new DamageSensor(botParams.TimeBeforeReaction, botParams.EventReactionTimeout);
            SoundSensor = new SoundSensor(botParams.TimeBeforeReaction, botParams.EventReactionTimeout, GetID(), head.transform, botParams.SoundThreshold); // TODO params
            PickupMemory = new PickupMemory(this);
            PickupKnowledgeBase = new PickupActivationEstimator(this);
            NavigationSystem = new NavigationSystem(this);
            GunManager = new GunManager(this);
            PickupPlanner = new PickupPlanner(this);
            goalMachine = new EntityGoalMachine(this);
            BotState = new BotState();

            NavigationSystem.Prepare();
            GunManager.Prepare(gms, this, null, ag);
            TargetMemory.Prepare();
            TargetKnowledgeBase.Prepare();
            PickupMemory.Prepare();
            PickupKnowledgeBase.Prepare();
            MovementController.Prepare();
            PickupPlanner.Prepare();
            MapWanderPlanner.Prepare();
        }

        public override void SetupEntity(int th, bool[] ag, GameManager gms, int id)
        {
            entityID = id;
            PrepareComponents(gms, ag);
            totalHealth = th;
            Health = th;
            gameManagerScript = gms;
            GunManager.TryEquipGun(GunManager.FindLowestActiveGun());
            var position = transform.position;
            SpawnInfoGameEvent.Instance.Raise(
                new SpawnInfo {x = position.x, z = position.z, entityId = entityID, spawnEntity = gameObject.name}
            );
        }

        public override void TakeDamage(int damage, int killerID)
        {
            if (!inGame) return;
            Health -= damage;
            var position = transform.position;
            HitInfoGameEvent.Instance.Raise(
                new HitInfo
                {
                    damage = damage,
                    hitEntityID = entityID,
                    hitEntity = gameObject.name,
                    hitterEntityID = killerID,
                    hitterEntity = "Player " + killerID,
                    x = position.x,
                    z = position.z
                }
            );
            if (killerID != entityID)
                // We just got damaged and it was not self-inflicted, we might need to search the enemy.
                DamageSensor.GotDamaged();

            if (Health <= 0 && !mustProcessDeath)
            {
                mustProcessDeath = true;
                killerId = killerID;
            }
        }

        protected override void Die(int id)
        {
            // TODO Reset the various states
            var position = transform.position;
            var enemyPos = enemy.transform.position;
            KillInfoGameEvent.Instance.Raise(
                new KillInfo
                {
                    killedEntity = gameObject.name,
                    killedEntityID = entityID,
                    killerEntity = "Player" + id,
                    killerEntityID = id,
                    killedX = position.x,
                    killedZ = position.z,
                    // TODO what about suicide?
                    killerX = enemyPos.x,
                    killerZ = enemyPos.z,
                }
            );
            gameManagerScript.AddScore(id, entityID);

            // TargetMemory.Reset();
            // SoundSensor.Reset();
            DamageSensor.Reset();
            goalMachine.SetIsIdle();
            goalMachine.Reset();

            SetInGame(false);
            // Start the respawn process.
            gameManagerScript.ManageEntityDeath(gameObject, this);
        }

        public override void Respawn()
        {
            var position = transform.position;
            SpawnInfoGameEvent.Instance.Raise(
                new SpawnInfo {x = position.x, z = position.z, entityId = entityID, spawnEntity = gameObject.name}
            );
            Health = totalHealth;
            GunManager.ResetAmmo();
            // ActivateLowestGun();

            SetInGame(true);
        }


        public override void SlowEntity(float penalty)
        {
            SightController.SetInputPenalty(penalty);
            MovementController.SetInputPenalty(penalty);
        }

        public override void HealFromMedkit(MedkitPickable medkit)
        {
            if (Health + medkit.RestoredHealth > totalHealth)
                Health = totalHealth;
            else
                Health += medkit.RestoredHealth;

            if (mustProcessDeath)
            {
                Debug.LogWarning("An entity recovered health in the same turn it died!");
                if (Health > 0)
                {
                    Debug.LogWarning("Additionally, it should no longer die");
                    mustProcessDeath = false;
                }
            }

            
            PickupMemory.MarkConsumed(medkit);
        }

        public override bool CanBeSupplied(bool[] suppliedGuns)
        {
            return GunManager.CanBeSupplied(suppliedGuns);
        }

        public override void SupplyFromAmmoCrate(AmmoPickable ammoCrate)
        {
            GunManager.SupplyGuns(ammoCrate.SuppliedGuns, ammoCrate.AmmoAmounts);
            PickupMemory.MarkConsumed(ammoCrate);
        }

        public override void SetInGame(bool b, bool isGameEnded = false)
        {
            goalMachine.SetIsIdle(!b);
            NavigationSystem.SetEnabled(b);
            GetComponent<CapsuleCollider>().enabled = b;
            MeshVisibility.SetMeshVisible(transform, b);
            inGame = b;

            if (isGameEnded && loggingGame)
            {
                // Send all logging info
                loggingComponent.PublishAndRelease();
            }
        }

        /// <summary>
        /// Returns the curiosity level of the entity.
        /// </summary>
        public CuriosityLevel GetCuriosity()
        {
            return botParams.Curiosity;
        }

        /// <summary>
        /// Returns the prediction skill of the entity.
        /// </summary>
        public float GetPredictionSkill()
        {
            return botParams.Prediction;
        }

        /// <summary>
        /// Returns the enemy of this entity.
        /// </summary>
        public Entity.Entity GetEnemy()
        {
            return enemy;
        }

        /// <summary>
        /// Sets the enemy of this entity. Use this before a call to <see cref="SetupEntity"/>
        /// </summary>
        public void SetEnemy(Entity.Entity enemy)
        {
            this.enemy = enemy;
        }

        /// <summary>
        /// Sets the characteristics of the bot. Must be used before calling <see cref="SetupEntity"/>
        /// </summary>
        public void SetCharacteristics(BotCharacteristics botParams)
        {
            this.botParams = botParams;
        }

        private void OnDestroy()
        {
            SoundSensor.Release();
        }
    }

    // TODO Define BotState contents (health, ammo, current target, timeouts for stuff, ...) or remove this...
    [Serializable]
    public class BotState
    {
        [SerializeField] private int health;

        public int Health
        {
            get => health;
            set => health = value;
        }
    }
}