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
    ctx.Duration = 600;                  // seconds; 0 is a one-shot,
                                         // EffectContext.EndlessDuration runs until death
    ctx.AddStat("walkspeed", 0.2f);      // scaled by ctx.PotencyMul
    ctx.GlowStrength = 8;
}, domain: "mymod");
```

`domain` decides where EffectLib looks for the effect's lang keys
(`mymod:waterbreathe`, falling back to `effectlib:waterbreathe`). Pass `iconTexture:` for an
explicit HUD icon, or leave it and the HUD shows the item the effect came from.

Apply it to a player:

```csharp
EffectManager manager = EntityBehaviorPlayerEffects.ManagerFor(entityPlayer);
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
mod's `en.json` is all the HUD and the gain/lose messages need. Its HUD icon needs nothing
either - with no `hudIcon` set the HUD draws the item that registered the effect (the flask,
wand or herb the player used); add `"hudIcon": "mymod:textures/hud/foo.png"` to the
`effectinfo` to override that.

`"duration"` is seconds; `0` (or an absent key) is a one-shot that fires once, and `-1`
runs the effect until the player dies, logs out without retention, or it is cleared.

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
`BarrelCoating`.

Coating carries no config of its own. A mod installs its rules once, the same way
`EffectPolicy.SetGate` is installed - build a `CoatingConfig` and pass it to
`CoatingPolicy.Configure(...)`. Every hook is optional and falls back to a permissive or
no-op default:

```csharp
CoatingPolicy.Configure(new CoatingConfig
{
    AllowCoating     = () => MyConfig.AllowCoating,
    MaxCharges       = () => MyConfig.CoatCharges,
    EffectMultiplier = () => MyConfig.CoatDampening,   // a coated hit is weaker than drinking
    IsCoatableWeapon     = col => col.Tags.Contains("blade"),
    IsCoatableProjectile = col => col.Code.Path.Contains("arrow"),
    IsEffectCoatable = id => MyConfig.CoatableEffects.Contains(id),  // per-effect, not the master switch
    AllowBarrelCoating  = () => MyConfig.AllowBarrelCoating,   // plus BarrelConsumeLitres / BarrelCheckLitres
    ApplySideEffects = (id, target, mul) => { /* extra per-hit work */ },
    GetBlockReason   = (id, player, ctx) => null,             // a lang key to refuse the hit, or null
});
```

The `CombatOverhaul*` hooks on `CoatingConfig` exist for one thing: Combat Overhaul's weapons
never reach EffectLib's on-hit patches, so `Alchemy.CombatOverhaulCompat` routes their
coatings into CO's own `WeaponBuffSystem` (which calls back into `CoatedEffects.Apply`).
Leave them unset and every coating lives on the item stack.

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
modifiers in `EffectPrimitives` - see `EffectLib.UtilityEffects` and its built-in
`IEffectHandler`, `UtilityEffectHandler`, which every EffectLib mod registers automatically.

Sizing is gated by `EffectCapability.Resize` and is scoped by the domain that applied it, the
same way `ResetsEffects` is - a purging brew undoes another mod's grow potion only if its
scope covers that mod's domain.

The same fields exist in JSON as `respawn`, `reshape`, `retainedNutrition`,
`temporalStabilityGain`, `sizeChange`, `sizeMinHeight` and `sizeMaxHeight`.

## Extension points

`EffectContext` and the built-in utility effects above cover what EffectLib applies itself.
Anything further a mod-specific effect needs to do belongs in a handler, registered from
your `StartServerSide` (handlers only ever fire server-side):

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
/efflib list [filter]                                             what can be given
/efflib give <player> <effect> [magnitude] [duration] [interval]  give bob fly, give bob glow 200
/efflib dot <player> <amount> <interval> [duration] [damagetype]  dot bob -2 0.5 60
```

`<effect>` is a name from `list`, or `stat:<name>` for any entity stat
(`give bob stat:walkspeed 0.5 300`). Magnitude is the amount and is ignored for on/off
effects, but still holds its argument slot - pass any number for it (`give bob fly 1 endless`).
`duration` is a number of seconds (default 600) or `endless` for one that lasts until the
player dies, logs out without retention, or is cleared. Duration is ignored for one-shot
effects such as `health`, `size` and `respawn` unless you pass an `interval`, which repeats
the one-shot every that many seconds for the duration (an endless interval repeats forever -
`give bob size 0.2 endless 5`).

`dot` is the health case of that, spelled for convenience: with a finite duration it drives
the engine's own ticking damage source so it behaves like any other damage over time; an
endless `dot` is driven by a repeat timer instead. A positive amount heals.

Whole effects registered by a mod — a potion and everything it bundles:

```
/efflib registered [filter]                             registered effect ids, with their domain
/efflib apply <player> <effectid> [potency] [duration]  potency scales stats and health
```

`apply`'s `duration` is seconds, or `endless`, or omitted to keep whatever duration the
effect defines for itself.

Shared:

```
/efflib show [player]                                 what is running (default: you)
/efflib clear <player> [effect]                       one effect, or everything if omitted
```

`give` and `apply` replace an already running instance rather than being refused by the
refresh policy. `clear` without an effect id does a full reset, so handlers undo lasting
state too. Giving an effect whose capability the server has gated off is refused with an
explanation rather than silently doing nothing.

Individual effects are ordinary registered effects under an `efflib:<name>` id, so they
persist and resume like any other; a repeating one also stores its interval and damage type
in its save record, since those are not part of the id. Stat effects (`efflib:stat:<name>`)
are open-ended, so they are resolved on demand via `EffectRegistry.AddResolver` rather than
registered up front - which is also how a saved one rebuilds after a restart.

## HUD

The HUD is entirely EffectLib's - a dependent mod adds no HUD code. It shows every effect in
the persisted tree with a countdown, plus one built-in endless row while a grow or shrink is
in effect (that is a size *state*, not a tracked effect).

A row's icon is resolved in two steps:

1. **An explicit texture** - the effect's `hudIcon` field (`"effectinfo": { "effectId": "...",
   "hudIcon": "mymod:textures/hud/foo.png" }`), or the `iconTexture:` argument to
   `EffectRegistry.Register`.
2. **The item the effect came from** - `EffectRegistration.IconSource` (set to the collectible
   for every behavior/JSON-registered effect), then, failing that, a scan for whatever loaded
   collectible carries this id in its `effectinfo` attribute. So a code-registered effect
   still shows the flask/wand/herb it belongs to, with nothing shipped.

The grow/shrink row looks for `effectlib:textures/hud/effects/grown.png` / `shrunk.png`.

## Save keys

Effects persist under `alchemyEffects` on the player, with stat modifiers keyed
`potionmod-<id>` - both inherited from Alchemy 2.x. The size state lives in
`effectlib:sizeDelta` (and `effectlib:base*`).
