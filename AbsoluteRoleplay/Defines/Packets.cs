using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AbsoluteRoleplay.Packets
{
    internal class Packets
    {
        public enum ReceveirPackets
        {
            SWelcomeMessage = 1,
            SRecLoginStatus = 2,
            SRecAccPermissions = 3,
            SRecProfileBio = 4,
            SRecExistingProfile = 5,
            SSendProfile = 20,
            SDoneSending = 21,
            SNoProfileBio = 22,
            SNoProfile = 23,
            SSendProfileHook = 24,
            SSendNoProfileHooks = 25,
            SRecNoTargetHooks = 26,
            SRecNoTargetBio = 27,
            SRecTargetHooks = 28,
            SRecTargetBio = 29,
            SRecTargetProfile = 30,
            SRecNoTargetProfile = 31,
            SRecProfileStory = 32,
            SRecTargetStory = 33,
            SRecBookmarks = 34,
            SRecNoTargetStory = 35,
            SRecNoProfileStory = 36,
            SRecProfileGallery = 37,
            SRecGalleryImageLoaded = 38,
            SRecImageDeletionStatus = 39,
            SRecNoTargetGallery = 40,
            SRecTargetGallery = 41,
            SRecNoProfileGallery = 42,
            CProfileAlreadyReported = 43,
            CProfileReportedSuccessfully = 44,
            SSendProfileNotes = 45,
            SSendNoProfileNotes = 46,
            SSendNoAuthorization = 47,
            SSendVerificationMessage = 48,
            SSendVerified = 49,
            SSendPasswordModificationForm = 50,
            SSendOOC = 51,
            SSendTargetOOC = 52,
            SSendNoOOCInfo = 53,
            SSendNoTargetOOCInfo = 54,
            ReceiveConnections = 55,
            ReceiveNewConnectionRequest = 56,
            ReceiveChatMessage = 57,
            ReceiveGroupMemberships = 58,
        }
        public enum SenderPackets
        {
            CHelloServer = 1,
            CLogin = 2,
            CCreateProfile = 3,
            CFetchProfile = 4,
            CCreateProfileBio = 5,
            CRecProfileHooks = 6,
            CRecTargetProfileRequest = 7,
            CRecRegister = 8,
            CRecStoryCreation = 9,
            CRecBookmarkRequest = 10,
            CRecPlayerBookmark = 11,
            CRecRemovePlayerBookmark = 12,
            CRecGalleryImage = 13,
            CRecGalleryImagesRequest = 14,
            CRecGalleryRemoveImageRequest = 15,
            CReportProfile = 16,
            CAddProfileNotes = 17,
            CRecVerificationKey = 18,
            SSubmitRestorationRequest = 19,
            SRecPasswordChange = 20,
            SSendOOC = 21,
            SSendProfileAccessUpdate = 22,
            SSendConnectionsRequest = 23,
            SSendProfileStatus = 24,
        }
    }
}
