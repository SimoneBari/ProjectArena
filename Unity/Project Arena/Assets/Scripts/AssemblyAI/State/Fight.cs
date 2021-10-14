using System.Collections.Generic;
using BehaviorDesigner.Runtime;
using UnityEngine;

namespace AssemblyAI.State
{
    public class Fight : IState
    {
        public Fight(AIEntity entity)
        {
            this.entity = entity;
        }

        public float CalculateTransitionScore()
        {
            // TODO maybe we see enemy, but we want to run away?
            var canSee = entity.CanSeeEnemy();
            return canSee ? 0.95f : 0.0f;
        }

        private AIEntity entity;
        private ExternalBehaviorTree externalBT;
        private BehaviorTree behaviorTree;
        private List<IState> outgoingStates = new List<IState>();

        public void Enter()
        {
            externalBT = Resources.Load<ExternalBehaviorTree>("Behaviors/Fight");
            behaviorTree = entity.gameObject.AddComponent<BehaviorTree>();
            behaviorTree.StartWhenEnabled = false;
            behaviorTree.RestartWhenComplete = true;
            behaviorTree.ExternalBehavior = externalBT;
            behaviorTree.EnableBehavior();
            BehaviorManager.instance.UpdateInterval = UpdateIntervalType.Manual;
                
            outgoingStates.Add(new Wander(entity));
            outgoingStates.Add(new SearchForLostEnemy(entity));
            outgoingStates.Add(new LookForPickups(entity));
        }
    
        public void Update()
        {
            var bestScore = CalculateTransitionScore();
            IState bestState = null;
            foreach (var state in outgoingStates)
            {
                var score = state.CalculateTransitionScore();
                if (score > bestScore)
                {
                    bestScore = score;
                    bestState = state;
                }
            }

            if (bestState != null)
                entity.SetNewState(bestState);
            else
                BehaviorManager.instance.Tick(behaviorTree);
        }
    
        public void Exit()
        {
            Resources.UnloadAsset(externalBT);
            Object.Destroy(behaviorTree);
        }
    }
}