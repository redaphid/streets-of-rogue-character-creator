#!/usr/bin/env python3
# Wizard character-sprite generator: img2img from vanilla VampireS + IPAdapter on
# SoR character portraits, restyled to a frail purple-robed wizard. SoR char format:
# <Name>S = 44x36 front-facing character sprite. Generate at 512, downscale nearest to 44x36.
import json, sys, time, urllib.request, os, io
HOST="http://localhost:8188"; HERE=os.path.dirname(os.path.abspath(__file__))
CKPT="dreamshaper_8.safetensors"
LORA="pixel_art_style_by_skormino_v7.05_test_72img.safetensors"
def _post(p,pl): return json.load(urllib.request.urlopen(urllib.request.Request(HOST+p,json.dumps(pl).encode(),{"Content-Type":"application/json"})))
def upload(path):
    import mimetypes,uuid
    b=open(path,'rb').read(); bnd="----wz"+uuid.uuid4().hex; fn=os.path.basename(path)
    body=(f'--{bnd}\r\nContent-Disposition: form-data; name="image"; filename="{fn}"\r\n'
          f'Content-Type: image/png\r\n\r\n').encode()+b+f'\r\n--{bnd}--\r\n'.encode()
    r=urllib.request.Request(HOST+"/upload/image",body,{"Content-Type":f"multipart/form-data; boundary={bnd}"})
    return json.load(urllib.request.urlopen(r))["name"]
POS=("16-bit SNES pixel art game sprite, tiny super deformed chibi wizard, "
     "EXTREMELY big round head, tiny short stubby body, two heads tall, squat proportions, "
     "bright deep purple hooded robe and pointed purple wizard hat, pale round face, small white beard, "
     "single character standing front facing the camera, symmetrical, bold thick dark outline, "
     "flat solid colors, very limited palette, chunky pixel blob character, "
     "cute simple mascot, plain flat white background")
NEG=("photorealistic, 3d render, anime, painterly, smooth gradient, soft shading, realistic, "
     "hi-res, text, watermark, blurry, jpeg artifacts, full scene, background scenery, multiple "
     "characters, two figures, side view, three-quarter, overhead, tall, realistic proportions, "
     "long legs, detailed anatomy, slim, lanky, holding staff, weapon, sword, blue robe")
def graph(seed, denoise, ipw, refs, init):
    g={"1":{"class_type":"CheckpointLoaderSimple","inputs":{"ckpt_name":CKPT}},
       "11":{"class_type":"LoraLoader","inputs":{"model":["1",0],"clip":["1",1],"lora_name":LORA,"strength_model":1.0,"strength_clip":1.0}},
       "2":{"class_type":"CLIPTextEncode","inputs":{"clip":["11",1],"text":POS}},
       "3":{"class_type":"CLIPTextEncode","inputs":{"clip":["11",1],"text":NEG}},
       "6":{"class_type":"VAEDecode","inputs":{"samples":["5",0],"vae":["1",2]}},
       }
    if init and init!="-":
        g["30"]={"class_type":"LoadImage","inputs":{"image":upload(init)}}
        g["4"]={"class_type":"VAEEncode","inputs":{"pixels":["30",0],"vae":["1",2]}}
    else:
        g["4"]={"class_type":"EmptyLatentImage","inputs":{"width":512,"height":512,"batch_size":1}}
    model=["11",0]
    g["8"]={"class_type":"IPAdapterUnifiedLoader","inputs":{"model":["1",0],"preset":"STANDARD (medium strength)"}}
    for i,r in enumerate(refs): g[str(20+i)]={"class_type":"LoadImage","inputs":{"image":upload(r)}}
    node=["20",0]
    for i in range(1,len(refs)):
        b=str(40+i); g[b]={"class_type":"ImageBatch","inputs":{"image1":node,"image2":[str(20+i),0]}}; node=[b,0]
    g["9"]={"class_type":"IPAdapterAdvanced","inputs":{"model":["8",0],"ipadapter":["8",1],"image":node,
            "weight":ipw,"weight_type":"style transfer","combine_embeds":"average","start_at":0.0,"end_at":0.9,"embeds_scaling":"V only"}}
    model=["9",0]
    g["5"]={"class_type":"KSampler","inputs":{"model":model,"positive":["2",0],"negative":["3",0],"latent_image":["4",0],
            "seed":seed,"steps":28,"cfg":7.0,"sampler_name":"dpmpp_2m","scheduler":"karras","denoise":denoise}}
    g["10"]={"class_type":"Image Rembg (Remove Background)","inputs":{"images":["6",0],"model":"isnet-general-use","transparency":True,"alpha_matting":False,"alpha_matting_foreground_threshold":240,"alpha_matting_background_threshold":20,"alpha_matting_erode_size":4,"post_processing":True,"only_mask":False,"background_color":"none"}}
    g["7"]={"class_type":"SaveImage","inputs":{"images":["10",0],"filename_prefix":"wiz"}}
    return g
def run(name, seed, denoise, ipw, refs, init):
    pid=_post("/prompt",{"prompt":graph(seed,denoise,ipw,refs,init)})["prompt_id"]
    for _ in range(600):
        time.sleep(1); h=json.load(urllib.request.urlopen(f"{HOST}/history/{pid}"))
        if pid in h:
            e=h[pid]
            if e.get("status",{}).get("status_str")=="error": raise SystemExit(json.dumps(e["status"])[:1500])
            for o in e["outputs"].values():
                if "images" in o:
                    im=o["images"][0]
                    data=urllib.request.urlopen(f"{HOST}/view?filename={im['filename']}&subfolder={im.get('subfolder','')}&type={im['type']}").read()
                    open(f"{HERE}/{name}_512.png","wb").write(data); print("wrote",name,"512"); return
    raise SystemExit("timeout")
if __name__=="__main__":
    ref=lambda n: f"{HERE}/../_wizref/{n}.png"
    run(sys.argv[1], int(sys.argv[2]), float(sys.argv[3]), float(sys.argv[4]),
        [ref("VampireS"),ref("CopS"),ref("DoctorS")], (sys.argv[5] if len(sys.argv)>5 else "-"))
