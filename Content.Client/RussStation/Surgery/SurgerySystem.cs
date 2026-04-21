using Content.Client.UserInterface.Controls;
using Content.Shared.RussStation.Surgery;
using Content.Shared.RussStation.Surgery.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client.RussStation.Surgery;

public sealed class SurgerySystem : SharedSurgerySystem
{
    private SimpleRadialMenu? _menu;

    private static readonly SpriteSpecifier DefaultProcedureIcon = new SpriteSpecifier.Rsi(
        new ResPath("Objects/Specific/Medical/Surgery/scalpel.rsi"), "scalpel");

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<OpenSurgeryMenuEvent>(OnOpenSurgeryMenu);
        SubscribeNetworkEvent<OpenOrganMenuEvent>(OnOpenOrganMenu);
    }

    private void OnOpenSurgeryMenu(OpenSurgeryMenuEvent ev)
    {
        CloseMenu();

        var byCategory = new Dictionary<SurgeryCategory, List<(string Id, SurgeryProcedurePrototype Proto)>>();
        foreach (var procedureId in ev.ProcedureIds)
        {
            if (!ProtoManager.TryIndex<SurgeryProcedurePrototype>(procedureId, out var proto))
            {
                Log.Warning($"Server sent unknown surgery procedure prototype: {procedureId}");
                continue;
            }

            if (!byCategory.TryGetValue(proto.Category, out var list))
                byCategory[proto.Category] = list = new List<(string, SurgeryProcedurePrototype)>();
            list.Add((procedureId, proto));
        }

        var topLevel = new List<RadialMenuOptionBase>();
        foreach (var category in CategoryDisplayOrder)
        {
            if (!byCategory.TryGetValue(category, out var procedures) || procedures.Count == 0)
                continue;

            var nested = new List<RadialMenuOptionBase>();
            foreach (var (id, proto) in procedures)
            {
                var capturedId = id;
                var target = ev.Target;
                var bedsheet = ev.Bedsheet;

                nested.Add(new RadialMenuActionOption<string>(
                    _ => OnProcedureSelected(target, bedsheet, capturedId),
                    capturedId)
                {
                    ToolTip = Loc.GetString(proto.Name),
                    IconSpecifier = RadialMenuIconSpecifier.With(proto.Icon ?? DefaultProcedureIcon),
                });
            }

            topLevel.Add(new RadialMenuNestedLayerOption(nested)
            {
                ToolTip = Loc.GetString(CategoryLocale(category)),
                IconSpecifier = RadialMenuIconSpecifier.With(CategoryIcon(category)),
            });
        }

        if (topLevel.Count == 0)
            return;

        _menu = new SimpleRadialMenu();
        _menu.SetButtons(topLevel);
        _menu.OpenOverMouseScreenPosition();
    }

    private void OnProcedureSelected(NetEntity target, NetEntity bedsheet, string procedureId)
    {
        CloseMenu();
        RaiseNetworkEvent(new SelectSurgeryProcedureEvent(target, bedsheet, procedureId));
    }

    private void OnOpenOrganMenu(OpenOrganMenuEvent ev)
    {
        CloseMenu();

        var buttons = new List<RadialMenuOptionBase>();

        foreach (var (organId, name, protoId) in ev.Organs)
        {
            var id = organId;
            var target = ev.Target;

            var option = new RadialMenuActionOption<NetEntity>(
                _ => OnOrganSelected(target, id),
                id)
            {
                ToolTip = protoId != null ? $"{name} ({protoId})" : name,
            };

            if (protoId != null)
            {
                option.IconSpecifier = RadialMenuIconSpecifier.With(
                    new SpriteSpecifier.EntityPrototype(protoId));
            }

            buttons.Add(option);
        }

        if (buttons.Count == 0)
            return;

        _menu = new SimpleRadialMenu();
        _menu.SetButtons(buttons);
        _menu.OpenOverMouseScreenPosition();
    }

    private void OnOrganSelected(NetEntity target, NetEntity organId)
    {
        CloseMenu();
        RaiseNetworkEvent(new SelectOrganEvent(target, organId));
    }

    private void CloseMenu()
    {
        _menu?.Close();
        _menu = null;
    }

    private static readonly SurgeryCategory[] CategoryDisplayOrder =
    {
        SurgeryCategory.WoundRepair,
        SurgeryCategory.TendWounds,
        SurgeryCategory.OrganManipulation,
        SurgeryCategory.Implants,
        SurgeryCategory.Advanced,
    };

    private static string CategoryLocale(SurgeryCategory category) => category switch
    {
        SurgeryCategory.WoundRepair => "surgery-category-wound-repair",
        SurgeryCategory.TendWounds => "surgery-category-tend-wounds",
        SurgeryCategory.OrganManipulation => "surgery-category-organ-manipulation",
        SurgeryCategory.Implants => "surgery-category-implants",
        SurgeryCategory.Advanced => "surgery-category-advanced",
        _ => category.ToString(),
    };

    private static SpriteSpecifier CategoryIcon(SurgeryCategory category) => category switch
    {
        SurgeryCategory.WoundRepair => new SpriteSpecifier.EntityPrototype("SurgicalDrape"),
        SurgeryCategory.TendWounds => new SpriteSpecifier.EntityPrototype("Hemostat"),
        SurgeryCategory.OrganManipulation => new SpriteSpecifier.Rsi(
            new ResPath("@RussStation/Interface/Surgery/procedures.rsi"), "organmanipulation_icon"),
        SurgeryCategory.Implants => new SpriteSpecifier.EntityPrototype("Scalpel"),
        SurgeryCategory.Advanced => new SpriteSpecifier.EntityPrototype("Scalpel"),
        _ => DefaultProcedureIcon,
    };
}
