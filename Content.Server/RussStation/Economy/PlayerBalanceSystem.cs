using Content.Shared.GameTicking;
using Content.Shared.RussStation.Economy;
using Content.Shared.RussStation.Economy.Components;
using Robust.Shared.Configuration;

namespace Content.Server.RussStation.Economy;

public sealed class PlayerBalanceSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private int _defaultStartingBalance;

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, EconomyCCVars.DefaultStartingBalance, v => _defaultStartingBalance = v, true);

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null)
            return;

        var comp = EnsureComp<PlayerBalanceComponent>(args.Mob);
        comp.Balance = _defaultStartingBalance;
        Dirty(args.Mob, comp);
    }

    /// <summary>
    /// Try to deduct an amount from a player's balance. Returns false if insufficient funds.
    /// </summary>
    public bool TryDeduct(EntityUid uid, int amount, PlayerBalanceComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return false;

        if (comp.Balance < amount)
            return false;

        comp.Balance -= amount;
        Dirty(uid, comp);
        return true;
    }

    /// <summary>
    /// Add funds to a player's balance.
    /// </summary>
    public void AddBalance(EntityUid uid, int amount, PlayerBalanceComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        comp.Balance += amount;
        Dirty(uid, comp);
    }

    /// <summary>
    /// Get a player's current balance. Returns 0 if no component.
    /// </summary>
    public int GetBalance(EntityUid uid, PlayerBalanceComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return 0;

        return comp.Balance;
    }
}
