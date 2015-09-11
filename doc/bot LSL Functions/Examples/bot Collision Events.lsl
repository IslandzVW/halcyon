key botID;
//Whether the this script is currently receiving collision events from the bot
integer receivingCollisionEvents = FALSE;

default 
{
    state_entry()
    {
	    //Causes this object to become phantom
		llSetStatus(STATUS_PHANTOM, TRUE);
	    
		//Creates the bot in the sim at the given location
        botID = botCreateBot("Test", "Bot", "", llGetPos(), BOT_CREATE_DEFAULT);
    }
    
    touch_start(integer n)
    {
        if(!receivingCollisionEvents)
        {
		    //Register ourself for collision events from the bot
			// This will fire collision, collision_start, collision_end,
			// land_collision, land_collision_start, and land_collision_end
			// events for all scripts in the object when the bot collides
			// with something.
            botRegisterForCollisionEvents(botID);
        }
        else
        {
		    //Stops this object from receiving collision events from the bot.
            botDeregisterFromCollisionEvents(botID);
        }
        receivingCollisionEvents = !receivingCollisionEvents;
    }
    
    collision_start(integer n)
    {
	    //Prints who started collided with the bot
        llSay(0, llDetectedName(0) + " started pushing me.");
    }
    
    collision(integer n)
    {
	    //Prints who is continuing to collide with the bot
        llSay(0, llDetectedName(0) + " is pushing me.");
    }
    
    collision_end(integer n)
    {
	    //Prints who has finished colliding with the bot
        llSay(0, llDetectedName(0) + " pushed me.");
    }
}