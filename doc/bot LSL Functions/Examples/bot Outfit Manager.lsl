integer gListener;     // Identity of the listener associated with the dialog, so we can clean up when not needed
integer rListener;
key userWhoTouched; //The user who touched the object

default 
{
    touch_start(integer n)
    {
        llListenRemove(gListener);
        // get the UUID of the person touching this prim
        userWhoTouched = llDetectedKey(0);
		//Only let the script owner touch this object
		if(userWhoTouched == llGetOwner())
		{
			// Listen to any reply from that user only, and only on the same channel to be used by llDialog
			// It's best to set up the listener before issuing the dialog
			gListener = llListen(-99, "", userWhoTouched, "");
			// Send a dialog to that person. We'll use a fixed negative channel number for simplicity
			llDialog(userWhoTouched, "\nDo you want to add, remove, or list all bot outfits?", ["Add", "Remove", "List All" ] , -99);
			// Start a one-minute timer, after which we will stop listening for responses
			llSetTimerEvent(60.0);
		}
    }
    
    listen(integer chan, string name, key id, string msg)
    {
        if(chan == -99)
        {
            // If the user clicked the "Yes" button, kill this prim.
            if (msg == "Add")
            {
			    //Add the listener for the add outfit request
                rListener = llListen(-97, "", userWhoTouched, "");
                
				//Ask the user what the new outfit name will be named
                llTextBox(userWhoTouched, "The name of the outfit that will be added.", -97);
            }
            else if (msg == "Remove")
            {
                list allBots = botGetBotOutfits() + ["Cancel"];
                
				//Add the listener for removing an outfit
                rListener = llListen(-98, "", userWhoTouched, "");
				
				//Ask the user which outfit to remove
                llDialog(userWhoTouched, "\nClick the outfit name to remove.",  allBots, -98);
            }
            else if(msg == "List All")
            {
			    //Prints out a list of all bot outfits owned by the script owner
                list allBots = botGetBotOutfits();
                integer i = 0;
                string botOutfitNameStr = "List of all bot outfits that you own:\n";
                for(i = 0; i < llGetListLength(allBots); i++)
                {
                    string botOutfitName = llList2String(allBots, i);
                    botOutfitNameStr += botOutfitName + "\n";
                }
				//Send the 
                llInstantMessage(userWhoTouched, botOutfitNameStr);
                
                // Make the timer fire immediately, to do clean-up actions
                llSetTimerEvent(0.1);    
            }
        }
        else if(chan == -98)
        {
            if (msg != "Cancel")
            {
                //It's a remove request, remove the outfit with the given name
                botRemoveOutfit(msg);
            }
                
            // Make the timer fire immediately, to do clean-up actions
            llSetTimerEvent(0.1);   
        }    
        else if(chan == -97)
        {
            botSetOutfit(msg);
            
            // Make the timer fire immediately, to do clean-up actions
            llSetTimerEvent(0.1);   
        } 
    }
    timer()
    {
        // Stop listening. It's wise to do this to reduce lag
        llListenRemove(gListener);
        llListenRemove(rListener);
        // Stop the timer now that its job is done
        llSetTimerEvent(0.0);// you can use 0 as well to save memory
    }
}