using Content.Shared.Foldable;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;

namespace Content.Shared.RussStation.Foldable;

/// <summary>
/// Plays a buckle sound when an entity is folded or unfolded by a user.
/// </summary>
public sealed class FoldSoundSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private static readonly SoundPathSpecifier FoldSound = new("/Audio/Effects/buckle.ogg");

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FoldableComponent, FoldedEvent>(OnFolded);
    }

    private void OnFolded(EntityUid uid, FoldableComponent component, ref FoldedEvent args)
    {
        if (args.User != null)
            _audio.PlayPredicted(FoldSound, uid, args.User);
    }
}
