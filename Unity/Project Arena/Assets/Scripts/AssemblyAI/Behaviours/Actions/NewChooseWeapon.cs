using AI.KnowledgeBase;
using AssemblyAI.AI.Layer1.Actuator;
using AssemblyEntity.Component;
using BehaviorDesigner.Runtime.Tasks;
using UnityEngine;

namespace AssemblyAI.Behaviours.Actions
{
    public class NewChooseWeapon : Action
    {
        private AIEntity entity;
        private Entity enemy;
        private AISightController sightController;
        private TargetKnowledgeBase targetKb;
        private GunManager gunManager;

        public override void OnAwake()
        {
            entity = gameObject.GetComponent<AIEntity>();
            enemy = entity.GetEnemy();
            sightController = entity.SightController;
            targetKb = entity.TargetKb;
            gunManager = entity.GunManager;
        }

        public override TaskStatus OnUpdate()
        {
            var headPos = sightController.GetHeadPosition();
            var enemyPos = enemy.transform.position;
            var enemyDistance = (headPos - enemyPos).magnitude;

            // Choose blast weapon if we cannot see the enemy right now
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            var mustChooseBlastWeapon = targetKb.LastTimeDetected != Time.time;
            
            var selectedGun = -1;
            var bestGunScore = 0f;
            for (var i = 0; i < gunManager.NumberOfGuns; i++)
            {
                if (!gunManager.IsGunActive(i)) continue;
                if (mustChooseBlastWeapon && !gunManager.IsGunBlastWeapon(i)) continue;

                var gunScore = gunManager.GetGunScore(i, enemyDistance);
                if (gunScore > bestGunScore)
                {
                    bestGunScore = gunScore;
                    selectedGun = i;
                }
            }

            if (selectedGun != -1)
            {
                // We found a good weapon, switch!
                gunManager.TryEquipGun(selectedGun);
            }

            return TaskStatus.Running;
        }
    }
}