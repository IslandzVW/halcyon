key botID;
integer playingAnimation = FALSE;
//The name or UUID of the animation that will be played on the bot
string animationName = "away";

default
{
	state_entry()
	{
	    //This creates a bot in the sim with the given name, outfit at the given start position.
		botID = botCreateBot("Test", "Bot", "", llGetPos(), BOT_CREATE_DEFAULT);
	}
	
	touch_start(integer n)
	{
		if(playingAnimation)
		{
			//Stops the animation that botStartAnimation started playing
			botStopAnimation(botID, animationName);
		}
		else
		{
		    //Starts playing an animation on the bot with the given name or UUID
			botStartAnimation(botID, animationName);
		}
		playingAnimation = !playingAnimation;
	}
}