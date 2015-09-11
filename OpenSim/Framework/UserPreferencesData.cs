/*
 * Copyright(C) 2010 Inworldz LLC
 * Initial Revision:  2010-06-27 David C. Daeschler
 */

using System;
using System.Collections.Generic;
using System.Text;
using OpenMetaverse;

namespace OpenSim.Framework
{
    /// <summary>
    /// Data structure that represents user preferences 
    /// </summary>
    public class UserPreferencesData
    {
        /// <summary>
        /// The id of the user these preferences are for
        /// </summary>
        private UUID _userId;

        /// <summary>
        /// Whether or not the user should receive IMs through their email
        /// </summary>
        private bool _receiveIMsViaEmail;

        /// <summary>
        /// Whether or not the user is listed in the directory
        /// </summary>
        private bool _listedInDirectory;



        /// <summary>
        /// The id of the user these preferences are for
        /// </summary>
        public UUID UserId
        {
            get
            {
                return _userId;
            }
        }

        /// <summary>
        /// Whether or not the user should receive IMs through their email
        /// </summary>
        public bool ReceiveIMsViaEmail
        {
            get
            {
                return _receiveIMsViaEmail;
            }
        }

        /// <summary>
        /// Whether or not the user is listed in the directory
        /// </summary>
        public bool ListedInDirectory
        {
            get
            {
                return _listedInDirectory;
            }
        }

        /// <summary>
        /// Returns the default set of preferences and marks them as being owned by the given user
        /// </summary>
        /// <param name="userId"></param>
        /// <returns></returns>
        public static UserPreferencesData GetDefaultPreferences(UUID userId)
        {
            UserPreferencesData defaultPrefs = new UserPreferencesData(userId, true, false);
            return defaultPrefs;
        }


        /// <summary>
        /// Creates a new user preferences block
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="recieveIMsViaEmail"></param>
        /// <param name="listedInDirectory"></param>
        public UserPreferencesData(UUID userId, bool recieveIMsViaEmail, bool listedInDirectory)
        {
            _userId = userId;
            _receiveIMsViaEmail = recieveIMsViaEmail;
            _listedInDirectory = listedInDirectory;
        }
    }
}
