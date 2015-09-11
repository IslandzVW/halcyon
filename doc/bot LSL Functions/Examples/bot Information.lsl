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
		//Returns the user who touched this object
		key user = llDetectedKey(0);
		//Returns the owner of the user who touched the object
	    key ownerOfBot = botGetOwner(user);
		//Returns whether the user who touched the object is a bot
		integer isABot = botIsBot(user);
		//Returns the position of the user if the user is a bot
		vector position = botGetPos(user);
		//Returns the name of the user if the user is a bot
		string botName = botGetName(user);
		
		//Print out all the information to local chat
		string userName = llKey2Name(user);
		llSay(0, "Information about " + userName + " (" + (string)user + ")");
		llSay(0, "    Is a bot: " + (string)isABot);
		llSay(0, "    Owner: " + (string)ownerOfBot);
		llSay(0, "    Position: " + (string)position);
		llSay(0, "    Bot name: " + botName);
	}
}