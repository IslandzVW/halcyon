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
		//Sends a linked message to all objects attached the bot with the given parameters.
		integer num = -1;
		string msg = "message";
		key key_uuid = llDetectedKey(0);
        botMessageLinked(botID, num, msg, key_uuid);
    }
}