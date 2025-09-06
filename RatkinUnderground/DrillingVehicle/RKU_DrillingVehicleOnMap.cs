using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using RimWorld.Planet;
using RimWorld;
using HarmonyLib;
using static HarmonyLib.AccessTools;
using System.IO;
using Verse.Grammar;

namespace RatkinUnderground;
public class RKU_DrillingVehicleOnMap : Caravan
{
    public bool isHandled = false; // 标记是否已经处理过进任务地图

    public int destinationTile = -1;
    private bool arrived;
    private int initialTile = -1;
    private float traveledPct;
    private int eventTile = -1;

    private const float TilesPerSecond = 0.1f; // 移动进度

    private Vector3 Start => Find.WorldGrid.GetTileCenter(initialTile);
    private Vector3 End => Find.WorldGrid.GetTileCenter(destinationTile);

    public int hitPoints = -1;  // 传递耐久

    public float getTraveledPct() 
    {
        return traveledPct;
    }
    public void resetTraveledPct()
    {
        traveledPct = 0;
    }
    public bool IsArrived() => arrived;
    public float GetTraveledPct() => traveledPct;

    public override Vector3 DrawPos
    {
        get
        {
            if (!arrived && pather.MovingNow)
            {
                // 获取下一个格子
                int nextTile = pather.nextTile;
                if (nextTile < 0 || nextTile == Tile)
                {
                    nextTile = destinationTile;
                }

                // 计算当前位置和下一个位置
                Vector3 start = Find.WorldGrid.GetTileCenter(Tile);
                
                Vector3 end = Find.WorldGrid.GetTileCenter(nextTile);

                // 使用插值计算当前显示位置
                Vector3 tweenedPos = Vector3.Lerp(start, end, traveledPct);
                //Log.Message($"绘制的起始点{start}");
                return tweenedPos + CaravanTweenerUtility.CaravanCollisionPosOffsetFor(this);
            }
            return base.DrawPos;
        }
    }
    public override bool ExpandingIconFlipHorizontal
    {
        get
        {
            if (!pather.MovingNow)
            {
                return true;
            }

            Vector2 vector = GenWorldUI.WorldToUIPosition(Start);
            Vector2 vector2 = GenWorldUI.WorldToUIPosition(End);
            return vector2.x > vector.x; 
        }
    }

    public override float ExpandingIconRotation
    {
        get
        {
            if (!def.rotateGraphicWhenTraveling)
            {
                return base.ExpandingIconRotation;
            }

            Vector2 vector = GenWorldUI.WorldToUIPosition(Start);
            Vector2 vector2 = GenWorldUI.WorldToUIPosition(End);
            float num = Mathf.Atan2(vector2.y - vector.y, vector2.x - vector.x) * 57.29578f;

            // 确保角度始终为正
            if (num < 0)
            {
                num += 360f;
            }

            return num + 90f;
        }
    }

    public override void DrawExtraSelectionOverlays()
    {
        if (IsPlayerControlled && pather.curPath != null)
        {
            // 使用原版Caravan的路径绘制方法
            float num = 0.05f;
            Vector3 drawPos = DrawPos;
            Vector3 tileCenter3 = Find.WorldGrid.GetTileCenter(Peek(0));
            drawPos += drawPos.normalized * num;
            tileCenter3 += tileCenter3.normalized * num;
            if ((drawPos - tileCenter3).sqrMagnitude > 0.005f)
            {
                GenDraw.DrawWorldLineBetween(drawPos, tileCenter3);
            }

        }

        gotoMote.RenderMote();
    }

    public int Peek(int nodesAhead)
    {
        return pather.curPath.NodesReversed[pather.curPath.NodesLeftCount - 1 - nodesAhead];
    }


    public override void PostAdd()
    {
        base.PostAdd();
        initialTile = base.Tile;
        if (destinationTile >= 0)
        {
            tweener.ResetTweenedPosToRoot();
        }
    }
    public override void Tick()
    {
        
        base.Tick();

        // 检查是否有新的目标（通过pather的Destination）
        if (pather?.curPath != null && pather.Destination != destinationTile)
        {
            // 当钻机正在移动时，忽略目标变更请求
            if (!arrived && traveledPct > 0.01f)
            {
                // 强制路径器恢复原来的目标
                Messages.Message("RKU.CantChangeDestination", MessageTypeDefOf.PositiveEvent);
                pather.StopDead();
                pather.StartPath(destinationTile, null);
                return;
            }
            
            // 只有在钻机静止或刚开始移动时才允许切换目标
            destinationTile = pather.Destination;
            initialTile = Tile;
            traveledPct = 0f;
            arrived = false;
        }
        
        if (!arrived && pather.MovingNow)
        {            
            int nextTile = pather.nextTile;
            if (nextTile < 0 || nextTile == Tile)
            {
                nextTile = destinationTile;
            }

            float movePercentPerTick = TilesPerSecond / 60f;

            var nodes = pather.curPath.NodesReversed.Count;
            // 增加移动进度
            traveledPct += movePercentPerTick/ nodes;   //匀速
            //traveledPct += movePercentPerTick;    
            //Log.Message($"当前进度：{traveledPct}");
            if (traveledPct >= 1f)
            {
                // 已经到达下一个格子
                traveledPct = 0f;

                // 更新初始位置
                initialTile = nextTile;

                // 设置实际位置
                base.Tile = nextTile;
                tweener.ResetTweenedPosToRoot();

                // 检查是否到达最终目标
                if (nextTile == destinationTile)
                {
                    //nodeIndex = 1;
                    //detectTick = 0f;
                    Arrived();
                }
            }
        }
    }

    private void Arrived()
    {
        arrived = true;

        // 完全停止寻路器
        if (destinationTile == pather.Destination)
        {
            pather.StopDead();
        }

        // 更新位置
        base.Tile = destinationTile;
        tweener.ResetTweenedPosToRoot();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref destinationTile, "destinationTile", 0);
        Scribe_Values.Look(ref arrived, "arrived", defaultValue: false);
        Scribe_Values.Look(ref initialTile, "initialTile", 0);
        Scribe_Values.Look(ref traveledPct, "traveledPct", 0f);
    }

    public override IEnumerable<Gizmo> GetGizmos()
    {
        foreach (var gizmo in base.GetGizmos())
        {
            yield return gizmo;
        }

        if (!arrived && pather.MovingNow)
        {
            yield return new Command_Action
            {
                defaultLabel = "Test Event",
                defaultDesc = "Trigger the test event for the drilling vehicle.",
                icon = ContentFinder<Texture2D>.Get("UI/Commands/LaunchShip"),
                action = () =>
                {
                    //Log.Message($"生成事件前的进度{traveledPct}");
                    IncidentDef incidentDef = DefDatabase<IncidentDef>.AllDefs.ToList().FindAll(o=>o.defName.Contains(" ")).RandomElement();
                    if (incidentDef != null)
                    {
                        
                        int nodes = pather.curPath.NodesReversed.Count;
                        int index = (int)(traveledPct * (nodes - 1));
                        index = Mathf.Clamp(index, 0, nodes - 1);
                        int eventTile = pather.curPath.NodesReversed[index];
                        var temp = this;
                        temp.Tile = eventTile;
                        //Log.Message($"地图上drill的Tile：{base.Tile}");
                        IncidentParms parms = StorytellerUtility.DefaultParmsNow(incidentDef.category, temp);
                        //Log.Message($"parm的Tile：{parms.target.Tile}");
                        parms.target = this;
                        parms.faction = Faction.OfPlayer;
                        incidentDef.Worker.TryExecute(parms);
                        //Log.Message($"生成事件后的进度{traveledPct}");
                        traveledPct = 0;    //生成事件后起始地点会变，进度归0
                    }
                }
            };
        }
    }
}