#!/usr/bin/env python3
"""Character-creator asset generator — drives ComfyUI's HTTP API to make Streets
of Rogue-styled character art from a prompt.

Two asset kinds, matching the two things a custom character needs:
  icon  -> a 64x64 ability icon (an isolated object on white, background removed)
  body  -> a 44x36 front-facing character portrait ("<Name>S" sprite), background
           removed, autocropped and centred with headroom, palette-quantized

Both share the calibrated Streets of Rogue pixel-art style (dreamshaper_8 +
skormino pixel LoRA, IPAdapter 'style transfer' onto vanilla SoR reference crops)
from the gm spooky-swamp pipeline, retuned per kind. Every job is deterministic
(fixed seed).

Usage:
  scripts/generate-assets.py icon OUT.png "a glowing magic orb and wand" [--seed N]
  scripts/generate-assets.py body OUT.png "frail purple-robed wizard with a beard" [--seed N]
  # IPAdapter refs for bodies default to $CC_BODY_REFS (colon-separated vanilla
  # portrait PNGs); icons use no refs by default (--refs to add).

The refs are vanilla SoR crops used ONLY as IPAdapter style anchors and are never
redistributed - keep them out of the repo (point CC_BODY_REFS at a local dir).
"""
import argparse, json, os, time, urllib.request, uuid
from PIL import Image

HOST = os.environ.get("COMFY", "http://localhost:8188")
CKPT = os.environ.get("CKPT", "dreamshaper_8.safetensors")
LORA = os.environ.get("LORA", "pixel_art_style_by_skormino_v7.05_test_72img.safetensors")
SIZE = 512
STEPS, CFG, SAMPLER, SCHED = 28, 7.0, "dpmpp_2m", "karras"

# Shared style scaffold. {subject} is the caller's description; the rest locks the
# SoR chunky-pixel look. Icons and bodies differ mainly in camera + background.
STYLE = ("16-bit SNES pixel art, {subject}, {frame}, crisp hand-drawn game sprite, "
         "bold thick dark outline, flat solid colors, very limited palette, chunky "
         "readable shapes, {bg}")
NEG_BASE = ("photorealistic, 3d render, anime, manga, painterly, smooth gradient, soft "
            "shading, realistic, hi-res, text, watermark, signature, blurry, jpeg "
            "artifacts, duplicate, two copies, multiple objects side by side")

# Icons are inanimate objects (the ChaosMagic 'hat' bug: the icon read as a wizard
# HAT, so hats/cones are hard-negatives here).
ICON_FRAME = "single isolated object centered, arcane spell / magic item icon"
ICON_BG = "plain flat white background"
ICON_NEG = ("hat, wizard hat, pointed hat, cone, witch hat, cap, person, figure, face, "
            "head, character, creature, hand, arm, landscape, scenery")

# Bodies are billboarded chibi characters (SoR proportions: huge head, stubby body).
BODY_FRAME = ("tiny super deformed chibi character, EXTREMELY big round head, short stubby "
              "body, two heads tall, single character standing front facing the camera, symmetrical")
BODY_BG = "single isolated character centered on plain flat white background, full body"
BODY_NEG = ("full scene, background scenery, multiple characters, two figures, side view, "
            "three-quarter, overhead, tall, realistic proportions, long legs, slim, lanky, "
            "person, hologram")


def _post(path, payload):
    req = urllib.request.Request(HOST + path, json.dumps(payload).encode(),
                                 {"Content-Type": "application/json"})
    return json.load(urllib.request.urlopen(req))


def _upload(path):
    name = os.path.basename(path)
    bnd = "----cc" + uuid.uuid4().hex
    body = (f'--{bnd}\r\nContent-Disposition: form-data; name="image"; filename="{name}"\r\n'
            f'Content-Type: image/png\r\n\r\n').encode() + open(path, "rb").read() + \
           f"\r\n--{bnd}--\r\n".encode()
    req = urllib.request.Request(HOST + "/upload/image", body,
                                 {"Content-Type": f"multipart/form-data; boundary={bnd}"})
    return json.load(urllib.request.urlopen(req))["name"]


def _graph(subject, frame, bg, neg, seed, refs, ipw):
    pos = STYLE.format(subject=subject, frame=frame, bg=bg)
    g = {
        "1": {"class_type": "CheckpointLoaderSimple", "inputs": {"ckpt_name": CKPT}},
        "11": {"class_type": "LoraLoader", "inputs": {"model": ["1", 0], "clip": ["1", 1],
               "lora_name": LORA, "strength_model": 1.0, "strength_clip": 1.0}},
        "2": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["11", 1], "text": pos}},
        "3": {"class_type": "CLIPTextEncode", "inputs": {"clip": ["11", 1], "text": neg}},
        "6": {"class_type": "VAEDecode", "inputs": {"samples": ["5", 0], "vae": ["1", 2]}},
        "4": {"class_type": "EmptyLatentImage", "inputs": {"width": SIZE, "height": SIZE, "batch_size": 1}},
    }
    model = ["11", 0]
    if refs:
        g["8"] = {"class_type": "IPAdapterUnifiedLoader",
                  "inputs": {"model": ["1", 0], "preset": "STANDARD (medium strength)"}}
        for i, r in enumerate(refs):
            g[str(20 + i)] = {"class_type": "LoadImage", "inputs": {"image": _upload(r)}}
        node = ["20", 0]
        for i in range(1, len(refs)):
            bid = str(40 + i)
            g[bid] = {"class_type": "ImageBatch", "inputs": {"image1": node, "image2": [str(20 + i), 0]}}
            node = [bid, 0]
        g["9"] = {"class_type": "IPAdapterAdvanced",
                  "inputs": {"model": ["8", 0], "ipadapter": ["8", 1], "image": node, "weight": ipw,
                             "weight_type": "style transfer", "combine_embeds": "average",
                             "start_at": 0.0, "end_at": 0.9, "embeds_scaling": "V only"}}
        model = ["9", 0]
    g["5"] = {"class_type": "KSampler",
              "inputs": {"model": model, "positive": ["2", 0], "negative": ["3", 0],
                         "latent_image": ["4", 0], "seed": seed, "steps": STEPS, "cfg": CFG,
                         "sampler_name": SAMPLER, "scheduler": SCHED, "denoise": 1.0}}
    g["10"] = {"class_type": "Image Rembg (Remove Background)",
               "inputs": {"images": ["6", 0], "model": "isnet-general-use", "transparency": True,
                          "alpha_matting": False, "alpha_matting_foreground_threshold": 240,
                          "alpha_matting_background_threshold": 20, "alpha_matting_erode_size": 4,
                          "post_processing": True, "only_mask": False, "background_color": "none"}}
    g["7"] = {"class_type": "SaveImage", "inputs": {"images": ["10", 0], "filename_prefix": "ccgen"}}
    return g


def _render(subject, frame, bg, neg, seed, refs, ipw):
    pid = _post("/prompt", {"prompt": _graph(subject, frame, bg, neg, seed, refs, ipw)})["prompt_id"]
    for _ in range(600):
        time.sleep(1)
        h = json.load(urllib.request.urlopen(f"{HOST}/history/{pid}"))
        if pid in h:
            e = h[pid]
            if e.get("status", {}).get("status_str") == "error":
                raise SystemExit(json.dumps(e["status"], indent=1)[:2000])
            for o in e["outputs"].values():
                if "images" in o:
                    im = o["images"][-1]
                    data = urllib.request.urlopen(
                        f"{HOST}/view?filename={im['filename']}&subfolder={im.get('subfolder','')}"
                        f"&type={im['type']}").read()
                    return Image.open(__import__("io").BytesIO(data)).convert("RGBA")
    raise SystemExit("timeout")


def _autocrop(img):
    bbox = img.split()[-1].getbbox()
    return img.crop(bbox) if bbox else img


def gen_icon(out, subject, seed, refs, ipw):
    img = _render(subject, ICON_FRAME, ICON_BG, ICON_NEG + ", " + NEG_BASE, seed, refs, ipw)
    img = _autocrop(img)
    # Fit into a square then downscale to 64x64 with nearest (crisp pixels).
    side = max(img.size)
    sq = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    sq.paste(img, ((side - img.width) // 2, (side - img.height) // 2))
    sq.resize((64, 64), Image.NEAREST).save(out)
    print("wrote icon", out)


def gen_body(out, subject, seed, refs, ipw):
    img = _render(subject, BODY_FRAME, BODY_BG, BODY_NEG + ", " + NEG_BASE, seed, refs, ipw)
    img = _autocrop(img)
    # SoR <Name>S is 44x36 with the figure ~20x30 centred with headroom, bottom-aligned.
    fig = img.copy()
    fig.thumbnail((20, 30), Image.NEAREST)
    canvas = Image.new("RGBA", (44, 36), (0, 0, 0, 0))
    canvas.paste(fig, ((44 - fig.width) // 2, 36 - fig.height - 1), fig)
    # Quantize to a small flat palette (crisp SoR look), keep alpha.
    rgb = canvas.convert("RGB").quantize(colors=14, method=Image.MEDIANCUT).convert("RGB")
    rgb.putalpha(canvas.split()[-1])
    rgb.save(out)
    print("wrote body", out)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("kind", choices=["icon", "body"])
    ap.add_argument("out")
    ap.add_argument("subject")
    ap.add_argument("--seed", type=int, default=3303)
    ap.add_argument("--refs", default=os.environ.get("CC_BODY_REFS", ""))
    ap.add_argument("--ipw", type=float, default=None)
    a = ap.parse_args()
    refs = [r for r in a.refs.split(":") if r] if a.refs else []
    os.makedirs(os.path.dirname(os.path.abspath(a.out)), exist_ok=True)
    if a.kind == "icon":
        gen_icon(a.out, a.subject, a.seed, refs, a.ipw if a.ipw is not None else 0.6)
    else:
        gen_body(a.out, a.subject, a.seed, refs, a.ipw if a.ipw is not None else 0.8)


if __name__ == "__main__":
    main()
