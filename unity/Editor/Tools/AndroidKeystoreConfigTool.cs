using System;
using Newtonsoft.Json.Linq;
using UnityEditor;

namespace Unimancer
{
    /// <summary>
    /// Configures Android signing (custom keystore + key alias).
    ///
    /// SECURITY: the password values are applied to PlayerSettings but are NEVER
    /// returned in the result. The response carries only boolean flags indicating
    /// which fields were set, so secrets never travel back over the bridge.
    /// </summary>
    public class AndroidKeystoreConfigTool : McpToolBase
    {
        /// <inheritdoc />
        public override string Name => "android_keystore_config";

        /// <inheritdoc />
        public override string Description =>
            "Configure Android signing (custom keystore + key alias). Passwords are applied but never returned.";

        /// <inheritdoc />
        public override bool IsAsync => false;

        /// <summary>
        /// Apply the provided signing configuration to PlayerSettings.Android.
        /// </summary>
        /// <param name="parameters">useCustomKeystore plus optional keystore/key alias fields.</param>
        /// <returns>Booleans describing what was set (no secret values), or { error }.</returns>
        public override JObject Execute(JObject parameters)
        {
            try
            {
                if (parameters["useCustomKeystore"] != null)
                    PlayerSettings.Android.useCustomKeystore = parameters["useCustomKeystore"].ToObject<bool>();

                // Track what we set without ever echoing the values back.
                var setKeystorePath = parameters["keystorePath"] != null;
                var setKeystorePass = parameters["keystorePass"] != null;
                var setKeyaliasName = parameters["keyaliasName"] != null;
                var setKeyaliasPass = parameters["keyaliasPass"] != null;

                if (setKeystorePath)
                    PlayerSettings.Android.keystoreName = parameters["keystorePath"].ToString();
                if (setKeystorePass)
                    PlayerSettings.Android.keystorePass = parameters["keystorePass"].ToString();
                if (setKeyaliasName)
                    PlayerSettings.Android.keyaliasName = parameters["keyaliasName"].ToString();
                if (setKeyaliasPass)
                    PlayerSettings.Android.keyaliasPass = parameters["keyaliasPass"].ToString();

                return new JObject
                {
                    ["useCustomKeystore"] = PlayerSettings.Android.useCustomKeystore,
                    ["keystorePathSet"] = setKeystorePath,
                    ["keystorePassSet"] = setKeystorePass,
                    ["keyaliasNameSet"] = setKeyaliasName,
                    ["keyaliasPassSet"] = setKeyaliasPass,
                };
            }
            catch (Exception e)
            {
                return new JObject { ["error"] = e.Message };
            }
        }
    }
}
