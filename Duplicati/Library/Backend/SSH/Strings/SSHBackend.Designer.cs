﻿//------------------------------------------------------------------------------
// <auto-generated>
//     This code was generated by a tool.
//     Runtime Version:2.0.50727.3053
//
//     Changes to this file may cause incorrect behavior and will be lost if
//     the code is regenerated.
// </auto-generated>
//------------------------------------------------------------------------------

namespace Duplicati.Library.Backend.Strings {
    using System;
    
    
    /// <summary>
    ///   A strongly-typed resource class, for looking up localized strings, etc.
    /// </summary>
    // This class was auto-generated by the StronglyTypedResourceBuilder
    // class via a tool like ResGen or Visual Studio.
    // To add or remove a member, edit your .ResX file then rerun ResGen
    // with the /str option, or rebuild your VS project.
    [global::System.CodeDom.Compiler.GeneratedCodeAttribute("System.Resources.Tools.StronglyTypedResourceBuilder", "2.0.0.0")]
    [global::System.Diagnostics.DebuggerNonUserCodeAttribute()]
    [global::System.Runtime.CompilerServices.CompilerGeneratedAttribute()]
    internal class SSHBackend {
        
        private static global::System.Resources.ResourceManager resourceMan;
        
        private static global::System.Globalization.CultureInfo resourceCulture;
        
        [global::System.Diagnostics.CodeAnalysis.SuppressMessageAttribute("Microsoft.Performance", "CA1811:AvoidUncalledPrivateCode")]
        internal SSHBackend() {
        }
        
        /// <summary>
        ///   Returns the cached ResourceManager instance used by this class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Resources.ResourceManager ResourceManager {
            get {
                if (object.ReferenceEquals(resourceMan, null)) {
                    global::System.Resources.ResourceManager temp = new global::System.Resources.ResourceManager("Duplicati.Library.Backend.Strings.SSHBackend", typeof(SSHBackend).Assembly);
                    resourceMan = temp;
                }
                return resourceMan;
            }
        }
        
        /// <summary>
        ///   Overrides the current thread's CurrentUICulture property for all
        ///   resource lookups using this strongly typed resource class.
        /// </summary>
        [global::System.ComponentModel.EditorBrowsableAttribute(global::System.ComponentModel.EditorBrowsableState.Advanced)]
        internal static global::System.Globalization.CultureInfo Culture {
            get {
                return resourceCulture;
            }
            set {
                resourceCulture = value;
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Login failed due to bad credentials, log: {0}.
        /// </summary>
        internal static string AuthenticationError {
            get {
                return ResourceManager.GetString("AuthenticationError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Timeout while closing session.
        /// </summary>
        internal static string CloseTimeoutError {
            get {
                return ResourceManager.GetString("CloseTimeoutError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Timeout occured while connecting, log: {0}.
        /// </summary>
        internal static string ConnectionTimeoutError {
            get {
                return ResourceManager.GetString("ConnectionTimeoutError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Delete complete.
        /// </summary>
        internal static string DebugDeleteFooter {
            get {
                return ResourceManager.GetString("DebugDeleteFooter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Delete start.
        /// </summary>
        internal static string DebugDeleteHeader {
            get {
                return ResourceManager.GetString("DebugDeleteHeader", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Get complete.
        /// </summary>
        internal static string DebugGetFooter {
            get {
                return ResourceManager.GetString("DebugGetFooter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Get start.
        /// </summary>
        internal static string DebugGetHeader {
            get {
                return ResourceManager.GetString("DebugGetHeader", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to List complete.
        /// </summary>
        internal static string DebugListFooter {
            get {
                return ResourceManager.GetString("DebugListFooter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to List start.
        /// </summary>
        internal static string DebugListHeader {
            get {
                return ResourceManager.GetString("DebugListHeader", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to parse line: {0}.
        /// </summary>
        internal static string DebugParseFailed {
            get {
                return ResourceManager.GetString("DebugParseFailed", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Put complete.
        /// </summary>
        internal static string DebugPutFooter {
            get {
                return ResourceManager.GetString("DebugPutFooter", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Put start.
        /// </summary>
        internal static string DebugPutHeader {
            get {
                return ResourceManager.GetString("DebugPutHeader", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to delete file: {0}.
        /// </summary>
        internal static string DeleteError {
            get {
                return ResourceManager.GetString("DeleteError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to This backend can read and write data to an SSH based backend, using SFTP. Allowed formats are &quot;ssh://hostname/folder&quot; or &quot;ssh://username:password@hostname/folder&quot;. NOTE: This backend does not support throttling uploads or downloads, and requires that sftp is installed (using putty for windows)..
        /// </summary>
        internal static string Description {
            get {
                return ResourceManager.GetString("Description", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The SSH backend relies on an external program (sftp) to work. Since the external program may change at any time, this may break the backend. Enable this option to get debug information about the ssh connection written to the console..
        /// </summary>
        internal static string DescriptionDebugToConsoleLong {
            get {
                return ResourceManager.GetString("DescriptionDebugToConsoleLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Prints debug info to the console.
        /// </summary>
        internal static string DescriptionDebugToConsoleShort {
            get {
                return ResourceManager.GetString("DescriptionDebugToConsoleShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The password used to connect to the server. This may also be supplied as the environment variable \&quot;FTP_PASSWORD\&quot;..
        /// </summary>
        internal static string DescriptionFTPPasswordLong {
            get {
                return ResourceManager.GetString("DescriptionFTPPasswordLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Supplies the password used to connect to the server.
        /// </summary>
        internal static string DescriptionFTPPasswordShort {
            get {
                return ResourceManager.GetString("DescriptionFTPPasswordShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The username used to connect to the server. This may also be supplied as the environment variable &quot;FTP_USERNAME&quot;..
        /// </summary>
        internal static string DescriptionFTPUsernameLong {
            get {
                return ResourceManager.GetString("DescriptionFTPUsernameLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Supplies the username used to connect to the server.
        /// </summary>
        internal static string DescriptionFTPUsernameShort {
            get {
                return ResourceManager.GetString("DescriptionFTPUsernameShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The full path to the &quot;sftp&quot; application..
        /// </summary>
        internal static string DescriptionSFTPCommandLong {
            get {
                return ResourceManager.GetString("DescriptionSFTPCommandLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The path to the &quot;sftp&quot; program.
        /// </summary>
        internal static string DescriptionSFTPCommandShort {
            get {
                return ResourceManager.GetString("DescriptionSFTPCommandShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Supply any extra commandline arguments, which are passed unaltered to the ssh application.
        /// </summary>
        internal static string DescriptionSSHOptionsLong {
            get {
                return ResourceManager.GetString("DescriptionSSHOptionsLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Extra options to the ssh commands.
        /// </summary>
        internal static string DescriptionSSHOptionsShort {
            get {
                return ResourceManager.GetString("DescriptionSSHOptionsShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The SSH backend relies on an external program (sftp) to work. Since the external program may hang, Duplicati must use a timeout to detect a stall in the external program. Use this option to adjust the timeout. Minimum allowed value is one minute, maximum allowed is one hour..
        /// </summary>
        internal static string DescriptionTransferTimeoutLong {
            get {
                return ResourceManager.GetString("DescriptionTransferTimeoutLong", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Sets the timeout for transfering a file.
        /// </summary>
        internal static string DescriptionTransferTimeoutShort {
            get {
                return ResourceManager.GetString("DescriptionTransferTimeoutShort", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to SSH based.
        /// </summary>
        internal static string DisplayName {
            get {
                return ResourceManager.GetString("DisplayName", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Timeout while downloading file.
        /// </summary>
        internal static string DownloadTimeoutError {
            get {
                return ResourceManager.GetString("DownloadTimeoutError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Folder not found: {0}, log: {1}.
        /// </summary>
        internal static string FolderNotFoundError {
            get {
                return ResourceManager.GetString("FolderNotFoundError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to validate the remote directory: {0}.
        /// </summary>
        internal static string FolderVerificationError {
            get {
                return ResourceManager.GetString("FolderVerificationError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to The host is not authenticated, please connect to the host using SSH, and then re-rerun Duplicati, log: {0}.
        /// </summary>
        internal static string HostNotAuthenticatedError {
            get {
                return ResourceManager.GetString("HostNotAuthenticatedError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to start the SSH application ({0}).\nMake sure that &quot;expect&quot; is installed.\nError message: {1}.
        /// </summary>
        internal static string LaunchErrorLinux {
            get {
                return ResourceManager.GetString("LaunchErrorLinux", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to start the SSH application ({0}).\nMake sure that &quot;putty&quot; is installed, and you have set the correct path.\nError message: {1}.
        /// </summary>
        internal static string LaunchErrorWindows {
            get {
                return ResourceManager.GetString("LaunchErrorWindows", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A password was expected, but passwordless login was specified.
        /// </summary>
        internal static string PasswordMissingError {
            get {
                return ResourceManager.GetString("PasswordMissingError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Unexpected error: {0}.
        /// </summary>
        internal static string UnexpectedConnectionError {
            get {
                return ResourceManager.GetString("UnexpectedConnectionError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Got unexpected exit response.
        /// </summary>
        internal static string UnexpectedExitResponseError {
            get {
                return ResourceManager.GetString("UnexpectedExitResponseError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Failed to get expected response to command: {0}.
        /// </summary>
        internal static string UnexpectedResponseError {
            get {
                return ResourceManager.GetString("UnexpectedResponseError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to Timeout while uploading file.
        /// </summary>
        internal static string UploadTimeoutError {
            get {
                return ResourceManager.GetString("UploadTimeoutError", resourceCulture);
            }
        }
        
        /// <summary>
        ///   Looks up a localized string similar to A username was expected, but none was supplied.
        /// </summary>
        internal static string UsernameMissingError {
            get {
                return ResourceManager.GetString("UsernameMissingError", resourceCulture);
            }
        }
    }
}
