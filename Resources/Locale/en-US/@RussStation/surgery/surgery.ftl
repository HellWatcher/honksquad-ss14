# Surgery System

## Draping
surgery-patient-not-down = The patient must be lying down for surgery.

## Procedure Selection
surgery-no-procedures = No surgical procedures available.
surgery-procedure-started = You begin {$procedure} on {THE($target)}.
surgery-procedure-complete = The procedure is complete. Cauterize to close the wound.

## Step Popups
surgery-step-incision = {CAPITALIZE(THE($user))} slices {THE($target)} open. Blood wells up around the cut.
surgery-step-retract = {CAPITALIZE(THE($user))} pries the incision apart, exposing raw tissue.
surgery-step-clamp = {CAPITALIZE(THE($user))} clamps the severed vessels shut on {THE($target)}, stemming the bleed.
surgery-step-saw = {CAPITALIZE(THE($user))} saws through dense tissue on {THE($target)}.
surgery-step-cauterize = {CAPITALIZE(THE($user))} sears the wound on {THE($target)} shut with a sizzle.
surgery-step-treat-brute = {CAPITALIZE(THE($user))} stitches torn muscle on {THE($target)} back together.
surgery-step-treat-burn = {CAPITALIZE(THE($user))} scrapes away charred flesh on {THE($target)} and dresses what's left.
surgery-step-remove-organ = {CAPITALIZE(THE($user))} plunges a hand into {THE($target)} and pulls out an organ.
surgery-step-set-bones = {CAPITALIZE(THE($user))} wrenches {POSS-ADJ($target)} shattered bones back into line.
surgery-step-treat-burn-wounds = {CAPITALIZE(THE($user))} cauterizes and dresses {POSS-ADJ($target)} weeping burns.

## Alerts
alerts-surgery-draped-name = Surgical Drapes
alerts-surgery-draped-desc = You are draped for surgery. They will fall off if you stand.

## Examine
surgery-examine-active = [color=cyan]{CAPITALIZE(SUBJECT($target))} {CONJUGATE-BE($target)} undergoing {$procedure}.[/color]
surgery-examine-draped = [color=cyan]{CAPITALIZE(SUBJECT($target))} {CONJUGATE-BE($target)} draped and prepared for surgery.[/color]

## Tool Feedback
surgery-wrong-tool = That's not the right tool for this step.
surgery-step-repeat-done = The treatment has done all it can.

## Organ Operations
surgery-organ-removed = {CAPITALIZE(THE($organ))} has been removed.
surgery-organ-inserted = {CAPITALIZE(THE($organ))} has been inserted.
surgery-organ-already-exists = The patient already has {THE($organ)}.
surgery-organ-insert-failed = The organ cannot be inserted.
surgery-organ-remove-failed = The organ could not be removed.
surgery-no-organs-to-remove = There are no removable organs.

## Validation
surgery-already-draped = The patient is already draped for surgery.
surgery-drape-missing = The surgical drape is no longer available.
surgery-procedure-invalid = The surgical procedure is no longer valid.
surgery-busy = You are already doing something.
surgery-nothing-to-tend = {CAPITALIZE(THE($target))} has no wounds that this procedure can treat.

## Guidebook
guide-entry-surgery = Surgery

## Tool Qualities
tool-quality-retracting-name = Retracting
tool-quality-retracting-tool-name = Retractor
tool-quality-clamping-name = Clamping
tool-quality-clamping-tool-name = Hemostat
tool-quality-cauterizing-name = Cauterizing
tool-quality-cauterizing-tool-name = Cautery
tool-quality-drilling-name = Drilling
tool-quality-drilling-tool-name = Drill
tool-quality-bonesetting-name = Bone Setting
tool-quality-bonesetting-tool-name = Bone Setter
tool-quality-draping-name = Draping
tool-quality-draping-tool-name = Drape

## Procedure Names
surgery-procedure-tend-wounds-brute = Tend Wounds (Brute)
surgery-procedure-tend-wounds-burn = Tend Wounds (Burn)
surgery-procedure-organ-manipulation = Organ Manipulation
surgery-procedure-wound-repair-fracture = Set Fractures
surgery-procedure-wound-repair-burn = Treat Burns

## Procedure Categories
surgery-category-wound-repair = Wound Repair
surgery-category-tend-wounds = Tend Wounds
surgery-category-organ-manipulation = Organ Manipulation
surgery-category-implants = Implants
surgery-category-advanced = Advanced

## Interrupt
surgery-interrupt-patient = Your surgery is cut short as you move!
surgery-interrupt-surgeon = {CAPITALIZE(THE($target))} moves! The procedure is ruined.
