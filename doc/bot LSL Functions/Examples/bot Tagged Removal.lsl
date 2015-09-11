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
		//Remove all bots from the sim with the given tag
		botRemoveBotsWithTag(botTag);
	}
}