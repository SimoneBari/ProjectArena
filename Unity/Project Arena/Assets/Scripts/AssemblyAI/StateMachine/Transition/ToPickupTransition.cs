using System;
using AssemblyAI.State;

namespace AssemblyAI.StateMachine.Transition
{
    public class ToPickupTransition : ITransition
    {
        private readonly LookForPickups lookForPickups;
        private readonly Action action;

        public ToPickupTransition(AIEntity entity, Action action = null)
        {
            this.action = action;
            lookForPickups = new LookForPickups(entity);
        }
        public float GetScore()
        {
            return lookForPickups.CalculateTransitionScore();
        }

        public IState GetNextState()
        {
            return lookForPickups;
        }

        public void OnActivate()
        {
            action?.Invoke();
        }
    }
}