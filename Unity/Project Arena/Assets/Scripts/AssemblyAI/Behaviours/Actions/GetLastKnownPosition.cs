using System;
using AI.KnowledgeBase;
using AssemblyAI.AI.Layer2;
using AssemblyAI.Behaviours.Variables;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using Others;
using UnityEngine;
using Action = BehaviorDesigner.Runtime.Tasks.Action;
using Random = UnityEngine.Random;

namespace AssemblyAI.Behaviours.Actions
{
    [Serializable]
    public class GetLastKnownPosition : Action
    {
        [SerializeField] private SharedSelectedPathInfo lastKnownPositionPath;
        [SerializeField] private SharedBool searchDueToDamage;
        private AIEntity entity;
        private Entity enemy;
        private PositionTracker enemyTracker;
        private TargetKnowledgeBase knowledgeBase;
        private NavigationSystem navSystem;

        public override void OnAwake()
        {
            entity = GetComponent<AIEntity>();
            enemy = entity.GetEnemy();
            knowledgeBase = entity.TargetKb;
            navSystem = entity.NavigationSystem;

            enemyTracker = enemy.GetComponent<PositionTracker>();
        }

        public override TaskStatus OnUpdate()
        {
            Debug.Log("Entity " + gameObject.name + " will look because of damage? " + searchDueToDamage.Value);
            return !searchDueToDamage.Value ? EstimateEnemyPositionFromKnowledge() : EstimateEnemyPositionFromDamage();
        }

        private TaskStatus EstimateEnemyPositionFromKnowledge()
        {
            var delay = Time.time - knowledgeBase.GetLastSightedTime();
            var (position, velocity) = enemyTracker.GetPositionAndVelocityFromDelay(delay);

            // Try to estimate the position of the enemy after it has gone out of sight
            var estimatedPosition = position + velocity * 0.1f;

            var pathToEstimatedPos = navSystem.CalculatePath(estimatedPosition);
            if (pathToEstimatedPos.IsComplete())
            {
                lastKnownPositionPath.Value = pathToEstimatedPos;
                return TaskStatus.Success;
                
            }

            // Point wasn't valid, perhaps estimated position was OOB, use position
            var pathToPosition = navSystem.CalculatePath(position);
            lastKnownPositionPath.Value = pathToPosition;
            return TaskStatus.Success;
        }

        private TaskStatus EstimateEnemyPositionFromDamage()
        {
            // Estimate enemy position: get enemy position (assuming it's also the position from which 
            // we got damaged. Draw a line between my pos in the direction of the enemy and pick any point
            // in such line (maybe in the second half of the line, otherwise we seek too close). Draw a circle
            // around that point and pick one point inside of it. That's the enemy estimated position.
            var (enemyPos, _) = enemyTracker.GetPositionAndVelocityFromDelay(0);

            var myPosition = transform.position;
            var direction = enemyPos - myPosition;
            Debug.DrawRay(myPosition, direction);
            if (Physics.Raycast(myPosition, direction, out var hit, direction.magnitude * 2f))
            {
                var chosenDistance = (0.5f + Random.value * 0.5f) * hit.distance;
                var pointOnLine = myPosition + chosenDistance * direction.normalized;

                // size of radius is 1/3 of the distance, so we avoid looking behind us
                var radiusSize = hit.distance * Random.value * 0.3f;
                var circle = Random.insideUnitCircle * radiusSize;
                var chosenPos = pointOnLine;
                chosenPos.x += circle.x;
                chosenPos.z += circle.y;

                var path = navSystem.CalculatePath(chosenPos);
                if (path.IsComplete())
                {
                    lastKnownPositionPath.Value = path;
                    return TaskStatus.Success;
                }
                // The position chosen is not valid... choose the point we have hit?
                var path2 = navSystem.CalculatePath(hit.point);
                lastKnownPositionPath.Value = path2;
                return TaskStatus.Success;
            }
            // How come we haven't spotted anything in the enemy direction? Not even the enemy? Impossible!
            throw new ApplicationException("We couldn't spot the enemy even when raycasting towards it!");
        }
    }
}