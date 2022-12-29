using System;
using AI.Behaviours.Variables;
using AI.Layers.KnowledgeBase;
using AI.Layers.SensingLayer;
using BehaviorDesigner.Runtime.Tasks;
using Entity;
using Others;
using UnityEngine;
using Action = BehaviorDesigner.Runtime.Tasks.Action;
using Random = UnityEngine.Random;

namespace AI.Behaviours.Actions
{
    /// <summary>
    /// Estimates the position of the enemy, either because we are searching for it or because we recently took damage.
    /// </summary>
    [Serializable]
    public class GetLastKnownPosition : Action
    {
        [SerializeField] private SharedSelectedPathInfo lastKnownPositionPath;
        private DamageSensor damageSensor;
        private SoundSensor soundSensor;
        private Entity.Entity enemy;
        private PositionTracker enemyTracker;
        private AIEntity entity;
        private TargetKnowledgeBase _targetKnowledgeBase;
        private NavigationSystem navSystem;
        private int layerMask;

        public override void OnAwake()
        {
            entity = GetComponent<AIEntity>();
            enemy = entity.GetEnemy();
            _targetKnowledgeBase = entity.TargetKnowledgeBase;
            navSystem = entity.NavigationSystem;
            damageSensor = entity.DamageSensor;
            soundSensor = entity.SoundSensor;
            enemyTracker = enemy.GetComponent<PositionTracker>();
            layerMask = LayerMask.GetMask("Default", "Entity", "Wall");
        }

        public override TaskStatus OnUpdate()
        {
            var damageTime = damageSensor.WasDamagedRecently
                ? damageSensor.LastTimeDamaged
                : float.MinValue;

            var noiseTime = soundSensor.HeardShotRecently
                ? soundSensor.LastTimeHeardShot
                : float.MinValue;

            var triggeringEventTime = Mathf.Max(damageTime, noiseTime);
            
            var lossTime = _targetKnowledgeBase.LastTimeDetected;
            
            if (triggeringEventTime > lossTime)
                // The most recent event was getting damaged or hearing noise, so use this knowledge to guess position
                return EstimateEnemyPositionFromTriggeringEvent(triggeringEventTime);
            // The most recent event was losing the enemy, so use this knowledge to guess position
            return EstimateEnemyPositionFromKnowledge();
        }

        private TaskStatus EstimateEnemyPositionFromKnowledge()
        {
            var delay = _targetKnowledgeBase.LastTimeDetected;
            var (delayedPosition, velocity) = enemyTracker.GetPositionAndVelocityForRange(delay - 0.5f, delay);

            // Try to estimate the position of the enemy after it has gone out of sight
            var estimatedPosition = delayedPosition + velocity * 0.1f;

            var pathToEstimatedPos = navSystem.CalculatePath(estimatedPosition);
            if (pathToEstimatedPos.IsComplete())
            {
                lastKnownPositionPath.Value = pathToEstimatedPos;
                return TaskStatus.Success;
            }

            // Point wasn't valid, perhaps estimated position was OOB, use position
            var pathToDelayedPosition = navSystem.CalculatePath(delayedPosition);
            if (pathToDelayedPosition.IsComplete())
            {
                lastKnownPositionPath.Value = pathToDelayedPosition;
                return TaskStatus.Success;
            }

            throw new ArgumentException("Impossible to reach the enemy, estimated position in not valid!");
        }

        private TaskStatus EstimateEnemyPositionFromTriggeringEvent(float time)
        {
            // Estimate enemy position: get enemy position (assuming it's also the position from which 
            // we got damaged. Draw a line between my pos in the direction of the enemy and pick any point
            // in such line (maybe in the second half of the line, otherwise we seek too close). Draw a circle
            // around that point and pick one point inside of it. That's the enemy estimated position.

            var (enemyPos, _) = enemyTracker.GetPositionAndVelocityForRange(time - 0.5f, time);

            var myPosition = transform.position;
            var direction = enemyPos - myPosition;
            var distance = direction.magnitude * 2f;
            if (Physics.Raycast(myPosition, direction, out var hit, distance, layerMask))
            {
                distance = hit.distance;
            }

            for (var i = 0; i < 5; i++)
            {
                var chosenDistance = (0.5f + Random.value * 0.5f) * distance;
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
            }

            // // The position chosen is not valid... choose the point we have hit?
            // var path2 = navSystem.CalculatePath(hit.point);
            // if (path2.IsComplete())
            // {
            //     lastKnownPositionPath.Value = path2;
            //     return TaskStatus.Success;
            // }

            // todo i'd like to understand why is the point unreachable here...
            // ... choose enemy position...
            var path3 = navSystem.CalculatePath(enemyPos);
            if (path3.IsComplete())
            {
                lastKnownPositionPath.Value = path3;
                return TaskStatus.Success;
            }

            // Give up on life
            throw new ArgumentException("Cannot get valid path to enemy");
        }
    }
}