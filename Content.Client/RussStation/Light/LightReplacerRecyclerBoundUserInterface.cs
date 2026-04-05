using Content.Client.UserInterface.Controls;
using Content.Shared.Light.Components;
using Content.Shared.RussStation.Light;
using Robust.Client.UserInterface;
using Robust.Shared.GameObjects;
using Robust.Shared.Prototypes;

namespace Content.Client.RussStation.Light;

public sealed class LightReplacerRecyclerBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _protoManager = default!;
    [Dependency] private readonly IComponentFactory _compFactory = default!;

    private SimpleRadialMenu? _menu;

    public LightReplacerRecyclerBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        if (!EntMan.TryGetComponent<LightReplacerRecyclerComponent>(Owner, out var recycler))
            return;

        _menu = this.CreateWindow<SimpleRadialMenu>();
        _menu.SetButtons(CreateButtons(recycler));
        _menu.OpenOverMouseScreenPosition();
    }

    private IEnumerable<RadialMenuOptionBase> CreateButtons(LightReplacerRecyclerComponent recycler)
    {
        var bulbs = new List<RadialMenuActionOptionBase>();
        var tubes = new List<RadialMenuActionOptionBase>();

        foreach (var protoId in recycler.PrintablePrototypes)
        {
            if (!_protoManager.TryIndex<EntityPrototype>(protoId, out var proto))
                continue;

            var isTube = false;
            if (proto.TryGetComponent<LightBulbComponent>(out var bulbComp, _compFactory))
                isTube = bulbComp.Type == LightBulbType.Tube;

            var option = new RadialMenuActionOption<EntProtoId>(OnOptionSelected, protoId)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(protoId),
                ToolTip = Loc.GetString("light-replacer-recycler-option",
                    ("name", proto.Name),
                    ("cost", recycler.PrintCost)),
            };

            if (isTube)
                tubes.Add(option);
            else
                bulbs.Add(option);
        }

        var result = new List<RadialMenuOptionBase>();

        if (bulbs.Count > 0)
        {
            result.Add(new RadialMenuNestedLayerOption(bulbs)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new EntProtoId("LightBulb")),
                ToolTip = Loc.GetString("light-replacer-recycler-category-bulbs"),
            });
        }

        if (tubes.Count > 0)
        {
            result.Add(new RadialMenuNestedLayerOption(tubes)
            {
                IconSpecifier = RadialMenuIconSpecifier.With(new EntProtoId("LightTube")),
                ToolTip = Loc.GetString("light-replacer-recycler-category-tubes"),
            });
        }

        return result;
    }

    private void OnOptionSelected(EntProtoId protoId)
    {
        SendMessage(new LightReplacerPrintMessage(protoId));
    }
}
