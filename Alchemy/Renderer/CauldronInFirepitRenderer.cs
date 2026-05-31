using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.GameContent;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace Alchemy
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    public class CauldronInFirepitRenderer : IInFirepitRenderer, ITexPositionSource
    {
        public double RenderOrder => 0.5;
        public int RenderRange => 20;

        public Size2i AtlasSize =>
            usingItemAtlas ? capi.ItemTextureAtlas.Size : capi.BlockTextureAtlas.Size;
        public TextureAtlasPosition this[string textureCode] =>
            textureCode == "liquid" && liquidTexPos != null
                ? liquidTexPos
                : capi.BlockTextureAtlas.UnknownTexturePosition;

        private readonly ICoreClientAPI capi;
        private readonly BlockPos pos;
        private readonly BlockEntityFirepit firepit;
        private MultiTextureMeshRef cauldronMeshRef;
        private MultiTextureMeshRef liquidMeshRef;
        private MultiTextureMeshRef stickMeshRef;
        private TextureAtlasPosition liquidTexPos;
        private bool usingItemAtlas;
        private string lastLiquidCode;
        private int lastLiquidAmount = -1;
        private int currentLevel = 1;
        private readonly float capacityLitres;
        private const int LiquidLevels = 4;
        private readonly Matrixf ModelMat = new();

        private static readonly float[] LevelSurfaceY =
        [
            0f,
            5f / 16f,
            8f / 16f,
            10f / 16f,
            12f / 16f,
        ];
        private readonly float FirepitYOffset;
        private const float StirMinTemperature = 50f;
        private const float StirYOffset = 0.50f;
        private const float RestYOffset = 0.37f;
        private const float RestRadialOffsetX = -0.13f;
        private const float RestRadialOffsetZ = 0.03f;
        private const float RestXTiltDeg = -10f;
        private const float BubbleMinTemperature = 100f;
        private const float BubbleMaxTemperature = 200f;
        private const float BubbleMaxQuantity = 4f;
        private readonly AirBubbleParticles bubbleParticles = new()
        {
            Range = 0.4f,
            horVelocityMul = 0.3f,
            LifeLength = 0.3f,
        };
        private ILoadedSound cookingSound;
        private float temp;
        private readonly float stickBaseAngle;

        public CauldronInFirepitRenderer(
            ICoreClientAPI capi,
            ItemStack stack,
            BlockPos pos,
            BlockEntityFirepit firepit,
            bool drawCauldronMesh = true
        )
        {
            this.capi = capi;
            this.pos = pos;
            this.firepit = firepit;

            capacityLitres = stack.Block.Attributes?["cookingSlotCapacityLitres"].AsFloat(6f) ?? 6f;
            FirepitYOffset =
                stack.Block.Attributes?["inFirePitProps"]?["transform"]?["translation"]?[
                    "y"
                ].AsFloat(1f / 16f)
                ?? 1f / 16f;

            if (drawCauldronMesh)
            {
                capi.Tesselator.TesselateBlock(stack.Block, out MeshData cauldronMesh);
                cauldronMeshRef = capi.Render.UploadMultiTextureMesh(cauldronMesh);
            }

            ItemStack stickStack = (
                stack.Attributes["stirringSpoon"] as ItemstackAttribute
            )?.value?.Clone();
            if (stickStack != null)
            {
                stickStack.ResolveBlockOrItem(capi.World);
                if (stickStack.Item != null)
                {
                    capi.Tesselator.TesselateItem(stickStack.Item, out MeshData stickMesh);
                    stickMeshRef = capi.Render.UploadMultiTextureMesh(stickMesh);
                }
            }

            stickBaseAngle = stack.Attributes.GetInt("stirringSpoonFacing", 0) * GameMath.PIHALF;

            RebuildLiquidMesh();
        }

        private void RebuildLiquidMesh()
        {
            liquidMeshRef?.Dispose();
            liquidMeshRef = null;
            liquidTexPos = null;

            if (firepit.Inventory is not InventorySmelting inv)
                return;

            ItemStack liquidStack = null;
            float totalLitres = 0f;

            ItemSlot outputSlot = inv[2];
            if (
                !outputSlot.Empty
                && outputSlot.Itemstack?.Collectible?.Attributes?["waterTightContainerProps"].Exists
                    == true
            )
            {
                liquidStack = outputSlot.Itemstack;
                WaterTightContainableProps outProps = BlockLiquidContainerBase.GetContainableProps(
                    outputSlot.Itemstack
                );
                if (outProps != null && outProps.ItemsPerLitre > 0)
                    totalLitres = outputSlot.Itemstack.StackSize / outProps.ItemsPerLitre;
            }
            else
            {
                foreach (ItemSlot slot in inv.CookingSlots)
                {
                    if (
                        !slot.Empty
                        && slot.Itemstack
                            ?.Collectible
                            ?.Attributes
                            ?["waterTightContainerProps"]
                            .Exists == true
                    )
                    {
                        liquidStack ??= slot.Itemstack;
                        WaterTightContainableProps slotProps =
                            BlockLiquidContainerBase.GetContainableProps(slot.Itemstack);
                        if (slotProps != null && slotProps.ItemsPerLitre > 0)
                            totalLitres += slot.Itemstack.StackSize / slotProps.ItemsPerLitre;
                    }
                }
            }

            if (liquidStack == null)
                return;

            // I first attempt to find the liquid's waterTightContainerProps texture in the block atlas
            // and then fall back to the item's own baked texture in the item atlas.
            // If that also somehow doesn't work, I just use the UnknownTexture, which is better than crashing
            usingItemAtlas = false;
            WaterTightContainableProps props = BlockLiquidContainerBase.GetContainableProps(
                liquidStack
            );
            if (props?.Texture?.Baked != null)
            {
                liquidTexPos = capi.BlockTextureAtlas.Positions[props.Texture.Baked.TextureSubId];
            }
            else if (liquidStack.Item?.FirstTexture?.Baked != null)
            {
                liquidTexPos = capi.ItemTextureAtlas.Positions[
                    liquidStack.Item.FirstTexture.Baked.TextureSubId
                ];
                usingItemAtlas = true;
            }
            else
            {
                liquidTexPos = capi.BlockTextureAtlas.UnknownTexturePosition;
            }

            float fillRatio = capacityLitres > 0 ? totalLitres / capacityLitres : 0f;
            currentLevel = GameMath.Clamp(
                (int)Math.Ceiling(fillRatio * LiquidLevels),
                1,
                LiquidLevels
            );

            Shape liquidShape = Shape.TryGet(
                capi,
                $"alchemy:shapes/block/cauldron-liquid-{currentLevel}.json"
            );
            if (liquidShape == null)
                return;

            capi.Tesselator.TesselateShape(
                "cauldron-liquid",
                liquidShape,
                out MeshData liquidMesh,
                this
            );

            liquidMeshRef = capi.Render.UploadMultiTextureMesh(liquidMesh);
        }

#pragma warning disable CA1816 // Dispose methods should call SuppressFinalize
        public void Dispose()
#pragma warning restore CA1816 // Dispose methods should call SuppressFinalize
        {
            cauldronMeshRef?.Dispose();
            liquidMeshRef?.Dispose();
            stickMeshRef?.Dispose();

            cookingSound?.Stop();
            cookingSound?.Dispose();
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            IRenderAPI rpi = capi.Render;
            Vec3d camPos = capi.World.Player.Entity.CameraPos;

            rpi.GlDisableCullFace();
            rpi.GlToggleBlend(true);

            IStandardShaderProgram prog = rpi.PreparedStandardShader(pos.X, pos.Y, pos.Z);

            prog.DontWarpVertices = 0;
            prog.AddRenderFlags = 0;
            prog.RgbaAmbientIn = rpi.AmbientColor;
            prog.RgbaFogIn = rpi.FogColor;
            prog.FogMinIn = rpi.FogMin;
            prog.FogDensityIn = rpi.FogDensity;
            prog.RgbaTint = ColorUtil.WhiteArgbVec;
            prog.NormalShaded = 1;
            prog.ExtraGodray = 0;
            prog.SsaoAttn = 0;
            prog.AlphaTest = 0.05f;
            prog.OverlayOpacity = 0;

            if (cauldronMeshRef != null)
            {
                prog.ModelMatrix = ModelMat
                    .Identity()
                    .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                    .Translate(0f, FirepitYOffset, 0f)
                    .Values;

                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

                rpi.RenderMultiTextureMesh(cauldronMeshRef, "tex");
            }

            if (liquidMeshRef != null)
            {
                prog.ModelMatrix = ModelMat
                    .Identity()
                    .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                    .Translate(0f, FirepitYOffset, 0f)
                    .Values;

                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

                rpi.RenderMultiTextureMesh(liquidMeshRef, "tex");
            }

            if (stickMeshRef != null)
            {
                float stirAngle =
                    stickBaseAngle
                    + (
                        temp > StirMinTemperature
                            ? capi.World.ElapsedMilliseconds % 3000L / 3000f * GameMath.TWOPI
                            : 0f
                    );
                bool isStirring = temp > StirMinTemperature;
                float elapsed = capi.World.ElapsedMilliseconds / 1000f;
                float bob = isStirring ? GameMath.Sin(elapsed * 4f) * 0.015f : 0f;
                float wobble = isStirring ? GameMath.Sin(elapsed * 3f) * 4f : 0f;

                float spoonY = isStirring ? StirYOffset + bob : RestYOffset;
                float xTilt = isStirring ? wobble : RestXTiltDeg;
                float radialX = isStirring ? 0f : RestRadialOffsetX;
                float radialZ = isStirring ? 0f : RestRadialOffsetZ;

                prog.ModelMatrix = ModelMat
                    .Identity()
                    .Translate(pos.X - camPos.X, pos.Y - camPos.Y, pos.Z - camPos.Z)
                    .Translate(0.5f, FirepitYOffset + spoonY, 0.5f)
                    .RotateY(stirAngle)
                    .Translate(-0.25f - radialX, 0f, -0.25f - radialZ)
                    .RotateX(GameMath.DEG2RAD * (180f + xTilt))
                    .RotateZ(GameMath.DEG2RAD * 90f)
                    .Scale(0.75f, 0.85f, 0.75f)
                    .Translate(-0.7f, -0.4f, -0.7f)
                    .Values;

                prog.ViewMatrix = rpi.CameraMatrixOriginf;
                prog.ProjectionMatrix = rpi.CurrentProjectionMatrix;

                rpi.RenderMultiTextureMesh(stickMeshRef, "tex");
            }

            prog.Stop();
        }

        public void OnUpdate(float temperature)
        {
            temp = temperature;
            float soundIntensity = GameMath.Clamp((temp - 50) / 50, 0, 1);
            SetCookingSoundVolume(soundIntensity);

            float bubbleIntensity = GameMath.Clamp(
                (temperature - BubbleMinTemperature)
                    / (BubbleMaxTemperature - BubbleMinTemperature),
                0f,
                1f
            );
            if (bubbleIntensity > 0f)
            {
                bubbleParticles.quantity = bubbleIntensity * BubbleMaxQuantity;
                bubbleParticles.BasePos.Set(
                    pos.X + 0.5,
                    pos.Y + FirepitYOffset + LevelSurfaceY[currentLevel],
                    pos.Z + 0.5
                );
                capi.World.SpawnParticles(bubbleParticles);
            }

            if (firepit.Inventory is not InventorySmelting inv)
                return;

            string currentCode = null;
            int currentAmount = 0;

            if (
                inv[2]?.Itemstack?.Collectible?.Attributes?["waterTightContainerProps"].Exists
                == true
            )
            {
                currentCode = inv[2].Itemstack.Collectible.Code.ToString();
                currentAmount = inv[2].Itemstack.StackSize;
            }
            else
            {
                foreach (ItemSlot slot in inv.CookingSlots)
                {
                    if (
                        !slot.Empty
                        && slot.Itemstack
                            ?.Collectible
                            ?.Attributes
                            ?["waterTightContainerProps"]
                            .Exists == true
                    )
                    {
                        currentCode ??= slot.Itemstack.Collectible.Code.ToString();
                        currentAmount += slot.Itemstack.StackSize;
                    }
                }
            }

            if (currentCode != lastLiquidCode || currentAmount != lastLiquidAmount)
            {
                lastLiquidCode = currentCode;
                lastLiquidAmount = currentAmount;
                RebuildLiquidMesh();
            }
        }

        public void OnCookingComplete() { }

        private void SetCookingSoundVolume(float volume)
        {
            float scaledVolume = volume * 0.5f;
            if (scaledVolume > 0)
            {
                if (cookingSound == null)
                {
                    cookingSound = capi.World.LoadSound(
                        new SoundParams()
                        {
                            Location = new AssetLocation("sounds/moltenmetal.ogg"),
                            ShouldLoop = true,
                            Position = pos.ToVec3f().Add(0.5f, 0.25f, 0.5f),
                            DisposeOnFinish = false,
                            Range = 10f,
                            ReferenceDistance = 3f,
                            Volume = scaledVolume,
                        }
                    );
                    cookingSound.Start();
                }
                else
                {
                    cookingSound.SetVolume(scaledVolume);
                }
            }
            else
            {
                if (cookingSound != null)
                {
                    cookingSound.Stop();
                    cookingSound.Dispose();
                    cookingSound = null;
                }
            }
        }
    }
}
