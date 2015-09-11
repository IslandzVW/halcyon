integer isTyping = FALSE;
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
		if(!isTyping)
		{
			//Makes the bot begin typing
			botStartTyping(botID);
			botSay(botID, 0, "I'm starting to type");
		}
		else if(isTyping)
		{
			botSay(botID, 0, "I'm finished typing");
			//Makes the bot stop typing
			botStopTyping(botID);
		}
		isTyping = !isTyping;
	}
}