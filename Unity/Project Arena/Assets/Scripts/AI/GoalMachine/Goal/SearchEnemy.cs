using AI.AI.Layer1;
using AI.AI.Layer2;
using AI.AI.Layer3;
using BehaviorDesigner.Runtime;
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
        private readonly BehaviorTree behaviorTree;
        private readonly DamageSensor damageSensor;
        private readonly AIEntity entity;
        private readonly ExternalBehaviorTree externalBt;
        private readonly TargetPlanner targetPlanner;
        private float startSearchTime = NO_TIME;

        public SearchEnemy(AIEntity entity)
        {
            this.entity = entity;
            targetPlanner = entity.TargetPlanner;
            damageSensor = entity.DamageSensor;
            externalBt = Resources.Load<ExternalBehaviorTree>("Behaviors/SearchEnemy");
            behaviorTree = entity.gameObject.AddComponent<BehaviorTree>();
            behaviorTree.StartWhenEnabled = false;
            behaviorTree.RestartWhenComplete = true;
            behaviorTree.ExternalBehavior = externalBt;
        }

        public float GetScore()
        {
            var searchDueToLoss = targetPlanner.HasLostTarget();
            var searchDueToDamage = !targetPlanner.HasSeenTarget() && damageSensor.WasDamagedRecently;
            if (entity.GetEnemy().IsAlive && (searchDueToLoss || searchDueToDamage))
            {
                // ReSharper disable once CompareOfFloatsByEqualityOperator
                if (startSearchTime == NO_TIME)
                    return 0.7f;
                // Slowly decrease want to search. After 5 secs, it's zero
                return 1f - (Time.time - startSearchTime) / 5f;
            }

            return 0f;
        }

        public void Enter()
        {
            behaviorTree.EnableBehavior();
            BehaviorManager.instance.RestartBehavior(behaviorTree);
            // TODO searchStartTime should be replaced by last time enemy detected / last time took damage
            startSearchTime = Time.time;
        }

        public void Update()
        {
            entity.IsFocusingOnEnemy = true;
            BehaviorManager.instance.Tick(behaviorTree);
        }

        public void Exit()
        {
            startSearchTime = NO_TIME;
            entity.IsFocusingOnEnemy = false;
            behaviorTree.DisableBehavior();
        }
    }
}