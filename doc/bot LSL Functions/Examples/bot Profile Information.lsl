//The about text that is displayed on the front page of the profile
string aboutText = "About this bot!";

//The email of the bot
string emailAddress = "bot@example.com";

//The image that is shown on the main page of the profile
// (This is set to a default water image for this script)
key profileImage = "00000000-0000-2222-3333-100000001041";

//The URL that is displayed on the Web page of the profile
string profileURL = "http://google.com";

//UUID of the bot
key botID;

default
{
    state_entry()
    {
        //This creates a bot in the sim with the given name, outfit at the given start position.
        botID = botCreateBot("Test", "Bot", "", llGetPos(), BOT_CREATE_DEFAULT);

		//Set the profile information for the bot
		
		list profileInformation = [BOT_ABOUT_TEXT, aboutText, BOT_EMAIL, emailAddress,
			BOT_IMAGE_UUID, profileImage, BOT_PROFILE_URL, profileURL];
		
        botSetProfileParams(botID, profileInformation);
    }
	
	touch_start(integer n)
	{
		list profileInformation = botGetProfileParams(botID, [BOT_ABOUT_TEXT, BOT_EMAIL, BOT_IMAGE_UUID, BOT_PROFILE_URL]);
		
		llSay(0, "Bot profile information");
		llSay(0, "About Text: " + llList2String(profileInformation, 0));
		llSay(0, "Email: " + llList2String(profileInformation, 1));
		llSay(0, "Image UUID: " + (string)llList2Key(profileInformation, 2));
		llSay(0, "Profile URL: " + llList2String(profileInformation, 3));
	}
}