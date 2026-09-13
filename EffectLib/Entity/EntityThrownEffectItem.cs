using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

#pragma warning disable IDE0130
namespace EffectLib
#pragma warning restore IDE0130
{
    // Generic "throw any effect item like a rock" projectile. Renders as whatever item/block was
    // thrown (same trick as vanilla EntityThrownItem), and on impact resolves that collectible's
    // own "effectinfo" attribute and applies it to every living entity in a splash radius.
    public class EntityThrownEffectItem : EntityProjectile
    {
        private bool impactApplied;

        public override void OnTesselation(ref Shape entityShape, string shapePathForLogging)
        {
            base.OnTesselation(ref entityShape, shapePathForLogging);

            if (Api is not ICoreClientAPI capi || ProjectileStack == null)
                return;

            ProjectileStack.ResolveBlockOrItem(World);
            CompositeShape srcShape = ProjectileStack.Class == EnumItemClass.Item
                ? ProjectileStack.Item.Shape
                : ProjectileStack.Block.Shape;
            if (srcShape == null)
                return;

            IDictionary<string, CompositeTexture> srcTextures = ProjectileStack.Class == EnumItemClass.Item
                ? ProjectileStack.Item.Textures
                : ProjectileStack.Block.Textures;

            entityShape = capi.TesselatorManager.GetCachedShape(srcShape.Base);

            IDictionary<string, CompositeTexture> textures = Properties.Client.Textures;
            foreach (KeyValuePair<string, CompositeTexture> val in srcTextures)
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
            base.ImpactOnEntity(target);
            ApplyImpact();
        }

        public override void OnCollided() => ApplyImpact();

        protected override void IsColliding(EntityPos pos, double impactSpeed)
        {
            if (impactSpeed < 0.05)
                return;
            ApplyImpact();
        }

        private void ApplyImpact()
        {
            if (impactApplied || World.Side != EnumAppSide.Server)
                return;
            impactApplied = true;

            ProjectileStack?.ResolveBlockOrItem(World);
            CollectibleObject collectible = ProjectileStack?.Collectible;
            JsonObject def = collectible?.Attributes?[EffectInfoKey];
            string effectId = def?.Exists == true ? def["effectId"].AsString()?.ToLowerInvariant() : null;

            if (string.IsNullOrWhiteSpace(effectId))
            {
                Die(EnumDespawnReason.Death);
                return;
            }

            if (!EffectRegistry.IsRegistered(effectId))
                JsonEffectDefinition.RegisterFrom(effectId, collectible.Code.Domain, def, collectible.Code);

            float radius = def["throwRadius"].AsFloat(3f);
            string displayName = EffectLang.Name(effectId);

            foreach (
                Entity target in World.GetEntitiesAround(
                    Pos.XYZ,
                    radius,
                    radius,
                    e => e is EntityAgent && e.Alive
                )
            )
                CoatedEffects.Apply(effectId, target, 1f, displayName);

            Die(EnumDespawnReason.Death);
        }

        private const string EffectInfoKey = "effectinfo";
    }
}
