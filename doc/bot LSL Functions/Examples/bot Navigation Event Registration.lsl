integer deregistered = TRUE;
key botID;

//The list of positions that the bot will be heading to, 
// along with waiting for 1 second between each position change
list positions = [
    llGetPos() + <5,0,0>,
    1.0,
    llGetPos() + <0,5,0>,
    1.0,
    llGetPos() + <-5,0,0>,
    1.0,
    llGetPos() + <0,-5,0>,
    1.0];
    
//The form of travel that the bot will utilize when moving between each position
list travelModes = [
    BOT_TRAVELMODE_WALK,
    BOT_TRAVELMODE_WAIT,
    BOT_TRAVELMODE_RUN,
    BOT_TRAVELMODE_WAIT,
    BOT_TRAVELMODE_FLY,
    BOT_TRAVELMODE_WAIT,
    BOT_TRAVELMODE_TELEPORT,
    BOT_TRAVELMODE_WAIT];
   
//The list of options that will be passed into botSetNavigationPoints
list options = [BOT_MOVEMENT_TYPE, BOT_MOVEMENT_FLAG_FOLLOW_INDEFINITELY,
    BOT_MOVEMENT_TELEPORT_AFTER, 5];

default
{
    state_entry()
    {
        //Creates the bot in the sim at the given position
        botID = botCreateBot("Test", "Bot", "", llGetPos(), BOT_CREATE_DEFAULT);
        
        //Sets the navigation points for the bot to follow
        botSetNavigationPoints(botID, positions, travelModes, options);
        
        //Deregister for now from navigation events until 
        botDeregisterFromNavigationEvents(botID);
    }
    
    touch_start(integer n)
    {
        if(deregistered)
        {
            //Register this script to navigation events for the bot
            botRegisterForNavigationEvents(botID);
        }
        else
        {
            //Deregister this script from navigation events
            botDeregisterFromNavigationEvents(botID);
        }
        deregistered = !deregistered;
        llSay(0, "Deregistered: " + (string)deregistered);
    }
    
    bot_update(string botID, integer flag, list parameters)
    {
        if(flag == BOT_MOVE_COMPLETE)
        {
            vector currentBotPosition = llList2Vector(parameters, 0);
            llSay(0, "Move completed - Bot position: " + (string)currentBotPosition);
        }
        else if(flag == BOT_MOVE_UPDATE)
        {
            integer nextNode = llList2Integer(parameters, 0);
            vector currentBotPosition = llList2Vector(parameters, 1);
            llSay(0, "Move to next node " + (string)nextNode + " completed - Bot position: " + (string)currentBotPosition);
        }
        else if(flag == BOT_MOVE_FAILED)
        {
            integer nextNode = llList2Integer(parameters, 0);
            vector currentBotPosition = llList2Vector(parameters, 1);
            llSay(0, "Move to next node " + (string)nextNode + " failed, teleporting to next node - Bot position: " + (string)currentBotPosition);
        }
        else if(flag == BOT_MOVE_AVATAR_LOST)
        {
            vector avatarPosition = llList2Vector(parameters, 0);
            float distanceAway = llList2Float(parameters, 1);
            vector currentBotPosition = llList2Vector(parameters, 2);
            llSay(0, "Lost avatar at " + (string)avatarPosition + ", avatar is " + (string)distanceAway + "m away - Bot position: " + (string)currentBotPosition);
        }
    }
}