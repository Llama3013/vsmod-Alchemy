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
        private bool particlesSpawned;

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
        }

        protected override void ImpactOnEntity(Entity target)
        {
            SpawnParticlesClientSide();
            base.ImpactOnEntity(target);
            ApplyPotionSplash();
        }

        public override void OnCollided()
        {
            SpawnParticlesClientSide();
            ApplyPotionSplash();
            base.OnCollided
        }

        protected override void IsColliding(EntityPos pos, double impactSpeed)
        {
            if (impactSpeed < 0.05)
                return;
            SpawnParticlesClientSide();
            ApplyPotionSplash();
        }

        private void SpawnParticlesClientSide()
        {
            if (World.Side != EnumAppSide.Client || particlesSpawned)
                return;
            particlesSpawned = true;
            SpawnSplashParticles();
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

            World.PlaySoundAt(breakSound, Pos.X, Pos.Y, Pos.Z, null, true, 16f, 1f);

            ApplyContentsEffect();

            Die(EnumDespawnReason.Death);
        }

        private void ApplyContentsEffect()
        {
            if (ProjectileStack?.Collectible is not BlockLiquidContainerBase container)
                return;

            ItemStack contentStack = container.GetContent(ProjectileStack);
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

            if (!PotionConsumableLogic.IsCoatingAllowed(potionId))
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
            {
                WeaponCoatEffects.Apply(potionId, entity, multiplier, displayName);
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

        public override void OnEntityDespawn(EntityDespawnData despawn)
        {
            SpawnParticlesClientSide();

            base.OnEntityDespawn(despawn);
        }

        private void SpawnSplashParticles()
        {
            if (Api is not ICoreClientAPI capi)
                return;

            int color = GetLiquidColor(capi);

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

        // Need to resolve the average colour of the potion liquid's texture so the splash matches what
        // the flask was carrying. Only valid client-side. Seems to be wrong colour on certain potions
        private int GetLiquidColor(ICoreClientAPI capi)
        {
            int fallback = ColorUtil.ToRgba(160, 230, 230, 230);

            ItemStack flaskStack = ProjectileStack;
            if (flaskStack == null)
                return fallback;

            flaskStack.ResolveBlockOrItem(World);
            if (flaskStack.Collectible is not BlockLiquidContainerBase container)
                return fallback;

            ItemStack content = container.GetContent(flaskStack);
            WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(
                content
            );
            if (props?.Texture == null)
                return fallback;

            capi.BlockTextureAtlas.GetOrInsertTexture(
                props.Texture,
                out _,
                out TextureAtlasPosition texPos
            );
            if (texPos == null)
                return fallback;

            return ColorUtil.ToRgba(
                160,
                ColorUtil.ColorR(texPos.AvgColor),
                ColorUtil.ColorG(texPos.AvgColor),
                ColorUtil.ColorB(texPos.AvgColor)
            );
        }
    }
}
