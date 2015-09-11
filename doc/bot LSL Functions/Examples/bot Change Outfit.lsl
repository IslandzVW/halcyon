//The outfit name that will be created or updated 
// with botSetOutfit and that will be passed into 
// botCreateBot and botChangeOutfit to set the
// bots' appearance
string outfitName = "Test Outfit";
string outfitName2 = "Test Outfit 2";
key botID;
integer outfit = 0;

default 
{
    state_entry()
    {
        //Creates the bot with the given outfit name specified name above
		// or if the outfitName is "", the current appearance of the owner
        botID = botCreateBot("Test", "Bot", outfitName, llGetPos(), BOT_CREATE_DEFAULT);
    }
    
    touch_start(integer n)
    {
        if (outfit == 0)
        {
		    //Changes what the bot is wearing to the outfit "Test Outfit 2"
            botChangeOutfit(botID, outfitName2);
            outfit = 1;
        }
        else
        {
		    //Changes what the bot is wearing back to the outfit "Test Outfit"
            botChangeOutfit(botID, outfitName);
            outfit = 0;
        }
    }
}