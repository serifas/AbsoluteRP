using System;
using System.Collections.Generic;

namespace Networking
{
    // Payload for Welcome Message
    public class WelcomeMessagePayload
    {
        public string Message { get; set; }
    }

    // Payload for sending status messages (e.g. login status)
    public class SendStatusMessagePayload
    {
        public string Username { get; set; }
        public int Status { get; set; }
    }

    // Payload for sending bookmarks
    public class SendBookmarksPayload
    {
        public string Bookmark { get; set; }
    }

    // Payload for when no target profile is found
    public class SendNoTargetProfilePayload
    {
        public string CharacterName { get; set; }
        public string CharacterWorld { get; set; }
    }

    // Payload indicating the completion of a sending process
    public class DoneSendingPayload
    {
        public bool Status { get; set; }
    }

    // Payload for sending image deletion status
    public class SendDeletedImageStatusPayload
    {
        public int Status { get; set; }
        public int Index { get; set; }
        public int FileCount { get; set; }
    }

    // Payload for sending gallery images
    public class SendGalleryImagesPayload
    {
        public List<string> Urls { get; set; }
        public bool[] NSFWImages { get; set; }
        public bool[] TriggerImages { get; set; }
        public int ProfileId { get; set; }
    }

    // Payload indicating if an existing profile is found or not
    public class SendExistingProfilePayload
    {
        public bool Status { get; set; }
    }

    // Payload for sending a target profile's basic information
    public class SendTargetProfilePayload
    {
        public string CharacterName { get; set; }
        public string CharacterWorld { get; set; }
    }

    // Payload for sending hooks (user-defined custom actions or tags)
    public class SendHooksPayload
    {
        public SortedList<int, Tuple<string, string>> Hooks { get; set; }
    }

    // Payload for sending a profile's story
    public class SendProfileStoryPayload
    {
        public string StoryTitle { get; set; }
        public SortedList<int, Tuple<string, string>> Chapters { get; set; }
    }

    // Payload for sending a target profile's story
    public class SendTargetStoryPayload
    {
        public string StoryTitle { get; set; }
        public SortedList<int, Tuple<string, string>> Chapters { get; set; }
    }

    // Payload for sending a profile's biography
    public class SendProfileBioPayload
    {
        public int ProfileID { get; set; }
        public byte[] Avatar { get; set; }
        public string Name { get; set; }
        public string Race { get; set; }
        public string Gender { get; set; }
        public string Age { get; set; }
        public string Height { get; set; }
        public string Weight { get; set; }
        public string AtFirstGlance { get; set; }
        public int Alignment { get; set; }
        public int Personality1 { get; set; }
        public int Personality2 { get; set; }
        public int Personality3 { get; set; }
    }

    // Payload for sending target profile biography
    public class SendTargetBioPayload
    {
        public int ProfileID { get; set; }
        public byte[] Avatar { get; set; }
        public string Name { get; set; }
        public string Race { get; set; }
        public string Gender { get; set; }
        public string Age { get; set; }
        public string Height { get; set; }
        public string Weight { get; set; }
        public string AtFirstGlance { get; set; }
        public int Alignment { get; set; }
        public int Personality1 { get; set; }
        public int Personality2 { get; set; }
        public int Personality3 { get; set; }
    }

    // Payload for sending image load confirmation
    public class SendImageLoadedPayload
    {
        public int Index { get; set; }
    }

    // Payload for sending OOC (out-of-character) information
    public class SendOocPayload
    {
        public string Ooc { get; set; }
    }

    // Payload for sending profile notes
    public class SendProfileNotesPayload
    {
        public string Notes { get; set; }
    }

    // Payload for sending password modification form data
    public class SendPasswordModificationFormPayload
    {
        public string Email { get; set; }
    }

    // Payload for sending a list of connections (friendships, etc.)
    public class SendConnectionsPayload
    {
        public List<Connection> Connections { get; set; }
    }

    // Sample connection class for use in connections payload
    public class Connection
    {
        public string RequesterName { get; set; }
        public string RequesterWorld { get; set; }
        public string ReceiverName { get; set; }
        public string ReceiverWorld { get; set; }
        public int Status { get; set; }
        public bool IsReceiver { get; set; }
    }
}
