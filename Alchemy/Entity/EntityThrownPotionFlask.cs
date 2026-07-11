using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class EntityThrownPotionFlask : EntityProjectile
    {
        private bool splashApplied;
        private bool inLiquid;
        private float liquidRestAccum;

        public override bool ApplyGravity => !Stuck && !inLiquid;

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging)
        {
            base.OnTesselation(ref entityShape, shapePathForLogging);

            if (Api is not ICoreClientAPI capi || ProjectileStack == null)
                return;

            ProjectileStack.ResolveBlockOrItem(World);
            Block block = ProjectileStack.Block;
            if (block == null)
                return;

            IDictionary<string, CompositeTexture> textures = Properties.Client.Textures;
            foreach (var val in block.Textures)
            {
                CompositeTexture ownTex = val.Value.Clone();
                textures[val.Key] = ownTex;
                ownTex.Bake(Api.Assets);
                capi.EntityTextureAtlas.GetOrInsertTexture(
                    ownTex.Baked.TextureFilenames[0],
                    out int textureSubid,
                    out _
                );
                ownTex.Baked.TextureSubId = textureSubid;
            }

            UseLiquidShape(capi, ref entityShape, textures);
        }

        private void UseLiquidShape(
            ICoreClientAPI capi,
            ref Shape entityShape,
            IDictionary<string, CompositeTexture> textures
        )
        {
            if (ProjectileStack.Collectible is not BlockLiquidContainerBase container)
                return;

            ItemStack contentStack = container.GetContent(ProjectileStack);
            if (contentStack == null || contentStack.StackSize <= 0)
                return;

            WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(
                contentStack
            );
            if (props?.Texture == null)
                return;

            string liquidShapeLoc = Properties.Attributes?["liquidShapeLoc"].AsString();
            if (string.IsNullOrEmpty(liquidShapeLoc))
                return;

            Shape liquidShape = Shape.TryGet(capi, liquidShapeLoc);
            if (liquidShape == null)
                return;

            entityShape = liquidShape;

            CompositeTexture contentTex = props.Texture.Clone();
            textures["content"] = contentTex;
            contentTex.Bake(capi.Assets);
            capi.EntityTextureAtlas.GetOrInsertTexture(
                contentTex.Baked.TextureFilenames[0],
                out int subId,
                out _
            );
            contentTex.Baked.TextureSubId = subId;
        }

        public override void OnGameTick(float dt)
        {
            base.OnGameTick(dt);
            if (!inLiquid || ShouldDespawn)
                return;

            float drag = (float)Math.Pow(0.9, dt);
            Pos.Motion.X *= drag;
            Pos.Motion.Y *= drag;
            Pos.Motion.Z *= drag;

            Pos.Motion.Y -= dt * 0.008f;

            if (World.Side != EnumAppSide.Server)
                return;

            if (Pos.Motion.LengthSq() < 0.015 * 0.015)
            {
                liquidRestAccum += dt;
                if (liquidRestAccum > 3f)
                {
                    ProjectileStack?.ResolveBlockOrItem(World);
                    if (ProjectileStack != null)
                        World.SpawnItemEntity(ProjectileStack, Pos.XYZ);
                    Die();
                }
            }
            else
            {
                liquidRestAccum = 0f;
            }
        }

        public override void OnCollideWithLiquid()
        {
            inLiquid = true;
            Pos.Motion.X *= 0.5f;
            Pos.Motion.Y *= 0.5f;
            Pos.Motion.Z *= 0.5f;
            base.OnCollideWithLiquid();
        }

        protected override void ImpactOnEntity(Entity target)
        {
            base.ImpactOnEntity(target);
            ApplyPotionSplash();
        }

        public override void OnCollided()
        {
            if (inLiquid && Math.Max(motionBeforeCollide.Length(), Pos.Motion.Length()) < 0.2)
                return;
            ApplyPotionSplash();
        }

        protected override void IsColliding(EntityPos pos, double impactSpeed)
        {
            if (impactSpeed < 0.05)
                return;
            ApplyPotionSplash();
        }

        private void ApplyPotionSplash()
        {
            if (splashApplied)
                return;
            splashApplied = true;

            if (World.Side != EnumAppSide.Server)
                return;

            AssetLocation breakSound =
                (ProjectileStack?.Collectible as Block)?.Sounds?.Break.Location
                ?? new AssetLocation("game:sounds/block/glass");

            World.PlaySoundAt(
                breakSound,
                Pos.X,
                Pos.Y,
                Pos.Z,
                null,
                true,
                16f,
                inLiquid ? 0.4f : 1f
            );

            if (!inLiquid)
            {
                ItemStack contentStack = GetContentStack();
                if (contentStack == null)
                {
                    Die(EnumDespawnReason.Death);
                    return;
                }
                SpawnSplashParticles(GetSplashColor(contentStack));
                ApplyWetness();
                ApplyContentsEffect(contentStack);
            }

            Die(EnumDespawnReason.Death);
        }

        private ItemStack GetContentStack()
        {
            if (ProjectileStack?.Collectible is not BlockLiquidContainerBase container)
                return null;
            return container.GetContent(ProjectileStack);
        }

        private static int GetSplashColor(ItemStack contentStack)
        {
            int fallback = ColorUtil.ToRgba(160, 230, 230, 230);
            if (contentStack?.Collectible?.Attributes == null)
                return fallback;

            int[] rgb = contentStack.Collectible.Attributes["particleColor"].AsArray<int>();
            if (rgb == null || rgb.Length < 3)
                return fallback;

            return ColorUtil.ToRgba(160, rgb[0], rgb[1], rgb[2]);
        }

        private void SpawnSplashParticles(int color)
        {
            SimpleParticleProperties particles = new(
                40,
                80,
                color,
                Pos.XYZ.AddCopy(-0.5, 0, -0.5),
                Pos.XYZ.AddCopy(0.5, 0.3, 0.5),
                new Vec3f(-1.5f, 0.5f, -1.5f),
                new Vec3f(1.5f, 2.5f, 1.5f),
                1.5f,
                0.05f,
                0.1f,
                0.5f,
                EnumParticleModel.Quad
            )
            {
                OpacityEvolve = EvolvingNatFloat.create(EnumTransformFunction.LINEARREDUCE, 200f),
                ShouldDieInLiquid = true,
            };
            World.SpawnParticles(particles);
        }

        private void ApplyContentsEffect(ItemStack contentStack)
        {
            if (contentStack == null)
                return;

            if (
                !PotionConsumableLogic.TryReadPotionInfo(
                    contentStack,
                    out string potionId,
                    out string strength
                )
            )
                return;

            if (!PotionConsumableLogic.IsThrowableAllowed(potionId))
                return;

            float multiplier =
                AlchemyConfig.Loaded.ThrowableFlaskEffectMultiplier
                * PotionConsumableLogic.GetStrengthMultiplier(strength);
            string displayName = ResolveDisplayName(contentStack);

            float radius = AlchemyConfig.Loaded.ThrowableFlaskSplashRadius;
            Entity[] targets = World.GetEntitiesAround(
                Pos.XYZ,
                radius,
                radius,
                e => e is EntityAgent && e.Alive
            );

            foreach (Entity entity in targets)
                WeaponCoatEffects.Apply(potionId, entity, multiplier, displayName);
        }

        private void ApplyWetness()
        {
            if (World.Side != EnumAppSide.Server)
                return;

            float radius = AlchemyConfig.Loaded.ThrowableFlaskSplashRadius;
            Entity[] targets = World.GetEntitiesAround(
                Pos.XYZ,
                radius,
                radius,
                e => e is EntityAgent && e.Alive
            );

            foreach (Entity entity in targets)
            {
                float current = entity.WatchedAttributes.GetFloat("wetness");
                entity.WatchedAttributes.SetFloat("wetness", Math.Min(1f, current + 0.35f));
            }
        }

        private static string ResolveDisplayName(ItemStack contentStack)
        {
            CollectibleObject col = contentStack?.Collectible;
            if (col?.Code == null)
                return "";
            string typePrefix = col is Block ? "block" : "item";
            return Lang.Get($"{col.Code.Domain}:{typePrefix}-{col.Code.Path}");
        }
    }
}
