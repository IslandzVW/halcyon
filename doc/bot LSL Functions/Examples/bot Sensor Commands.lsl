integer scanning = FALSE;
key botID;

default 
{
    state_entry()
    {
	    //Creates the bot at the given position in the sim
        botID = botCreateBot("Test", "Bot", "", llGetPos(), BOT_CREATE_DEFAULT);
    }
 
    touch_start(integer x)
	{
	    //Performs a single scan from the bot's position for all agents
		// 10m away from the bot
        botSensor(botID, "", NULL_KEY, AGENT, 10.0, PI);
    }
 
    sensor(integer x) 
	{
	    //Gets the bot that triggered this sensor event
        key botID = iwDetectedBot();
		
        string text = ""; 
		vector myPos = botGetPos(botID);
		text = botGetName(botID) + " sees:\n";
        // Loop through avatars from furthest to nearest
        while ((--x) >= 0)
		{
            key id = llDetectedKey(x);
 
            // Get display name, or use legacy name as a fallback
            string name = llGetDisplayName(id);
            if (("" == name) || ("???" == name)) 
				name = llDetectedName(x);
 
            // Add distance to the name
            name += " (" + (string)((integer)llVecDist(myPos, llDetectedPos(x))) + "m)";
            if (text) name = "\n" + name;
            text += name;
        }
		//Sets the names of the avatars away from the bot
        llSetText(text, <1.0, 1.0, 1.0>, 1.0);
 
        if (!scanning)
		{
            // Repeat the scan every 30 seconds
            botSensorRepeat(botID, "", NULL_KEY, AGENT, 10.0, PI, 30.0);
            scanning = TRUE;
        }
    } 
	
	no_sensor() 
	{
	    //No avatars were found near the bot
		
		//Get the ID of the bot that caused the no_sensor
        key detectedBotID = iwDetectedBot();
		
        // No avatars nearby, lets turn off the scan
        llSetText("", ZERO_VECTOR, 0.0);
        botSensorRemove();
        scanning = FALSE;
    }
}