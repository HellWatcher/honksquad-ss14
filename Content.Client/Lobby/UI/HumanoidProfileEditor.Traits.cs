using System.Linq;
using Content.Client.Lobby.UI.Roles;
using Content.Client.Stylesheets;
using Content.Shared.CCVar;
using Content.Shared.Traits;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{

    /// <summary>
    /// Refreshes traits selector
    /// </summary>
    public void RefreshTraits()
    {
        TraitsList.RemoveAllChildren();

        var traits = _prototypeManager.EnumeratePrototypes<TraitPrototype>().OrderBy(t => Loc.GetString(t.Name)).ToList();
        TabContainer.SetTabTitle(3, Loc.GetString("humanoid-profile-editor-traits-tab"));

        if (traits.Count < 1)
        {
            TraitsList.AddChild(new Label
            {
                Text = Loc.GetString("humanoid-profile-editor-no-traits"),
                FontColorOverride = Color.Gray,
            });
            return;
        }

        // Setup model
        Dictionary<string, List<string>> traitGroups = new();
        List<string> defaultTraits = new();
        traitGroups.Add(TraitCategoryPrototype.Default, defaultTraits);

        foreach (var trait in traits)
        {
            if (trait.Category == null)
            {
                defaultTraits.Add(trait.ID);
                continue;
            }

            if (!_prototypeManager.HasIndex(trait.Category))
                continue;

            var group = traitGroups.GetOrNew(trait.Category);
            group.Add(trait.ID);
        }

        // HONK START - Global trait point budget display
        var globalMax = _cfgManager.GetCVar(CCVars.MaxTraitPoints);
        var globalSpent = 0;

        // Pre-calculate global spending
        foreach (var (_, categoryTraits) in traitGroups)
        {
            foreach (var traitProto in categoryTraits)
            {
                var trait = _prototypeManager.Index<TraitPrototype>(traitProto);
                if (Profile?.TraitPreferences.Contains(trait.ID) == true)
                    globalSpent += trait.Cost;
            }
        }

        var globalAvailable = globalMax - globalSpent;

        TraitsList.AddChild(new Label
        {
            Text = Loc.GetString("humanoid-profile-editor-trait-points-available",
                ("points", globalAvailable)),
            FontColorOverride = globalAvailable >= 0 ? Color.LimeGreen : Color.Red,
            Margin = new Thickness(0, 0, 0, 8),
            StyleClasses = { StyleClass.LabelHeading },
            HorizontalAlignment = HAlignment.Center,
        });

        // Wrapping container for category columns
        var columnsContainer = new GridContainer
        {
            Columns = 3,
            HorizontalExpand = true,
        };
        TraitsList.AddChild(columnsContainer);
        // HONK END

        // Create UI view from model (sorted alphabetically by display name)
        foreach (var (categoryId, categoryTraits) in traitGroups.OrderBy(g =>
            g.Key == TraitCategoryPrototype.Default
                ? string.Empty
                : _prototypeManager.TryIndex<TraitCategoryPrototype>(g.Key, out var cat)
                    ? Loc.GetString(cat.Name)
                    : g.Key))
        {
            if (categoryTraits.Count == 0)
                continue;

            TraitCategoryPrototype? category = null;

            // HONK START - Each category gets its own vertical column
            var column = new BoxContainer
            {
                Orientation = BoxContainer.LayoutOrientation.Vertical,
                HorizontalExpand = true,
            };
            // HONK END

            if (categoryId != TraitCategoryPrototype.Default)
            {
                category = _prototypeManager.Index<TraitCategoryPrototype>(categoryId);
                // Label
                column.AddChild(new Label // HONK - Changed from TraitsList to column
                {
                    Text = Loc.GetString(category.Name),
                    Margin = new Thickness(0, 0, 0, 0),
                    StyleClasses = { StyleClass.LabelHeading },
                });
            }

            List<TraitPreferenceSelector?> selectors = new();
            var selectionCount = 0;

            foreach (var traitProto in categoryTraits)
            {
                var trait = _prototypeManager.Index<TraitPrototype>(traitProto);
                var selector = new TraitPreferenceSelector(trait);

                selector.Preference = Profile?.TraitPreferences.Contains(trait.ID) == true;
                if (selector.Preference)
                    selectionCount += trait.Cost;

                selector.PreferenceChanged += preference =>
                {
                    if (preference)
                    {
                        Profile = Profile?.WithTraitPreference(trait.ID, _prototypeManager);
                    }
                    else
                    {
                        Profile = Profile?.WithoutTraitPreference(trait.ID, _prototypeManager);
                    }

                    SetDirty();
                    RefreshTraits(); // If too many traits are selected, they will be reset to the real value.
                };
                selectors.Add(selector);
            }

            // HONK START - Point-buy: show category cap if set
            if (category is { MaxTraitPoints: >= 0 })
            {
                column.AddChild(new Label
                {
                    Text = Loc.GetString("humanoid-profile-editor-trait-category-cap",
                        ("spent", selectionCount),
                        ("max", category.MaxTraitPoints)),
                    FontColorOverride = selectionCount <= category.MaxTraitPoints ? Color.LightGray : Color.Red,
                    Margin = new Thickness(0, 0, 0, 5),
                });
            }

            // HONK END

            foreach (var selector in selectors)
            {
                if (selector == null)
                    continue;

                column.AddChild(selector); // HONK - Changed from TraitsList to column
            }

            columnsContainer.AddChild(column); // HONK - Add to columns
        }
    }
}
