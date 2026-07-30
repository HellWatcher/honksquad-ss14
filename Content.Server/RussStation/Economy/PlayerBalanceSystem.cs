using Content.Server.RussStation.Memories;
using Content.Shared.Access.Systems;
using Content.Shared.GameTicking;
using Content.Shared.RussStation.Economy;
using Content.Shared.RussStation.Economy.Components;
using SharedEconomyConstants = Content.Shared.RussStation.Economy.EconomyConstants;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.RussStation.Economy;

/// <summary>
/// Raised on the mob entity when its balance changes.
/// </summary>
public sealed class BalanceChangedEvent : EntityEventArgs
{
    public EntityUid Mob;

    public BalanceChangedEvent(EntityUid mob)
    {
        Mob = mob;
    }
}

public sealed partial class PlayerBalanceSystem : EntitySystem
{
    [Dependency] private IConfigurationManager _cfg = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private IGameTiming _timing = default!;
    [Dependency] private SharedIdCardSystem _idCard = default!;
    [Dependency] private MemoriesSystem _memories = default!;

    private int _defaultStartingBalance;

    /// <summary>
    /// Index of account numbers to their owning entity for fast lookups.
    /// Rebuilt each round as players spawn.
    /// </summary>
    private readonly Dictionary<string, EntityUid> _accountIndex = new(StringComparer.OrdinalIgnoreCase);

    public override void Initialize()
    {
        base.Initialize();

        Subs.CVar(_cfg, EconomyCCVars.DefaultStartingBalance, v => _defaultStartingBalance = v, true);

        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestart);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawn);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerBalanceComponent, ComponentRemove>(OnRemove);
    }

    private void OnRoundRestart(RoundRestartCleanupEvent ev)
    {
        _accountIndex.Clear();
    }

    private void OnPlayerSpawn(PlayerSpawnCompleteEvent args)
    {
        if (args.JobId == null)
            return;

        var comp = EnsureComp<PlayerBalanceComponent>(args.Mob);
        comp.Balance = _defaultStartingBalance;
        comp.JobId = args.JobId;

        // Generate unique hex account number.
        comp.AccountNumber = GenerateAccountNumber();
        _accountIndex[comp.AccountNumber] = args.Mob;

        // Stamp account number onto the player's ID card.
        if (_idCard.TryFindIdCard(args.Mob, out var idCard))
        {
            var bankComp = EnsureComp<BankLinkedCardComponent>(idCard);
            bankComp.AccountNumber = comp.AccountNumber;
        }

        // Register account number in the player's memories.
        _memories.AddMemory(args.Mob, "memories-key-account-number", comp.AccountNumber);
    }

    /// <summary>
    /// When a player takes over a mob that has no account and is holding a blank ID, auto-create one.
    /// Skips if the mob already has any account (even with a blank ID in hand).
    /// </summary>
    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (HasComp<PlayerBalanceComponent>(args.Entity))
            return;

        if (!_idCard.TryFindIdCard(args.Entity, out var idCard))
            return;

        var bankComp = CompOrNull<BankLinkedCardComponent>(idCard);
        if (!string.IsNullOrEmpty(bankComp?.AccountNumber))
            return;

        var accountNumber = CreateAccount(args.Entity, _defaultStartingBalance);
        bankComp = EnsureComp<BankLinkedCardComponent>(idCard);
        bankComp.AccountNumber = accountNumber;
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
    public bool TryDeduct(EntityUid uid, int amount, PlayerBalanceComponent? comp = null, string? description = null)
    {
        if (!Resolve(uid, ref comp, false))
            return false;

        if (comp.Balance < amount)
            return false;

        comp.Balance -= amount;
        RecordTransaction(comp, -amount, description ?? "Debit");
        RaiseLocalEvent(uid, new BalanceChangedEvent(uid));
        return true;
    }

    /// <summary>
    /// Add funds to a player's balance.
    /// </summary>
    public void AddBalance(EntityUid uid, int amount, PlayerBalanceComponent? comp = null, string? description = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        comp.Balance += amount;
        RecordTransaction(comp, amount, description ?? "Credit");
        RaiseLocalEvent(uid, new BalanceChangedEvent(uid));
    }

    private void RecordTransaction(PlayerBalanceComponent comp, int amount, string description)
    {
        comp.Transactions.Add(new TransactionRecord(amount, description, _timing.CurTime));

        if (comp.Transactions.Count > PlayerBalanceComponent.MaxTransactions)
            comp.Transactions.RemoveAt(SharedEconomyConstants.OldestTransactionIndex);
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
    /// Directly overwrites the balance. Use <see cref="AddBalance"/> or
    /// <see cref="TryDeduct"/> for normal gameplay paths; this is for tests
    /// and admin tooling that need to seed state.
    /// </summary>
    public void SetBalance(EntityUid uid, int amount, PlayerBalanceComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        comp.Balance = amount;
    }

    /// <summary>
    /// Assigns the player's job id used for wage tier lookup.
    /// </summary>
    public void SetJobId(EntityUid uid, string? jobId, PlayerBalanceComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        comp.JobId = jobId;
    }

    /// <summary>
    /// Toggles or sets whether paycheck notifications are muted for this entity.
    /// Exposed so the wallet cartridge UI can flip the flag without poking the component directly.
    /// </summary>
    public void SetPaycheckMuted(EntityUid uid, bool muted, PlayerBalanceComponent? comp = null)
    {
        if (!Resolve(uid, ref comp, false))
            return;

        comp.PaycheckMuted = muted;
    }

    /// <summary>
    /// Create a new bank account for an entity, invalidating any previous account.
    /// Intentionally resets balance to startingBalance (default 0) -- creating a new account
    /// means the old one and its funds are gone. This is the intended penalty for account replacement.
    /// </summary>
    public string CreateAccount(EntityUid uid, int startingBalance = EconomyConstants.NewAccountStartingBalance)
    {
        var comp = EnsureComp<PlayerBalanceComponent>(uid);

        if (!string.IsNullOrEmpty(comp.AccountNumber))
            _accountIndex.Remove(comp.AccountNumber);

        comp.Balance = startingBalance;
        comp.AccountNumber = GenerateAccountNumber();
        _accountIndex[comp.AccountNumber] = uid;

        _memories.AddMemory(uid, "memories-key-account-number", comp.AccountNumber);
        RaiseLocalEvent(uid, new BalanceChangedEvent(uid));

        return comp.AccountNumber;
    }

    /// <summary>
    /// Generate an 8-character hex account number, ensuring uniqueness within the round.
    /// </summary>
    private string GenerateAccountNumber()
    {
        string number;
        do
        {
            number = _random.Next(EconomyConstants.AccountNumberMinValue, int.MaxValue).ToString(EconomyConstants.AccountNumberHexFormat);
        }
        while (_accountIndex.ContainsKey(number));

        return number;
    }
}
