using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Server;

#pragma warning disable IDE0130
namespace EffectLib
#pragma warning restore IDE0130
{
    public class EffectCommands : ModSystem
    {
        private const int DefaultDurationSec = 600;

        private ICoreServerAPI sapi;

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
                        + "duration is in seconds or 'endless' for one that lasts until death, and "
                        + "a non-zero interval repeats the effect every that many seconds. Use "
                        + "'all' as the player to affect everyone online."
                )
                .WithArgs(
                    api.ChatCommands.Parsers.Word("player"),
                    api.ChatCommands.Parsers.Word("effect", PrimitiveSuggestions()),
                    api.ChatCommands.Parsers.OptionalFloat("magnitude", 1f),
                    api.ChatCommands.Parsers.OptionalWord("duration"),
                    api.ChatCommands.Parsers.OptionalFloat("interval", 0f)
                )
                .HandleWith(OnGive)
                .EndSubCommand();

            cmd.BeginSubCommand("dot")
                .WithDescription(
                    "Damage or heal over time. Amount is per tick, negative to damage; interval "
                        + "is seconds between ticks. Duration is in seconds or 'endless'. Damage "
                        + "type defaults to Poison, or Heal for a positive amount."
                )
                .WithArgs(
                    api.ChatCommands.Parsers.Word("player"),
                    api.ChatCommands.Parsers.Float("amount"),
                    api.ChatCommands.Parsers.Float("interval"),
                    api.ChatCommands.Parsers.OptionalWord("duration"),
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
                        + "Potency scales it; duration in seconds (or 'endless') overrides its own, "
                        + "otherwise the effect keeps whatever duration it defines."
                )
                .WithArgs(
                    api.ChatCommands.Parsers.Word("player"),
                    api.ChatCommands.Parsers.Word("effectid", RegisteredSuggestions()),
                    api.ChatCommands.Parsers.OptionalFloat("potency", 1f),
                    api.ChatCommands.Parsers.OptionalWord("duration")
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

        private static string[] PrimitiveSuggestions() =>
            [.. EffectPrimitives.All.Select(p => p.Name).OrderBy(n => n, StringComparer.Ordinal)];

        private static string[] RegisteredSuggestions() =>
            [
                .. EffectRegistry
                    .Registrations.Keys.Where(id => !EffectPrimitives.IsPrimitiveId(id))
                    .OrderBy(id => id, StringComparer.Ordinal),
            ];

        private static TextCommandResult OnList(TextCommandCallingArgs args)
        {
            string filter = args[0] as string;

            List<EffectPrimitive> matches =
            [
                .. EffectPrimitives
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
            foreach (EffectPrimitive e in matches)
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
                        !EffectPrimitives.IsPrimitiveId(reg.Id)
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
            float interval = (float)args[4];

            if (!TryParseDuration(args[3] as string, out int? duration))
                return TextCommandResult.Error(DurationParseError);

            if (string.IsNullOrEmpty(name))
                return TextCommandResult.Error("Name an effect. Use /efflib list to see them.");

            bool isStat = name.StartsWith("stat:", StringComparison.OrdinalIgnoreCase);
            EffectPrimitive primitive = isStat ? null : EffectPrimitives.Get(name);

            if (!isStat && primitive == null)
                return TextCommandResult.Error(
                    $"No individual effect named '{name}'. Use /efflib list, or 'stat:name' "
                        + "for an entity stat. For a whole potion-style effect use /efflib apply."
                );

            if (isStat && name.Length <= "stat:".Length)
                return TextCommandResult.Error("Name the stat, e.g. stat:walkspeed.");

            bool ignoresMagnitude = primitive?.Kind == EffectValueKind.Flag;
            if (!ignoresMagnitude && Math.Abs(magnitude) <= float.Epsilon)
                return TextCommandResult.Error(
                    "Magnitude must not be zero - the effect would do nothing."
                );

            if (isStat && interval > 0f)
                return TextCommandResult.Error(
                    "A stat effect is held for its whole duration, so an interval means nothing here."
                );

            string effectId = isStat
                ? EffectPrimitives.StatIdPrefix + name["stat:".Length..].ToLowerInvariant()
                : EffectPrimitives.IdFor(primitive.Name);

            if (primitive?.Capability != null && !EffectPolicy.IsAllowed(primitive.Capability))
                return TextCommandResult.Error(
                    $"'{primitive.Name}' needs the '{primitive.Capability}' capability, which this "
                        + "server has disabled in its config, so it would have no effect."
                );

            bool instant = primitive?.Instant == true && interval <= 0f;

            return ApplyToTargets(
                args,
                playerArg,
                effectId,
                magnitude,
                instant ? 0 : (duration ?? DefaultDurationSec),
                interval > 0f ? $"{name} every {interval:0.##}s" : name,
                repeatSec: interval
            );
        }

        private TextCommandResult OnDot(TextCommandCallingArgs args)
        {
            string playerArg = args[0] as string;
            float amount = (float)args[1];
            float interval = (float)args[2];
            string damageTypeArg = args[4] as string;

            if (!TryParseDuration(args[3] as string, out int? durationArg))
                return TextCommandResult.Error(DurationParseError);
            int duration = durationArg ?? DefaultDurationSec;

            if (Math.Abs(amount) <= float.Epsilon)
                return TextCommandResult.Error("Amount must not be zero.");

            if (interval <= 0f)
                return TextCommandResult.Error("Interval must be greater than 0 seconds.");

            if (duration == 0)
                return TextCommandResult.Error(
                    "Duration must be greater than 0 seconds, or 'endless'."
                );

            EnumDamageType? damageType = null;
            if (!string.IsNullOrWhiteSpace(damageTypeArg))
            {
                if (!Enum.TryParse(damageTypeArg, true, out EnumDamageType parsed))
                    return TextCommandResult.Error(
                        $"'{damageTypeArg}' is not a damage type. Try Poison, Heal, Injury, Acid, Fire."
                    );
                damageType = parsed;
            }

            string effectId = EffectPrimitives.IdFor("health");
            string label = $"{amount:+0.##;-0.##} HP every {interval:0.##}s";

            return ApplyToTargets(
                args,
                playerArg,
                effectId,
                amount,
                duration,
                label,
                repeatSec: interval,
                damageType: damageType
            );
        }

        private TextCommandResult OnApply(TextCommandCallingArgs args)
        {
            string playerArg = args[0] as string;
            string effectId = (args[1] as string)?.ToLowerInvariant();
            float potency = (float)args[2];

            if (!TryParseDuration(args[3] as string, out int? duration))
                return TextCommandResult.Error(DurationParseError);

            if (!EffectRegistry.IsRegistered(effectId))
                return TextCommandResult.Error(
                    $"No effect '{effectId}' is registered. Use /efflib registered to see them."
                );

            if (Math.Abs(potency) <= float.Epsilon)
                return TextCommandResult.Error("Potency must not be zero.");

            return ApplyToTargets(args, playerArg, effectId, potency, duration, effectId);
        }

        private const string DurationParseError =
            "Duration must be a whole number of seconds, or 'endless' for one that lasts until death.";

        private static bool TryParseDuration(string arg, out int? seconds)
        {
            seconds = null;
            if (string.IsNullOrWhiteSpace(arg))
                return true;

            switch (arg.Trim().ToLowerInvariant())
            {
                case "endless":
                case "permanent":
                case "forever":
                case "infinite":
                case "inf":
                case "-1":
                    seconds = EffectContext.EndlessDuration;
                    return true;
            }

            if (
                int.TryParse(arg, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed)
                && parsed >= 0
            )
            {
                seconds = parsed;
                return true;
            }

            return false;
        }

        private TextCommandResult ApplyToTargets(
            TextCommandCallingArgs args,
            string playerArg,
            string effectId,
            float potency,
            int? durationOverride,
            string label,
            float repeatSec = 0f,
            EnumDamageType? damageType = null
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
                EffectManager manager = EntityBehaviorPlayerEffects.ManagerFor(target.Entity);
                EffectContext ctx = manager == null
                    ? null
                    : EffectRegistry.Build(effectId, potency);

                if (ctx == null)
                {
                    failed.Add(target.PlayerName);
                    continue;
                }

                if (durationOverride.HasValue)
                    ctx.Duration = durationOverride.Value;

                if (repeatSec > 0f)
                    EffectPrimitives.MakeRepeating(ctx, repeatSec, damageType);

                if (manager.IsActive(effectId))
                    manager.RemoveEffect(effectId, false);

                if (manager.TryApply(effectId, ctx, DisplayName(effectId, label)))
                    applied++;
                else
                    failed.Add(target.PlayerName);
            }

            string durationNote =
                durationOverride == EffectContext.EndlessDuration ? " (endless)"
                : durationOverride.GetValueOrDefault() > 0 ? $" for {durationOverride.Value}s"
                : "";
            string summary = $"Gave {label}{durationNote} to {applied} player(s).";
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

            EffectManager manager = EntityBehaviorPlayerEffects.ManagerFor(target.Entity);
            if (manager == null)
                return TextCommandResult.Error($"{target.PlayerName} has no effect manager yet.");

            List<ActiveEffectInfo> running = manager.GetActiveEffects();
            if (running.Count == 0)
                return TextCommandResult.Success($"{target.PlayerName} has no active effects.");

            StringBuilder sb = new();
            sb.Append(target.PlayerName).Append(" has ").Append(running.Count).Append(" effect(s):");
            foreach (
                ActiveEffectInfo info in running
                    .OrderBy(i => i.Endless)
                    .ThenBy(i => i.RemainingSec)
            )
            {
                sb.AppendLine()
                    .Append("  ")
                    .Append(info.Id)
                    .Append("  ")
                    .Append(info.Endless ? "endless" : FormatTime(info.RemainingSec) + " left");
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

            string effectId = effect;
            if (!string.IsNullOrEmpty(effect) && EffectPrimitives.Get(effect) != null)
                effectId = EffectPrimitives.IdFor(effect);
            else if (effect?.StartsWith("stat:", StringComparison.OrdinalIgnoreCase) == true)
                effectId = EffectPrimitives.StatIdPrefix + effect["stat:".Length..];
            effectId = effectId?.ToLowerInvariant();

            int cleared = 0;

            foreach (IServerPlayer target in targets)
            {
                EffectManager manager = EntityBehaviorPlayerEffects.ManagerFor(target.Entity);
                if (manager == null)
                    continue;

                if (string.IsNullOrEmpty(effectId))
                {
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
