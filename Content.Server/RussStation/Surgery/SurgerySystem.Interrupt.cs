using Content.Shared.Buckle.Components;
using Content.Shared.RussStation.Surgery;
using Content.Shared.RussStation.Surgery.Components;
using Content.Shared.Standing;
using Content.Shared.Tools;
using Robust.Shared.Prototypes;

namespace Content.Server.RussStation.Surgery;

public sealed partial class SurgerySystem
{
    private static readonly ProtoId<ToolQualityPrototype> InterruptSlicingQuality = "Slicing";
    private static readonly ProtoId<ToolQualityPrototype> InterruptClampingQuality = "Clamping";
    private static readonly ProtoId<ToolQualityPrototype> InterruptCauterizingQuality = "Cauterizing";

    private void InitializeInterrupt()
    {
        SubscribeLocalEvent<SurgeryDrapedComponent, StoodEvent>(OnDrapedStood);
        SubscribeLocalEvent<SurgeryDrapedComponent, UnbuckledEvent>(OnDrapedUnbuckled);
    }

    private void OnDrapedStood(Entity<SurgeryDrapedComponent> ent, ref StoodEvent args)
    {
        CancelActiveSurgery(ent.Owner);
    }

    private void OnDrapedUnbuckled(Entity<SurgeryDrapedComponent> ent, ref UnbuckledEvent args)
    {
        // Only react when the patient was on a dedicated surgery surface; slipping off a chair
        // back-rest or a random bed shouldn't void an ongoing procedure.
        if (!HasComp<SurgerySurfaceComponent>(args.Strap.Owner))
            return;

        CancelActiveSurgery(ent.Owner);
    }

    // Tears down a draped patient's active procedure, applies the clamp-release consequence when
    // applicable, and notifies surgeon + patient. The DoAfter itself is cancelled by BreakOnMove
    // on the surgery DoAfterArgs, so we only need to clean up components and popups here.
    private void CancelActiveSurgery(EntityUid patient)
    {
        EntityUid? surgeon = null;
        if (_activeSurgeryQuery.TryComp(patient, out var active))
        {
            ApplyInterruptConsequences(patient, active);
            surgeon = active.Surgeon;
        }

        RemComp<ActiveSurgeryComponent>(patient);
        // OnDrapedShutdown drops the bedsheet, clears the alert, and deletes the drape visual.
        // Removed last so the popups below still have a valid patient.
        RemComp<SurgeryDrapedComponent>(patient);

        _popup.PopupEntity(Loc.GetString("surgery-interrupt-patient"), patient, patient);
        if (surgeon is { } surgeonUid && Exists(surgeonUid))
            _popup.PopupEntity(Loc.GetString("surgery-interrupt-surgeon", ("target", patient)), patient, surgeonUid);
    }

    // Restores the bleed the hemostat had clamped off, when the procedure started with an incision,
    // reached a clamping step, and hasn't cauterised yet. Any other phase leaves the accumulated
    // step effects as they are: those effects are the consequence on their own.
    private void ApplyInterruptConsequences(EntityUid patient, ActiveSurgeryComponent active)
    {
        if (active.ProcedureId is not { } procId ||
            !ProtoManager.TryIndex<SurgeryProcedurePrototype>(procId, out var proto))
            return;

        var incisionStep = -1;
        var clampStep = -1;
        var cauteryStep = -1;
        for (var i = 0; i < proto.Steps.Count; i++)
        {
            var quality = proto.Steps[i].GetQuality();
            if (incisionStep < 0 && quality == InterruptSlicingQuality)
                incisionStep = i;
            else if (incisionStep >= 0 && clampStep < 0 && quality == InterruptClampingQuality)
                clampStep = i;
            else if (quality == InterruptCauterizingQuality)
            {
                cauteryStep = i;
                break;
            }
        }

        if (incisionStep < 0 || clampStep < 0)
            return;
        if (active.CurrentStep <= clampStep)
            return;
        if (cauteryStep >= 0 && active.CurrentStep > cauteryStep)
            return;

        _bloodstream.TryModifyBleedAmount((patient, null), SurgeryConstants.IncisionBleedAmount);
    }
}
