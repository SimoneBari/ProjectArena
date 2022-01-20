using System;
using AssemblyAI.AI.Layer2;
using AssemblyAI.Behaviours.Variables;
using BehaviorDesigner.Runtime.Tasks;
using Others;
using UnityEngine;
using UnityEngine.AI;
using Action = BehaviorDesigner.Runtime.Tasks.Action;
using Random = UnityEngine.Random;

namespace AssemblyAI.Behaviours.Actions
{
    [Serializable]
    public class PredictTargetPosition : Action
    {
        [SerializeField] private SharedSelectedPathInfo pathChosen;
        private AIEntity entity;
        private Entity enemy;
        private NavigationSystem navSystem;
        private float predictionSkill;
        private bool failedPreviousPrediction;
        public override void OnAwake()
        {
            entity = GetComponent<AIEntity>();
            navSystem = entity.NavigationSystem;
            enemy = entity.GetEnemy();
            predictionSkill = entity.GetPredictionSkill();
        }

        public override TaskStatus OnUpdate()
        {
            if (failedPreviousPrediction) return TaskStatus.Failure;

            if (Random.value > predictionSkill)
            {
                failedPreviousPrediction = true;
                return TaskStatus.Failure;
            }

            var path = navSystem.CalculatePath(enemy.transform.position);
            if (path.IsComplete())
            {
                pathChosen.Value = path;
                if (pathChosen.Value.corners.Length == 0)
                {
                    Debug.Log("NO");
                }

                return TaskStatus.Success;
            }

            // This shouldn't happen... Why is the enemy in a position that cannot be reached?
            Debug.LogWarning("Enemy position prediction failed! Enemy is unreachable");
            return TaskStatus.Failure;
        }
    }
}