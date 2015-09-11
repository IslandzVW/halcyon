rotation rot_xyzq;
rotation rot;
key botID;
 
default
{
    state_entry()
    {
	    //Calculate the initial rotation that we're going to be setting
		// and the rotation that we're going to add each time
		
        vector xyz_angles = <0,0,30.0>; // This is to define a 30 degree change
        vector angles_in_radians = xyz_angles*DEG_TO_RAD; // Change to Radians
        rot_xyzq = llEuler2Rot(angles_in_radians); // Change to a Rotation
        rot = <0,0,0,1>;
        
		//Creates the bot in the sim at the given position
        botID = botCreateBot("Test", "Bot", "", llGetPos(), BOT_CREATE_DEFAULT);
    }
 
    touch_start(integer s)
    {
	    //Increment the rotation that we need to set the bot to
        rot = rot * rot_xyzq;
		// Set the bot's rotation to the newly calculated rotation
        botSetRotation(botID, rot);
    }
}