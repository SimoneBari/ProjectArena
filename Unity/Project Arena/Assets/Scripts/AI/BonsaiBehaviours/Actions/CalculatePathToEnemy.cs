using AI.Layers.KnowledgeBase;
using Bonsai;
using Others;
using UnityEngine;
using UnityEngine.AI;
using Task = Bonsai.Core.Task;

namespace AI.BonsaiBehaviours.Actions
{
    /// <summary>
    /// Attempts to estimated the current position of an enemy after it was lost.
    /// The higher the prediction score of the enemy, the more likely the prediction is correct.
    /// </summary>
    [BonsaiNode("Tasks/")]
    public class CalculatePathToEnemy : Task
    {
        private Entity.Entity enemy;
        private AIEntity entity;
        private NavigationSystem navSystem;
        public string enemyPathKey;
        private NavMeshPath EnemyPath
        {
            set => Blackboard.Set(enemyPathKey, value);
        }
        
        public override void OnStart()
        {
            entity = Actor.GetComponent<AIEntity>();
            navSystem = entity.NavigationSystem;
            enemy = entity.GetEnemy();
        }

        public override Status Run()
        {
            if (!enemy.IsAlive)
            {
                return Status.Failure;
            }
            var path = navSystem.CalculatePath(enemy.transform.position);
            if (path.IsComplete())
            {
                EnemyPath = path;
                return Status.Success;
            }

            // This shouldn't happen... Why is the enemy in a position that cannot be reached?
            Debug.LogWarning("Enemy position prediction failed! Enemy is unreachable");
            return Status.Failure;
        }
    
}}