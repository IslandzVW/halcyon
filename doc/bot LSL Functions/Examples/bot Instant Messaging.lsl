key botID;

default
{
	state_entry()
	{
	    //This creates a bot in the sim with the given name, outfit at the given start position.
		botID = botCreateBot("Test", "Bot", "", llGetPos(), BOT_CREATE_DEFAULT);
	}
	
	touch_start(integer n)
	{
	    //Has the bot send an instant message to the person who clicked on this object.
		botSendInstantMessage(botID, llDetectedKey(0), "Hi, I'm a bot, and I'm messaging you.");
	}
}