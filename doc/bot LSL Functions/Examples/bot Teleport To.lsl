//The index into the positions array of the position
// that we are currently at
integer position = 0;
//The ID of the bot we created
key botID;

//The list of positions that we will be teleporting to
list positions = [
    llGetPos() + <5,0,0>,
    llGetPos() + <0,5,0>,
    llGetPos() + <-5,0,0>,
    llGetPos() + <0,-5,0>];
    
default
{
    state_entry()
    {
        //Creates the bot in the sim
        botID = botCreateBot("Test", "Bot", "", llGetPos(), BOT_CREATE_DEFAULT);
        
        //Have the bot teleport to a new location every 5 seconds
        llSetTimerEvent(5);
    }
    
    timer()
    {
        //Get the next position to teleport to
        vector pos = llList2Vector(positions, position++);
        //Teleport the bot to the position
        botTeleportTo(botID, pos);
        
        //Go back to the beginning of the position list
        if(position == 4)
            position = 0;
    }
}
