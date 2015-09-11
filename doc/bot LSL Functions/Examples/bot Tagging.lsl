//By adding a tag to a bot, you can group multiple bots in the sim 
// together to better allow you to control many bots.
//The tag commands allow you to add, remove, and retrieve a list of bot IDs.
string botTag = "Bot Test Tag";

default
{
	state_entry()
	{
	    //This creates a bot in the sim with the given name, outfit at the given start position.
		key botID = botCreateBot("Test", "Bot", "", llGetPos(), BOT_CREATE_DEFAULT);
		
		//Add the tag to the bot
		botAddTag(botID, botTag);
	}
	
	touch_start(integer n)
	{
		//Get a list of all bots in the sim with the tag stored in botTag
		list bots = botGetBotsWithTag(botTag);
		
		//Print out a list of all of the bots in the list
		integer i = 0;
		llSay(0, "Bots with the tag " + botTag);
		for(i = 0; i < llGetListLength(bots); i++)
		{
			key botID = llList2Key(bots, i);
			llSay(0, "    " + botGetName(botID));
		}
	}
}