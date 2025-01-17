using AI.Layers.KnowledgeBase;
using Bonsai;
using Maps.MapGenerator;
using Task = Bonsai.Core.Task;

namespace AI.BonsaiBehaviours.Actions
{
    /// <summary>
    /// Selects the area to visit next while wandering, based on the selection given by the <see cref="MapMemory"/>
    /// </summary>
    [BonsaiNode("Tasks/")]
    public class SelectAreaToVisit : Task
    {
        private MapWanderPlanner wanderPlanner;
        public string areaChosenKey;

        private Area AreaChosen
        {
            set => Blackboard.Set(areaChosenKey, value);
        }

        public override void OnStart()
        {
            var entity = Actor.GetComponent<AIEntity>();
            wanderPlanner = entity.MapWanderPlanner;
        }

        public override Status Run()
        {
            AreaChosen = wanderPlanner.GetRecommendedArea();
            return Status.Success;
        }
    }
}