key botID;

default
{
    state_entry()
    {
        //Creates the bot in the sim at the given position
        botID = botCreateBot("Test", "Bot", "", llGetPos(), BOT_CREATE_DEFAULT);
        
		//The center of the allowed wandering area
		vector origin = llGetPos();
		//The distance away from the origin along the x-axis that the bot is allowed to wander
		float xDistance = 10;
		//The distance away from the origin along the y-axis that the bot is allowed to wander
		float yDistance = 10;
		
		//The list of options that will be passed into botWanderWithin
		// This can contain BOT_WANDER_MOVEMENT_TYPE and BOT_WANDER_TIME_BETWEEN_NODES
		//   BOT_WANDER_MOVEMENT_TYPE sets what form of movement the bot will utilize to wander
		//     around. The valid parameters for this are BOT_TRAVELMODE_WALK, BOT_TRAVELMODE_RUN,
		//     BOT_TRAVELMODE_FLY, BOT_TRAVELMODE_TELEPORT.
		//   BOT_WANDER_TIME_BETWEEN_NODES sets the amount of time that the bot will wait 
		//     (in seconds) before moving on to the next position.
		list options = [BOT_WANDER_MOVEMENT_TYPE, BOT_TRAVELMODE_WALK, 
			            BOT_WANDER_TIME_BETWEEN_NODES, 1];
		
        //Sets the allowed area that the bot can wander in
        botWanderWithin(botID, origin, xDistance, yDistance, options);
    }
	
	touch_start(integer n)
	{
	    //Stop the bots movement
	    botStopMovement(botID);
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