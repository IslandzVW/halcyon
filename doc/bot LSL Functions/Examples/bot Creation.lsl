//The first name of the bot
string FirstName = "Test";
//The last name of the bot
string LastName = "Bot";
//The outfit name that the bot will be wearing
// If this is set to "", then the bot will be wearing
// the clothes that the owner is wearing currently.
// If this is not set to "", it will load the outfit
// from the database that was created by botSetOutfit.
string OutfitName = "";
//The position that the bot will be created at in the sim
vector startPosition = llGetPos();

//This stores the ID of the bot, which is passed into the all other bot
// commands to identify which bot is to be moved/edited.
key botID;

default
{
	state_entry()
	{
	    //This creates a bot in the sim with the given name, outfit at the given start position.
		botID = botCreateBot(FirstName, LastName, OutfitName, startPosition, BOT_CREATE_DEFAULT);
	}
	
	touch_start(integer n)
	{
		//Removes the bot that we created from the sim
		botRemoveBot(botID);
	}
}