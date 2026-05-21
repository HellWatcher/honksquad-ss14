using Content.Shared.RussStation.Skillchips.Systems;

namespace Content.Client.RussStation.Skillchips;

/// <summary>
/// Client placeholder for <see cref="SharedSkillchipSystem"/> so the shared
/// abstract base resolves via IoC client-side. Consumers like the hedgetrimming
/// system need to query <c>HasCapability</c> during prediction.
/// </summary>
public sealed class SkillchipSystem : SharedSkillchipSystem
{
}
