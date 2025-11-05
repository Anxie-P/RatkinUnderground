using HarmonyLib;
using RimWorld.Planet;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace RatkinUnderground
{
    [StaticConstructorOnStartup]
    internal class RemoveMapPatch
    {
        [HarmonyPatch(typeof(MapParent), "GetGizmos")]
        public static class MapParent_GetGizmos_Patch
        {
            [HarmonyPostfix]
            public static void MapParent_GetGizmosPostfix(ref IEnumerable<Gizmo> __result, MapParent __instance)
            {
                if (__instance.MapGeneratorDef.HasModExtension<RKU_MapGeneratorDefModExtension>())
                {
                    var originalGizmos = __result.ToList();

                    Command_Action command_Action = new Command_Action();
                    command_Action.defaultLabel = "RKU_RandomSiteDeleteMap".Translate();
                    command_Action.defaultDesc = "RKU_CommandViewQuestDesc".Translate();
                    command_Action.icon = ContentFinder<Texture2D>.Get("UI/Commands/AbandonHome");
                    command_Action.action = delegate
                    {
                        Find.WindowStack.Add(new Dialog_Confirm(__instance));
                    };
                    originalGizmos.Add(command_Action);

                    __result = originalGizmos;
                }
            }
        }

        public class Dialog_Confirm : Window
        {
            bool confirmed = false;
            MapParent mapParent;
            public Dialog_Confirm(MapParent mapParent)
            {
                doCloseButton = false;
                doCloseX = true;
                closeOnClickedOutside = true;
                absorbInputAroundWindow = true;
                this.mapParent = mapParent;
                this.forcePause = true;
            }

            public override void DoWindowContents(Rect inRect)
            {
                Listing_Standard listing_Standard = new Listing_Standard();
                listing_Standard.Begin(inRect);
                
                listing_Standard.Label("RKU_RandomSite_Confirm".Translate());
                listing_Standard.Gap();
                
                listing_Standard.End();
                
                // 使用原版风格的按钮布局
                float buttonWidth = 120f;
                float buttonHeight = 30f;
                float centerX = inRect.center.x - buttonWidth / 2f;
                float buttonY = inRect.yMax - buttonHeight - 10f;
                
                if (Widgets.ButtonText(new Rect(centerX - buttonWidth - 5f, buttonY, buttonWidth, buttonHeight), "Cancel".Translate()))
                {
                    this.Close();
                }
                
                if (Widgets.ButtonText(new Rect(centerX + 5f, buttonY, buttonWidth, buttonHeight), "RKU_RandomSite_Confirm_Accept".Translate()))
                {
                    confirmed = true;
                    this.mapParent.Destroy();
                    this.Close();
                }
            }
        }
    }
}