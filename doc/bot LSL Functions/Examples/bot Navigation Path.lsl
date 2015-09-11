integer paused = 0;
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
// Valid values are:
//  BOT_TRAVELMODE_WALK
//  BOT_TRAVELMODE_RUN
//  BOT_TRAVELMODE_FLY
//  BOT_TRAVELMODE_TELEPORT
//  BOT_TRAVELMODE_WAIT
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
// This can contain BOT_MOVEMENT_TYPE and BOT_MOVEMENT_TELEPORT_AFTER
//  BOT_MOVEMENT_TYPE can have the secondary parameter
//   BOT_MOVEMENT_FLAG_NONE or BOT_MOVEMENT_FLAG_FOLLOW_INDEFINITELY
//   BOT_MOVEMENT_FLAG_NONE will stop the bot from moving once all positions have been traveled to once.
//   BOT_MOVEMENT_FLAG_FOLLOW_INDEFINITELY will cause the bot to start at the beginning of the 
//     list again once it has finished, making the bot continuously loop through the position list.
//  BOT_MOVEMENT_TELEPORT_AFTER can have the float that is the number of seconds
//   till the bot will teleport to the next position if it gets stuck.
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
        if(paused)
        {
            //Allows the bot to continue moving on the path that it was previously traveling
            botResumeMovement(botID);
        }
        else
        {
            //Stops the bot movement until botResumeMovement is called.
            botPauseMovement(botID);
        }
        paused = !paused;
    }
}
