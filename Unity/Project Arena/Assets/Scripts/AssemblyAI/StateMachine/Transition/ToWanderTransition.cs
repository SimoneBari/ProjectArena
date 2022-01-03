using System;
using AssemblyAI.State;

namespace AssemblyAI.StateMachine.Transition
{
    public class ToWanderTransition : ITransition
    {
        private readonly Wander wander;
        private readonly Action action;

        public ToWanderTransition(AIEntity entity, Action action = null)
        {
            this.action = action;
            wander = new Wander(entity);
        }

        public float GetScore()
        {
            return wander.CalculateTransitionScore();
        }

        public IState GetNextState()
        {
            return wander;
        }

        public void OnActivate()
        {
            action?.Invoke();
        }
    }
}