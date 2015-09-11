string FirstName = "Test";
string LastName = "Bot";
string OutfitName = "";
vector startPosition = llGetPos();

integer chatType = 0;
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
        if(chatType == 0)
        {
            //Makes the bot whisper the given text
            botWhisper(botID, 0, "I'm whispering");
        }
        else if(chatType == 1)
        {
            //Makes the bot say the given text
            botSay(botID, 0, "I'm talking");
        }
        else if(chatType == 2)
        {
            //Makes the bot shout the given text
            botShout(botID, 0, "I'm shouting");
            chatType = -1;
        }
        chatType++;
    }
}