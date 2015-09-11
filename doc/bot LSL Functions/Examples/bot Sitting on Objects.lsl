integer isSitting = FALSE;
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
        if(!isSitting)
        {
            //The key of the object to sit on
            key objectID = llGetKey();
            
            //Have the bot sit on this object
            botSitObject(botID, objectID);
        }
        else
        {
            //Causes the bot to stand up if the bot is sitting on something
            botStandUp(botID);
        }
        isSitting = !isSitting;
    }
}