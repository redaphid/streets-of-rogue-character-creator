# SoR character body format (measured) + the wizard art

## The real format (extracted from the game with UnityPy, verified by eye)
A Streets of Rogue character's on-screen identity is a set of tiny tk2d sprites,
keyed by character name in `GameResources`:

| sprite key      | size (px) | role |
|-----------------|-----------|------|
| `<Name>S`       | **44×36** | the front-facing character sprite — the character-select portrait / base body (in `bodyDic`) |
| `G_<Name>S`     | 44×36     | grayscale version the game recolors via the tint system (in `bodyGDic`) |
| `<Name>SE`      | ~40×36    | a South-East facing directional variant |

The in-world WALKING body is a SEPARATE, SHARED 8-direction set (`Body`,
`BodyN/NE/E/SE/S/SW/W/NW`, each 44×36) plus composited HEAD + hair/facial layers.
Characters reuse that shared rig; their identity lives mostly in the `<Name>S`
sprite + head. (Reference template: the **Vampire** body, the wizard's `baseBody`.)

Style: chunky, big-head / small-body (~2 heads tall), front-facing/billboarded,
bold dark outline, flat solid colors, very limited palette. At 44×36 characters
read as a few colored blocks (the Cop is a blue blob with a yellow badge).

## What we generated (this delivery)
- **`WizardS.png`** (44×36, alpha) — the character-select portrait: a frail
  purple-hooded arcane wizard, matched to SoR proportions/palette/framing.
- **`G_WizardS.png`** (44×36, alpha) — grayscale version for the game's tint system.

Made with ComfyUI (`generate.py`): SD1.5 `dreamshaper_8` + skormino pixel LoRA,
txt2img with IPAdapter 'style transfer' anchored on the vanilla `VampireS`/`CopS`/
`DoctorS` portraits (locks SoR chunky-character proportions + palette), rendered
at 512 then background-removed, autocropped, scaled to a 20×30 figure centered
with headroom in a 44×36 transparent canvas, and palette-quantized to 14 colors
for crisp flat pixels. Winner: seed 3303, denoise 1.0, IP weight 0.8.

## What is NOT delivered (honest scope)
Only the **portrait / `S` sprite** is generated — the highest-value, single-sprite
piece (what you see on the select screen). A full custom **in-world animated body**
would require the whole 8-direction walk set drawn as CONSISTENT frames (same
character across N/NE/E/SE/S/SW/W/NW), plus matching head/hair layers — a
frame-by-frame spritesheet job that single-image generation can't do coherently.
Until then the wizard can keep reusing the shared body rig (baseBody="Vampire")
for walking, with `WizardS` as its portrait/identity.
