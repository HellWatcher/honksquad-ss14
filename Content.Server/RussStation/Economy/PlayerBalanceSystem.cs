using Content.Server.RussStation.Memories;
using Content.Shared.Access.Components;
using Content.Shared.Access.Systems;
using Content.Shared.GameTicking;
using Content.Shared.RussStation.Economy;
using Content.Shared.RussStation.Economy.Components;
using Robust.Shared.Configuration;
using Robust.Shared.Random;

namespace Content.Server.RussStation.Economy;

public sealed class PlayerBalanceSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedIdCardSystem _idCard = default!;
    [Dependency] private readonly MemoriesSystem _memories = default!;

    private int _defaultStartingBalance;

    /// <summary>
    /// Index of account numbers to their owning entity for fast lookups.
    /// Rebuilt each round as players spawn.
    /// </summary>
    private readonly Dictionary<string, EntityUid> _accountIndex = new();

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, EconomyCCVars.DefaultStartingBalance, v => _defaultStartingBalance = v, true);

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<PlayerBalanceComponent, ComponentRemove>(OnRemove);
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null)
            return;

        var comp = EnsureComp<PlayerBalanceComponent>(args.Mob);
        comp.Balance = _defaultStartingBalance;

        // Generate unique account number and PIN.
        comp.AccountNumber = GenerateAccountNumber();
        comp.Pin = GeneratePin();
        _accountIndex[comp.AccountNumber] = args.Mob;

        Dirty(args.Mob, comp);

        // Stamp account number onto the player's ID card.
        if (_idCard.TryFindIdCard(args.Mob, out var idCard))
        {
            var idComp = Comp<IdCardComponent>(idCard);
            idComp.AccountNumber = comp.AccountNumber;
            Dirty(idCard, idComp);
        }

        // Register account info in the player's memories.
        _memories.AddMemory(args.Mob, "memories-key-account-number", comp.AccountNumber);
        _memories.AddMemory(args.Mob, "memories-key-pin", comp.Pin);
    }

    private void OnRemove(EntityUid uid, PlayerBalanceComponent comp, ComponentRemove args)
    {
        if (!string.IsNullOrEmpty(comp.AccountNumber))
            _accountIndex.Remove(comp.AccountNumber);
    }

    /// <summary>
    /// Look up the entity that owns the given account number. Returns false if not found.
    /// </summary>
    public bool TryGetByAccount(string accountNumber, out EntityUid owner)
    {
        return _accountIndex.TryGetValue(accountNumber, out owner);
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

    /// <summary>
    /// Generate an 8-digit account number, ensuring uniqueness within the round.
    /// </summary>
    private string GenerateAccountNumber()
    {
        string number;
        do
        {
            number = _random.Next(10000000, 99999999).ToString();
        }
        while (_accountIndex.ContainsKey(number));

        return number;
    }

    /// <summary>
    /// Generate a 4-digit PIN.
    /// </summary>
    private string GeneratePin()
    {
        return _random.Next(0, 9999).ToString("D4");
    }
}
