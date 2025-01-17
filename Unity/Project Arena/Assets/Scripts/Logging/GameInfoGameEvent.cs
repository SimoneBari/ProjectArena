using Events;

namespace Logging
{
    public struct GameInfo
    {
        public int gameDuration;
        public string scene;
    }

    public class BaseGameInfoGameEvent : GameEventBase<GameInfo>
    {
    }

    public sealed class GameInfoGameEvent : ClassSingleton<BaseGameInfoGameEvent>
    {
    }
}