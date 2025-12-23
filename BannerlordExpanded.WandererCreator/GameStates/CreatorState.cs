using TaleWorlds.Core;

namespace BannerlordExpanded.WandererCreator.GameStates
{
    /// <summary>
    /// Simple GameState that keeps the custom game session alive.
    /// BarberState is pushed on top of this, and when it pops, we return here.
    /// </summary>
    public class CreatorState : GameState
    {
        public CreatorState() : base()
        {
        }

        public override bool IsMenuState => true;
    }
}
