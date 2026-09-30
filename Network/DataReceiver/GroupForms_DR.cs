using AbsoluteRP;
using AbsoluteRP.Defines;
using AbsoluteRP.Helpers;
using AbsoluteRP.RsUI.Pages;
using AbsoluteRP.Windows.Ect;
using AbsoluteRP.Windows.Listings;
using AbsoluteRP.Windows;
using AbsoluteRP.Windows.Moderator;
using AbsoluteRP.Windows.Profiles;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows;
using AbsoluteRP.Windows.Profiles.ProfileTypeWindows.ProfileLayoutTypes;
using AbsoluteRP.Windows.Social.Views;
using AbsoluteRP.Windows.Social.Views.Groups;
using AbsoluteRP.Windows.Social.Views.SubViews;
using Dalamud.Interface.Textures.TextureWraps;
using FFXIVClientStructs.FFXIV.Common.Math;
using Serilog;
using System.Linq;
using System.Xml.Linq;
using Networking;

namespace AbsoluteRP.Network
{
    // Group form channel packets. Split out of DataReceiver.
    internal class GroupForms_DR
    {
        // Form Channel state (channel ID -> fields/submissions)
        public static Dictionary<int, List<FormField>> formFields = new Dictionary<int, List<FormField>>();

        public static Dictionary<int, List<FormSubmission>> formSubmissions = new Dictionary<int, List<FormSubmission>>();
        public static string formSubmitResultMessage = string.Empty;
        public static bool formSubmitResultSuccess = false;

        /// Handles form fields response for a channel
        public static void HandleFormFields(byte[] data)
        {
            try
            {
                Plugin.PluginLog.Info($"[HandleFormFields] Received form fields packet");
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int channelId = buffer.ReadInt();
                int fieldCount = buffer.ReadInt();
                Plugin.PluginLog.Info($"[HandleFormFields] channelId={channelId}, fieldCount={fieldCount}");

                var fields = new List<FormField>();
                for (int i = 0; i < fieldCount; i++)
                {
                    var field = new FormField
                    {
                        id = buffer.ReadInt(),
                        channelId = buffer.ReadInt(),
                        title = buffer.ReadString(),
                        fieldType = buffer.ReadInt(),
                        isOptional = buffer.ReadBool(),
                        sortOrder = buffer.ReadInt()
                    };
                    fields.Add(field);
                    Plugin.PluginLog.Info($"[HandleFormFields] Field {i}: id={field.id}, title='{field.title}', type={field.fieldType}");
                }

                buffer.Dispose();

                formFields[channelId] = fields;
                Plugin.PluginLog.Info($"[HandleFormFields] Updated cache for channelId={channelId} with {fields.Count} fields");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleFormFields error: {ex.Message}");
            }
        }

        /// Handles form submissions response for a channel
        public static void HandleFormSubmissions(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                int channelId = buffer.ReadInt();
                int submissionCount = buffer.ReadInt();

                var submissions = new List<FormSubmission>();
                for (int i = 0; i < submissionCount; i++)
                {
                    var submission = new FormSubmission
                    {
                        id = buffer.ReadInt(),
                        channelId = buffer.ReadInt(),
                        userId = buffer.ReadInt(),
                        profileId = buffer.ReadInt(),
                        profileName = buffer.ReadString(),
                        submittedAt = DateTimeOffset.FromUnixTimeMilliseconds(buffer.ReadLong()).LocalDateTime,
                        values = new List<FormSubmissionValue>()
                    };

                    int valueCount = buffer.ReadInt();
                    for (int j = 0; j < valueCount; j++)
                    {
                        submission.values.Add(new FormSubmissionValue
                        {
                            fieldId = buffer.ReadInt(),
                            fieldTitle = buffer.ReadString(),
                            value = buffer.ReadString()
                        });
                    }

                    submissions.Add(submission);
                }

                buffer.Dispose();

                formSubmissions[channelId] = submissions;
                Plugin.PluginLog.Debug($"HandleFormSubmissions: channelId={channelId}, submissionCount={submissionCount}");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleFormSubmissions error: {ex.Message}");
            }
        }

        /// Handles form submission result
        public static void HandleFormSubmitResult(byte[] data)
        {
            try
            {
                ByteBuffer buffer = new ByteBuffer();
                buffer.WriteBytes(data);
                buffer.ReadInt(); // packet ID

                formSubmitResultSuccess = buffer.ReadBool();
                formSubmitResultMessage = buffer.ReadString();

                buffer.Dispose();

                Plugin.PluginLog.Debug($"HandleFormSubmitResult: success={formSubmitResultSuccess}, message={formSubmitResultMessage}");
            }
            catch (Exception ex)
            {
                Plugin.PluginLog.Debug($"HandleFormSubmitResult error: {ex.Message}");
            }
        }
    }
}
