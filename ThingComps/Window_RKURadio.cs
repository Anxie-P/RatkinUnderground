using Verse;

public class Dialog_RKU_Radio : Window
{
    private bool hasTraded = false;

    public void GiveSoldThingToTrader(Thing toGive, int countToGive, Pawn playerNegotiator)
    {
        if (toGive != null && !toGive.Destroyed && countToGive > 0)
        {
            toGive.Destroy();
            hasTraded = true; // 标记发生了交易
        }
    }

    public void GiveSoldThingToPlayer(Thing toGive, int countToGive, Pawn playerNegotiator)
    {
        if (toGive != null && !toGive.Destroyed && countToGive > 0)
        {
            Thing thing = toGive.SplitOff(countToGive);
            if (thing != null)
            {
                AddToCargoList(thing);
                hasTraded = true; // 标记发生了交易
            }
        }
    }
}