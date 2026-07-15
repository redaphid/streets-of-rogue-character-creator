using System;
using System.Collections.Generic;
using UnityEngine;

namespace CharacterCreator
{
    // Which tk2d sprite collection (and GameResources dictionary) a custom sprite
    // integrates into. Mirrors the RogueLibs SpriteScope, trimmed to what a custom
    // character needs.
    public enum SpriteScope
    {
        Items,   // ability/item icons  -> "Items" collection + gr.itemDic
        Bodies,  // character body/portrait -> "Bodies" collection + gr.bodyDic
        Agents,  // agent head/eyes      -> "Agents" collection + gr.head/eyesDic
    }

    // A standalone port of RogueLibs' RogueSprite tk2d injection (RogueLibsCore/
    // Sprites/RogueSprite.cs), with NO dependency on the RogueLibs DLL. It does the
    // "done right" thing WizardMod/old CharacterCreator skipped: besides writing the
    // Unity UI Sprite into a GameResources dictionary (what the HUD Image reads), it
    // also builds a real tk2dSpriteDefinition and appends it to the matching tk2d
    // collection, so every tk2d renderer (the special-ability indicator, world
    // spawns) finds the sprite by name too - not just the inventory Image.
    //
    // For Bodies/Agents it ALSO writes the GameResources dictionary entries that
    // RogueLibs left commented out (its SpriteScope.Agents was a no-op) - the exact
    // gap that stops a custom character from wearing its own body art. Solving it
    // here is what makes the Wizard independent of a Vampire string-alias reskin.
    public sealed class CustomSprite
    {
        public readonly string Name;
        public readonly SpriteScope Scope;
        public readonly float Ppu;
        public readonly Texture2D Texture;
        public readonly Sprite Sprite; // the Unity UI sprite the HUD Image renders

        private bool grWritten;
        private bool tk2dAdded;

        // Collections captured from the game as tk2d loads them, and the queue of
        // sprites created before their collection existed (eager injection against a
        // not-yet-loaded collection is a silent no-op - the RogueLibs lesson).
        private static readonly Dictionary<SpriteScope, tk2dSpriteCollectionData> Collections =
            new Dictionary<SpriteScope, tk2dSpriteCollectionData>();
        private static readonly Dictionary<SpriteScope, List<CustomSprite>> Pending =
            new Dictionary<SpriteScope, List<CustomSprite>>();
        private static readonly Dictionary<string, CustomSprite> ByName =
            new Dictionary<string, CustomSprite>();

        private CustomSprite(string name, SpriteScope scope, Texture2D texture, float ppu)
        {
            Name = name;
            Scope = scope;
            Ppu = ppu;
            Texture = texture;
            // Full-texture menu sprite (RogueSprite.CreateSprite). RogueLibs Y-flips a
            // sub-region because Unity's origin is bottom-left and tk2d's is top-left;
            // for a full texture that flip is the identity, so a plain rect is correct.
            Sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                                   new Vector2(0.5f, 0.5f), ppu);
            Sprite.name = name;
        }

        // Creates and (immediately or when its collection loads) integrates a sprite
        // from PNG bytes. Idempotent by name.
        public static CustomSprite Create(string name, SpriteScope scope, byte[] png, float ppu = 64f)
        {
            if (ByName.TryGetValue(name, out CustomSprite existing)) return existing;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false) { name = name };
            tex.LoadImage(png);
            tex.filterMode = FilterMode.Point;

            var sprite = new CustomSprite(name, scope, tex, ppu);
            ByName[name] = sprite;
            sprite.Define();
            return sprite;
        }

        // Idempotent and retryable: writes the GameResources UI sprite as soon as
        // GameResources exists (the HUD reads that), and appends the tk2d definition
        // as soon as the target collection has been captured. Callers may re-invoke
        // safely to pick up state that wasn't ready on an earlier pass.
        public void Define()
        {
            WriteGameResources();
            if (!tk2dAdded)
            {
                if (Collections.TryGetValue(Scope, out tk2dSpriteCollectionData coll) && coll != null)
                    AddToCollection(coll);
                else
                    Enqueue(this);
            }
        }

        public static void Redefine(string name)
        {
            if (ByName.TryGetValue(name, out CustomSprite s)) s.Define();
        }

        private static void Enqueue(CustomSprite s)
        {
            if (!Pending.TryGetValue(s.Scope, out List<CustomSprite> list))
                Pending[s.Scope] = list = new List<CustomSprite>();
            if (!list.Contains(s)) list.Add(s);
        }

        // The GameResources side: the Unity Sprite the UI / name lookups read.
        // For Bodies/Agents these are exactly the writes RogueLibs left commented out
        // (its SpriteScope.Agents no-op) - writing them is what lets a custom
        // character wear its own art rather than alias an existing body.
        private void WriteGameResources()
        {
            if (grWritten) return;
            GameResources gr = GameController.gameController != null
                ? GameController.gameController.gameResources : null;
            if (gr == null) return;
            switch (Scope)
            {
                case SpriteScope.Items:
                    gr.itemDic[Name] = Sprite; gr.itemList.Add(Sprite); break;
                case SpriteScope.Bodies:
                    gr.bodyDic[Name] = Sprite; gr.bodyList.Add(Sprite); break;
                case SpriteScope.Agents:
                    gr.headDic[Name] = Sprite; gr.headList.Add(Sprite);
                    gr.eyesDic[Name] = Sprite; gr.eyesList.Add(Sprite); break;
            }
            grWritten = true;
        }

        // The tk2d side: a real definition appended to the collection so tk2d
        // renderers (the special-ability indicator, world spawns) resolve the name.
        private void AddToCollection(tk2dSpriteCollectionData coll)
        {
            if (tk2dAdded) return;
            float scale = 64f / Ppu / coll.invOrthoSize / coll.halfTargetHeight;
            tk2dSpriteDefinition def = CreateDefinition(Texture, scale);
            def.name = Name;
            AddDefinition(coll, def);
            tk2dAdded = true;
            Plugin.Log.LogInfo("CustomSprite '" + Name + "' added to tk2d collection '" + coll.name + "'.");
        }

        // Harmony postfix target: called by the game as each tk2d collection loads.
        // Captures the collections we care about and flushes any sprites that were
        // created before their collection existed.
        public static void RegisterCollection(tk2dSpriteCollectionData data)
        {
            if (data == null) return;
            SpriteScope scope;
            switch (data.name)
            {
                case "Items": scope = SpriteScope.Items; break;
                case "Bodies": scope = SpriteScope.Bodies; break;
                case "Agents": scope = SpriteScope.Agents; break;
                default: return;
            }
            Collections[scope] = data;
            if (Pending.TryGetValue(scope, out List<CustomSprite> list) && list.Count > 0)
            {
                foreach (CustomSprite s in list.ToArray())
                {
                    s.WriteGameResources();
                    s.AddToCollection(data);
                }
                list.Clear();
            }
        }

        // ---- the ported tk2d primitives (RogueSprite.CreateDefinition/AddDefinition) ----

        private static tk2dSpriteDefinition CreateDefinition(Texture2D texture, float scale)
        {
            var region = new Rect(0f, 0f, texture.width, texture.height);
            Vector2 anchor = region.center - region.position;
            var texDim = new Vector2(texture.width, texture.height);

            const float epsilon = 0.001f;
            Vector2 uvTopLeft = new Vector2(region.xMin + epsilon, texDim.y - region.yMax - epsilon) / texDim;
            Vector2 uvBottomRight = new Vector2(region.xMax - epsilon, texDim.y - region.yMin + epsilon) / texDim;

            Vector3 posBottomLeft = new Vector3(-anchor.x, anchor.y - region.height) * scale;
            Vector3 posTopRight = new Vector3(region.width - anchor.x, anchor.y) * scale;
            Vector3 b = new Vector3(-anchor.x, anchor.y - region.height) * scale;
            Vector3 a = new Vector3(region.width - anchor.x, anchor.y) * scale;

            var mat = new Material(Shader.Find("tk2d/BlendVertexColor")) { name = texture.name, mainTexture = texture };
            return new tk2dSpriteDefinition
            {
                material = mat,
                materialInst = mat,
                flipped = tk2dSpriteDefinition.FlipMode.None,
                extractRegion = false,
                colliderType = tk2dSpriteDefinition.ColliderType.Unset,
                positions = new[]
                {
                    new Vector3(posBottomLeft.x, posBottomLeft.y),
                    new Vector3(posTopRight.x, posBottomLeft.y),
                    new Vector3(posBottomLeft.x, posTopRight.y),
                    new Vector3(posTopRight.x, posTopRight.y),
                },
                uvs = new[]
                {
                    new Vector2(uvTopLeft.x, uvTopLeft.y),
                    new Vector2(uvBottomRight.x, uvTopLeft.y),
                    new Vector2(uvTopLeft.x, uvBottomRight.y),
                    new Vector2(uvBottomRight.x, uvBottomRight.y),
                },
                normals = Array.Empty<Vector3>(),
                tangents = Array.Empty<Vector4>(),
                indices = new[] { 0, 3, 1, 2, 3, 0 },
                boundsData = new[] { (a + b) / 2f, a - b },
                untrimmedBoundsData = new[] { (a + b) / 2f, a - b },
                texelSize = new Vector2(scale, scale),
            };
        }

        private static void AddDefinition(tk2dSpriteCollectionData coll, tk2dSpriteDefinition def)
        {
            var newDefs = new tk2dSpriteDefinition[coll.spriteDefinitions.Length + 1];
            Array.Copy(coll.spriteDefinitions, newDefs, coll.spriteDefinitions.Length);
            newDefs[newDefs.Length - 1] = def;
            coll.spriteDefinitions = newDefs;

            var newMats = new Material[coll.materials.Length + 1];
            Array.Copy(coll.materials, newMats, coll.materials.Length);
            newMats[newMats.Length - 1] = def.material;
            coll.materials = newMats;

            var newTex = new Texture[coll.textures.Length + 1];
            Array.Copy(coll.textures, newTex, coll.textures.Length);
            newTex[newTex.Length - 1] = def.material.mainTexture;
            coll.textures = newTex;

            // Without these four the new name is never found by GetSpriteIdByName.
            coll.inst.materialIdsValid = false;
            coll.InitMaterialIds();
            coll.ClearDictionary();
            coll.InitDictionary();
        }
    }
}
