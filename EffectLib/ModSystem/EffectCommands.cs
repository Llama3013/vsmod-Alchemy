using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

#pragma warning disable IDE0130 // Namespace does not match folder structure
namespace EffectLib
#pragma warning restore IDE0130 // Namespace does not match folder structure
{
    /// <summary>
    /// Admin commands for inspecting and handing out effects. Covers both the individual
    /// effects EffectLib understands and whole effects registered by any mod.
    /// </summary>
    public class EffectCommands : ModSystem
    {
        private const int DefaultDurationSec = 600;

        private ICoreServerAPI sapi;

        // After EffectLibMod and after content mods have registered their effects, so the
        // suggestion lists are populated when the command is built.
        public override double ExecuteOrder() => 0.3;

        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Server;

        public override void StartServerSide(ICoreServerAPI api)
        {
            sapi = api;

            IChatCommand cmd = api
                .ChatCommands.Create("efflib")
                .WithDescription(
                    "Inspect and hand out effects. Subcommands: list, registered, give, apply, show, clear."
                )
                .RequiresPrivilege(Privilege.controlserver);

            cmd.BeginSubCommand("list")
                .WithDescription(
                    "List the individual effects that can be given. Optional filter matches part of a name."
                )
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("filter"))
                .HandleWith(OnList)
                .EndSubCommand();

            cmd.BeginSubCommand("give")
                .WithDescription(
                    "Give one individual effect. Use a name from 'list', or 'stat:name' for any "
                        + "entity stat. Magnitude is the amount (ignored for on/off effects), "
                        + "duration is in seconds, and a non-zero interval repeats the effect "
                        + "every that many seconds. Use 'all' as the player to affect everyone online."
                )
                .WithArgs(
                    api.ChatCommands.Parsers.Word("player"),
                    api.ChatCommands.Parsers.Word("effect", AtomicSuggestions()),
                    api.ChatCommands.Parsers.OptionalFloat("magnitude", 1f),
                    api.ChatCommands.Parsers.OptionalInt("duration", -1),
                    api.ChatCommands.Parsers.OptionalFloat("interval", 0f)
                )
                .HandleWith(OnGive)
                .EndSubCommand();

            cmd.BeginSubCommand("dot")
                .WithDescription(
                    "Damage or heal over time. Amount is per tick, negative to damage; interval "
                        + "is seconds between ticks. Damage type defaults to Poison, or Heal for "
                        + "a positive amount."
                )
                .WithArgs(
                    api.ChatCommands.Parsers.Word("player"),
                    api.ChatCommands.Parsers.Float("amount"),
                    api.ChatCommands.Parsers.Float("interval"),
                    api.ChatCommands.Parsers.OptionalInt("duration", DefaultDurationSec),
                    api.ChatCommands.Parsers.OptionalWord("damagetype")
                )
                .HandleWith(OnDot)
                .EndSubCommand();

            cmd.BeginSubCommand("registered")
                .WithDescription(
                    "List whole effects registered by mods, such as potions. Optional filter."
                )
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("filter"))
                .HandleWith(OnListRegistered)
                .EndSubCommand();

            cmd.BeginSubCommand("apply")
                .WithDescription(
                    "Apply a whole registered effect by its id, as if the player had used it. "
                        + "Potency scales it; duration in seconds overrides its own."
                )
                .WithArgs(
                    api.ChatCommands.Parsers.Word("player"),
                    api.ChatCommands.Parsers.Word("effectid", RegisteredSuggestions()),
                    api.ChatCommands.Parsers.OptionalFloat("potency", 1f),
                    api.ChatCommands.Parsers.OptionalInt("duration", -1)
                )
                .HandleWith(OnApply)
                .EndSubCommand();

            cmd.BeginSubCommand("show")
                .WithDescription("Show the effects currently running on a player.")
                .WithArgs(api.ChatCommands.Parsers.OptionalWord("player"))
                .HandleWith(OnShow)
                .EndSubCommand();

            cmd.BeginSubCommand("clear")
                .WithDescription(
                    "Remove one effect from a player, or every effect if no id is given. "
                        + "Use 'all' as the player to affect everyone online."
                )
                .WithArgs(
                    api.ChatCommands.Parsers.Word("player"),
                    api.ChatCommands.Parsers.OptionalWord("effect")
                )
                .HandleWith(OnClear)
                .EndSubCommand();

            base.StartServerSide(api);
        }

        private static string[] AtomicSuggestions() =>
            [.. AtomicEffects.All.Select(e => e.Name).OrderBy(n => n, StringComparer.Ordinal)];

        // Snapshotted for tab-completion. Effects registered later still work, they just are
        // not suggested.
        private static string[] RegisteredSuggestions() =>
            [
                .. EffectRegistry
                    .Registrations.Keys.Where(id => !AtomicEffects.IsAtomicId(id))
                    .OrderBy(id => id, StringComparer.Ordinal),
            ];

        private static TextCommandResult OnList(TextCommandCallingArgs args)
        {
            string filter = args[0] as string;

            List<AtomicEffect> matches =
            [
                .. AtomicEffects
                    .All.Where(e =>
                        string.IsNullOrEmpty(filter)
                        || e.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        || e.Description.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    )
                    .OrderBy(e => e.Instant)
                    .ThenBy(e => e.Name, StringComparer.Ordinal),
            ];

            if (matches.Count == 0)
                return TextCommandResult.Success($"No individual effect matches '{filter}'.");

            StringBuilder sb = new();
            sb.Append(matches.Count).Append(" individual effect(s):");
            foreach (AtomicEffect e in matches)
            {
                sb.AppendLine()
                    .Append("  ")
                    .Append(e.Name.PadRight(20))
                    .Append(Describe(e.Kind).PadRight(10))
                    .Append(e.Instant ? "[once] " : "")
                    .Append(e.Description);
            }
            sb.AppendLine()
                .Append("  ")
                .Append("stat:name".PadRight(20))
                .Append("number".PadRight(10))
                .Append("any entity stat, e.g. stat:walkspeed");

            return TextCommandResult.Success(sb.ToString());
        }

        private static string Describe(EffectValueKind kind) =>
            kind switch
            {
                EffectValueKind.Flag => "on/off",
                EffectValueKind.Whole => "whole",
                _ => "number",
            };

        private static TextCommandResult OnListRegistered(TextCommandCallingArgs args)
        {
            string filter = args[0] as string;

            List<EffectRegistration> matches =
            [
                .. EffectRegistry
                    .Registrations.Values.Where(reg =>
                        !AtomicEffects.IsAtomicId(reg.Id)
                        && (
                            string.IsNullOrEmpty(filter)
                            || reg.Id.Contains(filter, StringComparison.OrdinalIgnoreCase)
                            || reg.Domain.Contains(filter, StringComparison.OrdinalIgnoreCase)
                        )
                    )
                    .OrderBy(reg => reg.Domain, StringComparer.Ordinal)
                    .ThenBy(reg => reg.Id, StringComparer.Ordinal),
            ];

            if (matches.Count == 0)
                return TextCommandResult.Success(
                    string.IsNullOrEmpty(filter)
                        ? "No whole effects are registered."
                        : $"No registered effect matches '{filter}'."
                );

            StringBuilder sb = new();
            sb.Append(matches.Count).Append(" registered effect(s):");
            foreach (EffectRegistration reg in matches)
            {
                sb.AppendLine().Append("  ").Append(reg.Id).Append("  [").Append(reg.Domain).Append(']');
            }

            return TextCommandResult.Success(sb.ToString());
        }

        private TextCommandResult OnGive(TextCommandCallingArgs args)
        {
            string playerArg = args[0] as string;
            string name = (args[1] as string)?.Trim();
            float magnitude = (float)args[2];
            int duration = (int)args[3];
            float interval = (float)args[4];

            if (string.IsNullOrEmpty(name))
                return TextCommandResult.Error("Name an effect. Use /efflib list to see them.");

            bool isStat = name.StartsWith("stat:", StringComparison.OrdinalIgnoreCase);
            AtomicEffect atomic = isStat ? null : AtomicEffects.Get(name);

            if (!isStat && atomic == null)
                return TextCommandResult.Error(
                    $"No individual effect named '{name}'. Use /efflib list, or 'stat:name' "
                        + "for an entity stat. For a whole potion-style effect use /efflib apply."
                );

            if (isStat && name.Length <= "stat:".Length)
                return TextCommandResult.Error("Name the stat, e.g. stat:walkspeed.");

            bool ignoresMagnitude = atomic?.Kind == EffectValueKind.Flag;
            if (!ignoresMagnitude && Math.Abs(magnitude) <= float.Epsilon)
                return TextCommandResult.Error(
                    "Magnitude must not be zero - the effect would do nothing."
                );

            if (isStat && interval > 0f)
                return TextCommandResult.Error(
                    "A stat effect is held for its whole duration, so an interval means nothing here."
                );

            string effectId;
            if (isStat)
                effectId = AtomicEffects.StatIdPrefix + name["stat:".Length..].ToLowerInvariant();
            else if (interval > 0f)
                effectId = AtomicEffects.RepeatingIdFor(atomic.Name, interval);
            else
                effectId = AtomicEffects.IdFor(atomic.Name);

            // A capability the server has switched off applies as a no-op, which looks like the
            // command failed. Say so rather than reporting a success that did nothing.
            if (atomic?.Capability != null && !EffectPolicy.IsAllowed(atomic.Capability))
                return TextCommandResult.Error(
                    $"'{atomic.Name}' needs the '{atomic.Capability}' capability, which this "
                        + "server has disabled in its config, so it would have no effect."
                );

            // A repeating one-shot is no longer instant: it needs a duration to repeat within.
            bool instant = atomic?.Instant == true && interval <= 0f;

            return ApplyToTargets(
                args,
                playerArg,
                effectId,
                magnitude,
                instant ? 0 : (duration >= 0 ? duration : DefaultDurationSec),
                interval > 0f ? $"{name} every {interval:0.##}s" : name
            );
        }

        private TextCommandResult OnDot(TextCommandCallingArgs args)
        {
            string playerArg = args[0] as string;
            float amount = (float)args[1];
            float interval = (float)args[2];
            int duration = (int)args[3];
            string damageTypeArg = args[4] as string;

            if (Math.Abs(amount) <= float.Epsilon)
                return TextCommandResult.Error("Amount must not be zero.");

            if (interval <= 0f)
                return TextCommandResult.Error("Interval must be greater than 0 seconds.");

            if (duration <= 0)
                return TextCommandResult.Error("Duration must be greater than 0 seconds.");

            EnumDamageType? damageType = null;
            if (!string.IsNullOrWhiteSpace(damageTypeArg))
            {
                if (!Enum.TryParse(damageTypeArg, true, out EnumDamageType parsed))
                    return TextCommandResult.Error(
                        $"'{damageTypeArg}' is not a damage type. Try Poison, Heal, Injury, Acid, Fire."
                    );
                damageType = parsed;
            }

            string effectId = AtomicEffects.RepeatingIdFor("health", interval, damageType);
            string label = $"{amount:+0.##;-0.##} HP every {interval:0.##}s";

            return ApplyToTargets(args, playerArg, effectId, amount, duration, label);
        }

        private TextCommandResult OnApply(TextCommandCallingArgs args)
        {
            string playerArg = args[0] as string;
            string effectId = (args[1] as string)?.ToLowerInvariant();
            float potency = (float)args[2];
            int duration = (int)args[3];

            if (!EffectRegistry.IsRegistered(effectId))
                return TextCommandResult.Error(
                    $"No effect '{effectId}' is registered. Use /efflib registered to see them."
                );

            if (Math.Abs(potency) <= float.Epsilon)
                return TextCommandResult.Error("Potency must not be zero.");

            return ApplyToTargets(args, playerArg, effectId, potency, duration, effectId);
        }

        // durationSec < 0 keeps whatever duration the effect defines for itself.
        private TextCommandResult ApplyToTargets(
            TextCommandCallingArgs args,
            string playerArg,
            string effectId,
            float potency,
            int durationSec,
            string label
        )
        {
            if (
                !TryResolveTargets(
                    args,
                    playerArg,
                    out List<IServerPlayer> targets,
                    out TextCommandResult error
                )
            )
                return error;

            int applied = 0;
            List<string> failed = [];

            foreach (IServerPlayer target in targets)
            {
                EffectManager manager = EntityBehaviorEffects.ManagerFor(target.Entity);
                EffectContext ctx = manager == null
                    ? null
                    : EffectRegistry.Build(effectId, potency);

                if (ctx == null)
                {
                    failed.Add(target.PlayerName);
                    continue;
                }

                if (durationSec >= 0)
                    ctx.Duration = durationSec;

                // An admin asking for an effect should always get it, so clear any running
                // instance first rather than letting the refresh policy reject it.
                if (manager.IsActive(effectId))
                    manager.RemoveEffect(effectId, false);

                if (manager.TryApply(effectId, ctx, DisplayName(effectId, label)))
                    applied++;
                else
                    failed.Add(target.PlayerName);
            }

            string summary = $"Gave {label} to {applied} player(s).";
            if (failed.Count > 0)
                summary += $" Failed for: {string.Join(", ", failed)}.";

            return applied > 0
                ? TextCommandResult.Success(summary)
                : TextCommandResult.Error(summary);
        }

        private TextCommandResult OnShow(TextCommandCallingArgs args)
        {
            string name = args[0] as string;

            if (!TryResolveSingle(args, name, out IServerPlayer target, out TextCommandResult error))
                return error;

            EffectManager manager = EntityBehaviorEffects.ManagerFor(target.Entity);
            if (manager == null)
                return TextCommandResult.Error($"{target.PlayerName} has no effect manager yet.");

            List<ActiveEffectInfo> running = manager.GetActiveEffects();
            if (running.Count == 0)
                return TextCommandResult.Success($"{target.PlayerName} has no active effects.");

            StringBuilder sb = new();
            sb.Append(target.PlayerName).Append(" has ").Append(running.Count).Append(" effect(s):");
            foreach (ActiveEffectInfo info in running.OrderBy(i => i.RemainingSec))
            {
                sb.AppendLine()
                    .Append("  ")
                    .Append(info.Id)
                    .Append("  ")
                    .Append(FormatTime(info.RemainingSec))
                    .Append(" left");
                if (Math.Abs(info.PotencyMul - 1f) > 0.001f)
                    sb.Append("  x").Append(info.PotencyMul.ToString("0.##"));
            }

            return TextCommandResult.Success(sb.ToString());
        }

        private TextCommandResult OnClear(TextCommandCallingArgs args)
        {
            string playerArg = args[0] as string;
            string effect = (args[1] as string)?.Trim();

            if (
                !TryResolveTargets(
                    args,
                    playerArg,
                    out List<IServerPlayer> targets,
                    out TextCommandResult error
                )
            )
                return error;

            // Accept either a full effect id or a bare individual-effect name.
            string effectId = effect;
            if (!string.IsNullOrEmpty(effect) && AtomicEffects.Get(effect) != null)
                effectId = AtomicEffects.IdFor(effect);
            else if (effect?.StartsWith("stat:", StringComparison.OrdinalIgnoreCase) == true)
                effectId = AtomicEffects.StatIdPrefix + effect["stat:".Length..];
            effectId = effectId?.ToLowerInvariant();

            int cleared = 0;

            foreach (IServerPlayer target in targets)
            {
                EffectManager manager = EntityBehaviorEffects.ManagerFor(target.Entity);
                if (manager == null)
                    continue;

                if (string.IsNullOrEmpty(effectId))
                {
                    // Full reset, so handlers also undo lasting state such as a size change.
                    if (manager.HasAnyActive)
                        cleared++;
                    manager.ResetAll();
                }
                else if (manager.IsActive(effectId))
                {
                    manager.RemoveEffect(effectId);
                    cleared++;
                }
            }

            return TextCommandResult.Success(
                string.IsNullOrEmpty(effectId)
                    ? $"Cleared all effects from {targets.Count} player(s); {cleared} had some running."
                    : $"Removed {effectId} from {cleared} of {targets.Count} player(s)."
            );
        }

        // "all" targets everyone online; anything else is a player name.
        private bool TryResolveTargets(
            TextCommandCallingArgs args,
            string playerArg,
            out List<IServerPlayer> targets,
            out TextCommandResult error
        )
        {
            error = null;

            if (string.Equals(playerArg, "all", StringComparison.OrdinalIgnoreCase))
            {
                targets = [.. sapi.World.AllOnlinePlayers.OfType<IServerPlayer>()];
                if (targets.Count == 0)
                    error = TextCommandResult.Error("Nobody is online.");
                return error == null;
            }

            bool ok = TryResolveSingle(args, playerArg, out IServerPlayer single, out error);
            targets = [];
            if (ok)
                targets.Add(single);
            return ok;
        }

        private bool TryResolveSingle(
            TextCommandCallingArgs args,
            string playerName,
            out IServerPlayer target,
            out TextCommandResult error
        )
        {
            error = null;

            if (string.IsNullOrEmpty(playerName))
            {
                target = args.Caller.Player as IServerPlayer;
                if (target == null)
                    error = TextCommandResult.Error(
                        "Name a player - the console has no player of its own."
                    );
                return target != null;
            }

            target = sapi
                .World.AllOnlinePlayers.OfType<IServerPlayer>()
                .FirstOrDefault(p =>
                    string.Equals(p.PlayerName, playerName, StringComparison.OrdinalIgnoreCase)
                );

            if (target == null)
                error = TextCommandResult.Error($"No online player named '{playerName}'.");

            return target != null;
        }

        // Best effort: a mod can ship a lang key named after the effect id for a nicer label,
        // otherwise the name the admin typed is shown.
        private static string DisplayName(string effectId, string fallback) =>
            EffectLang.NameIfExists(effectId) ?? fallback;

        private static string FormatTime(int sec) =>
            sec >= 3600
                ? $"{sec / 3600}:{sec / 60 % 60:00}:{sec % 60:00}"
                : $"{sec / 60}:{sec % 60:00}";

        public override void Dispose()
        {
            sapi = null;
            base.Dispose();
        }
    }
}
