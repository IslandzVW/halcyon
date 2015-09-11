default
{
    touch_start(integer n)
    {
	    //Gets a list of all of the bot IDs in the sim
		// whether the script owner can control the 
		// bot or not.
        list allBots = botGetAllBotsInRegion();
        
		//Print out all the names of the bots being removed
		// and remove them from the sim if we have the permission to.
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