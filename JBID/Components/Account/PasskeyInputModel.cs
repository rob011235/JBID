// Copyright (c) 2026 Robert Garner
// SPDX-License-Identifier: MIT

namespace JBID.Server.Components.Account
{
    /// <summary>
    /// Contains the browser response to a passkey operation.
    /// </summary>
    public class PasskeyInputModel
    {
        /// <summary>
        /// Gets or sets the credential response serialized as JSON.
        /// </summary>
        public string? CredentialJson { get; set; }

        /// <summary>
        /// Gets or sets the error reported by the browser.
        /// </summary>
        public string? Error { get; set; }
    }
}
