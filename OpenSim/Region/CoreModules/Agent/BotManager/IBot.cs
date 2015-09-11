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

namespace OpenSim.Region.CoreModules.Agent.BotManager
{
    public interface IBot
    {
        /// <summary>
        /// The first name of the bot
        /// </summary>
        string FirstName { get; }

        /// <summary>
        /// The last name of the bot
        /// </summary>
        string LastName { get; }

        /// <summary>
        /// The full name of the bot
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Whether the bot is currently frozen
        /// </summary>
        bool Frozen { get; }

        /// <summary>
        /// The UUID of the bot
        /// </summary>
        UUID AgentID { get; }

        /// <summary>
        /// The UUID of the owner of this bot
        /// </summary>
        UUID OwnerID { get; }

        /// <summary>
        /// The Scene that this bot is in
        /// </summary>
        IScene Scene { get; }

        /// <summary>
        /// The class that controls the movement of the bot
        /// </summary>
        BotMovementController MovementController { get; }

        /// <summary>
        /// List of script item IDs that are registered for path update events
        /// </summary>
        List<UUID> RegisteredScriptsForPathUpdateEvents { get; }

        /// <summary>
        /// The time that this bot was added to hte sim
        /// </summary>
        DateTime TimeCreated { get; }

        /// <summary>
        /// Causes the bot to say the given text on the given channel
        /// </summary>
        /// <param name="channel">The channel to speak the text on</param>
        /// <param name="message">The text to say</param>
        /// <param name="sourceType">How the text is to be said</param>
        void Say(int channel, string message, ChatTypeEnum sourceType);

        /// <summary>
        /// Sends an instant message to the given agent with the given message
        /// </summary>
        /// <param name="agent">The agent to send the message to</param>
        /// <param name="message">The message to send</param>
        void SendInstantMessage(UUID agent, string message);

        /// <summary>
        /// Causes the bot to sit on an object
        /// </summary>
        /// <param name="objectID">The ID of the object to sit on</param>
        /// <returns>Whether the bot is now sitting on the object</returns>
        bool SitOnObject(UUID objectID);

        /// <summary>
        /// Causes the bot to stand up
        /// </summary>
        /// <returns>Whether the bot is now standing</returns>
        bool StandUp();
        
        /// <summary>
        /// Causes the bot to touch an object
        /// </summary>
        /// <param name="objectID">The ID of the object to touch</param>
        /// <returns>Whether the bot touched the object</returns>
        bool TouchObject(UUID objectID);

        /// <summary>
        /// Causes the bot to give an inventory item to a user
        /// </summary>
        /// <param name="part">The object that the inventory item to give is in</param>
        /// <param name="objName">The name of the inventory item</param>
        /// <param name="objId">The ID of the inventory item</param>
        /// <param name="assetType">The assetType of the inventory item</param>
        /// <param name="destId">The ID of the user to send the item to</param>
        void GiveInventoryObject(SceneObjectPart part, string objName, UUID objId, byte assetType, UUID destId);

        /// <summary>
        /// Causes the bot to begin playing an animation
        /// </summary>
        /// <param name="animID">The ID of an animation to play. If UUID.Zero, anim will be checked to find the animation ID</param>
        /// <param name="anim">The name of an animation in the given object or of a default animation</param>
        /// <param name="objectID">The object that is starting the animation</param>
        /// <returns>Whether the animation is now playing</returns>
        bool StartAnimation(UUID animID, string anim, UUID objectID);

        /// <summary>
        /// Causes the bot to stop playing an animation
        /// </summary>
        /// <param name="animID">The ID of an animation to stop. If UUID.Zero, anim will be checked to find the animation ID</param>
        /// <param name="anim">The name of an animation in the given object or of a default animation</param>
        /// <returns>Whether the animation was successfully stopped</returns>
        bool StopAnimation(UUID animID, string anim);

        /// <summary>
        /// Sets the speed that the bot will move
        /// </summary>
        /// <param name="speed">The speed multipler of the bot (0 will stop all movement, 1 is default speed, 2 is twice the speed, etc)</param>
        /// <returns>Whether the speed multiplier was successfully set</returns>
        bool SetSpeed(float speed);
    }
}
