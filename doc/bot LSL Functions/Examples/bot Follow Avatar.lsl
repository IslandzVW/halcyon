integer paused = 0;
key botID;

default
{
    state_entry()
    {
	    //Creates the bot in the sim
        botID = botCreateBot("Test", "Bot", "", llGetPos(), BOT_CREATE_DEFAULT);
		
		//Causes the bot to begin following the given avatar.
		key avatarToFollow = llGetOwner();
		//Contains a list of options that will change how the 
		// bot will follow the avatar.
		// The options are the following:
		// BOT_ALLOW_RUNNING - Sets whether the bot is allowed to run
		//   when attempting to follow the avatar. Takes a boolean
		//   (TRUE or FALSE) as to whether the bot can run.
		// BOT_ALLOW_FLYING - Sets whether the bot is allowed to fly
		//   when attempting to follow the avatar. Takes a boolean
		//   (TRUE or FALSE) as to whether the bot can fly.
		// BOT_ALLOW_JUMPING - Sets whether the bot is allowed to jump
		//   when attempting to follow the avatar. Takes a boolean
		//   (TRUE or FALSE) as to whether the bot can jump.
		// BOT_FOLLOW_OFFSET - Causes the bot to attempt to stay some 
		//   offset away from the avatar rather than attempting to get
		//   as close to the avatar as possible. Takes a vector parameter
		//   as the distance away to follow.
		// BOT_REQUIRES_LINE_OF_SIGHT - Causes the bot to only follow
		//   the avatar if the avatar is within sight and will 
		//   trigger the lost avatar event when the avatar leaves sight.
		//   Takes a boolean parameter (TRUE or FALSE).
		// BOT_START_FOLLOWING_DISTANCE - The distance before the bot
		//   will begin moving after the avatar once the bot has gotten
        //	 to the avatar and stopped. Takes a float that is the distance
		//   away from the avatar.
		// BOT_STOP_FOLLOWING_DISTANCE - The distance before the bot 
		//   will stop moving closer to the avatar. Takes a float that 
		//   is the distance away from the avatar.
		// BOT_LOST_AVATAR_DISTANCE - The distance before 
		//   the bot gives up attempting to follow the avatar and 
		//   triggers the lost avatar event. Takes a float that is the 
		//   distance away from the avatar.
		list botFollowingOptions = [BOT_ALLOW_FLYING, FALSE];
        botFollowAvatar(botID, avatarToFollow, botFollowingOptions);
    }
    
    touch_start(integer n)
    {
        if(paused)
        {
		    //Resumes the bot's movement once it has been paused by
			// botPauseMovement.
            botResumeMovement(botID);
        }
        else
        {
		    //Stops the bot's movements from either botFollowAvatar or 
			// botSetNavigationPath until botResumeMovement is called.
            botPauseMovement(botID);
        }
        paused = !paused;
    }
}
