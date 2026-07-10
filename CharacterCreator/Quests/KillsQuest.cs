namespace CharacterCreator
{
    // The default big quest: defeat `targetKills` foes with your special ability.
    // Only kills the ability caused count (the framework tags ability bolts and tells
    // us viaAbility). This is what every character gets unless it declares another
    // "kind".
    [BigQuestKind("kills")]
    public class KillsQuest : BigQuest
    {
        private int kills;
        public override int Progress => kills;

        public override void OnKill(Agent victim, bool viaAbility)
        {
            if (viaAbility) kills++;
        }
    }
}
