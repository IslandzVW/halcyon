default
{
    touch_start(integer n)
    {
	    //Gets a list of all of the bot IDs that are owned by
		// the script owner or that are able to be controlled
		// by any user (NULL_KEY owned bots)
        list allBots = botGetAllMyBotsInRegion();
        
		//Print out all the names of the bots being removed
		// and remove them from the sim
        integer i = 0;
        for(i = 0; i < llGetListLength(allBots); i++)
        {
            key bot = llList2Key(allBots, i);
			//Say the name of the bot that we are removing
            llSay(0, "Removing bot: " + botGetName(bot));
			//Remove the bot from the sim
            botRemoveBot(bot);
        }
    }
}