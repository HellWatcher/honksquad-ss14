using Content.Server.Popups;
using Content.Shared.Body;
using Content.Shared.DoAfter;
using Content.Shared.IdentityManagement;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.RussStation.Body;
using Content.Shared.RussStation.Medical;
using Robust.Shared.Containers;

namespace Content.Server.RussStation.Medical;

/// <summary>
/// Handles the autosurgeon: a single-use device that installs a cybernetic organ
/// via a do-after, bypassing surgery.
/// </summary>
public sealed partial class AutosurgeonSystem : EntitySystem
{
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private SharedDoAfterSystem _doAfter = default!;
    [Dependency] private SharedContainerSystem _container = default!;
    [Dependency] private SharedTransformSystem _xform = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<AutosurgeonComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<AutosurgeonComponent, AutosurgeonDoAfterEvent>(OnDoAfterComplete);
    }

    private void OnAfterInteract(EntityUid uid, AutosurgeonComponent comp, AfterInteractEvent args)
    {
        if (args.Handled || args.Target == null || !args.CanReach)
            return;

        var target = args.Target.Value;

        if (!HasComp<BodyComponent>(target))
        {
            _popup.PopupEntity(Loc.GetString("autosurgeon-no-body"), target, args.User);
            args.Handled = true;
            return;
        }

        var isSelfUse = args.User == target;
        var delay = isSelfUse ? comp.SelfInstallTime : comp.InstallTime;

        var doAfterArgs = new DoAfterArgs(EntityManager, args.User, delay,
            new AutosurgeonDoAfterEvent(), uid, target: target, used: uid)
        {
            BreakOnDamage = true,
            BreakOnMove = true,
            NeedHand = true,
        };

        if (_doAfter.TryStartDoAfter(doAfterArgs))
        {
            if (isSelfUse)
            {
                _popup.PopupEntity(
                    Loc.GetString("autosurgeon-installing-self"),
                    target, args.User);
            }
            else
            {
                var targetName = Identity.Entity(target, EntityManager);
                _popup.PopupEntity(
                    Loc.GetString("autosurgeon-installing-user", ("target", targetName)),
                    target, args.User);

                var userName = Identity.Entity(args.User, EntityManager);
                _popup.PopupEntity(
                    Loc.GetString("autosurgeon-installing-target", ("user", userName)),
                    args.User, target, PopupType.LargeCaution);
            }
        }

        args.Handled = true;
    }

    private void OnDoAfterComplete(EntityUid uid, AutosurgeonComponent comp, AutosurgeonDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Target == null)
            return;

        InstallOrgan(uid, comp, args.User, args.Target.Value);
        args.Handled = true;
    }

    private void InstallOrgan(EntityUid autosurgeon, AutosurgeonComponent comp, EntityUid user, EntityUid target)
    {
        if (!TryComp<BodyComponent>(target, out var body) || body.Organs == null)
        {
            _popup.PopupEntity(Loc.GetString("autosurgeon-no-body"), target, user);
            return;
        }

        // Spawn the organ entity
        var organ = Spawn(comp.OrganPrototype, Transform(target).Coordinates);

        if (!TryComp<OrganComponent>(organ, out var organComp))
        {
            Log.Error("Autosurgeon organ prototype {OrganPrototype} missing OrganComponent", comp.OrganPrototype);
            QueueDel(organ);
            return;
        }

        // Remove any same-category organ (dropping it next to the body) and install the new one.
        OrganReplacementHelper.ReplaceOrganByCategory(
            EntityManager, _container, _xform, target, body.Organs, organ, organComp.Category);

        var organName = MetaData(organ).EntityName;
        _popup.PopupEntity(
            Loc.GetString("autosurgeon-success", ("organ", organName)),
            target, user);

        // Single-use: delete the autosurgeon
        QueueDel(autosurgeon);
    }
}
