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
		string name = llDetectedName(0);
		//Print out who touched the object
		llSay(0, "Touched by " + name);
	}
	
	collision_start(integer n)
	{
		//Have the bot touch this object
		botTouchObject(botID, llGetKey());
	}
}