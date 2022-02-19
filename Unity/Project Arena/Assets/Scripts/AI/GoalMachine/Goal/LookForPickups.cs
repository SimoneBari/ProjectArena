using System;
using AI.AI.Layer3;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using Pickables;
using UnityEngine;

namespace AI.GoalMachine.Goal
{
    /// <summary>
    /// Pickup goal.
    /// Deals with selecting and reaching a pickup, plus fighting the enemy if required.
    /// The goal plan is contained in a behaviour tree.
    /// </summary>
    public class LookForPickups : IGoal
    {
        private readonly BehaviorTree behaviorTree;
        private readonly ExternalBehaviorTree externalBt;
        private readonly PickupPlanner pickupPlanner;
        private readonly float scoreMultiplier = 1.0f;
        private Pickable currentPickable;
        private Pickable nextPickable;

        public LookForPickups(AIEntity entity)
        {
            pickupPlanner = entity.PickupPlanner;
            var recklessness = entity.Recklessness;
            switch (recklessness)
            {
                case Recklessness.Low:
                    scoreMultiplier *= 1.3f;
                    break;
                case Recklessness.Neutral:
                    break;
                case Recklessness.High:
                    scoreMultiplier /= 1.3f;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            
            externalBt = Resources.Load<ExternalBehaviorTree>("Behaviors/NewPickup");
            behaviorTree = entity.gameObject.AddComponent<BehaviorTree>();
            behaviorTree.StartWhenEnabled = false;
            behaviorTree.ResetValuesOnRestart = true;
            behaviorTree.ExternalBehavior = externalBt;
        }

        public float GetScore()
        {
            nextPickable = pickupPlanner.GetChosenPickup();
            return scoreMultiplier * pickupPlanner.GetChosenPickupScore();
        }


        public void Enter()
        {
            // Do not enable behavior, it will be done in Update!
            currentPickable = null;
        }

        public void Update()
        {
            if (currentPickable != nextPickable)
            {
                behaviorTree.DisableBehavior();
                behaviorTree.EnableBehavior();
                BehaviorManager.instance.RestartBehavior(behaviorTree);

                // var pickupInfo = new SelectedPickupInfo
                // {
                //     pickup = nextPickable,
                //     estimatedActivationTime = nextPickableActivationTime
                // };
                // behaviorTree.SetVariableValue("ChosenPickup", pickupInfo);
                // behaviorTree.SetVariableValue("ChosenPickupPosition", nextPickable.transform.position);
                // behaviorTree.SetVariableValue("ChosenPath", newPath);

                currentPickable = nextPickable;
            }

            BehaviorManager.instance.Tick(behaviorTree);

            if (behaviorTree.ExecutionStatus != TaskStatus.Running)
            {
                // The tree finished execution. We must have picked up the pickable or maybe our activation time
                // estimate was wrong. Force update of the planner?
                // Must first force update of the knowledge base? The knowledge base automatically knows the status
                // of the pickup if we are close (even when not looking).
                pickupPlanner.ForceUpdate();
                behaviorTree.DisableBehavior();
                behaviorTree.EnableBehavior();
                BehaviorManager.instance.RestartBehavior(behaviorTree);
            }
        }

        public void Exit()
        {
            behaviorTree.DisableBehavior();
        }
    }
}