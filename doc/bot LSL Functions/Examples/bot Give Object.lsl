key botID;
 
default
{
    state_entry()
    {
	    //Creates the bot in the sim
        botID = botCreateBot("Test", "Bot", "", llGetPos(), BOT_CREATE_DEFAULT);
    }
 
    touch_start(integer n)
    {
	    //Causes the bot to give the object that is in the
        // object that contains this script to the specified
		// user. Bots can only give inventory to users that are
		// currently in the same sim as the bot.
		string inventoryItem = "New Script";
		key userToGiveItemTo = llGetOwner();
        botGiveInventory(botID, userToGiveItemTo, inventoryItem);
    }
}