using System.Collections.Generic;
using AI.KnowledgeBase;
using BehaviorDesigner.Runtime;
using UnityEngine;

namespace AssemblyAI.State
{
    public class SearchForLostEnemy : IState
    {
        public SearchForLostEnemy(AIEntity entity, bool searchDueToDamage = false)
        {
            this.entity = entity;
            targetKB = entity.GetComponent<TargetKnowledgeBase>();
            this.searchDueToDamage = searchDueToDamage;
        }
    
        private AIEntity entity;
        private TargetKnowledgeBase targetKB;
        private bool searchDueToDamage;
        private ExternalBehaviorTree externalBT;
        private BehaviorTree behaviorTree;
        private List<IState> outgoingStates = new List<IState>();
        private float startSearchTime = float.NaN;

        public float CalculateTransitionScore()
        {
            if (entity.GetEnemy().isAlive && !targetKB.HasSeenTarget())
            {
                if (float.IsNaN(startSearchTime))
                    return 0.7f;
                // Slowly decrease want to search. After 5 secs, it's zero
                return 1f - (Time.time - startSearchTime) / 5f;
            }
            return 0f;
        }
        public void Enter()
        {
            externalBT = Resources.Load<ExternalBehaviorTree>("Behaviors/SearchForLostEnemy");
            behaviorTree = entity.gameObject.AddComponent<BehaviorTree>();
            behaviorTree.StartWhenEnabled = false;
            behaviorTree.RestartWhenComplete = true;
            behaviorTree.ExternalBehavior = externalBT;
            behaviorTree.EnableBehavior();
            BehaviorManager.instance.UpdateInterval = UpdateIntervalType.Manual;
            behaviorTree.SetVariableValue("SearchDueToDamage", searchDueToDamage);
            startSearchTime = Time.time;
            outgoingStates.Add(new Wander(entity));
            outgoingStates.Add(new LookForPickups(entity));
            outgoingStates.Add(new ResumeFight(entity));
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