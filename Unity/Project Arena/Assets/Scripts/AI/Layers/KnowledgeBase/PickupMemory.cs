using System.Collections.Generic;
using AI.Layers.SensingLayer;
using Pickables;
using UnityEngine;
using Utils;

namespace AI.Layers.KnowledgeBase
{
    /// <summary>
    /// This component deals with keeping track of the activation times of all the pickups in the map.
    /// </summary>
    public class PickupMemory
    {
        public class PickupInfo
        {
            public float lastTimeSeenActive;
            public float lastTimeSeen;
            public float lastTimeCollected;

            public PickupInfo(float lastTimeSeenActive, float lastTimeSeen, float lastTimeCollected)
            {
                this.lastTimeSeenActive = lastTimeSeenActive;
                this.lastTimeSeen = lastTimeSeen;
                this.lastTimeCollected = lastTimeCollected;
            }
        }

        private readonly Dictionary<Pickable, PickupInfo> pickupInfos = new(new MonoBehaviourEqualityComparer<Pickable>());

        private readonly AIEntity me;
        private SightSensor sightSensor;
        private readonly int layerMask;

        public PickupMemory(AIEntity me)
        {
            this.me = me;
            DetectPickups();
            layerMask = LayerMask.GetMask("Default", "Wall", "Pickable", "Floor");
        }

        /// <summary>
        /// Finish setting up the entity.
        /// </summary>
        public void Prepare()
        {
            sightSensor = me.SightSensor;
        }

        // Detects all the pickups in the scene.
        private void DetectPickups()
        {
            var spawners = Object.FindObjectsOfType<Pickable>();
            foreach (var spawner in spawners)
            {
                pickupInfos.Add(spawner, new PickupInfo(Time.time, Time.time, -1));
            }
        }

        /// <summary>
        /// Updates the knowledge base.
        /// </summary>
        public void Update()
        {
            var keyList = new List<Pickable>(pickupInfos.Keys);
            foreach (var pickable in keyList)
            {
                var position = pickable.gameObject.transform.position;
                // Let us give the bot a slightly unfair advantage: if they are close to the powerup, they
                // know its status. This is because it would be very easy for a human to turn around and check, 
                // but complex (to implement) in the AI of the bot.
                if ((position - me.transform.position).sqrMagnitude > 4 &&
                    !sightSensor.CanSeeObject(pickable.transform, layerMask))
                {
                    continue;
                }

                if (pickable.IsActive)
                {
                    pickupInfos[pickable].lastTimeSeenActive = Time.time;
                }

                pickupInfos[pickable].lastTimeSeen = Time.time;
            }
        }

        /// <summary>
        /// Get the list of pickups saved in the knowledge base.
        /// </summary>
        public Dictionary<Pickable, PickupInfo>.KeyCollection GetPickups()
        {
            return pickupInfos.Keys;
        }


        public Dictionary<Pickable, PickupInfo> GetPickupsInfo()
        {
            return pickupInfos;
        }

        /// <summary>
        /// Marks a specific pickup as consumed. 
        /// </summary>
        public void MarkConsumed(Pickable pickable)
        {
            pickupInfos[pickable].lastTimeSeen = Time.time;
            pickupInfos[pickable].lastTimeSeenActive = Time.time;
            pickupInfos[pickable].lastTimeCollected = Time.time;
        }
    }
}