integer listenHandle;
 
default
{
    state_entry()
    {
	    //Creates the bot in the sim at the given location
        key botID = botCreateBot("Test", "Bot", "", llGetPos(), BOT_CREATE_DEFAULT);
        
		//Sets a listen handle on the bot that will listen to public chat for 
		// chat commands from the script owner. This differs from llListen as 
		// that would listen from this object's location, while this will listen
		// from the bot's current position.
		listenHandle = botListen(botID, PUBLIC_CHANNEL, "", llGetOwner(), "");
    }
 
    listen(integer channel, string name, key id, string message)
    {
	    //Gets the ID of the bot that generated this listen event
        string botID = iwDetectedBot();
		//Get the name of the bot from the bot ID
        string botName = botGetName(botID);
		//Tell the owner what the bot heard
        llOwnerSay(botName + " heard " + name + " say " + message);
        
        //Stop listening till the script resets
        llListenRemove(listenHandle);
        
		//Remove the bot now that we are done with it
        botRemoveBot(botID);
    }
}