/*
 * Copyright (c) 2015, InWorldz Halcyon Developers
 * All rights reserved.
 * 
 * Redistribution and use in source and binary forms, with or without
 * modification, are permitted provided that the following conditions are met:
 * 
 *   * Redistributions of source code must retain the above copyright notice, this
 *     list of conditions and the following disclaimer.
 * 
 *   * Redistributions in binary form must reproduce the above copyright notice,
 *     this list of conditions and the following disclaimer in the documentation
 *     and/or other materials provided with the distribution.
 * 
 *   * Neither the name of halcyon nor the names of its
 *     contributors may be used to endorse or promote products derived from
 *     this software without specific prior written permission.
 * 
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS"
 * AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE
 * IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
 * DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT HOLDER OR CONTRIBUTORS BE LIABLE
 * FOR ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL
 * DAMAGES (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
 * SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
 * CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY,
 * OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse;
using OpenSim.Framework;
using OpenSim.Region.Framework.Scenes;

namespace OpenSim.Region.Framework.Interfaces
{
    public interface IBotManager
    {
        #region Bot Management

        /// <summary>
        /// Creates a bot and rezzes it inworld
        /// </summary>
        /// <param name="firstName">The first name of the bot</param>
        /// <param name="lastName">The surname of the bot</param>
        /// <param name="startPos">The position to rez the bot inworld</param>
        /// <param name="outfitName">The outfit the bot will wear (or blank to wear what the owner is currently wearing inworld)</param>
        /// <param name="itemID">The item that contains the script that is creating this bot</param>
        /// <param name="ownerID">The owner of the bot (UUID.Zero if all users can edit the bot)</param>
        /// <param name="reason">The reason the bot was not created (null if successful)</param>
        /// <returns>The UUID of the newly created bot, or UUID.Zero if bot creation failed</returns>
        UUID CreateBot(string firstName, string lastName, Vector3 startPos, string outfitName, UUID itemID, UUID ownerID, out string reason);

        /// <summary>
        /// Removes a bot from the region if the user has the permission to do so
        /// </summary>
        /// <param name="botID">The bot to remove</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether the removal was successful</returns>
        bool RemoveBot(UUID botID, UUID attemptingUser);

        /// <summary>
        /// Returns whether a given UUID is a bot in this region
        /// </summary>
        /// <param name="userID">The UUID to check</param>
        /// <returns>Whether the UUID belongs to a bot in this region</returns>
        bool IsBot(UUID userID);

        /// <summary>
        /// Returns the owner of a bot if the bot exists, otherwise, returns UUID.Zero
        /// </summary>
        /// <param name="botID">The bot</param>
        /// <returns>The owner of the bot</returns>
        UUID GetBotOwner(UUID botID);

        /// <summary>
        /// Returns the name of the bot
        /// </summary>
        /// <param name="botID">The bot</param>
        /// <returns>The name of the bot if the bot exists, otherwise, string.empty</returns>
        string GetBotName(UUID botID);

        /// <summary>
        /// Changes the given bot's owner to a new owner if the attempting user has the permission to do so
        /// </summary>
        /// <param name="botID">The bot whose owner will change</param>
        /// <param name="newOwnerID">The new userID that the owner will be changed to</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether the owner was changed successfully</returns>
        bool ChangeBotOwner(UUID botID, UUID newOwnerID, UUID attemptingUser);

        /// <summary>
        /// Returns a list of all bots in the region regardless of who owns them
        /// </summary>
        /// <returns></returns>
        List<UUID> GetAllBots();
        
        /// <summary>
        /// Returns a list of bots in the region that the given user has permissions for
        /// </summary>
        /// <param name="owner">The user</param>
        /// <returns></returns>
        List<UUID> GetAllOwnedBots(UUID owner);

        /// <summary>
        /// Set the profile info for the bot (so that users can look at the profile without it being blank
        /// </summary>
        /// <param name="botID">The bot whose profile will be updated</param>
        /// <param name="aboutText">Main profile about text</param>
        /// <param name="email">The email to show</param>
        /// <param name="imageUUID">Main image on the profile</param>
        /// <param name="profileURL">The profileURL to show</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns></returns>
        bool SetBotProfile(UUID botID, string aboutText, string email, 
            UUID? imageUUID, string profileURL, UUID attemptingUser);

        /// <summary>
        /// Checks to make sure that the given user can issue commands for the given bot
        /// </summary>
        /// <param name="botID">The bot</param>
        /// <param name="attemptingUser">The user attempting to issue some command to the bot</param>
        /// <returns>Whether the user has permission to issue the command</returns>
        bool CheckPermission(UUID botID, UUID attemptingUser);

        #endregion

        #region Bot Movement

        /// <summary>
        /// Sets the movement multiplier of the bot
        /// </summary>
        /// <param name="botID">The bot whose movement multiplier will be changed</param>
        /// <param name="speed">The speed multiplier</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether the movement multiplier was set successfully</returns>
        bool SetBotSpeed(UUID botID, float speed, UUID attemptingUser);

        /// <summary>
        /// Gets the position of the given bot in the region
        /// </summary>
        /// <param name="botID">The bot whose position will be found</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>The position of the bot, or Vector3.Zero if the user does not have permission</returns>
        Vector3 GetBotPosition(UUID botID, UUID attemptingUser);

        /// <summary>
        /// Moves the bot to a new location in the same region (teleports the bot)
        /// </summary>
        /// <param name="botID">The bot to teleport</param>
        /// <param name="position">The new postion to put the bot</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether the teleport was successfully completed</returns>
        bool SetBotPosition(UUID botID, Vector3 position, UUID attemptingUser);

        /// <summary>
        /// Sets the rotation of the bot inworld
        /// </summary>
        /// <param name="botID">The bot whose rotation will be set</param>
        /// <param name="rotation">The rotation to move the bot to</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether setting the rotation was successful</returns>
        bool SetBotRotation(UUID botID, Quaternion rotation, UUID attemptingUser);

        /// <summary>
        /// Causes the bot to follow an avatar around indefinitely
        /// </summary>
        /// <param name="botID">The bot who will begin following an avatar</param>
        /// <param name="avatarID">The avatar to follow</param>
        /// <param name="options">Options that determine how the bot will follow the avatar</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Information on whether the bot is now following the avatar or not</returns>
        BotMovementResult StartFollowingAvatar(UUID botID, UUID avatarID, Dictionary<int, object> options, UUID attemptingUser);

        /// <summary>
        /// Stops a bot from moving
        /// </summary>
        /// <param name="botID">The bot who will be prevented from moving</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether this operation was successful</returns>
        bool StopMovement(UUID botID, UUID attemptingUser);

        /// <summary>
        /// Sets the navigation nodes for the bot and begins traversing the nodes
        /// </summary>
        /// <param name="botID">The bot who will begin moving</param>
        /// <param name="positions">The nodes to move along</param>
        /// <param name="modes">The modes of travel that the bot will use to move along the nodes</param>
        /// <param name="options">Options that determine how the bot will follow the path</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Information on whether the bot is now traversing the nodes or not</returns>
        BotMovementResult SetBotNavigationPoints(UUID botID, List<Vector3> positions, List<TravelMode> modes, Dictionary<int, object> options, UUID attemptingUser);

        /// <summary>
        /// Causes the bot to wander randomly within the given area
        /// </summary>
        /// <param name="botID">The bot that will begin wandering</param>
        /// <param name="origin">The center of the wandering area</param>
        /// <param name="distances">The distance on each axis that the character can wander</param>
        /// <param name="dictOptions">The options that determine how the bot will move around</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Information on whether the bot is now wandering or not</returns>
        BotMovementResult WanderWithin(UUID botID, Vector3 origin, Vector3 distances, Dictionary<int, object> dictOptions, UUID attemptingUser);

        /// <summary>
        /// Causes the bot to temporarily stop moving (until <see cref="ResumeBotMovement"/> is called)
        /// </summary>
        /// <param name="botID">The bot who will stop moving</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether the operation was successful</returns>
        bool PauseBotMovement(UUID botID, UUID attemptingUser);

        /// <summary>
        /// Causes the bot to resume movement after <see cref="PauseBotMovement"/> has been called
        /// </summary>
        /// <param name="botID">The bot who will begin moving again</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether the operation was successful</returns>
        bool ResumeBotMovement(UUID botID, UUID attemptingUser);

        //Flee from avatar
        //Wandering?

        #endregion

        #region Bot Event Registration

        /// <summary>
        /// Register the script for path update events from the given bot
        /// </summary>
        /// <param name="botID">The bot to send update events from</param>
        /// <param name="itemID">The item that will be getting the path update events</param>
        /// <param name="attemptingUser">The user attempting to issue a command to the bot</param>
        /// <returns>Whether the operation succeeded</returns>
        bool BotRegisterForPathUpdateEvents(UUID botID, UUID itemID, UUID attemptingUser);

        /// <summary>
        /// Stops the script from getting path update events from the given bot
        /// </summary>
        /// <param name="botID">The bot that will stop sending path update events to the script</param>
        /// <param name="itemID">The script item that will stop receiving path update events</param>
        /// <param name="attemptingUser">The user attempting to issue a command to the bot</param>
        /// <returns>Whether the operation succeeded</returns>
        bool BotDeregisterFromPathUpdateEvents(UUID botID, UUID itemID, UUID attemptingUser);

        /// <summary>
        /// Registers the object to receieve collision events from the bot
        /// </summary>
        /// <param name="botID">The bot that will be sending collision events to the object</param>
        /// <param name="group">The group that will receieve collision events from the bot</param>
        /// <param name="attemptingUser">The user attempting to issue a command to the bot</param>
        /// <returns>Whether the operation succeeded</returns>
        bool BotRegisterForCollisionEvents(UUID botID, SceneObjectGroup group, UUID attemptingUser);

        /// <summary>
        /// Stops sending collision events from the bot to the object
        /// </summary>
        /// <param name="botID">The bot that will stop sending collision events to the object</param>
        /// <param name="group">The group that will stop receiving collision events from the bot</param>
        /// <param name="attemptingUser">The user attempting to issue a command to the bot</param>
        /// <returns>Whether the collision event was deregistered</returns>
        bool BotDeregisterFromCollisionEvents(UUID botID, SceneObjectGroup group, UUID attemptingUser);

        #endregion

        #region Bot Appearance

        /// <summary>
        /// Saves an avatar's appearance to the bot outfit table
        /// </summary>
        /// <param name="userID">The avatar whose appearance will be copied to the bot outfit table</param>
        /// <param name="outfitName">The name of the outfit that will be used to set the bot's appearance later</param>
        /// <returns>Whether the operation was successful</returns>
        bool SaveOutfitToDatabase(UUID userID, string outfitName, out string reason);

        /// <summary>
        /// Removes a bot outfit from the database
        /// </summary>
        /// <param name="userID">The user that owns the bot outfit</param>
        /// <param name="outfitName">The name of the outfit</param>
        void RemoveOutfitFromDatabase(UUID userID, string outfitName);

        /// <summary>
        /// Changes the outfit that the bot is wearing
        /// <param name="botID">The bot who will change outfits</param>
        /// <param name="outfitName">The name of the outfit</param>
        /// <param name="attemptingUser">The user attempting to issue a command to the bot</param>
        /// <returns>Whether the outfit was successfully changed</returns>
        bool ChangeBotOutfit(UUID botID, string outfitName, UUID attemptingUser, out string reason);

        /// <summary>
        /// Gets all bot outfits owned by the user
        /// </summary>
        /// <param name="userID">The user to check for</param>
        /// <returns>The list of all outfit names owned by the user</returns>
        List<string> GetBotOutfitsByOwner(UUID userID);

        #endregion

        #region Bot Animation

        /// <summary>
        /// Starts an animation on the given bot
        /// </summary>
        /// <param name="botID">The bot who will begin playing an animation</param>
        /// <param name="animID">The asset UUID of an animation. If this is UUID.Zero, <paramref name="anim"/> will be used to determine which animation will be played</param>
        /// <param name="anim">Only used if <paramref name="animID"/> is UUID.Zero. This refers to a default animation by the animation's name.</param>
        /// <param name="objectID">The object that is changing this bot's animation</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether the animation was successfully started</returns>
        bool StartBotAnimation(UUID botID, UUID animID, string anim, UUID objectID, UUID attemptingUser);

        /// <summary>
        /// Stops an animation that is running on the given bot
        /// </summary>
        /// <param name="botID">The bot that will stop playing an animation</param>
        /// <param name="animID">The asset UUID of an animation that is playing. If this is UUID.Zero, <paramref name="anim"/> will be used to determine which animation will be stopped</param>
        /// <param name="animation">Only used if <paramref name="animID"/> is UUID.Zero. This will stop a default animation with this name.</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether the animation was successfully stopped</returns>
        bool StopBotAnimation(UUID botID, UUID animID, string animation, UUID attemptingUser);

        #endregion

        #region Tagging

        /// <summary>
        /// Adds a tag to a bot that can later be used to identify a group of bots and also remove them
        /// </summary>
        /// <param name="botID">Bot to add the tag to</param>
        /// <param name="tag">The tag to add to the bot</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether the add operation was successful</returns>
        bool AddTagToBot(UUID botID, string tag, UUID attemptingUser);

        /// <summary>
        /// Returns true if a bot has the specified tag
        /// </summary>
        /// <param name="botID">Bot to check the tag of</param>
        /// <param name="tag">The tag to check for</param>
        /// <returns>true if the bot has the tag, false if not</returns>
        bool BotHasTag(UUID botID, string tag);

        /// <summary>
        /// Returns a list of all tags associated with a bot.
        /// </summary>
        /// <param name="botID">Bot to list the tags of</param>
        /// <returns>A list of all tags associated with a bot, if any.</returns>
        List<string> GetBotTags(UUID botID);

        /// <summary>
        /// Return all bots with the given tag attached in this region
        /// </summary>
        /// <param name="tag">The tag to look for</param>
        /// <returns>List of all bots with the given tag in the region</returns>
        List<UUID> GetBotsWithTag(string tag);

        /// <summary>
        /// Removes a tag from the given bot
        /// </summary>
        /// <param name="botID">Bot to remove the tag from</param>
        /// <param name="tag">The tag to remove</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether the remove operation was successful</returns>
        bool RemoveTagFromBot(UUID botID, string tag, UUID attemptingUser);

        /// <summary>
        /// Removes all bots from the region that have the given tag
        /// </summary>
        /// <param name="tag">The tag to check against</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether all bots in the region were able to be removed by this operation. 
        /// This can return false if the user does not have permission to remove one or more bots.</returns>
        bool RemoveBotsWithTag(string tag, UUID attemptingUser);

        #endregion

        #region Region Interaction Methods

        /// <summary>
        /// Sits a bot on an object
        /// </summary>
        /// <param name="botID">The bot who will sit down</param>
        /// <param name="objectID">The object part ID to sit on</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether the sit operation completed successfully</returns>
        bool SitBotOnObject(UUID botID, UUID objectID, UUID attemptingUser);

        /// <summary>
        /// Stands a bot up if the bot is sitting
        /// </summary>
        /// <param name="botID">The bot who will stand up</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether the bot is now standing</returns>
        bool StandBotUp(UUID botID, UUID attemptingUser);

        /// <summary>
        /// Forces the bot to touch an object
        /// </summary>
        /// <param name="botID">The bot who will touch the object</param>
        /// <param name="objectID">The object to touch</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether the bot touched the object</returns>
        bool BotTouchObject(UUID botID, UUID objectID, UUID attemptingUser);

        /// <summary>
        /// Causes the bot to give an inventory item from the given part
        /// </summary>
        /// <param name="botID">The bot who will offer the inventory item</param>
        /// <param name="part">The object that contains the inventory item that is being given</param>
        /// <param name="objName">The name of the item that is being given</param>
        /// <param name="objId">The UUID of the item being given</param>
        /// <param name="assetType">The assetType of the item being given</param>
        /// <param name="destId">The user that the item is being given to</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether the give was completed successfully</returns>
        bool GiveInventoryObject(UUID botID, SceneObjectPart part, string objName, UUID objId, byte assetType, UUID destId, UUID attemptingUser);

        #endregion

        #region Chat Methods

        /// <summary>
        /// Forces the bot to say some form of text in chat
        /// </summary>
        /// <param name="botID">The bot that will say something</param>
        /// <param name="channel">The channel to say the message on</param>
        /// <param name="message">The text that the bot will speak</param>
        /// <param name="sourceType">The form of speaking that the bot will do</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether the bot successfully spoke the message</returns>
        bool BotChat(UUID botID, int channel, string message, ChatTypeEnum sourceType, UUID attemptingUser);

        /// <summary>
        /// Sends an instant message from the bot to the given user
        /// </summary>
        /// <param name="botID">The bot who will send an instant message</param>
        /// <param name="userID">The user to send the instant message to</param>
        /// <param name="message">The message to send to the user</param>
        /// <param name="attemptingUser">The user that is attempting this operation (UUID.Zero to force permissions)</param>
        /// <returns>Whether the instant message was successfully sent</returns>
        bool SendInstantMessageForBot(UUID botID, UUID userID, string message, UUID attemptingUser);

        #endregion
    }

    /// <summary>
    /// The different modes of travel that the bot can utilize when following navigation nodes
    /// </summary>
    public enum TravelMode
    {
        Walk = 1,
        Run = 2,
        Fly = 3,
        Teleport = 4,
        Wait = 5,
        None
    }

    /// <summary>
    /// The result of attmepting to move the bot
    /// </summary>
    public enum BotMovementResult
    {
        Success,
        UserNotFound,
        BotNotFound,
    }
}
