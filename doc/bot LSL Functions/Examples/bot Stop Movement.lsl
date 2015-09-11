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
    }
    
    touch_start(integer n)
    {
	    //Stops any type of bot movement
	    botStopMovement(botID);
	}
}