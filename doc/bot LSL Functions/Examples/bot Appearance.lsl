//The outfit name that will be created or updated 
// with botSetOutfit and that will be passed into 
// botCreateBot to set the bots' appearance
string outfitName = "Test Outfit";
key botID;

default 
{
    state_entry()
    {
		//Creates a bot outfit from your current appearance 
		// that you will be able to have bots wear later 
		// with the given name.
		// If an outfit exists with this name, it will be 
		// overridden with the new information.
		botSetOutfit(outfitName);
    }
	
	touch_start(integer n)
	{
		if (botID != "")
		{
		    //Remove the bot now that we've created it
			botRemoveBot(botID);
			botID = "";
		}
		else
		{
		    //Creates the bot with the given outfit name specified name above
			botID = botCreateBot("Test", "Bot", outfitName, llGetPos(), BOT_CREATE_DEFAULT);
		}
	}
}