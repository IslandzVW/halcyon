integer normalSpeed = TRUE;
key botID;

//The list of positions that the bot will be heading to
list positions = [
    llGetPos() + <5,0,0>,
    llGetPos() + <0,5,0>,
    llGetPos() + <-5,0,0>,
    llGetPos() + <0,-5,0>];
    
//The form of travel that the bot will utilize when moving between each position
list travelModes = [
    BOT_TRAVELMODE_WALK,
    BOT_TRAVELMODE_WALK,
    BOT_TRAVELMODE_WALK,
    BOT_TRAVELMODE_WALK];
   
//The list of options that will be passed into botSetNavigationPoints
list options = [BOT_MOVEMENT_TYPE, BOT_MOVEMENT_FLAG_FOLLOW_INDEFINITELY,
    BOT_MOVEMENT_TELEPORT_AFTER, 5];

default
{
    state_entry()
    {
        //Creates the bot in the sim
        botID = botCreateBot("Test", "Bot", "", llGetPos(), BOT_CREATE_DEFAULT);
        
        //Sets the navigation points for the bot to follow
        botSetNavigationPoints(botID, positions, travelModes, options);
    }
    
    touch_start(integer n)
    {
        if(normalSpeed)
        {
            botSetMovementSpeed(botID, 2);
        }
        else
        {
            botSetMovementSpeed(botID, 1);
        }
        normalSpeed = !normalSpeed;
    }
}
