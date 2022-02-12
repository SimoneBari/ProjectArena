using System;
using AI.AI.Layer3;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;
using Action = BehaviorDesigner.Runtime.Tasks.Action;

namespace AI.Behaviours.Actions
{
    [Serializable]
    public class GetPickupPosition : Action
    {
        [SerializeField] private SharedVector3 pickupPosition;
        private PickupPlanner pickupPlanner;

        public override void OnAwake()
        {
            pickupPlanner = GetComponent<AIEntity>().PickupPlanner;
        }

        public override TaskStatus OnUpdate()
        {
            var pickup = pickupPlanner.GetChosenPickup();
            pickupPosition.Value = pickup.transform.position;
            return TaskStatus.Success;
        }
    }
}