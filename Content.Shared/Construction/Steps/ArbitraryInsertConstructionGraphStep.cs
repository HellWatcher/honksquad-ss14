using Content.Shared.Examine;
using Robust.Shared.Utility;

namespace Content.Shared.Construction.Steps
{
    public abstract partial class ArbitraryInsertConstructionGraphStep : EntityInsertConstructionGraphStep
    {
        [DataField] public LocId Name { get; private set; } = string.Empty;

        [DataField] public SpriteSpecifier? Icon { get; private set; }

        public override void DoExamine(ExaminedEvent examinedEvent)
        {
            if (string.IsNullOrEmpty(Name))
                return;

            var stepName = Loc.GetString(Name);
            examinedEvent.PushMarkup(Loc.GetString("construction-insert-arbitrary-entity", ("stepName", stepName)));
        }

        public override ConstructionGuideEntry GenerateGuideEntry()
        {
            return new ConstructionGuideEntry
            {
                Localization = "construction-presenter-arbitrary-step",
                //HONK START - locale double-lookup fix: presenter already localizes, don't pre-resolve
                Arguments = new (string, object)[]{("name", Name.ToString())},
                //HONK END
                Icon = Icon,
            };
        }
    }
}
