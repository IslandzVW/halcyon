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
// This can contain BOT_MOVEMENT_TYPE or BOT_MOVEMENT_TELEPORT_AFTER
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
	    //Creates the bot in the sim at the given position
        botID = botCreateBot("Test", "Bot", "", llGetPos(), BOT_CREATE_DEFAULT);
        
        //Sets the navigation points for the bot to follow
        botSetNavigationPoints(botID, positions, travelModes, options);
    }
    
	//This event tells information about what has happened to the bot
	//It has three parameters
	// botID - The ID of the bot that has triggered the update
	// flags/parameters - The different flags that tell 
	//   what has happened to the bot and parameters that tell
	//   more about what the bot will do next.
	//   These can be the following:
	//     BOT_MOVE_COMPLETE - Triggered when moving is complete.
	//       This will occur when the bot has either made it to the avatar
	//       if the bot is following an avatar or when the bot has completed
	//       moving through all of it's navigation points. Parameters is a 
	//       list that contains the current position of the bot.
	//     BOT_MOVE_UPDATE - Triggered when moving to the next navigation
	//       point is complete. This is only fired when the bot is traversing
	//       navigation points, not when the bot is following an avatar.
	//       Parameters is a list that contains an integer that represents 
	//       which node the bot will attempt to navigate to next and the 
	//       current position of the bot.
	//     BOT_MOVE_FAILED - Triggered when the bot failed to move to the next
	//       navigation position. This will occur if the bot has been attempting 
	//       to move to the next navigation point for longer than the teleport 
	//       after property specifies. This is only fired when the bot is traversing 
	//       navigation points, not when the bot is following an avatar.
	//       Parameters is a list that contains an integer that represents 
	//       which node the bot will attempt to navigate to next and the 
	//       current position of the bot.
	//     BOT_MOVE_AVATAR_LOST - Triggered when the bot has stopped following the 
	//       avatar because it has lost it. The bot has either lost sight of the 
	//       avatar (only if the option is set) or the avatar has gotten far enough
	//       away that the distance is greater than the lost avatar distance set. 
	//       This is only triggered if the bot is following an avatar.
	//       Parameters contains the position of the avatar that we are attempting
	//       to follow or zero_vector if the avatar has left the sim, the distance
	//       between the avatar and the bot, and the current position of the bot.
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
