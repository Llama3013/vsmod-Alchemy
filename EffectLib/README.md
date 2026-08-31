# EffectLib

A Vintage Story library mod for applying, tracking and persisting timed effects on players.

It handles the parts every effect mod otherwise rewrites: stat modifiers, instant and
ticking health changes, entity-property capabilities (flight, climbing, fall damage,
knockback, weight, gravity, glow, water breathing, cold resistance), the utility effects
(teleport to spawn, character reshape, nutrition retention, temporal stability, grow/shrink),
weapon/arrow coating, expiry timers, surviving disconnects, and a shared HUD.

EffectLib adds no content and no config of its own. Alchemy is the reference consumer.

## Depending on it

`modinfo.json`:

```json
"dependencies": { "game": "1.22.0", "effectlib": "1.0.0" }
```

`.csproj` — compile against it, but do not copy it into your output; the game loads
`effectlib.dll` from the EffectLib mod itself:

```xml
<ProjectReference Include="..\EffectLib\EffectLib.csproj" Private="false" />
```

## Code-defined effects

Register a builder per effect id. It runs on every application, so it may read live config.

```csharp
EffectRegistry.Register("mymod:haste", ctx =>
{
    ctx.Duration = 600;                  // seconds
    ctx.AddStat("walkspeed", 0.2f);      // scaled by ctx.PotencyMul
    ctx.GlowStrength = 8;
}, domain: "mymod");
```

`domain` decides where EffectLib looks for the effect's lang keys
(`mymod:waterbreathe`, falling back to `effectlib:waterbreathe`) and its HUD icon
(`mymod:textures/hud/effects/haste.png`).

Apply it to a player:

```csharp
EffectManager manager = EntityBehaviorEffects.ManagerFor(entityPlayer);
EffectContext ctx = EffectRegistry.Build("mymod:haste", potencyMul: 1f);
manager.TryApply("mymod:haste", ctx, Lang.Get("mymod:haste"));
```

EffectLib attaches the behavior, resumes saved effects on login, suspends them on
disconnect and clears them on death by itself.

## Content-only effects

A JSON-only mod - no code, no `.dll`, just `modinfo.json` depending on `effectlib` and item
JSON - can grant an effect. Add the `EffectItem` behavior and an `effectinfo` attribute to
any item:

```json
{
  "code": "healingflask",
  "behaviors": [{ "name": "EffectItem" }],
  "attributes": {
    "effectinfo": {
      "effectId": "mymod:healingflask",
      "duration": 600,
      "stats": { "walkspeed": 0.2 },
      "glowStrength": 8
    }
  }
}
```

Right-click-hold to use it: it plays an eating animation and sound, shows a progress bar,
consumes one item (or, with `durabilityCost` set on an item that has `durability`, spends that
much durability instead - a wand, not a potion), and applies the effect - registering itself under the item's own mod
domain the moment it loads, so nothing else needs to call anything. A tooltip is built
automatically from the same `EffectContext`, using `effectlib:<key>` lang keys as a
fallback so an item needs no lang keys of its own to show something readable. The effect's
own name comes from its id used as a lang key, so `"mymod:healingflask": "Healing"` in the
mod's `en.json` is all the HUD and the gain/lose messages need. Its HUD icon needs nothing at
all: with no `<domain>:textures/hud/effects/<id>.png` shipped, the HUD draws the item that
registered the effect - the flask, wand or herb the player used.

The behavior takes its own JSON properties, all optional:

```json
{ "name": "EffectItem", "properties": {
  "attributeKey": "effectinfo",  // which attribute to read
  "idField": "effectId",         // which field inside it is the effect id
  "consumeOnUse": true,          // false for a reusable item, e.g. a wand
  "durabilityCost": 0,           // spend durability instead of the item, if it has any
  "consumeTime": 1.6,            // seconds to hold; 0 applies instantly on click
  "animation": "eat",
  "sound": "game:sounds/player/eat"
}}
```

### Liquid containers

A flask, bottle or jug uses `CollectibleBehaviorEffectLiquid` instead - the sibling behavior
`{ "name": "EffectLiquid" }`. Everything works the same way, except the `effectinfo`
attribute belongs to whatever the container currently holds, not the container itself, since
that changes every time it's filled:

```json
{ "code": "healingflask", "class": "BlockLiquidContainerBase",
  "behaviors": [{ "name": "EffectLiquid", "properties": { "consumeLitres": 0.25, "checkLitres": 0.25 } }]
}
```

Each content item declares its own `effectinfo` the same way an `EffectItem`'s does - the
behavior resolves and registers it from the content the first time it sees it, since what's
inside can't be known (or registered) up front the way a fixed item's own attribute can.

### Extending it from code

A mod with its own delivery rules - strength tiers, a config-driven consume time, liquid
containers, exclusivity groups - doesn't reimplement the hold-interact flow. It subclasses
`CollectibleBehaviorEffectItem` and overrides just the parts that vary, the way a
game-provided Vintage Story behavior is meant to be extended:

```csharp
public class MyPotionBehavior(CollectibleObject collObj) : CollectibleBehaviorEffectItem(collObj)
{
    // Registered elsewhere instead of read off this item's own attribute.
    protected override void RegisterOwnEffect() { }

    protected override bool TryResolveEffect(ItemSlot slot, EntityAgent byEntity, out string effectId, out float potencyMul)
    {
        effectId = MyPotionRegistry.IdFor(slot);
        potencyMul = MyConfig.Loaded.Strength;
        return effectId != null;
    }

    protected override float GetConsumeTime(EntityAgent byEntity) => MyConfig.Loaded.DrinkTime;
}
```

The hold-interact flow, progress bar, dedupe against a double-fired stop event, and the
generic tooltip all stay inherited. Every hook is documented on
`CollectibleBehaviorEffectItem` (and, for liquids, `CollectibleBehaviorEffectLiquid`) itself.
Alchemy is the full worked example, split the same way as the base behaviors:
`Alchemy.PotionConsumableBehavior` (item form - herb balls, carried portions) and
`Alchemy.PotionConsumableLiquidBehavior` (flasks and other liquid containers) both delegate
their strength tiers, exclusivity groups, config-scaled consume time and drinking side
effects to the same static calculations in `Alchemy.PotionConsumableLogic`, so neither
duplicates the other - each is little more than "resolve a stack, inject a config value".

`RegisterOwnEffect`'s default reads and registers its own item's attribute directly
(`JsonEffectDefinition.Apply`) - there's nothing to loop over, since `OnLoaded` already runs
per collectible type. Reach for `JsonEffectDefinition.Scan` instead when nothing is
consuming the item, so no behavior's `OnLoaded` would ever see it - a "coat-only" potion
with no drink behavior attached, or an effect a wholly different system (a trap, a status an
NPC applies) needs to look up by id before anything using it has ever loaded:

```csharp
JsonEffectDefinition.Scan(api, new JsonEffectScanOptions
{
    AttributeKey = "effectinfo",
    IdField = "effectId",
    Domain = "mymod",
});
```

Every field of `EffectContext` is available; see `JsonEffectDefinition.Fields`.

## Weapon/arrow coating

Coating applies an effect to a weapon or arrow now, for delivery on a later hit - a
poisoned blade, a healing arrow. Add `{ "name": "Coatable" }` to a weapon so it shows a
tooltip once coated, and `{ "name": "CoatSource" }` (or `CoatSourceLiquid` for a liquid
container) to whatever applies the coating - same `effectinfo` schema and item/liquid split
as `EffectItem`/`EffectLiquid`:

```json
{ "code": "poisonvial",
  "behaviors": [{ "name": "CoatSource" }],
  "attributes": { "effectinfo": { "effectId": "mymod:poison", "health": -1, "tickSec": 1, "duration": 5 } }
}
```

Hold the coating source in your off hand, the weapon in your main hand, and shift+right-click
- there is no held-interact equivalent for an off-hand item, so this is driven from
`OnHeldIdle` instead (`CollectibleBehaviorCoatSource.CoatingIdle` - call it from your own
item/block's `OnHeldIdle` if it does not already route through a `CollectibleBehavior`, the
same workaround Alchemy's `ItemPotion`/`BlockPotionFlask` use). A barrel of the same liquid
coats everything sitting in it at once instead, with no interaction needed - see
`BarrelCoating`/`BarrelCoatingConfig`.

Coating is entirely config-free like everything else, through `CoatingPolicy`:
`AllowCoating`, `MaxCharges`, `EffectMultiplier` (a dampening factor - a coated hit is
typically weaker than drinking the same effect), `IsCoatableWeapon`/`IsCoatableProjectile`
(what can be coated at all), `IsEffectCoatable` (which registered effects are currently
allowed to be delivered this way), and `ApplySideEffects`/`GetBlockReason` for anything
extra a mod layers onto a hit (Alchemy uses these for its drinking-style side effects and
exclusivity groups).

A combat mod with its own weapon-buff system for delivering the actual hit can keep a
coating in its own storage instead of the item stack's attributes - set
`CoatingPolicy.UsesAlternateWeaponStorage`/`UsesAlternateProjectileStorage` and the matching
`TryReadAlternateWeapon`/`WriteAlternateWeapon`/`WriteAlternateProjectile` hooks, and hand
`CoatedEffects.Apply`/`ResolveDisplayName` to that system as its own on-hit callback, the
way `Alchemy.CombatOverhaulCompat` does for Combat Overhaul's `WeaponBuffSystem`.
EffectLib's own on-hit Harmony patches never consult alternate storage, so the two paths
never both fire for the same coating.

## Purging other effects

`EffectContext.ResetsEffects` clears other running effects. By default it clears only effects
from **the domain that registered the purging effect**, so one mod's cure-all cannot wipe
another mod's effects. Widen it deliberately:

```csharp
ctx.ResetsEffects = true;
ctx.ResetDomains.Add("someothermod");        // also clear that mod's effects
ctx.ResetDomains.Add(EffectPurge.AnyDomain); // or clear everything
ctx.ResetEffectIds.Add("mymod:curse");       // or name individual effects
```

The same fields exist in JSON as `resetsEffects`, `resetDomains` and `resetEffectIds`.

Handlers receive the scope, so lasting state is undone only for domains actually covered:

```csharp
public void OnCleared(EntityPlayer entity, EffectPurge scope)
{
    if (scope.CoversDomain("mymod")) UndoMyLastingState(entity);
}
```

`EffectManager.PurgeFor(effectId, ctx)` applies an effect's own scope; `ResetAll()` clears
everything and is what death and logout use.

## Utility effects

Beyond the entity-property capabilities, `EffectContext` also carries five "utility"
fields that EffectLib carries out itself - no handler required, no dependent mod needed:

```csharp
ctx.Respawn = true;                 // teleport to spawn
ctx.Reshape = true;                 // reopen character customisation
ctx.RetainedNutrition = 0.5f;       // keep a fraction of current nutrition
ctx.TemporalStabilityGain = 0.2f;   // add to temporal stability
ctx.SizeChange = 0.3f;              // grow (positive) or shrink (negative), in blocks
ctx.SizeMinHeight = 0.5f;           // optional bounds; default 0.2-10 blocks
ctx.SizeMaxHeight = 3.0f;
```

These are one-shots: they fire once on application, same as `health` without a `tickSec`.
They are distinct from the simple per-tick entity-property effects above and from the stat
modifiers in `AtomicEffects` - see `EffectLib.UtilityEffects` and its built-in
`IEffectHandler`, `UtilityEffectHandler`, which every EffectLib mod registers automatically.

Sizing is gated by `EffectCapability.Resize` and is scoped by the domain that applied it, the
same way `ResetsEffects` is - a purging brew undoes another mod's grow potion only if its
scope covers that mod's domain.

The same fields exist in JSON as `respawn`, `reshape`, `retainedNutrition`,
`temporalStabilityGain`, `sizeChange`, `sizeMinHeight` and `sizeMaxHeight`.

## Extension points

`EffectContext` and the built-in utility effects above cover what EffectLib applies itself.
Anything further a mod-specific effect needs to do belongs in a handler:

```csharp
EffectHandlers.Register(new MyHandler());   // IEffectHandler
```

`OnApplied` fires for new effects only (not resumes), `OnRemoved` on expiry or removal,
`OnCleared` when everything is wiped, `OnRestored` after a login resume.

EffectLib ships no config, so a server owner's switches reach it through a gate:

```csharp
EffectPolicy.SetGate(cap => cap switch
{
    EffectCapability.Fly   => MyConfig.AllowFlight,
    EffectCapability.Climb => MyConfig.AllowClimb,
    _ => true,
});
```

Unknown capabilities are allowed, so a newer EffectLib never silently disables an effect
against an older gate.

## Admin commands

`/efflib`, requiring the `controlserver` privilege. Use `all` as the player name to target
everyone online. Both effect names and registered ids tab-complete.

Individual effects — the primitives of `EffectContext`, given one at a time:

```
/efflib list [filter]                                            what can be given
/efflib give <player> <effect> [magnitude] [seconds] [interval]  give bob fly, give bob glow 200
/efflib dot <player> <amount> <interval> [seconds] [damagetype]  dot bob -2 0.5 60
```

`<effect>` is a name from `list`, or `stat:<name>` for any entity stat
(`give bob stat:walkspeed 0.5 300`). Magnitude is the amount and is ignored for on/off
effects; duration defaults to 600s and is ignored for one-shot effects such as `health`,
`size` and `respawn` - unless you pass an `interval`, which repeats the one-shot every that
many seconds for the duration.

`dot` is the health case of that, spelled for convenience: it drives the engine's own ticking
damage source rather than a repeat timer, so it behaves like any other damage over time.

Whole effects registered by a mod — a potion and everything it bundles:

```
/efflib registered [filter]                            registered effect ids, with their domain
/efflib apply <player> <effectid> [potency] [seconds]  potency scales stats and health
```

Shared:

```
/efflib show [player]                                 what is running (default: you)
/efflib clear <player> [effect]                       one effect, or everything if omitted
```

`give` and `apply` replace an already running instance rather than being refused by the
refresh policy. `clear` without an effect id does a full reset, so handlers undo lasting
state too. Giving an effect whose capability the server has gated off is refused with an
explanation rather than silently doing nothing.

Individual effects are ordinary registered effects under an `efflib:` id, so they persist and
resume like any other. Stat effects are resolved on demand via `EffectRegistry.AddResolver`,
which is also how they survive a restart.

## HUD

The HUD is driven by the persisted effect tree. To add rows it cannot see - state your
mod tracks itself - or to supply fallback icons, register an `IHudEffectProvider` with
`EffectHud.Register`. Every member has a default, so implement only what you need.

A row's icon is resolved in order: a provider's `GetIconTexture`, then
`<domain>:textures/hud/effects/<effect id, ':' as '-'>.png`, then a provider's `GetIconStack`,
and finally the collectible that registered the effect (`EffectRegistration.IconSource`, set
automatically for every JSON-declared effect). So an icon is only ever worth shipping to
override the item's own.

## Save compatibility

The persisted tree key (`alchemyEffects`) and the stat modifier subkey (`potionmod-<id>`)
are inherited from Alchemy 2.x and deliberately unchanged, so existing worlds keep their
active effects across the split. The grow/shrink WatchedAttributes keys (`potionSizeDelta`,
`potionBaseHeight`, ...) are likewise unchanged; a player already resized before this mod
existed is assumed to have been resized by the `alchemy` domain, since no other mod could
have done it at the time.
