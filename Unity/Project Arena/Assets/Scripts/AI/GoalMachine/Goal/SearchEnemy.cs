using System;
using AI.Layers.KnowledgeBase;
using AI.Layers.SensingLayer;
using BehaviorDesigner.Runtime;
using Logging;
using UnityEngine;

namespace AI.GoalMachine.Goal
{
    /// <summary>
    /// Search enemy goal.
    /// Deals with searching/predicting the position of the enemy and reaching it.
    /// The goal plan is contained in a behaviour tree.
    /// </summary>
    public class SearchEnemy : IGoal
    {
        private const float NO_TIME = -1;
        private readonly TargetKnowledgeBase _targetKnowledgeBase;
        private readonly BehaviorTree behaviorTree;
        private readonly DamageSensor damageSensor;
        private readonly AIEntity entity;
        private readonly ExternalBehaviorTree externalBt;
        private readonly SoundSensor soundSensor;
        private Recklessness _recklessness;
        private bool resetInUpdate;
        private float startSearchTime = NO_TIME;

        public SearchEnemy(AIEntity entity)
        {
            this.entity = entity;
            _targetKnowledgeBase = entity.TargetKnowledgeBase;
            _recklessness = entity.Recklessness;
            damageSensor = entity.DamageSensor;
            soundSensor = entity.SoundSensor;
            externalBt = Resources.Load<ExternalBehaviorTree>("Behaviors/SearchEnemy");
            behaviorTree = entity.gameObject.AddComponent<BehaviorTree>();
            behaviorTree.StartWhenEnabled = false;
            behaviorTree.RestartWhenComplete = true;
            behaviorTree.ExternalBehavior = externalBt;
        }

        public float GetScore()
        {
            if (!entity.GetEnemy().IsAlive)
            {
                // Nothing to search
                return 0f;
            }

            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (startSearchTime != NO_TIME)
            {
                // We are already searching the enemy, slowly decrease will to search based on how much time has elapsed
                var lastEventTime = Mathf.Max(
                    damageSensor.WasDamagedRecently ? damageSensor.LastTimeDamaged : float.MinValue,
                    soundSensor.HeardShotRecently ? soundSensor.LastTimeHeardShot : float.MinValue
                    );
                if (lastEventTime > startSearchTime)
                {
                    // We get damaged or heard noise again, reset score.
                    resetInUpdate = true;
                    return 0.7f;
                }
                resetInUpdate = false;
                return 1f - (Time.time - startSearchTime) / 10f;
            }

            resetInUpdate = false;

            // TODO set triggering event here
            var searchDueToLoss = _targetKnowledgeBase.HasLostTarget();
            if (searchDueToLoss)
            {
                return _recklessness switch
                {
                    Recklessness.Low => 0.3f,
                    Recklessness.Neutral => 0.6f,
                    Recklessness.High => 0.9f,
                    _ => throw new ArgumentOutOfRangeException()
                };
            }
            
            var searchDueToSuspectedEnemy = !_targetKnowledgeBase.HasSeenTarget() && 
                                            (damageSensor.WasDamagedRecently || soundSensor.HeardShotRecently);
            return searchDueToSuspectedEnemy ? 0.7f : 0.0f;
        }

        public void Enter()
        {
            FocusingOnEnemyGameEvent.Instance.Raise(new FocusOnEnemyInfo {entityID = entity.GetID(), isFocusing = true});
            behaviorTree.EnableBehavior();
            BehaviorManager.instance.RestartBehavior(behaviorTree);
            // TODO searchStartTime should be replaced by last time enemy detected / last time took damage
            startSearchTime = Time.time;
        }

        public void Update()
        {
            if (resetInUpdate)
            {
                Exit();
                Enter();
            }
            BehaviorManager.instance.Tick(behaviorTree);
        }

        public void Exit()
        {
            startSearchTime = NO_TIME;
            FocusingOnEnemyGameEvent.Instance.Raise(new FocusOnEnemyInfo {entityID = entity.GetID(), isFocusing = false});
            behaviorTree.DisableBehavior();
        }
    }
}