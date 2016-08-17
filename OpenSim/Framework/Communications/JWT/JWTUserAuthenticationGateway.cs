namespace OpenSim.Framework.Communications.JWT
{
    /// <summary>
    /// Provides access to JWT tokens that are granted by authenticating a user
    /// against the halcyon user table
    /// </summary>
    class JWTUserAuthenticationGateway
    {
        private readonly IUserService _userService;

        public JWTUserAuthenticationGateway(IUserService userService)
        {
            _userService = userService;
        }


        /// <summary>
        /// Authenticates a user and returns a JWT token serialized as JSON
        /// </summary>
        /// <param name="username">The username to authenticate</param>
        /// <param name="password">The user's password</param>
        /// <param name="minLevel">The minimum godlevel this user must be at to generate a token</param>
        /// <param name="payloadOptions">Options for the generated payload</param>
        /// <returns>JWT token string</returns>
        public string Authenticate(string username, string password, int minLevel,
            PayloadOptions payloadOptions)
        {
            UserProfileData profile = _userService.GetUserProfile(username, password, true);

            return "";
        }
    }
}
