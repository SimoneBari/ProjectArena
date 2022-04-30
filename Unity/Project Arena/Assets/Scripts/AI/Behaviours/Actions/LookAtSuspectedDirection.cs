using System;
using AI.Layers.Actuators;
using AI.Layers.SensingLayer;
using BehaviorDesigner.Runtime.Tasks;
using Entity;
using UnityEngine;
using Action = BehaviorDesigner.Runtime.Tasks.Action;

namespace AI.Behaviours.Actions
{
    /// <summary>
    /// Makes the entity look at the direction where damage or sound came from.
    /// </summary>
    public class LookAtSuspectedDirection : Action
    {
        private DamageSensor damageSensor;
        private Vector3 lookPosition;
        private SightController sightController;

        public override void OnAwake()
        {
            var entity = GetComponent<AIEntity>();
            var enemy = entity.GetEnemy();
            var enemyTracker = enemy.GetComponent<PositionTracker>();
            damageSensor = entity.DamageSensor;
            sightController = entity.SightController;
            var delay = damageSensor.LastTimeDamaged;
            (lookPosition, _) = enemyTracker.GetPositionAndVelocityForRange(delay, delay);
        }

        public override TaskStatus OnUpdate()
        {
            sightController.LookAtPoint(lookPosition);
            return TaskStatus.Running;
        }
    }
}