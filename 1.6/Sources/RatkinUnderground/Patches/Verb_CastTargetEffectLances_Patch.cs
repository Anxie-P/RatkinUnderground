using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace RatkinUnderground.Patches
{
    //骑士长是boss才对！
    [HarmonyPatch(typeof(Verb_CastTargetEffectLances))]
    [HarmonyPatch("ValidateTarget")]
    public static class Verb_CastTargetEffectLances_ValidateTarget_Patch
    {
        static bool Prefix(ref bool __result, Verb_CastTargetEffectLances __instance, LocalTargetInfo target, bool showMessages = true)
        {
            Pawn pawn = target.Pawn;
            if (pawn != null && pawn.kindDef.defName == "RatkinKnightCommander")
            {
                __result = false;
                Widgets.MouseAttachedLabel("RKU_CannotTargetKnightCommander".Translate(pawn), 0f, -20f);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(Verb_CastTargetEffectLances))]
    [HarmonyPatch("OnGUI")]
    public static class Verb_CastTargetEffectLances_OnGUI_Patch
    {
        static void Prefix(ref Verb_CastTargetEffectLances __instance, LocalTargetInfo target)
        {
            if (__instance.CanHitTarget(target) && __instance.verbProps.targetParams.CanTarget(target.ToTargetInfo(__instance.caster.Map)))
            {
                Pawn pawn = target.Pawn;
                if (pawn != null && pawn.kindDef.defName == "RatkinKnightCommander")
                {
                    GenUI.DrawMouseAttachment(TexCommand.CannotShoot);
                    Widgets.MouseAttachedLabel("RKU_CannotTargetKnightCommander".Translate(pawn), 0f, -20f);
                }
            }
        }
    }
}
