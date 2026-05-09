using System.Linq;
using Content.Shared.Body;
using Content.Shared.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.RussStation.Surgery;
using Content.Shared.RussStation.Surgery.Components;
using Content.Shared.RussStation.Surgery.Effects;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Surgery;

public sealed partial class SurgerySystem
{
    private void InitializeOrgans()
    {
        // Organ-specific subscriptions can go here if needed.
    }

    private void TryInsertOrgan(EntityUid surgeon, EntityUid patient, EntityUid organ)
    {
        if (!TryComp<OrganComponent>(organ, out var organComp))
            return;

        if (!TryComp<BodyComponent>(patient, out var body) || body.Organs == null)
        {
            _popup.PopupEntity(Loc.GetString("surgery-organ-insert-failed"), patient, surgeon);
            return;
        }

        var organProtoId = MetaData(organ).EntityPrototype?.ID;

        // Block if the patient already has an equivalent organ.
        // Categorized organs deduplicate by category; null-category organs deduplicate by prototype ID.
        foreach (var existing in body.Organs.ContainedEntities)
        {
            if (!TryComp<OrganComponent>(existing, out var existingOrgan))
                continue;

            bool isDuplicate;
            if (organComp.Category != null && existingOrgan.Category != null)
                isDuplicate = existingOrgan.Category == organComp.Category;
            else if (organComp.Category == null && existingOrgan.Category == null)
                isDuplicate = organProtoId != null && organProtoId == MetaData(existing).EntityPrototype?.ID;
            else
                isDuplicate = false;

            if (!isDuplicate)
                continue;

            _popup.PopupEntity(
                Loc.GetString("surgery-organ-already-exists", ("organ", MetaData(existing).EntityName)),
                patient, surgeon);
            return;
        }

        _container.Insert(organ, body.Organs, force: true);
        _popup.PopupEntity(
            Loc.GetString("surgery-organ-inserted", ("organ", MetaData(organ).EntityName)),
            patient, surgeon);
    }

    private void OpenOrganRemovalMenu(EntityUid? surgeon, EntityUid patient)
    {
        if (surgeon == null || !TryComp<ActorComponent>(surgeon, out var actor))
            return;

        if (!TryComp<BodyComponent>(patient, out var body) || body.Organs == null)
            return;

        var organs = new List<(NetEntity, string, string?)>();
        foreach (var organ in body.Organs.ContainedEntities)
        {
            // Skip limbs, only show internal organs
            if (!TryComp<OrganComponent>(organ, out var organComp))
                continue;

            if (IsLimbCategory(organComp.Category))
                continue;

            var meta = MetaData(organ);
            organs.Add((GetNetEntity(organ), meta.EntityName, meta.EntityPrototype?.ID));
        }

        if (organs.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString("surgery-no-organs-to-remove"), patient, surgeon.Value);
            return;
        }

        RaiseNetworkEvent(new OpenOrganMenuEvent(GetNetEntity(patient), organs), actor.PlayerSession);
    }

    private void OnOrganSelected(SelectOrganEvent ev, EntitySessionEventArgs args)
    {
        if (args.SenderSession.AttachedEntity is not { } surgeon)
            return;

        if (!TryGetEntity(ev.Target, out var patient) || !TryGetEntity(ev.OrganId, out var organ))
            return;

        if (!_interaction.InRangeUnobstructed(surgeon, patient.Value))
            return;

        if (!TryComp<ActiveSurgeryComponent>(patient.Value, out var active) || active.Surgeon != surgeon)
            return;

        if (active.ProcedureId == null ||
            !ProtoManager.TryIndex<SurgeryProcedurePrototype>(active.ProcedureId.Value, out var proto))
            return;

        if (active.CurrentStep >= proto.Steps.Count)
            return;

        var step = proto.Steps[active.CurrentStep];
        // step.Effect is null for preset-supplied effects; GetEffect() resolves the preset fallback
        if (step.GetEffect() is not RemoveOrganEffect)
            return;

        if (!TryComp<BodyComponent>(patient.Value, out var body) || body.Organs == null)
            return;

        if (!body.Organs.ContainedEntities.Contains(organ.Value))
            return;

        if (!_pendingOrganRemovalTools.TryGetValue(patient.Value, out var tool) || !Exists(tool))
        {
            _popup.PopupEntity(Loc.GetString("surgery-organ-remove-failed"), patient.Value, surgeon);
            return;
        }

        StartOrganRemovalDoAfter(surgeon, patient.Value, tool, organ.Value, step, proto.Difficulty);
    }

    private void StartOrganRemovalDoAfter(
        EntityUid surgeon,
        EntityUid patient,
        EntityUid tool,
        EntityUid organ,
        SurgeryStep step,
        SurgeryDifficulty difficulty)
    {
        var duration = TimeSpan.FromSeconds(
            (float) GetStepDuration(step, patient, difficulty, surgeon).TotalSeconds
            * GetToolTierModifier(tool));

        var ev = new OrganRemovalDoAfterEvent { SelectedOrgan = GetNetEntity(organ) };
        var doAfterArgs = new DoAfterArgs(
            EntityManager, surgeon, duration, ev, patient,
            target: patient,
            used: tool)
        {
            NeedHand = true,
            BreakOnMove = true,
            BreakOnHandChange = true,
        };

        if (!_doAfter.TryStartDoAfter(doAfterArgs))
            _popup.PopupEntity(Loc.GetString("surgery-busy"), patient, surgeon);
    }

    private void OnOrganRemovalDoAfter(Entity<ActiveSurgeryComponent> ent, ref OrganRemovalDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled)
            return;

        args.Handled = true;
        var patient = ent.Owner;

        if (!TryGetEntity(args.SelectedOrgan, out var organ))
            return;

        if (!TryComp<BodyComponent>(patient, out var body) || body.Organs == null)
            return;

        if (!body.Organs.ContainedEntities.Contains(organ.Value))
        {
            if (args.User is { } u1)
                _popup.PopupEntity(Loc.GetString("surgery-organ-remove-failed"), patient, u1);
            return;
        }

        if (!_container.Remove(organ.Value, body.Organs))
        {
            if (args.User is { } u2)
                _popup.PopupEntity(Loc.GetString("surgery-organ-remove-failed"), patient, u2);
            return;
        }

        _pendingOrganRemovalTools.Remove(patient);

        if (!_hands.TryPickupAnyHand(args.User, organ.Value, checkActionBlocker: false))
            _xform.DropNextTo(organ.Value, patient);

        if (args.User is { } surgeon)
        {
            _popup.PopupEntity(
                Loc.GetString("surgery-step-remove-organ", ("user", surgeon), ("target", patient)),
                patient, surgeon);
            _popup.PopupEntity(
                Loc.GetString("surgery-organ-removed", ("organ", MetaData(organ.Value).EntityName)),
                patient, surgeon);
        }
    }

    private static bool IsLimbCategory(ProtoId<OrganCategoryPrototype>? category)
    {
        if (category == null)
            return false;

        return category.Value.Id is
            "Torso" or "Head" or
            "ArmLeft" or "ArmRight" or
            "HandLeft" or "HandRight" or
            "LegLeft" or "LegRight" or
            "FootLeft" or "FootRight";
    }
}
