using CharacterCreator;

// Cloner's Big Quest lives with the character, like its effect. This is a playstyle
// the base game has no equivalent of: it counts how many objects you've actually
// cloned. CloneEffect emits ctx.QuestEvent("clone") on each successful copy, and this
// quest tallies them - no kills involved.
namespace CharacterCreator.Characters.Cloner
{
    [BigQuestKind("clonecount")]
    public class CloneQuest : BigQuest
    {
        private int cloned;
        public override int Progress => cloned;

        public override void OnEvent(string name)
        {
            if (name == "clone") cloned++;
        }
    }
}
