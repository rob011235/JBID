// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBID.Server.Components.Account
{
    /// <summary>
    /// Identifies the passkey operation requested from the browser.
    /// </summary>
    public enum PasskeyOperation
    {
        /// <summary>
        /// Registers a new passkey.
        /// </summary>
        Create = 0,

        /// <summary>
        /// Authenticates using an existing passkey.
        /// </summary>
        Request = 1,
    }
}
